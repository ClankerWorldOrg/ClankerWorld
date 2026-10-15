using ClankerWorld.Simulation.World;

namespace ClankerWorld.Tests;

public sealed class AgentStoreTests
{
    private static readonly string[] RestoredInsertionOrder = ["second", "first"];

    [Fact]
    public void PreparingCareDoesNotPublishItBeforeTheOriginalCommitPoint()
    {
        var store = new AgentStore();
        store.PlaceFounder("care-recipient", new(1, 1));
        var before = store.Living["care-recipient"];

        var prepared = AgentStore.PrepareMeal(before, 9_000, new SurvivalCondition());

        Assert.Same(before, store.Living["care-recipient"]);
        store.CompleteDependentCare("care-recipient", prepared);
        Assert.Equal(9_000, store.Living["care-recipient"].FullnessBasisPoints);
        Assert.Equal(6_500, before.FullnessBasisPoints);
    }

    [Fact]
    public void RestoreSharesRecordsUntilAWritingStoreReplacesOneAndKeepsInsertionOrder()
    {
        var store = new AgentStore();
        store.PlaceFounder("second", new(2, 1));
        store.PlaceFounder("first", new(1, 1));
        var restored = new AgentStore(store.Living.Values, store.Deceased.Values);
        var before = store.Living["first"];
        Assert.Same(before, restored.Living["first"]);

        restored.RecordPosition("first", before, new(3, 1));

        Assert.Equal(RestoredInsertionOrder, restored.Living.Keys);
        Assert.Equal(new GridPoint(1, 1), store.Living["first"].Position);
        Assert.Equal(new GridPoint(3, 1), restored.Living["first"].Position);
    }

    [Fact]
    public void InvalidPhysicalValuesFailAtTheStoreBeforePublishing()
    {
        var store = new AgentStore();
        store.PlaceFounder("resident", new(1, 1));
        var before = store.Living["resident"];

        var failure = Assert.Throws<InvalidDataException>(() => store.RecordFullness("resident", before, -1));

        Assert.Contains("Physical agent", failure.Message, StringComparison.Ordinal);
        Assert.Same(before, store.Living["resident"]);
    }

    [Fact]
    public void EquipmentFailureRestoresTheExactPreTransitionRecord()
    {
        var store = new AgentStore();
        store.PlaceFounder("resident", new(1, 1));
        var before = store.Living["resident"];
        store.RecordEquipment("resident", before, new PersonalEquipment());
        store.RecordDecisionContext("resident", store.Living["resident"], "changed during the failed transition");

        store.RestoreEquipmentAfterFailure("resident", before);

        Assert.Same(before, store.Living["resident"]);
    }
}
