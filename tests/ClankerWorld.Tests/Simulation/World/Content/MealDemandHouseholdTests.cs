using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.World;

namespace ClankerWorld.Tests;

public sealed partial class ConcreteMealTests
{
    [Theory]
    [InlineData(3, true)]
    [InlineData(4, false)]
    public void MealReserveUsesItsOwnHouseholdAndKeepsOffersAcrossStrictReload(int servings, bool needed)
    {
        var (state, actor, household, house, recipe) = Prepared("bread", false);
        Assert.Equal(4, state.Agents.Count);
        Assert.Equal(2, state.Society.Society.GetHousehold(household).MemberIds.Count);
        var otherHouse = state.WorldSimulation!.Buildings.First(building => building.HouseholdId is not null &&
            building.HouseholdId != household && state.WorldContent!.Buildings.Single(definition =>
                definition.CanonicalId == building.DefinitionId).Tags.Contains("house"));
        var inventory = InventoryAuthority.AddLot(state.Society.Society.Inventory, "reserve-house-bread", "bread",
            household, servings, storageBuildingId: house.InstanceId);
        inventory = InventoryAuthority.AddLot(inventory, "reserve-other-bread", "bread", otherHouse.HouseholdId!, 2,
            storageBuildingId: otherHouse.InstanceId);
        state = FarmFieldTests.WithInventory(state, inventory) with
        {
            JevEnabled = false,
            RoutineHelper = RoutineHelperSettings.Off,
        };
        using var world = WorldRuntime.Restore(state, _ => new Chooser());
        world.SubmitInstruction(new("meal-reserve-refresh", "owner:meal-test", actor,
            OwnerInstructionKind.Suggestion, "Consider the food this household needs."));
        var saved = WorldRuntimeCodec.Encode(world.ExportState());
        using var reloaded = WorldRuntime.Restore(WorldRuntimeCodec.Decode(saved), _ => new Chooser());
        var offers = Assert.Single(world.PrepareChoiceTrialOffers(), offer => offer.AgentId == actor).Candidates;
        var restored = Assert.Single(reloaded.PrepareChoiceTrialOffers(), offer => offer.AgentId == actor).Candidates;
        Assert.Equal(offers.Select(offer => offer.Id), restored.Select(offer => offer.Id));
        var plans = offers.Select(offer => offer.ActionPlan).OfType<RecipeProjectActionPlan>()
            .Where(plan => plan.Recipe.CanonicalId == recipe.CanonicalId).ToArray();
        Assert.Equal(needed, plans.Length > 0);
        Assert.All(plans, plan => Assert.True(world.CheckActionPlan(plan).Allowed));
        Assert.Equal(servings, world.Society.Inventory.GetLot("reserve-house-bread").Quantity);
        Assert.Equal(2, world.Society.Inventory.GetLot("reserve-other-bread").Quantity);
    }
}
