using ClankerWorld.Simulation.World;

namespace ClankerWorld.Tests;

public sealed class ResumeCheckpointTests
{
    [Fact]
    public void ResumeIsInvisibleUntilPersistSucceedsAndFailurePreservesTheEpochAndEvents()
    {
        using var world = new WorldRuntime("durable-resume-boundary");
        world.Pause();
        var before = WorldRuntimeCodec.Encode(world.ExportState());
        Assert.Throws<IOException>(() => world.PersistCheckpoint(proposed =>
        {
            Assert.True(world.Society.IsPaused);
            Assert.False(proposed.Society.Society.IsPaused);
            throw new IOException("Fixture write failure");
        }, resumeOnSuccess: true));
        Assert.Equal(before, WorldRuntimeCodec.Encode(world.ExportState()));
        byte[]? saved = null;
        world.PersistCheckpoint(proposed =>
        {
            Assert.True(world.Society.IsPaused);
            Assert.False(proposed.Society.Society.IsPaused);
            saved = WorldRuntimeCodec.Encode(proposed);
            return proposed;
        }, resumeOnSuccess: true);
        Assert.False(world.Society.IsPaused);
        Assert.Equal(saved, WorldRuntimeCodec.Encode(world.ExportState()));
    }
}
