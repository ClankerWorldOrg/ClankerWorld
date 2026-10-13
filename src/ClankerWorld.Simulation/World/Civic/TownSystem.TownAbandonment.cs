using System.Security.Cryptography;
using System.Text;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

internal sealed partial class TownSystem
{
    internal static bool MayResettleTown(ITownWorld world, string actor, TownRuntimeState town) =>
            town.IsAbandoned && world.IsAdult(actor) && world.ReadyForBriefInteraction(actor) &&
            town.BorderTiles.Contains(world.Agents[actor].Position);

    internal string ResettlementText(ITownWorld world, string actor, TownRuntimeState town)
    {
        var previous = TownForResident(world, actor);
        var leaving = previous is not null ? $" You would leave {TownName(world, previous)}." : string.Empty;
        var care = TownCareGroup(world, actor, previous).Length > 1 ? " Your dependent children join with you without moving their bodies or changing care." : string.Empty;
        return $"Explicitly resettle abandoned {town.Name} where you stand and become its first resident. Its council restarts from living adult residents; existing laws remain.{leaving}{care} This gives no House, private property or household membership.";
    }

    internal void ResettleTown(ITownWorld world, string actor, TownRuntimeState town)
    {
        // Recheck immediately before committing: another accepted choice may already have revived it.
        town = towns.Single(item => item.Id == town.Id);
        if (!MayResettleTown(world, actor, town)) return;
        var previous = TownForResident(world, actor);
        var group = TownCareGroup(world, actor, previous);
        var moving = group.ToHashSet(StringComparer.Ordinal);
        foreach (var other in towns.Where(item => item.Id != town.Id && item.ResidentIds.Any(moving.Contains)).ToArray())
            SetTown(world, other with { ResidentIds = other.ResidentIds.Where(id => !moving.Contains(id)).ToArray() });
        SetTown(world, town with { ResidentIds = group });
        AdvanceTownCouncil(world);
        SettleTownAdmissions(world);
        world.AppendEvent(new TownResettledEventPayload(actor, town.Id, previous ?? "none", group.Length), world.Agents[actor].Position);
    }

    internal static string TownSalvageId(string townId, string lotId) => DerivedId.Hash(townId + "|" + lotId, WorldRuntime.CivicSharedRules.TownSalvagePrefix);

    internal IEnumerable<(TownRuntimeState Town, InventoryLot Lot, int Quantity)> AbandonedStockForPickup(ITownWorld world, string actor)
    {
        if (!world.Agents.TryGetValue(actor, out var person) || !world.ReadyForBriefInteraction(actor) ||
            world.Society.GetAgent(actor).AgeBand == SocietyAgeBand.Infant)
            yield break;
        var abandoned = towns.Where(town => town.IsAbandoned).ToDictionary(town => town.Id, StringComparer.Ordinal);
        if (abandoned.Count == 0) yield break;
        var room = world.FreeCarryCapacity(actor);
        foreach (var lot in world.Society.Inventory.Lots.OrderBy(item => item.Id, StringComparer.Ordinal))
        {
            if (!abandoned.TryGetValue(lot.OwnerId, out var town) || lot.CarrierId is not null ||
                lot.ContainerLotId is not null || lot.DeliveryBuildingId is not null || world.PhysicalUnreservedQuantity(lot) <= 0)
                continue;
            // Town ownership never opens a private building or supplies a location for missing goods.
            if (lot.GroundPosition is null && (lot.StorageBuildingId is not { } storage ||
                !world.WarehousesForTown(town.Id).Any(building => building.InstanceId == storage && building.HouseholdId is null)))
                continue;
            var range = lot.GroundPosition is not null ? WorldRuntime.CivicSharedRules.ResourceInteractionRange : 0;
            if (!world.IsWithinInteractionRange(person.Position, world.HouseholdStockPosition(lot), range)) continue;
            var cart = lot.ItemKind == InventoryContainerRules.Handcart;
            var quantity = InventoryContainerRules.IsContainer(lot.ItemKind)
                ? !WorldRuntime.CivicSharedRules.HasActiveContainerReservation(world.Society.Inventory, lot.Id) && (cart || world.VesselFits(lot, room)) ? 1 : 0
                : Math.Min(world.PhysicalUnreservedQuantity(lot), room);
            if (quantity > 0) yield return (town, lot, quantity);
        }
    }

    internal void AddTownSalvageCandidates(ITownWorld world, List<CognitionCandidate> candidates, string actor)
    {
        foreach (var (town, lot, quantity) in AbandonedStockForPickup(world, actor))
            candidates.Add(new(TownSalvageId(town.Id, lot.Id),
                $"Salvage {quantity} {lot.ItemKind.Replace('_', ' ')} from abandoned {town.Name} here. Only unreserved communal goods move; this grants no Town membership or private access." +
                (lot.ItemKind == InventoryContainerRules.Handcart ? " The handcart and its cargo stay on the ground until you pull it." : string.Empty), 120));
    }

    internal void SalvageTownStock(ITownWorld world, string actor, string candidate)
    {
        // Exact physical stock, current abandonment, reservations and capacity all matter at pickup.
        foreach (var (town, lot, quantity) in AbandonedStockForPickup(world, actor))
        {
            if (TownSalvageId(town.Id, lot.Id) != candidate) continue;
            world.ApplyInventoryTransition(inventory => InventoryAuthority.Transfer(inventory,
                WorldRuntime.CivicSharedRules.TownSalvageOperationId(world.WorldTick, actor, candidate),
                town.Id, actor, lot.Id, quantity,
                "town_stock_salvaged"));
            world.AppendEvent(new TownStockSalvagedEventPayload(actor, town.Id, lot.ItemKind, quantity), world.Agents[actor].Position);
            return;
        }
    }

}
