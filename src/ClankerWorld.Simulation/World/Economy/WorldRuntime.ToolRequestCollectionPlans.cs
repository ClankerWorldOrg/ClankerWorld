namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private ToolRequestCollectionActionPlan? PlanToolRequestCollection(string actor, ToolRequestCollectionChoice choice)
    {
        if (!agents.TryGetValue(actor, out var person)) return null;
        var request = toolMakingRequests.FirstOrDefault(item => ToolRequestCollectionChoice.Encode(item.Id) == choice.Encode());
        if (request is null || ToolRequestShop(request) is not { } smith) return null;
        var local = IsWithinInteractionRange(person.Position, smith.Position, ResourceInteractionRange);
        var quote = local ? ToolRequestQuote(actor, request, smith) : null;
        return new(actor, choice, request, smith,
            Reach(actor, smith.Position, ReachPurpose.ToolRequest, ResourceInteractionRange, 0, SwimmingPolicy.WhenCapable),
            quote is null ? null : new(quote.Goods, quote.GoodsQuantity, quote.Payment, quote.PaymentQuantity));
    }

    private ActionCheck CheckToolRequestCollectionPlan(ToolRequestCollectionActionPlan plan, ActorCapabilityPolicy? policy = null)
    {
        var capability = ActorCapabilities.Check(CapabilitiesFor(plan.Actor), plan.CandidateId,
            policy ?? ToolRequestCapabilities);
        if (!capability.Allowed) return capability;
        if (toolMakingRequests.FirstOrDefault(item => item.Id == plan.Request.Id) != plan.Request ||
            plan.Request.Status != ToolMakingRequestStatus.Ready || plan.Request.RequesterId != plan.Actor ||
            !IdleForToolRequest(plan.Actor) || ToolRequestShop(plan.Request) != plan.Smith)
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected finished-tool request or Blacksmith has changed");
        if (plan.Quote is not { } quote)
            return !IsWithinInteractionRange(agents[plan.Actor].Position, plan.Smith.Position, ResourceInteractionRange)
                ? new() : ActionCheck.Refuse(ActionRefusalReason.StockUnavailable, "no selected tool exchange is available at the Blacksmith");
        if (!IsWithinInteractionRange(agents[plan.Actor].Position, plan.Smith.Position, ResourceInteractionRange))
            return ActionCheck.Refuse(ActionRefusalReason.OutOfReach, "the selected Blacksmith is no longer within reach");
        if (plan.Request.JobId is not { } jobId ||
            !worldSimulation.ProductionJobs.Any(job => job.JobId == jobId && job.State == WorldProductionJobState.Completed) ||
            quote.Goods.Id != ProductionOutputLotId(jobId, 0))
            return ActionCheck.Refuse(ActionRefusalReason.StockUnavailable, "the selected finished tool is no longer available");
        return CheckSelectedShopQuote(plan.Actor, plan.Smith, quote, requestedGoods: true);
    }

    private void ApplyToolRequestCollectionPlan(ToolRequestCollectionActionPlan plan)
    {
        if (CheckActionPlan(plan).Refusal is { } refusal) { RefuseChoice(plan, refusal); return; }
        CompleteToolRequestCollection(plan);
    }
}
