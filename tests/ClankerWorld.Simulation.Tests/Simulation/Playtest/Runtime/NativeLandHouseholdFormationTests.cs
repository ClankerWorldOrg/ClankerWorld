using ClankerWorld.Simulation.Playtest;

namespace ClankerWorld.Simulation.Tests;

public sealed partial class GrownAgentHelperMemoryTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NativeLandHouseholdFormationContinuesAfterAnAdmittedQuietRefusal(bool later)
    {
        var initial = PrivateWorldRuntimeCodec.Decode(await (later ? LaterAdult : ConversationAdult).Value);
        var native = initial.Society.Society.Births.OrderBy(birth => birth.CommittedTick).Last().ChildId;
        Assert.True(native.Length > (later ? 256 : 128));
        Assert.Equal(later ? 3 : 1, initial.Society.Society.Births.Count);
        var formed = await FormNativeLandHouseholdAsync(initial, native, quietFirstRefusal: true);
        Assert.True(formed.Society.Society.GetInhabitant(native).HouseholdId!.Length > (later ? 256 : 128));
        var saved = PrivateWorldRuntimeCodec.Encode(formed);
        using var restored = PrivateWorldRuntime.Restore(PrivateWorldRuntimeCodec.Decode(saved), _ => new QuietProvider());
        Assert.Equal(saved, PrivateWorldRuntimeCodec.Encode(restored.ExportState()));
    }
}
