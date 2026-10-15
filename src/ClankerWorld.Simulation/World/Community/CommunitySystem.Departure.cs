using ClankerWorld.Simulation.Kernel;

namespace ClankerWorld.Simulation.World;

internal sealed partial class CommunitySystem
{
    internal static bool DepartHousehold(ICommunityDepartureWorld world, string actor, string cause, DepartureActionPlan? originalPlan = null)
    {
        if (!world.IsAdult(actor) || world.Society.GetAgent(actor).HouseholdId is not { } householdId)
            return false;
        if (cause == "displaced" && (world.HasDependentInPrimaryCare(actor) ||
            world.HoldsRelocationNotice(actor) && !world.MayRelocateFromHousehold(actor)))
            return false;
        var group = originalPlan?.CareGroup ?? world.MovingCareGroup(actor);
        var sharedProject = world.PhysicalAgents.Living[actor].Project;
        if (group.Any(id => world.Society.GetAgent(id).HouseholdId != householdId)) return false;
        var allowanceIds = new List<string>();
        var allowance = 0;
        // Ownership changes now, location does not. Collection remains a separate physical action.
        var nextInventory = world.Society.Inventory;
        // Food sealed in a storage pot stays with the household; the allowance takes loose portions.
        foreach (var selected in originalPlan?.Allowance ?? world.PlanDepartureAllowance(actor, householdId))
        {
            var lot = nextInventory.GetLot(selected.Stock.Id);
            var quantity = selected.Quantity;
            var transferId = world.DepartureTransferId(actor, world.WorldTick, world.PhysicalAgents.Living[actor].Departures?.Count ?? 0, lot.Id);
            InventoryGroundPosition? ground = lot.StorageBuildingId is null
                ? lot.GroundPosition ?? new InventoryGroundPosition(world.SharedStoragePosition.X, world.SharedStoragePosition.Y)
                : null;
            nextInventory = InventoryAuthority.Transfer(nextInventory, transferId, householdId, actor,
                lot.Id, quantity, "departure_allowance", lot.StorageBuildingId, destinationGroundPosition: ground, holdAccess: InventoryHoldRules.DeliveryFor(actor, lot.StorageBuildingId));
            allowanceIds.Add(quantity == lot.Quantity ? lot.Id : DerivedId.Create(lot.Id + "#transfer:" + transferId));
            allowance += quantity;
        }
        if (!world.LeaveHousehold(actor)) return false;
        world.CommitDepartureAllowance(nextInventory);
        world.CancelDepartureOrders(actor);
        foreach (var id in group)
        {
            var person = world.PhysicalAgents.Living[id];
            world.PhysicalAgents.RecordHouseholdDeparture(id, person, null, id == actor
                    ? (person.Housing ?? new()) with { Blocker = HousingBlockers.NoHousehold, Relocation = null }
                    : new(Blocker: HousingBlockers.NoAuthorizedHome), null);
        }
        world.PhysicalAgents.RecordDepartureAllowance(actor, world.PhysicalAgents.Living[actor], (world.PhysicalAgents.Living[actor].Departures ?? []).Append(
                new HouseholdDeparture(householdId, world.WorldTick, cause, group, allowanceIds, allowance, sharedProject)).ToArray());
        var personalJobs = world.PauseDepartingHouseholdWork(actor, householdId);
        foreach (var id in group) world.RestoreDepartureDeliveries(id, householdId);
        foreach (var job in personalJobs) world.AppendEvent("recipe_cancelled", $"{job.JobId}:{job.RecipeId}");
        world.MarkCurrentSchema();
        world.AppendEvent("household_left", $"{actor}|{householdId}|{cause}|{allowance}");
        world.ReconcileGuardianPlacements();
        return true;
    }

}
