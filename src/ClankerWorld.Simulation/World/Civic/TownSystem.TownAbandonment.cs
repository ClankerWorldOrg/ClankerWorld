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

    internal IEnumerable<(TownRuntimeState Town, InventoryLot Lot, int Quantity)> AbandonedStockForPickup(ITownWorld world, string actor) =>
        PlanTownSalvages(world, actor).Select(plan =>
            (towns.Single(town => town.Id == plan.Choice.TownId), world.Society.Inventory.GetLot(plan.Choice.StockId), plan.Quantity));

    internal void AddTownSalvageCandidates(ITownWorld world, List<CognitionCandidate> candidates, string actor)
    {
        foreach (var plan in PlanTownSalvages(world, actor))
        {
            var town = towns.Single(town => town.Id == plan.Choice.TownId);
            candidates.Add(new(plan.CandidateId,
                $"Salvage {plan.Quantity} {plan.ItemKind.Replace('_', ' ')} from abandoned {town.Name} here. Only unreserved communal goods move; this grants no Town membership or private access." +
                (plan.ItemKind == InventoryContainerRules.Handcart ? " The handcart and its cargo stay on the ground until you pull it." : string.Empty), 120)
            { ActionPlan = plan });
        }
    }
}
