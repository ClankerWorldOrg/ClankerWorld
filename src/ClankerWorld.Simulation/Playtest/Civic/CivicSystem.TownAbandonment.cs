using System.Security.Cryptography;
using System.Text;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.Playtest;

internal sealed partial class CivicSystem
{
    internal static bool MayResettleTown(ICivicTownWorld world, string actor, TownRuntimeState town) =>
            town.IsAbandoned && world.AdultResident(actor) && world.ReadyForBriefInteraction(actor) &&
            town.BorderTiles.Contains(world.Inhabitants[actor].Position);

    internal string ResettlementText(ICivicTownWorld world, string actor, TownRuntimeState town)
    {
        var previous = TownForResident(world, actor);
        var leaving = previous is not null ? $" You would leave {TownName(world, previous)}." : string.Empty;
        var care = TownCareGroup(world, actor, previous).Length > 1 ? " Your dependent children join with you without moving their bodies or changing care." : string.Empty;
        return $"Explicitly resettle abandoned {town.Name} where you stand and become its first resident. Its council restarts from living adult residents; existing laws remain.{leaving}{care} This gives no House, private property or household membership.";
    }

    internal void ResettleTown(ICivicTownWorld world, string actor, TownRuntimeState town)
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
        AdvanceTownGovernance(world);
        SettleTownAdmissions(world);
        world.AppendEvent("town_resettled", $"{town.Id}|{actor}|{previous ?? "none"}|{group.Length}", world.Inhabitants[actor].Position);
    }

    internal static string TownSalvageId(string townId, string lotId) => DerivedId.Hash(townId + "|" + lotId, PrivateWorldRuntime.CivicSharedRules.TownSalvagePrefix);

    internal IEnumerable<(TownRuntimeState Town, InventoryLot Lot, int Quantity)> AbandonedStockForPickup(ICivicTownWorld world, string actor)
    {
        if (!world.Inhabitants.TryGetValue(actor, out var person) || !world.ReadyForBriefInteraction(actor) ||
            world.Society.GetInhabitant(actor).AgeBand == SocietyAgeBand.Infant)
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
            var range = lot.GroundPosition is not null ? PrivateWorldRuntime.CivicSharedRules.ResourceInteractionRange : 0;
            if (!world.IsWithinInteractionRange(person.Position, world.HouseholdStockPosition(lot), range)) continue;
            var cart = lot.ItemKind == InventoryContainerRules.Handcart;
            var quantity = InventoryContainerRules.IsContainer(lot.ItemKind)
                ? !PrivateWorldRuntime.CivicSharedRules.HasActiveContainerReservation(world.Society.Inventory, lot.Id) && (cart || world.VesselFits(lot, room)) ? 1 : 0
                : Math.Min(world.PhysicalUnreservedQuantity(lot), room);
            if (quantity > 0) yield return (town, lot, quantity);
        }
    }

    internal void AddTownSalvageCandidates(ICivicTownWorld world, List<CognitionCandidate> candidates, string actor)
    {
        foreach (var (town, lot, quantity) in AbandonedStockForPickup(world, actor))
            candidates.Add(new(TownSalvageId(town.Id, lot.Id),
                $"Salvage {quantity} {lot.ItemKind.Replace('_', ' ')} from abandoned {town.Name} here. Only unreserved communal goods move; this grants no Town membership or private access." +
                (lot.ItemKind == InventoryContainerRules.Handcart ? " The handcart and its cargo stay on the ground until you pull it." : string.Empty), 120));
    }

    internal void SalvageTownStock(ICivicTownWorld world, string actor, string candidate)
    {
        // Exact physical stock, current abandonment, reservations and capacity all matter at pickup.
        foreach (var (town, lot, quantity) in AbandonedStockForPickup(world, actor))
        {
            if (TownSalvageId(town.Id, lot.Id) != candidate) continue;
            world.ApplyInventoryTransition(inventory => InventoryFixture.Transfer(inventory,
                PrivateWorldRuntime.CivicSharedRules.TownSalvageOperationId(world.WorldTick, actor, candidate),
                town.Id, actor, lot.Id, quantity,
                "town_stock_salvaged"));
            world.AppendEvent("town_stock_salvaged", $"{town.Id}|{actor}|{lot.ItemKind}|{quantity}", world.Inhabitants[actor].Position);
            return;
        }
    }

}
