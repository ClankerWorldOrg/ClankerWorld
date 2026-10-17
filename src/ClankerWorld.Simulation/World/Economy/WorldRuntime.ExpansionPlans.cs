using ClankerWorld.Simulation.Content;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private ExpansionActionPlan? PlanExpansion(string actor, PlacedBuilding building, OwnerQueuedInstruction? instruction = null)
    {
        var shape = ExpansionShapeForOrder(building, instruction);
        if (shape.Footprint is null) return null;
        var definition = worldContent.Buildings.Single(item => item.CanonicalId == building.DefinitionId);
        var costs = BuildingStorageRules.ExpansionCosts(definition, building, shape.Footprint);
        return new(actor, new(building.InstanceId), building, shape.Position, shape.Footprint, ExpansionOwner(building),
            PlanExpansionInputs(actor, building, costs), Reach(actor, building.Position, ReachPurpose.BuildingExpansion, 0, 0, SwimmingPolicy.WhenCapable));
    }

    private ExpansionMaterialInput[]? PlanExpansionInputs(string actor, PlacedBuilding building, IReadOnlyList<ContentQuantity> costs)
    {
        var inputs = new List<ExpansionMaterialInput>();
        var used = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var cost in costs)
        {
            var remaining = cost.Amount;
            foreach (var lot in ExpansionMaterialLots(actor, building).Where(lot => lot.ItemKind == cost.ResourceId).OrderBy(lot => lot.Id, StringComparer.Ordinal))
            {
                var quantity = Math.Min(remaining, Math.Max(0, AvailableLotQuantity(lot) - used.GetValueOrDefault(lot.Id)));
                if (quantity == 0) continue;
                inputs.Add(new(lot.Id, lot.OwnerId, lot.ItemKind, quantity));
                used[lot.Id] = used.GetValueOrDefault(lot.Id) + quantity;
                remaining -= quantity;
                if (remaining == 0) break;
            }
            if (remaining > 0) return null;
        }
        return inputs.ToArray();
    }

    private ActionCheck CheckExpansion(ExpansionActionPlan plan)
    {
        var capability = ActorCapabilities.Check(CapabilitiesFor(plan.Actor), plan.CandidateId,
            new(ActionLifeStages.Adults, AllowUrgentFood: true, AllowUrgentWarmth: true, AllowActiveOrder: true));
        if (!capability.Allowed) return capability;
        if (worldSimulation.Buildings.FirstOrDefault(building => building.InstanceId == plan.Building.InstanceId) is not { } current ||
            current != plan.Building || ExpansionOwner(current) != plan.OwnerId ||
            !MayExpandBuilding(plan.Actor, current, out _) || !CanFitExpansion(current, plan.TargetPosition, plan.TargetFootprint, out _))
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected expansion building, need or footprint has changed");
        if (plan.Inputs is not null && plan.Inputs.GroupBy(input => (input.LotId, input.OwnerId, input.ItemKind)).Any(group =>
                ExpansionMaterialLots(plan.Actor, current).FirstOrDefault(lot => lot.Id == group.Key.LotId && lot.OwnerId == group.Key.OwnerId && lot.ItemKind == group.Key.ItemKind) is not { } lot ||
                AvailableLotQuantity(lot) < group.Sum(input => input.Quantity)))
            return ActionCheck.Refuse(ActionRefusalReason.StockUnavailable, "the selected expansion input lots are unavailable");
        return agents[plan.Actor].Position == current.Position || Reach(plan.Actor, current.Position, ReachPurpose.BuildingExpansion, 0, 0, SwimmingPolicy.WhenCapable).Count > 0
            ? new() : ActionCheck.Refuse(ActionRefusalReason.OutOfReach, "the selected expansion building is out of reach");
    }
}
