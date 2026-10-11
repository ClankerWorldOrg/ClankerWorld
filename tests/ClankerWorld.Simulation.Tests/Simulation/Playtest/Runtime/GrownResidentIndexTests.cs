using ClankerWorld.Simulation.Playtest;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.Tests;

public sealed partial class GrownAgentHelperMemoryTests
{
    [Fact]
    public async Task GrownTownResidentIndexMatchesEveryLinearIdentityAcrossRenameAndReload()
    {
        using var world = PrivateWorldRuntime.Restore(PrivateWorldRuntimeCodec.Decode(await Adult.Value));
        Assert.NotEmpty(world.Towns);
        var birth = Assert.Single(world.Society.Births);
        Assert.Equal(SocietyAgeBand.Adult, world.Society.GetInhabitant(birth.ChildId).AgeBand);
        Assert.True(world.Society.Inhabitants.Count > PrivateWorldRuntime.RequiredFounders);
        var snapshot = world.Society;
        var bytes = PrivateWorldRuntimeCodec.Encode(world.ExportState());
        Check(snapshot);
        Assert.Equal(bytes, PrivateWorldRuntimeCodec.Encode(world.ExportState()));
        Assert.True(world.RenameAgent(birth.ChildId, "Index Grownresident"));
        Check(world.Society);
        Assert.NotEqual("Index Grownresident", snapshot.GetInhabitant(birth.ChildId).Name);
        Assert.Equal("Index Grownresident", world.Society.GetInhabitant(birth.ChildId).Name);
        var saved = PrivateWorldRuntimeCodec.Encode(world.ExportState());
        using var reload = PrivateWorldRuntime.Restore(PrivateWorldRuntimeCodec.Decode(saved));
        Check(reload.Society);
        Assert.Equal(saved, PrivateWorldRuntimeCodec.Encode(reload.ExportState()));
    }

    private static void Check(SocietyCheckpoint checkpoint)
    {
        foreach (var person in checkpoint.Inhabitants)
        {
            var linear = checkpoint.Inhabitants.Single(item => string.Equals(item.Id, person.Id, StringComparison.Ordinal));
            Assert.Same(linear, checkpoint.GetInhabitant(person.Id));
            Assert.Same(linear, checkpoint.GetInhabitant(person.Id));
        }
    }
}
