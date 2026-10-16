using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.World;

namespace ClankerWorld.Tests;

public sealed partial class WorldStorageOrderTests
{
    [Fact]
    public async Task OfferedPersonalStorageMovesTheWholeRemainderAfterAnExactOrderSplit()
    {
        var state = Prepared();
        var actor = Actor(state);
        const string lotId = "storage-remainder";
        var inventory = InventoryAuthority.AddLot(state.Society.Society.Inventory, lotId, "stone", actor, 8);
        using var world = WorldRuntime.Restore(WithInventory(state, inventory), _ => new SweepChoiceProvider());
        var receipt = Submit(world, actor, "four-stone", "store four stone in my House");
        Assert.True((await world.AdvanceOneTickAsync()).Advanced);
        Assert.Equal(("finished", 4), (Order(world, receipt).Status, Order(world, receipt).CompletedUnits));
        Assert.Equal(4, world.Society.Inventory.GetLot(lotId).Quantity);
        var sibling = Assert.Single(world.Society.Inventory.Lots, lot => lot.ProvenanceLotId == lotId);
        Assert.Equal((actor, House, 4), (sibling.OwnerId, sibling.StorageBuildingId, sibling.Quantity));

        world.SubmitInstruction(new("consider-remainder", "owner:test", actor,
            OwnerInstructionKind.Suggestion, "Consider storing the remaining stone."));
        var candidate = "household_store_personal:" + lotId;
        Assert.Contains(world.PrepareChoiceTrialOffers(), offer => offer.AgentId == actor &&
            offer.Candidates.Any(choice => choice.Id == candidate));
        var before = WorldRuntimeCodec.Encode(world.ExportState());
        using var idle = world.CopyForChoiceTrial(_ => new SweepChoiceProvider());
        await idle.ApplyChoiceTrialAsync();
        Assert.Null(idle.Society.Inventory.GetLot(lotId).StorageBuildingId);
        using var trial = world.CopyForChoiceTrial(_ => new SweepChoiceProvider(actor, candidate));
        var decisions = await trial.ApplyChoiceTrialAsync();
        var decision = Assert.Single(decisions, item => item.AgentId == actor);
        Assert.True(decision.Admission.Accepted);
        Assert.False(decision.Admission.FellBack);
        Assert.Equal(candidate, decision.Admission.Intention!.CandidateId);
        var remaining = trial.Society.Inventory.GetLot(lotId);
        Assert.Equal((actor, House, 4, (string?)null),
            (remaining.OwnerId, remaining.StorageBuildingId, remaining.Quantity, remaining.CarrierId));
        Assert.Equal(sibling, trial.Society.Inventory.GetLot(sibling.Id));
        Assert.Equal(8, trial.Society.Inventory.Lots.Where(lot => lot.Id == lotId || lot.ProvenanceLotId == lotId).Sum(lot => lot.Quantity));
        Assert.Equal(("finished", 4), (Order(trial, receipt).Status, Order(trial, receipt).CompletedUnits));
        Assert.Equal(2, trial.ExportState().Events.Count(item => item.Kind == "personal_goods_stored"));
        var saved = WorldRuntimeCodec.Encode(trial.ExportState());
        using var reloaded = WorldRuntime.Restore(WorldRuntimeCodec.Decode(saved), _ => new SweepChoiceProvider());
        Assert.Equal(remaining, reloaded.Society.Inventory.GetLot(lotId));
        Assert.Equal(sibling, reloaded.Society.Inventory.GetLot(sibling.Id));
        Assert.Equal(before, WorldRuntimeCodec.Encode(world.ExportState()));
        WorldRuntimeCodec.Decode(WorldRuntimeCodec.Encode(idle.ExportState()));
    }
}
