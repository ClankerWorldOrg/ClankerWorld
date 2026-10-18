using ClankerWorld.Server.Saves;
using ClankerWorld.Simulation.World;

namespace ClankerWorld.Tests;

public sealed partial class WorldProductionOrderTests
{
    [Fact]
    public async Task CompletedOrderBatchesStayValidAfterNewerUnorderedWorkRetires()
    {
        var state = Stock(Stock(Prepared(Shop), Shop, "cloth", 4), Shop, "rope", 2);
        using var ordered = Restore(state);
        var receipt = Submit(ordered, Actor(state), "history-two-sacks", "make two sacks");
        for (var tick = 0; tick < 120 && Order(ordered, receipt).Status != "finished"; tick++) await Tick(ordered);
        Assert.Equal(2, Order(ordered, receipt).CompletedUnits);
        var batches = ordered.WorldSimulation.ProductionJobs.Where(job => job.OrderInstructionId == receipt.InstructionId).ToArray();
        Assert.Equal(2, batches.Length);
        var later = WorldRuntimeCodec.Decode(await ProductionHistoryTests.BuildNativeHistory("cloth", ordered.ExportState()));
        using var world = WorldRuntime.Restore(later);
        var directory = Directory.CreateTempSubdirectory("clanker-ordered-job-history-");
        try
        {
            var file = new WorldStateFile(Path.Combine(directory.FullName, "world.json"));
            Assert.True(file.Save(world));
            Assert.All(batches, batch => Assert.Contains(world.WorldSimulation.ProductionJobs, job => job.JobId == batch.JobId));
            Assert.Equal(2, Order(world, receipt).CompletedUnits);
            using var loaded = file.LoadOrCreate(later.WorldSeed);
            Assert.Equal(WorldRuntimeCodec.Encode(world.ExportState()), WorldRuntimeCodec.Encode(loaded.ExportState()));
            Assert.Equal(2, Order(loaded, receipt).CompletedUnits);
        }
        finally { directory.Delete(recursive: true); }
    }
}
