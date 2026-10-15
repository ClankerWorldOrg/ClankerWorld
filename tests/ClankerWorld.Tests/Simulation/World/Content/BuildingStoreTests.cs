using ClankerWorld.Simulation.World;

namespace ClankerWorld.Tests;

public sealed class BuildingStoreTests
{
    private static PlacedBuilding House => new("house-first", "definition:test", new(1, 1), 0, "town:first", "household:first");
    private static BuildingExpansionJob Job => new("expansion-first", House.InstanceId, "worker:first", "household:first",
        0, House.Position, new(1, 1), new(2, 1, 1), 0, 10, WorldProductionJobState.Running, ["reservation:first"]);
    private static BuildingStore Store => new(WorldConstructionState.From(WorldContentSimulationState.Empty with { Buildings = [House] }));

    [Fact]
    public void ChangingTheHouseholdEndsOldGuestAccessAndLeavesOtherSnapshotsIntact()
    {
        var store = Store;
        store.RecordGuestInvitation(new(House.InstanceId, "guest:first", "host:first", 0, true));
        var before = store.State;

        store.ReassignOwner(House.InstanceId, House.TownId, "household:second", removeInvitations: true);

        Assert.Equal("household:second", Assert.Single(store.State.Buildings).HouseholdId);
        Assert.Empty(store.State.Guests!);
        Assert.Equal("household:first", Assert.Single(before.Buildings).HouseholdId);
        Assert.True(Assert.Single(before.Guests!).Active);
    }

    [Fact]
    public void RemovingABuildingPreservesTheExpansionDefinitionInItsHistory()
    {
        var store = Store;
        store.StartExpansion(Job with { State = WorldProductionJobState.Completed });
        var before = store.State;

        store.RemoveBuilding(House.InstanceId, House.DefinitionId);

        Assert.Empty(store.State.Buildings);
        Assert.Equal(House.DefinitionId, Assert.Single(store.State.Expansions!).DefinitionId);
        Assert.Single(before.Buildings);
        Assert.Null(Assert.Single(before.Expansions!).DefinitionId);
    }

    [Fact]
    public void AReplacementWorkerKeepsThePausedFootprintAndInputsAcrossRestore()
    {
        var store = Store;
        store.StartExpansion(Job);
        store.PauseExpansion(Job.JobId, 3);
        var before = store.State;
        var restored = new BuildingStore(before);
        var paused = Assert.Single(before.Expansions!);

        restored.ResumeExpansion(Job.JobId, 8 + paused.CompletionTick - paused.PausedAtTick!.Value, "worker:second");

        var resumed = Assert.Single(restored.State.Expansions!);
        Assert.Equal(7, resumed.CompletionTick - 8);
        Assert.Equal("worker:second", resumed.WorkerId);
        Assert.Equal(paused.TargetFootprint, resumed.TargetFootprint);
        Assert.Equal(paused.InputReservationIds, resumed.InputReservationIds);
        Assert.Equal(WorldProductionJobState.Paused, Assert.Single(before.Expansions!).State);
    }

    [Fact]
    public void InvalidBuildingWritesFailBeforePublishingTheirSnapshot()
    {
        var store = Store;
        var before = store.State;

        var failure = Assert.Throws<InvalidDataException>(() => store.PlaceBuildings([House]));

        Assert.Contains("Building records", failure.Message, StringComparison.Ordinal);
        Assert.Same(before, store.State);
    }
}
