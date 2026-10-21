using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.World;

namespace ClankerWorld.Tests;

public sealed class RecipePlanningEligibilityTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void WarmthFilteringKeepsGarmentWorkAndOrdinaryRecipesAcrossStrictReload(bool cold)
    {
        var (state, shop) = TailorTestWorld.Create("probe-a", 6);
        const string household = "household:first";
        var actor = state.Society.Society.Agents.First(person => person.HouseholdId == household).Id;
        var inventory = InventoryAuthority.AddLot(state.Society.Society.Inventory, "planning-cloth", "cloth", household, 2,
            storageBuildingId: shop);
        inventory = InventoryAuthority.AddLot(inventory, "planning-fiber", "fiber", household, 3, storageBuildingId: shop);
        state = WeatherTestFixture.WithWeather(state, WeatherKind.Snow) with
        {
            Society = state.Society with { Society = state.Society.Society with { Inventory = inventory } },
            Agents = state.Agents.Select(person => person with
            {
                Survival = (person.Survival ?? new()) with { WarmthBasisPoints = cold ? 1_000 : 10_000 },
                LastDecisionContext = null,
            }).ToArray(),
        };
        using var world = WorldRuntime.Restore(WorldRuntimeCodec.Decode(WorldRuntimeCodec.Encode(state)),
            _ => new ActionCoverageRecorder(chooseIdle: true));
        world.SubmitInstruction(new("recipe-warmth-refresh", "owner:planning-test", actor,
            OwnerInstructionKind.Suggestion, "Consider the work this household needs."));
        var plans = world.PrepareChoiceTrialOffers().Where(observation => observation.AgentId == actor)
            .SelectMany(observation => observation.Candidates).Select(candidate => candidate.ActionPlan)
            .OfType<RecipeProjectActionPlan>().ToArray();
        Assert.Contains(plans, plan => plan.Recipe.Outputs.Any(output => PersonalEquipmentRules.IsGarment(output.ResourceId)));
        Assert.Equal(!cold, plans.Any(plan => plan.Recipe.LocalId == "weave-cloth"));
        if (cold)
            Assert.All(plans, plan => Assert.Contains(plan.Recipe.Outputs,
                output => PersonalEquipmentRules.IsGarment(output.ResourceId)));
        Assert.All(plans, plan => Assert.True(world.CheckActionPlan(plan).Allowed));
    }
}
