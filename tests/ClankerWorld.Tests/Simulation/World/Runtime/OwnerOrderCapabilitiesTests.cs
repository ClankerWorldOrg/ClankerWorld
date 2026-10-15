using ClankerWorld.Simulation.Society;
using ClankerWorld.Simulation.World;

namespace ClankerWorld.Tests;

public sealed class OwnerOrderCapabilitiesTests
{
    [Theory]
    [InlineData("consume_food")]
    [InlineData("seek_food")]
    [InlineData("harvest_food")]
    [InlineData("move_to")]
    [InlineData("talk_to")]
    [InlineData("seek_shelter")]
    public void ChildUsableCommandsKeepTheirExistingLifeStages(string action)
    {
        foreach (var age in Enum.GetValues<SocietyAgeBand>())
            Assert.Equal(!LifeStageRules.IsInfant(age), Allowed(action, age));
    }

    [Theory]
    [InlineData("collect_food")]
    [InlineData("gather_material")]
    [InlineData("produce_item")]
    [InlineData("repair_tool")]
    [InlineData("repair_equipment")]
    [InlineData("till_field")]
    [InlineData("collect_goods")]
    [InlineData("store_equipment")]
    [InlineData("return_borrowed")]
    [InlineData("write_knowledge")]
    [InlineData("construct_building")]
    [InlineData("travel_by_boat")]
    [InlineData("accept_guardianship")]
    [InlineData("tend_fire")]
    [InlineData("animal_collect")]
    public void AdultCommandsKeepTheirExistingLifeStages(string action)
    {
        foreach (var age in Enum.GetValues<SocietyAgeBand>())
            Assert.Equal(LifeStageRules.IsAdult(age), Allowed(action, age));
    }

    private static bool Allowed(string action, SocietyAgeBand age)
    {
        var policy = OwnerOrderCapabilities.For(action);
        var ordinary = ActorCapabilities.Check(new(age, false, false, false, false, false), action, policy).Allowed;
        // This compatibility gate adds no boat, mounted, need or active-order
        // restriction; the order handlers still check their own physical step.
        Assert.Equal(ordinary, ActorCapabilities.Check(new(age, true, true, true, true, true), action, policy).Allowed);
        return ordinary;
    }
}
