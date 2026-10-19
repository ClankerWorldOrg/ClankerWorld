using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.World;

namespace ClankerWorld.Tests;

public sealed class ChoiceAgeGroupTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AdultGroupsRetainUrgencyAndOrderedOffersAcrossStrictReload(bool hungry)
    {
        var (state, shop) = TailorTestWorld.Create("probe-a", 6);
        const string household = "household:first";
        var actor = state.Society.Society.Agents.First(person => person.HouseholdId == household).Id;
        var inventory = InventoryAuthority.AddLot(state.Society.Society.Inventory, "choice-cloth", "cloth", household, 2,
            storageBuildingId: shop);
        inventory = InventoryAuthority.AddLot(inventory, "choice-fiber", "fiber", household, 3, storageBuildingId: shop);
        inventory = InventoryAuthority.AddLot(inventory, "choice-food", "food", actor, 1);
        state = WeatherTestFixture.WithWeather(state, WeatherKind.Clear) with
        {
            JevEnabled = false,
            RoutineHelper = RoutineHelperSettings.Off,
            Society = state.Society with { Society = state.Society.Society with { Inventory = inventory } },
            Agents = state.Agents.Select(person => person with
            {
                FullnessBasisPoints = hungry ? 1_000 : 10_000,
                Survival = (person.Survival ?? new()) with { WarmthBasisPoints = 10_000 },
                LastDecisionContext = null,
            }).ToArray(),
        };
        using var world = WorldRuntime.Restore(state, _ => new ActionCoverageRecorder(chooseIdle: true));
        world.SubmitInstruction(new("choice-group-refresh", "owner:choice-test", actor,
            OwnerInstructionKind.Suggestion, "Consider the work and food available here."));
        var saved = WorldRuntimeCodec.Encode(world.ExportState());
        using var reloaded = WorldRuntime.Restore(WorldRuntimeCodec.Decode(saved),
            _ => new ActionCoverageRecorder(chooseIdle: true));
        var candidates = Assert.Single(world.PrepareChoiceTrialOffers(), offer => offer.AgentId == actor).Candidates;
        var restored = Assert.Single(reloaded.PrepareChoiceTrialOffers(), offer => offer.AgentId == actor).Candidates;
        Assert.Equal(candidates.Select(candidate => candidate.Id), restored.Select(candidate => candidate.Id));
        Assert.Contains(candidates, candidate => candidate.Id == "safe_idle");
        Assert.Equal(hungry, candidates.Any(candidate => candidate.ActionPlan is ConsumeFoodActionPlan));
        var recipes = candidates.Select(candidate => candidate.ActionPlan).OfType<RecipeProjectActionPlan>().ToArray();
        Assert.Equal(!hungry, recipes.Any(plan => plan.Recipe.LocalId == "weave-cloth"));
        if (hungry) Assert.Empty(recipes);
        Assert.All(candidates.Where(candidate => candidate.ActionPlan is ConsumeFoodActionPlan or RecipeProjectActionPlan),
            candidate => Assert.True(world.CheckActionPlan(candidate.ActionPlan!).Allowed));
    }
}
