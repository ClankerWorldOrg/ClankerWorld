namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private ActionCheck CheckKnowledgeWriting(KnowledgeWritingActionPlan plan)
    {
        var capability = ActorCapabilities.Check(CapabilitiesFor(plan.Actor), plan.CandidateId,
            new(ActionLifeStages.Adults, AllowUrgentFood: false, AllowUrgentWarmth: false, AllowActiveOrder: true));
        if (!capability.Allowed) return capability;
        if (plan.Project is { } selected)
        {
            var current = KnowledgeWritingFor(plan.Actor);
            return current is not null && TownHearingProcedure.Digest(current with { WorkDone = 0, LastWorkedTick = 0 }) ==
                TownHearingProcedure.Digest(selected with { WorkDone = 0, LastWorkedTick = 0 }) && HasLiveWritingInputs(current) && HasWritingSource(current)
                ? new() : ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the original writing project or reserved inputs have changed");
        }
        if (!MayMakeKnowledgeArtifact(plan.Actor) || KnowledgeWritingFor(plan.Actor) is not null ||
            plan.Source is { } source && !HeldKnowledgeArtifacts(plan.Actor).Any(item => TownHearingProcedure.Digest(item) == TownHearingProcedure.Digest(source)) ||
            !RetainedWritingContentsAreKnown(plan) || AlreadyWrote(plan.Actor, plan.Kind, plan.Facts.ToArray(), plan.Recipes.ToArray()))
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected writing contents, source or opportunity have changed");
        if (plan.Pickup is { } pickup)
        {
            if (SharedStockItems(pickup.ItemKind, plan.Actor, GoodsUse.Collect).SingleOrDefault(lot => lot.Id == pickup.LotId) is not { } lot ||
                lot.OwnerId != pickup.OwnerId || AvailableLotQuantity(lot) < pickup.Quantity || FreeCarryCapacity(plan.Actor) < pickup.Quantity ||
                HouseholdStockPosition(lot) != pickup.Position || HouseholdStockInteractionRange(lot) != pickup.Range)
                return ActionCheck.Refuse(ActionRefusalReason.StockUnavailable, "the selected writing material pickup is unavailable");
            return IsWithinInteractionRange(agents[plan.Actor].Position, pickup.Position, pickup.Range) ||
                Reach(plan.Actor, pickup.Position, ReachPurpose.KnowledgeWriting, pickup.Range, 0, SwimmingPolicy.WhenCapable).Count > 0
                ? new() : ActionCheck.Refuse(ActionRefusalReason.OutOfReach, "the selected writing material is out of reach");
        }
        return plan.Materials.All(input => CarriedWritingMaterials(plan.Actor, input.ItemKind).Any(lot => lot.Id == input.LotId && AvailableLotQuantity(lot) >= input.Quantity))
            ? new() : ActionCheck.Refuse(ActionRefusalReason.StockUnavailable, "the selected carried writing supplies are unavailable");
    }

    private bool RetainedWritingContentsAreKnown(KnowledgeWritingActionPlan plan) =>
        plan.Facts.Count + plan.Recipes.Count > 0 && plan.Facts.All(fact => KnowsMapFact(plan.Actor, fact.Position)) &&
        plan.Recipes.All(recipe => KnowsRecipe(plan.Actor, recipe.RecipeId)) &&
        (plan.Source is null || HasCompleteCopyContents(plan.Actor, plan.Source, plan.Facts.ToArray(), plan.Recipes.ToArray()));
}
