using ClankerWorld.Simulation.World;

namespace ClankerWorld.Tests;

public sealed partial class ToolMakingRequestTests
{
    [Fact]
    public async Task CommissionWorkRetainsItsNativeAgreementAcrossRollbackReplayAndStaleWithdrawal()
    {
        var (state, buyer, seller, shop) = Prepared();
        using var placing = Restore(state, buyer, seller, new RequestChoices("tool_request_place:"), new RequestChoices());
        await Until(placing, world => world.ToolMakingRequests.Count == 1, 96);
        using var accepting = Restore(Roundtrip(placing), buyer, seller, new RequestChoices(), new RequestChoices("tool_request_accept:"));
        await Until(accepting, world => Assert.Single(world.ToolMakingRequests).Status == ToolMakingRequestStatus.Accepted, 96);
        var request = Assert.Single(accepting.ToolMakingRequests);
        Assert.Null(request.JobId);
        state = Roundtrip(accepting);
        var chooser = new ToolPlanChooser<ToolRequestWorkActionPlan>(seller,
            item => item.ActionPlan is ToolRequestWorkActionPlan);
        using var world = WorldRuntime.Restore(state, _ => chooser);
        world.SubmitInstruction(new("commission-work-guidance", "owner:test", seller,
            OwnerInstructionKind.Suggestion, "Think about your next task."));
        var bytes = WorldRuntimeCodec.Encode(world.ExportState());
        Assert.False((await world.AdvanceOneTickAsync(commitPermitted: () => false)).Advanced);
        Assert.Equal(bytes, WorldRuntimeCodec.Encode(world.ExportState()));
        using var replay = WorldRuntime.Restore(WorldRuntimeCodec.Decode(bytes), _ => new ToolPlanChooser<ToolRequestWorkActionPlan>(seller,
            item => item.ActionPlan is ToolRequestWorkActionPlan));
        var step = await world.AdvanceOneTickAsync();
        Assert.True(step.Advanced);
        Assert.True((await replay.AdvanceOneTickAsync()).Advanced);
        Assert.Equal(WorldRuntimeCodec.Encode(world.ExportState()), WorldRuntimeCodec.Encode(replay.ExportState()));
        var plan = Assert.IsType<ToolRequestWorkActionPlan>(chooser.Plan);
        Assert.Same(plan, step.Decisions.Single(decision => decision.AgentId == seller).ActionPlan);
        Assert.Equal((request.Id, seller, shop.InstanceId, request.RecipeId),
            (plan.Request.Id, plan.Request.WorkerId, plan.Smith.InstanceId, plan.Recipe.CanonicalId));
        Assert.Equal(plan.Choice, ToolRequestWorkChoice.Decode(plan.CandidateId));
        Assert.True(world.CheckActionPlan(plan).Allowed);
        _ = Roundtrip(world);
        var changedState = world.ExportState();
        var withdrawing = changedState.ToolMakingRequests!.Single();
        using var changed = WorldRuntime.Restore(changedState with
        {
            ToolMakingRequests = [withdrawing with { Status = ToolMakingRequestStatus.Withdrawn, LastTransitionTick = world.WorldTick }],
            Agents = changedState.Agents.Select(person => person.AgentId == seller ? person with { Project = null } : person).ToArray(),
        });
        var unchanged = WorldRuntimeCodec.Encode(changed.ExportState());
        Assert.Equal(ActionRefusalReason.TargetUnavailable, changed.CheckActionPlan(plan).Refusal!.Reason);
        Assert.Equal(unchanged, WorldRuntimeCodec.Encode(changed.ExportState()));
        Assert.Empty(changed.Society.Inventory.Offers);
    }
}
