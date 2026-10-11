using System.Reflection;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Playtest;

namespace ClankerWorld.Simulation.Tests;

public sealed class EnvironmentSnapshotIdentityTests
{
    [Fact]
    public async Task TrustedPreparationSharesAllFourEnvironmentSnapshots()
    {
        Func<string, IDecisionProvider> factory = _ => new DeterministicDecisionProvider();
        using var world = NormalPathWorld.CreateGenerated("environment-trusted-identity", factory);
        Assert.True((await world.AdvanceOneTickAsync()).Advanced);
        var committed = world.ExportState();
        var restore = typeof(PrivateWorldRuntime).GetMethod("RestoreCore", BindingFlags.Static | BindingFlags.NonPublic)!;
        using var prepared = (PrivateWorldRuntime)restore.Invoke(null, [committed, factory, 4, true, false, null, null, null])!;
        var copied = prepared.ExportState();
        Assert.Same(committed.Map, copied.Map);
        Assert.Same(committed.WorldContent, copied.WorldContent);
        Assert.Same(committed.WorldSystems, copied.WorldSystems);
        Assert.Same(committed.WorldSimulation, copied.WorldSimulation);
        Assert.Equal(PrivateWorldRuntimeCodec.Encode(committed), PrivateWorldRuntimeCodec.Encode(copied));
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
        Assert.Equal("EcologyResource", Assert.Throws<ArgumentOutOfRangeException>(() => PrivateWorldRuntime.Restore(broken)).ParamName);
    }
}
