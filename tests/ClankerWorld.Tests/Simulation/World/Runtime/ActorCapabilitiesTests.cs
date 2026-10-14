using ClankerWorld.Simulation.Society;
using ClankerWorld.Simulation.World;

namespace ClankerWorld.Tests;

public sealed class ActorCapabilitiesTests
{
    [Theory]
    [InlineData("boat", ActionRefusalReason.Boat)]
    [InlineData("mounted", ActionRefusalReason.Mounted)]
    [InlineData("food", ActionRefusalReason.UrgentNeed)]
    [InlineData("warmth", ActionRefusalReason.UrgentNeed)]
    [InlineData("order", ActionRefusalReason.ActiveOrder)]
    [InlineData("age", ActionRefusalReason.LifeStage)]
    public void DeclaredCapabilitiesGiveTypedRefusalsForChangedActorState(string change, ActionRefusalReason reason)
    {
        var state = new ActorCapabilityState(SocietyAgeBand.Adult, false, false, false, false, false);
        var policy = new ActorCapabilityPolicy(ActionLifeStages.Adults, AllowMounted: false, AllowUrgentFood: false);
        Assert.True(ActorCapabilities.Check(state, "store_town_resources", policy).Allowed);
        var changed = change switch
        {
            "boat" => state with { Aboard = true },
            "mounted" => state with { Mounted = true },
            "food" => state with { UrgentFood = true },
            "warmth" => state with { UrgentWarmth = true },
            "order" => state with { ActiveOrder = true },
            _ => state with { Age = SocietyAgeBand.Child },
        };
        var check = ActorCapabilities.Check(changed, "store_town_resources", policy);
        Assert.False(check.Allowed);
        Assert.Equal(reason, check.Refusal!.Reason);
        Assert.NotEmpty(check.Refusal.Explanation);
    }
}
