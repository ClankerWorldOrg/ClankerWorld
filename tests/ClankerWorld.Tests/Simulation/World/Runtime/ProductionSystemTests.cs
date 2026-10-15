using ClankerWorld.Simulation.World;

namespace ClankerWorld.Tests;

public sealed class ProductionSystemTests
{
    private static WorldProductionJob Job(string id = "job-first") =>
        new(id, "recipe:test", "shop-first", "worker:first", 10, 30, WorldProductionJobState.Running, []) { OwnerId = "household:first" };

    [Fact]
    public void TakingOverPausedWorkPreservesItsInputsAndRemainingTimeAcrossRestore()
    {
        var job = Job() with { InputReservationIds = ["reservation:one"] };
        var system = new ProductionSystem(WorldContentSimulationState.Empty, []);
        system.StartJob(job);
        system.PauseJob(job.JobId, 15);
        var construction = WorldConstructionState.From(WorldContentSimulationState.Empty);
        var saved = system.Snapshot(construction);
        var restored = new ProductionSystem(saved, []);

        var paused = Assert.Single(saved.ProductionJobs);
        restored.ResumeJob(job.JobId, 35 + paused.CompletionTick - paused.PausedAtTick!.Value, "knife:second", "worker:second");

        var resumed = Assert.Single(restored.State.Jobs);
        Assert.Equal(15, resumed.CompletionTick - 35);
        Assert.Equal("worker:second", resumed.WorkerId);
        Assert.Equal("knife:second", resumed.ToolLotId);
        Assert.Equal(job.InputReservationIds, resumed.InputReservationIds);
        Assert.Null(resumed.PausedAtTick);
        Assert.Equal(WorldProductionJobState.Running, resumed.State);
        Assert.Equal(WorldProductionJobState.Paused, Assert.Single(saved.ProductionJobs).State);
    }

    [Fact]
    public void InvalidProductionMutationFailsAtItsOwnerBeforePublishingState()
    {
        var system = new ProductionSystem(WorldContentSimulationState.Empty, []);
        system.StartJob(Job());
        var before = system.State;

        var failure = Assert.Throws<InvalidDataException>(() => system.PauseJob("job-first", 9));

        Assert.Contains("Production job", failure.Message, StringComparison.Ordinal);
        Assert.Same(before, system.State);
    }

    [Fact]
    public void RetiringRequestsClearsOnlyTheirJobLinksAndKeepsOldSnapshots()
    {
        var retired = new ToolMakingRequestState("request:first", "customer:first", "household:first", "shop-first", "recipe:test", "knife", 0, 0);
        var kept = retired with { Id = "request:second", RequesterId = "customer:second" };
        var system = new ProductionSystem(WorldContentSimulationState.Empty, [retired, kept]);
        system.StartJob(Job() with { ToolMakingRequestId = retired.Id });
        system.StartJob(Job("job-second") with { ToolMakingRequestId = kept.Id });
        var before = system.State;

        system.RetireRequests(new HashSet<string>(StringComparer.Ordinal) { retired.Id });

        Assert.Equal(kept, Assert.Single(system.State.Requests));
        Assert.Null(system.State.Jobs[0].ToolMakingRequestId);
        Assert.Equal(kept.Id, system.State.Jobs[1].ToolMakingRequestId);
        Assert.Equal(retired.Id, before.Jobs[0].ToolMakingRequestId);
        Assert.Equal(2, before.Requests.Count);
    }

    [Fact]
    public void ConstructionProjectionCannotReplaceProductionAndIsStableBetweenWrites()
    {
        var system = new ProductionSystem(WorldContentSimulationState.Empty, []);
        var construction = WorldConstructionState.From(WorldContentSimulationState.Empty);
        Assert.Same(WorldContentSimulationState.Empty, system.Snapshot(construction));
        system.StartJob(Job());
        var before = system.Snapshot(construction);
        var updated = construction with { Buildings = [new("shop-first", "building:test", new(1, 1), 0)] };

        var snapshot = system.Snapshot(updated);

        Assert.Single(snapshot.Buildings);
        Assert.Same(before.ProductionJobs, snapshot.ProductionJobs);
        Assert.Equal(before.NextProductionJobSequence, snapshot.NextProductionJobSequence);
        Assert.Same(snapshot, system.Snapshot(updated));
        Assert.Empty(before.Buildings);
    }
}
