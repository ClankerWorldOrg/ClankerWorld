namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    // Candidate creation asks many route questions from one agent's tile, and
    // most only need to know whether a route exists. One search per origin and
    // set of occupied tiles answers them all, so keep the latest few.
    private const int SharedRouteSearchLimit = 8;
    private readonly List<SharedRouteSearch> routeSearches = [];
    private readonly List<(UnoccupiedReachabilitySearch Search, bool Swimming)> reachabilitySearches = [];
    internal int RouteSearchesCreated { get; private set; }

    private bool HasUnoccupiedRoute(string actor, GridPoint origin, GridPoint destination,
        int interactionRange, bool allowSwimming = true, int additionalCarriedUnits = 0)
    {
        // Cart geometry depends on its actual hitch and load; keep its route planner.
        if (AttachedHandcart(actor) is not null)
            return FindUnoccupiedRoute(actor, origin, destination, interactionRange,
                allowSwimming, additionalCarriedUnits).Count > 0;
        var occupied = RouteOccupiedTiles(actor, origin, destination, interactionRange);
        if (occupied is null) return false;
        var swimming = allowSwimming && CanSwim(actor, origin, additionalCarriedUnits);
        lock (routeSearches)
        {
            var index = reachabilitySearches.FindIndex(item => item.Swimming == swimming &&
                ReferenceEquals(item.Search.Map, map) && item.Search.Destination == destination &&
                item.Search.InteractionRange == interactionRange && occupied.SetEquals(item.Search.Occupied));
            var search = index >= 0 ? reachabilitySearches[index].Search : NewSearch();
            if (index < 0) RouteSearchesCreated++;
            if (index >= 0) reachabilitySearches.RemoveAt(index);
            else if (reachabilitySearches.Count == SharedRouteSearchLimit) reachabilitySearches.RemoveAt(reachabilitySearches.Count - 1);
            reachabilitySearches.Insert(0, (search, swimming));
            var answer = search.CanReachFrom(origin);
            if (index >= 0 && DerivedVerification.RecomputeOnHit && answer != NewSearch().CanReachFrom(origin))
                throw new InvalidOperationException("Shared route reachability differs from its recomputation.");
            return answer;
        }

        UnoccupiedReachabilitySearch NewSearch() => new(map, occupied, destination, interactionRange,
            swimming ? point => SwimmingRules.Neighbors(map, point) : null);
    }

    /// <summary>
    /// The route <see cref="FindUnoccupiedRoute"/> takes from <paramref name="origin"/>
    /// to within <paramref name="interactionRange"/> of <paramref name="destination"/>,
    /// avoiding <paramref name="occupied"/>. Later questions from the same origin reuse the
    /// search while the map, Roads and occupied tiles are unchanged.
    /// </summary>
    private List<GridPoint> SharedUnoccupiedRoute(GridPoint origin, HashSet<GridPoint> occupied,
        GridPoint destination, int interactionRange, bool swimming = false)
    {
        lock (routeSearches)
        {
            var index = routeSearches.FindIndex(item => item.Matches(this, origin, occupied, swimming));
            SharedRouteSearch shared;
            if (index >= 0)
            {
                shared = routeSearches[index];
                routeSearches.RemoveAt(index);
            }
            else
            {
                RouteSearchesCreated++;
                shared = new SharedRouteSearch(new UnoccupiedRouteSearch(map, origin, occupied, swimming ? AgentStepCost : LegalRoadStepCost,
                    swimming ? point => SwimmingRules.Neighbors(map, point) : null),
                    roadTiles, roadTiles.Count, roadBridgeDecks, swimming);
                if (routeSearches.Count == SharedRouteSearchLimit)
                {
                    routeSearches[^1].Search.Dispose();
                    routeSearches.RemoveAt(routeSearches.Count - 1);
                }
            }
            routeSearches.Insert(0, shared);
            try
            {
                return shared.Search.RouteTo(destination, interactionRange);
            }
            catch
            {
                // A search stopped partway through could answer later questions
                // wrongly, so it is not kept.
                routeSearches.RemoveAt(0);
                shared.Search.Dispose();
                throw;
            }
        }
    }

    private void ReleaseRouteSearches()
    {
        lock (routeSearches)
        {
            foreach (var shared in routeSearches) shared.Search.Dispose();
            routeSearches.Clear();
            reachabilitySearches.Clear();
        }
    }

    /// <summary>
    /// A search and the Roads its step costs came from. Roads are only added while
    /// a world runs (saved Roads are repaired when it is restored), so the same set
    /// with the same count means the same Roads.
    /// </summary>
    private sealed record SharedRouteSearch(UnoccupiedRouteSearch Search, HashSet<GridPoint> Roads, int RoadCount,
        HashSet<GridPoint> RoadDecks, bool Swimming)
    {
        public bool Matches(WorldRuntime runtime, GridPoint origin, HashSet<GridPoint> occupied, bool swimming) =>
            Swimming == swimming &&
            Search.Origin == origin && ReferenceEquals(runtime.map, Search.Map) &&
            ReferenceEquals(runtime.roadTiles, Roads) && runtime.roadTiles.Count == RoadCount &&
            ReferenceEquals(runtime.roadBridgeDecks, RoadDecks) && occupied.SetEquals(Search.Occupied);
    }
}
