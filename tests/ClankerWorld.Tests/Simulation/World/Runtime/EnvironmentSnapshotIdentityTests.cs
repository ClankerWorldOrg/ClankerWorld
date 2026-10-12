using System.Reflection;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.World;

namespace ClankerWorld.Tests;

public sealed class EnvironmentSnapshotIdentityTests
{
    [Fact]
    public async Task TrustedPreparationSharesAllFourEnvironmentSnapshots()
    {
        Func<string, IDecisionProvider> factory = _ => new DeterministicDecisionProvider();
        using var world = NormalPathWorld.CreateGenerated("environment-trusted-identity", factory);
        Assert.True((await world.AdvanceOneTickAsync()).Advanced);
        var committed = world.ExportState();
        var restore = typeof(WorldRuntime).GetMethod("RestoreCore", BindingFlags.Static | BindingFlags.NonPublic)!;
        using var prepared = (WorldRuntime)restore.Invoke(null, [committed, factory, 4, true, false, null, null, null])!;
        var copied = prepared.ExportState();
        Assert.Same(committed.Map, copied.Map);
        Assert.Same(committed.WorldContent, copied.WorldContent);
        Assert.Same(committed.WorldSystems, copied.WorldSystems);
        Assert.Same(committed.WorldSimulation, copied.WorldSimulation);
        Assert.Equal(WorldRuntimeCodec.Encode(committed), WorldRuntimeCodec.Encode(copied));
    }

    [Fact]
    public void UntrustedLoadStillRejectsInvalidEcology()
    {
        using var world = NormalPathWorld.CreateGenerated("environment-untrusted-validation", _ => new DeterministicDecisionProvider());
        var valid = world.ExportState();
        var ecology = valid.WorldSystems!.Ecology;
        Assert.NotEmpty(ecology.Resources);
        var broken = valid with
        {
            WorldSystems = valid.WorldSystems with
            {
                Ecology = ecology with
                {
                    Resources = ecology.Resources.Select((resource, index) => index == 0
                        ? resource with { Quantity = -1 } : resource).ToArray(),
                },
            },
        };
        Assert.Equal("EcologyResource", Assert.Throws<ArgumentOutOfRangeException>(() => WorldRuntime.Restore(broken)).ParamName);
    }
}
