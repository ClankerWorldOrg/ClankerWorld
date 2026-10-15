using System.Collections.ObjectModel;

namespace ClankerWorld.Simulation.World;

internal sealed partial class TownSystem(CivicState state)
{
    internal MarketSystem Markets { get; } = new();

    internal CivicState State { get; private set; } = state;

    // Restore has always ordered these records before the next tick. Keep an
    // action's insertion order live, and share the canonical snapshot at that boundary.
    internal CivicState CheckpointState
    {
        get
        {
            var orderedTowns = Canonical(State.Towns, town => town.Id);
            var titles = Canonical(State.TownLandTitles, title => title.Id);
            var rights = Canonical(State.HouseholdLandUseRights, right => right.Id);
            var requests = Canonical(State.HouseholdLandUseRequests, request => request.Id);
            return ReferenceEquals(orderedTowns, State.Towns) && ReferenceEquals(titles, State.TownLandTitles) &&
                ReferenceEquals(rights, State.HouseholdLandUseRights) && ReferenceEquals(requests, State.HouseholdLandUseRequests)
                ? State : State with { Towns = orderedTowns, TownLandTitles = titles, HouseholdLandUseRights = rights, HouseholdLandUseRequests = requests };
        }
    }

    private static ReadOnlyCollection<T> Canonical<T>(ReadOnlyCollection<T> items, Func<T, string> key)
    {
        for (var index = 1; index < items.Count; index++)
            if (StringComparer.Ordinal.Compare(key(items[index - 1]), key(items[index])) > 0)
                return Array.AsReadOnly(items.OrderBy(key, StringComparer.Ordinal).ToArray());
        return items;
    }

    private FoodStewardship? foodStewardship
    {
        get => State.FoodStewardship;
        set => RestoreFoodStewardship(value);
    }

    internal void RestoreFoodStewardship(FoodStewardship? value)
    {
        if (!ReferenceEquals(State.FoodStewardship, value)) State = State with { FoodStewardship = value };
    }

    internal void RestoreTowns(IEnumerable<TownRuntimeState> value) => State = State with { Towns = Array.AsReadOnly(value.ToArray()) };
    internal void RestoreFounderSetup(FounderSetupState? value) => State = State with { FounderSetup = value };
    internal void RestoreTownLandTitles(IEnumerable<TownLandTitleRecord> value) => State = State with { TownLandTitles = Array.AsReadOnly(value.ToArray()) };
    internal void RestoreHouseholdLandUseRights(IEnumerable<HouseholdLandUseRight> value) => State = State with { HouseholdLandUseRights = Array.AsReadOnly(value.ToArray()) };
    internal void RestoreHouseholdLandUseRequests(IEnumerable<HouseholdLandUseRequest> value) => State = State with { HouseholdLandUseRequests = Array.AsReadOnly(value.ToArray()) };

    internal void ReplaceMarketState(ICivicWorld world, string townId, TownMarketState market)
    {
        var town = State.Towns.Single(item => item.Id == townId);
        SetTown(world, town with { Markets = town.Markets.Select(item => item.Id == market.Id ? market : item).ToArray() });
    }

    internal void RecordConstructedMarkets(ICivicWorld world, TownRuntimeState town, IReadOnlyList<TownMarketState> markets) => RecordMarkets(world, town, markets);
    internal void RecordRemovedMarketBuildings(ICivicWorld world, TownRuntimeState town, IReadOnlyList<TownMarketState> markets) => RecordMarkets(world, town, markets);
    private void RecordMarkets(ICivicWorld world, TownRuntimeState town, IReadOnlyList<TownMarketState> markets)
    {
        if (SystemInvariantChecks.Enabled)
            foreach (var market in markets) MarketSystem.ValidateLocalMarket(market);
        SetTown(world, town with { Markets = markets });
    }

    internal void SetTownState(TownRuntimeState town)
    {
        var updated = State.Towns.ToArray();
        var index = Array.FindIndex(updated, item => item.Id == town.Id);
        if (index < 0) RestoreTowns(State.Towns.Append(town));
        else if (!ReferenceEquals(updated[index], town))
        {
            updated[index] = town;
            State = State with { Towns = Array.AsReadOnly(updated) };
        }
    }


    private FounderSetupState? founderSetup { get => State.FounderSetup; set => RestoreFounderSetup(value); }
    private ReadOnlyCollection<TownRuntimeState> towns => State.Towns;
    private IReadOnlyList<TownLandTitleRecord> townLandTitles { get => State.TownLandTitles; set => RestoreTownLandTitles(value); }
    private IReadOnlyList<HouseholdLandUseRight> householdLandUseRights { get => State.HouseholdLandUseRights; set => RestoreHouseholdLandUseRights(value); }
    private IReadOnlyList<HouseholdLandUseRequest> householdLandUseRequests { get => State.HouseholdLandUseRequests; set => RestoreHouseholdLandUseRequests(value); }
}
