using ClankerWorld.Simulation.Kernel;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private static ActorCapabilityPolicy RareMiningPolicy => new(ActionLifeStages.Adults,
        AllowUrgentFood: false, AllowUrgentWarmth: false, AllowActiveOrder: true);

    private ActionCheck CheckRareMiningContext(string actor, RareMiningChoice choice)
    {
        var capability = ActorCapabilities.Check(CapabilitiesFor(actor), choice.Encode(), RareMiningPolicy);
        if (!capability.Allowed) return capability;
        if (agents[actor].Project is { Stage: not ("completed" or "cancelled"), RequiresFreshChoice: false })
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "their ongoing work comes first");
        return RareMaterialStockQuantity(actor, choice.ItemKind) < RareMaterialStockTarget ? new() :
            ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "there is already enough of this material");
    }

    private RareMiningActionPlan? PlanRareMining(string actor, string itemKind)
    {
        var choice = new RareMiningChoice(itemKind);
        if (itemKind is not ("gold_ore" or "diamond") ||
            !CheckRareMiningContext(actor, choice).Allowed ||
            ToolProgressionRules.BestUsableTool(society.Checkpoint.Inventory, actor, ToolFamily.Pickaxe) is not { } pick ||
            ToolProgressionRules.Find(pick.ItemKind)!.Tier < 3 ||
            MaterialSource(itemKind, actor) is not { } source || ProjectMaterialHarvest(actor, itemKind, source) is not { ToolLotId: { } toolId } harvest)
            return null;
        var tool = society.Checkpoint.Inventory.GetLot(toolId);
        return new(actor, choice, HouseholdFor(actor), source, harvest, tool.OwnerId, tool.ItemKind,
            Reach(actor, source.Position, ReachPurpose.MaterialGathering, ResourceInteractionRange, 0, SwimmingPolicy.WhenCapable));
    }

    private ActionCheck CheckRareMiningPlan(RareMiningActionPlan plan)
    {
        var context = CheckRareMiningContext(plan.Actor, plan.Choice);
        if (!context.Allowed) return context;
        if (HouseholdFor(plan.Actor) != plan.HouseholdId)
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "their household has changed");
        if (!map.Resources.Contains(plan.Source) || resources.GetValueOrDefault(plan.Source.Id) != ResourceState.Available ||
            !EcologyRules.Harvest(worldSystems.Ecology.GetResource(plan.Source.Id), 1).IsValid)
            return ActionCheck.Refuse(ActionRefusalReason.StockUnavailable, "the selected deposit is no longer available");
        if (plan.Harvest.ToolLotId is not { } toolId ||
            RecheckGoods(new(GoodsUse.CarriedInputs, plan.Actor, GoodsOwners.One(plan.ToolOwnerId), GoodsKinds.One(plan.ToolKind)), toolId) is not { Lot: var tool } ||
            !ToolProgressionRules.IsTopLevelCarriedLot(tool, plan.Actor))
            return ActionCheck.Refuse(ActionRefusalReason.StockUnavailable, "the selected mining pick is no longer available");
        if (FreeCarryCapacity(plan.Actor) < checked(plan.Harvest.Quantity + plan.Harvest.TreeSeedQuantity))
            return ActionCheck.Refuse(ActionRefusalReason.Capacity, "there is no room for the selected mining load");
        return IsWithinInteractionRange(agents[plan.Actor].Position, plan.Source.Position, ResourceInteractionRange) ||
            Reach(plan.Actor, plan.Source.Position, ReachPurpose.MaterialGathering, ResourceInteractionRange, 0, SwimmingPolicy.WhenCapable).Count > 0
            ? new() : ActionCheck.Refuse(ActionRefusalReason.OutOfReach, "the selected deposit is out of reach now");
    }

    private void ApplyRareMiningPlan(RareMiningActionPlan? plan)
    {
        if (plan is null) return;
        if (CheckRareMiningPlan(plan).Refusal is { } refusal) { RefuseChoice(plan, refusal); return; }
        GatherPlannedProjectMaterial(plan.Actor, agents[plan.Actor], plan.Choice.ItemKind, plan.Source, plan.Harvest);
    }
}
