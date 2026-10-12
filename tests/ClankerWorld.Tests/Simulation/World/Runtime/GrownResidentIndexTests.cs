using ClankerWorld.Simulation.World;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Tests;

public sealed partial class GrownAgentHelperMemoryTests
{
    [Fact]
    public async Task GrownTownResidentIndexMatchesEveryLinearIdentityAcrossRenameAndReload()
    {
        using var world = WorldRuntime.Restore(WorldRuntimeCodec.Decode(await Adult.Value));
        Assert.NotEmpty(world.Towns);
        var birth = Assert.Single(world.Society.Births);
        Assert.Equal(SocietyAgeBand.Adult, world.Society.GetAgent(birth.ChildId).AgeBand);
        Assert.True(world.Society.Agents.Count > WorldRuntime.RequiredFounders);
        var snapshot = world.Society;
        var bytes = WorldRuntimeCodec.Encode(world.ExportState());
        Check(snapshot);
        Assert.Equal(bytes, WorldRuntimeCodec.Encode(world.ExportState()));
        Assert.True(world.RenameAgent(birth.ChildId, "Index Grownresident"));
        Check(world.Society);
        Assert.NotEqual("Index Grownresident", snapshot.GetAgent(birth.ChildId).Name);
        Assert.Equal("Index Grownresident", world.Society.GetAgent(birth.ChildId).Name);
        var saved = WorldRuntimeCodec.Encode(world.ExportState());
        using var reload = WorldRuntime.Restore(WorldRuntimeCodec.Decode(saved));
        Check(reload.Society);
        Assert.Equal(saved, WorldRuntimeCodec.Encode(reload.ExportState()));
    }

    private static void Check(SocietyCheckpoint checkpoint)
    {
        foreach (var person in checkpoint.Agents)
        {
            var linear = checkpoint.Agents.Single(item => string.Equals(item.Id, person.Id, StringComparison.Ordinal));
            Assert.Same(linear, checkpoint.GetAgent(person.Id));
            Assert.Same(linear, checkpoint.GetAgent(person.Id));
        }
    }
}
