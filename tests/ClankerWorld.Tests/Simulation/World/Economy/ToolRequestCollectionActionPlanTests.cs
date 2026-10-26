using ClankerWorld.Simulation.World;

namespace ClankerWorld.Tests;

public sealed partial class ToolMakingRequestTests
{
    [Fact]
    public async Task FinishedToolCollectionRetainsExactOutputAndPaymentAcrossRollbackReplayAndStaleWithdrawal()
    {
        var (state, buyer, seller, shop) = await RequestAtStage(ToolMakingRequestStatus.Ready);
        var request = Assert.Single(state.ToolMakingRequests!);
        var chooser = new ToolPlanChooser<ToolRequestCollectionActionPlan>(buyer,
            item => item.ActionPlan is ToolRequestCollectionActionPlan { Quote: not null });
        using var world = WorldRuntime.Restore(state, _ => chooser);
        world.SubmitInstruction(new("finished-tool-guidance", "owner:test", buyer,
            OwnerInstructionKind.Suggestion, "Think about your next task."));
        var bytes = WorldRuntimeCodec.Encode(world.ExportState());
        Assert.False((await world.AdvanceOneTickAsync(commitPermitted: () => false)).Advanced);
        Assert.Equal(bytes, WorldRuntimeCodec.Encode(world.ExportState()));
        using var replay = WorldRuntime.Restore(WorldRuntimeCodec.Decode(bytes), _ => new ToolPlanChooser<ToolRequestCollectionActionPlan>(buyer,
            item => item.ActionPlan is ToolRequestCollectionActionPlan { Quote: not null }));
        var step = await world.AdvanceOneTickAsync();
        Assert.True(step.Advanced);
        Assert.True((await replay.AdvanceOneTickAsync()).Advanced);
        Assert.Equal(WorldRuntimeCodec.Encode(world.ExportState()), WorldRuntimeCodec.Encode(replay.ExportState()));
        var plan = Assert.IsType<ToolRequestCollectionActionPlan>(chooser.Plan);
        Assert.Same(plan, step.Decisions.Single(decision => decision.AgentId == buyer).ActionPlan);
        Assert.Equal(request, plan.Request);
        Assert.Equal(shop.InstanceId, plan.Smith.InstanceId);
        Assert.Equal(plan.Choice, ToolRequestCollectionChoice.Decode(plan.CandidateId));
        var quote = Assert.IsType<PlannedShopQuote>(plan.Quote);
        var offer = Assert.Single(world.Society.Inventory.Offers);
        Assert.Equal((quote.Goods.Id, quote.GoodsQuantity, quote.Payment.Id, quote.PaymentQuantity),
            (offer.FirstLotId, offer.FirstQuantity, offer.SecondLotId, offer.SecondQuantity));
        Assert.Equal((1, 3), (quote.GoodsQuantity, quote.PaymentQuantity));
        Assert.Equal("request-payment", quote.Payment.Id);
        _ = Roundtrip(world);
        using var withdrawing = Restore(WorldRuntimeCodec.Decode(bytes), buyer, seller,
            new RequestChoices("tool_request_withdraw:"), new RequestChoices());
        await Until(withdrawing, current => Assert.Single(current.ToolMakingRequests).Status == ToolMakingRequestStatus.Withdrawn, 96);
        var unchanged = WorldRuntimeCodec.Encode(withdrawing.ExportState());
        Assert.Equal(ActionRefusalReason.TargetUnavailable, withdrawing.CheckActionPlan(plan).Refusal!.Reason);
        Assert.Equal(unchanged, WorldRuntimeCodec.Encode(withdrawing.ExportState()));
        Assert.Empty(withdrawing.Society.Inventory.Offers);
    }
}
