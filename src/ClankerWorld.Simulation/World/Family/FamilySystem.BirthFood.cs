using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

internal sealed partial class FamilySystem
{
    internal static ParenthoodFood ParenthoodFoodReadiness(SocietyCheckpoint checkpoint, string caregiverId,
        IReadOnlyList<TownRuntimeState> townStates)
    {
        if (checkpoint.GetAgent(caregiverId).HouseholdId is not { } householdId) return new(0, 4);
        var required = checkpoint.Agents.Count(person => person.HouseholdId == householdId &&
            person.Status == SocietyAgentStatus.Active) * 2 + 4;
        return new(BirthFoodSources(checkpoint, caregiverId, townStates).Sum(match => match.Quantity), required);
    }

    internal static string ParenthoodFoodNote(SocietyCheckpoint checkpoint, string caregiverId,
        IReadOnlyList<TownRuntimeState> townStates, bool showAmounts = true)
    {
        var food = ParenthoodFoodReadiness(checkpoint, caregiverId, townStates);
        if (!showAmounts)
            return food.IsReady ? "The caregiver's household has enough ready-to-eat food."
                : "The caregiver's household needs more ready-to-eat food; grain and flour need cooking.";
        var amount = FormattableString.Invariant($"Household food: {food.AvailablePortions}/{food.RequiredPortions} ready-to-eat portions");
        return food.IsReady ? amount + "."
            : amount + FormattableString.Invariant($"; need {food.RequiredPortions - food.AvailablePortions} more. Grain and flour need cooking.");
    }

    internal static GoodsRequest? BirthFoodRequest(SocietyCheckpoint checkpoint, string actor)
    {
        var household = checkpoint.GetAgent(actor).HouseholdId ?? actor;
        var kinds = InventoryIndex.For(checkpoint.Inventory).OwnedBy(household)
            .Where(lot => FoodRules.IsEdible(lot.ItemKind)).Select(lot => lot.ItemKind).Distinct(StringComparer.Ordinal).ToArray();
        return kinds.Length == 0 ? null : new(GoodsUse.ConsumeAt, actor, GoodsOwners.One(household), new GoodsKinds(kinds));
    }

    internal static IReadOnlyList<GoodsMatch> BirthFoodSources(SocietyCheckpoint checkpoint, string actor,
        IReadOnlyList<TownRuntimeState> townStates) => BirthFoodRequest(checkpoint, actor) is { } request
            ? GoodsInventoryQuery.FindConsumableGoods(checkpoint.Inventory, request, (index, lot) =>
                lot.CarrierId is { } carrier && carrier != actor ? GoodsReason.Custody :
                InventoryHoldRules.For(index, lot).Any(hold => hold.Purpose == InventoryHoldPurpose.MarketStall) ? GoodsReason.MarketStall : null).Matches : [];

    internal static List<SocietyBirthFoodContribution>? BirthFood(IFamilyParenthoodWorld world, string actor)
    {
        var remaining = 4;
        var contributions = new List<SocietyBirthFoodContribution>();
        if (BirthFoodRequest(world.Society, actor) is not { } request) return null;
        foreach (var offered in BirthFoodSources(world.Society, actor, world.Towns))
        {
            if (world.RecheckGoods(request, offered.Lot.Id) is not { } current) return null;
            var quantity = Math.Min(remaining, current.Quantity);
            contributions.Add(new(current.Lot.Id, quantity));
            remaining -= quantity;
            if (remaining == 0) return contributions;
        }
        return null;
    }
}
