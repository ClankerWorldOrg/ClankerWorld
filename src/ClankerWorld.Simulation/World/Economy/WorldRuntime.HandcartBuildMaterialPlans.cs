using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Kernel;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private HandcartBuildMaterialActionPlan? PlanHandcartBuildMaterial(string actor, HandcartBuildMaterialChoice choice,
        RecipeDefinition? selected = null)
    {
        var recipe = selected ?? HandcartToBuild(actor);
        if (recipe is null || HouseholdFor(actor) is not { } home || HouseholdBuildingWithTag(home, "blacksmith") is not { } smith)
            return null;
        var input = recipe.Inputs.FirstOrDefault(item => item.ResourceId == choice.ItemKind);
        if (input.Amount <= 0 || SharedItem(choice.ItemKind, actor, smith.Position) is not { } stock ||
            RecheckGoods(SharedCollectionRequest(actor, stock.OwnerId, choice.ItemKind), stock.Id) is not { } match) return null;
        var pickup = new PlannedEquipmentPickup(stock.Id, stock.OwnerId, stock.ItemKind,
            HouseholdStockPosition(stock), HouseholdStockInteractionRange(stock));
        var quantity = Math.Min(input.Amount - CarriedMaterialQuantity(actor, input.ResourceId),
            Math.Min(match.Quantity, PickupCarryCapacity(actor, stock, smith.Position)));
        return new(actor, choice, home, recipe.CanonicalId, input.Amount, smith.InstanceId, smith.Position, pickup,
            GoodsPlanFamily.Capture(society.Checkpoint.Inventory, stock), quantity,
            Reach(actor, pickup.Position, ReachPurpose.HandcartBuildMaterial, pickup.InteractionRange, 0, SwimmingPolicy.WhenCapable));
    }

    private void AddHandcartBuildMaterialCandidate(List<CognitionCandidate> candidates, HandcartBuildMaterialActionPlan? plan)
    {
        if (plan is not null && CheckHandcartBuildMaterialPlan(plan).Allowed)
            candidates.Add(new(plan.CandidateId,
                $"Collect nearby {plan.Choice.ItemKind.Replace('_', ' ')} and carry it to build a handcart at your household Blacksmith.",
                25)
            { ActionPlan = plan });
    }

    private ActionCheck CheckHandcartBuildMaterialPlan(HandcartBuildMaterialActionPlan plan)
    {
        var capability = ActorCapabilities.Check(CapabilitiesFor(plan.Actor), plan.CandidateId,
            new(ActionLifeStages.Adults, AllowUrgentFood: true, AllowUrgentWarmth: true, AllowActiveOrder: true));
        if (!capability.Allowed) return capability;
        var recipe = worldContent.Recipes.FirstOrDefault(item => item.CanonicalId == plan.RecipeId);
        var smith = worldSimulation.Buildings.FirstOrDefault(item => item.InstanceId == plan.SmithId);
        if (HouseholdFor(plan.Actor) != plan.HouseholdId || recipe is null || smith is null || smith.HouseholdId != plan.HouseholdId ||
            smith.Position != plan.Destination || smith.DefinitionId != recipe.WorkstationBuildingId ||
            recipe.Inputs.FirstOrDefault(input => input.ResourceId == plan.Choice.ItemKind).Amount != plan.InputAmount ||
            !MayBuildHandcart(plan.Actor, plan.HouseholdId, recipe))
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected handcart recipe or building plan is no longer available");
        if (plan.Quantity <= 0 || plan.InputAmount - CarriedMaterialQuantity(plan.Actor, plan.Choice.ItemKind) < plan.Quantity)
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "they no longer need the planned material quantity");
        var pickup = plan.Pickup;
        if (RecheckGoods(SharedCollectionRequest(plan.Actor, pickup.OwnerId, pickup.ItemKind), pickup.StockId) is not { } match ||
            match.Quantity < plan.Quantity || !GoodsPlanFamily.Matches(society.Checkpoint.Inventory, match.Lot, plan.Family) ||
            HouseholdStockPosition(match.Lot) != pickup.Position || HouseholdStockInteractionRange(match.Lot) != pickup.InteractionRange)
            return ActionCheck.Refuse(ActionRefusalReason.StockUnavailable, "the selected building material is no longer available");
        if (PickupCarryCapacity(plan.Actor, match.Lot, plan.Destination) < plan.Quantity)
            return ActionCheck.Refuse(ActionRefusalReason.Capacity, "they cannot carry the planned materials back to the Blacksmith");
        return IsWithinInteractionRange(agents[plan.Actor].Position, pickup.Position, pickup.InteractionRange) ||
            Reach(plan.Actor, pickup.Position, ReachPurpose.HandcartBuildMaterial, pickup.InteractionRange, 0, SwimmingPolicy.WhenCapable).Count > 0
            ? new() : ActionCheck.Refuse(ActionRefusalReason.OutOfReach, "the selected building material is out of reach now");
    }

    private void ApplyHandcartBuildMaterialPlan(HandcartBuildMaterialActionPlan plan)
    {
        if (CheckHandcartBuildMaterialPlan(plan).Refusal is { } refusal) { RefuseChoice(plan, refusal); return; }
        var pickup = plan.Pickup;
        if (!IsWithinInteractionRange(agents[plan.Actor].Position, pickup.Position, pickup.InteractionRange))
        { MoveToward(plan.Actor, agents[plan.Actor], pickup.Position, "handcart_material", pickup.InteractionRange); return; }
        CompleteHandcartBuildMaterial(plan);
    }
}
