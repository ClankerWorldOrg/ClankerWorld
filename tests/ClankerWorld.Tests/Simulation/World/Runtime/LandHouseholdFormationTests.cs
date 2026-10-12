using ClankerWorld.Simulation;
using ClankerWorld.Simulation.World;

namespace ClankerWorld.Tests;

public sealed partial class GrownAgentHelperMemoryTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    [InlineData(false, true)]
    [InlineData(false, false, true)]
    public async Task NativeLandHouseholdFormationContinuesAfterAnAdmittedQuietRefusal(bool later, bool heldIdentityReply = false,
        bool cancelFoundReply = false)
    {
        var initial = WorldRuntimeCodec.Decode(await (later ? LaterAdult : ConversationAdult).Value);
        var native = initial.Society.Society.Births.OrderBy(birth => birth.CommittedTick).Last().ChildId;
        Assert.InRange(native.Length, 1, DerivedId.MaximumLength);
        Assert.Equal(later ? 3 : 1, initial.Society.Society.Births.Count);
        var formed = await FormNativeLandHouseholdAsync(initial, native, quietFirstRefusal: true, heldIdentityReply, cancelFoundReply);
        Assert.InRange(formed.Society.Society.GetInhabitant(native).HouseholdId!.Length, 1, DerivedId.MaximumLength);
        var saved = WorldRuntimeCodec.Encode(formed);
        using var restored = WorldRuntime.Restore(WorldRuntimeCodec.Decode(saved), _ => new QuietProvider());
        Assert.Equal(saved, WorldRuntimeCodec.Encode(restored.ExportState()));
    }
}
