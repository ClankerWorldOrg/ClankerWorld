using System.Collections.Concurrent;
using System.Globalization;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.World;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Tests;

/// <summary>
/// One generated Town whose Council approved a Market and whose residents built it from Town stock.
/// It is built once and shared, so each Market rule test starts from a copy of the same paid Market.
/// </summary>
internal static class PaidMarketWorld
{
    private static readonly Lazy<Task<byte[]>> Paid = new(BuildAsync, LazyThreadSafetyMode.ExecutionAndPublication);

    /// <summary>The paid Market with every adult fed, warm, free of personal projects and due a fresh decision.</summary>
    internal static async Task<WorldRuntimeState> StateAsync()
    {
        var state = WorldRuntimeCodec.Decode(await Paid.Value);
        var inventory = state.Society.Society.Inventory;
        foreach (var person in state.Agents)
        {
            inventory = InventoryAuthority.AddLot(inventory, "market-rules-cloak:" + person.AgentId, "rain_cloak", person.AgentId, 1);
            inventory = InventoryAuthority.AddLot(inventory, "market-rules-sack:" + person.AgentId, "sack", person.AgentId, 1);
        }
        return WithInventory(state, inventory) with
        {
            Agents = state.Agents.Select(person => person with
            {
                FullnessBasisPoints = 9_500,
                Survival = new SurvivalCondition(),
                Project = null,
                LastDecisionContext = null,
                Equipment = new PersonalEquipment("market-rules-cloak:" + person.AgentId, "market-rules-sack:" + person.AgentId),
            }).ToArray(),
        };
    }

    internal static TownRuntimeState Town(WorldRuntimeState state) => Assert.Single(state.Towns!);

    internal static TownMarketState Market(WorldRuntimeState state) => Assert.Single(Town(state).Markets);

    internal static TownMarketState Market(WorldRuntime world) => Assert.Single(Assert.Single(world.Towns).Markets);

    internal static GridPoint StallTile(WorldRuntimeState state, int slot) => MarketContent.StallSite(Market(state).Site, slot);

    internal static WorldRuntimeState WithInventory(WorldRuntimeState state, InventoryCheckpoint inventory) =>
        state with { Society = state.Society with { Society = state.Society.Society with { Inventory = MarketStockHolds.Update(inventory, state.Towns!) } } };

    internal static WorldRuntimeState WithMarket(WorldRuntimeState state, Func<TownMarketState, TownMarketState> change)
    {
        var updated = state with { Towns = state.Towns!.Select(town => town with { Markets = town.Markets.Select(change).ToArray() }).ToArray() };
        return WithInventory(updated, updated.Society.Society.Inventory);
    }

    internal static WorldRuntimeState At(WorldRuntimeState state, string actor, GridPoint position) => state with
    {
        Agents = state.Agents.Select(person => person.AgentId == actor ? person with { Position = position } : person).ToArray(),
    };

    internal static string HouseholdOf(WorldRuntimeState state, string actor) =>
        state.Society.Society.GetAgent(actor).HouseholdId!;

    internal static PlacedBuilding HouseOf(WorldRuntimeState state, string household) =>
        state.WorldSimulation!.Buildings.First(building => building.HouseholdId == household &&
            state.WorldContent!.Buildings.Single(definition => definition.CanonicalId == building.DefinitionId).Tags.Contains("house"));

    /// <summary>Stands the seller at the stall's doorway and records them as its current borrower.</summary>
    internal static WorldRuntimeState Borrowing(WorldRuntimeState state, string seller, int slot)
    {
        var market = Market(state);
        var stall = market.Stalls.Single(item => item.SlotIndex == slot);
        var occupancy = new MarketStallOccupancy("market-rules-occupancy:" + slot.ToString(CultureInfo.InvariantCulture),
            stall.BuildingId, seller, state.Society.Society.WorldTick)
        {
            SellerHouseholdId = HouseholdOf(state, seller),
        };
        return WithMarket(At(state, seller, MarketContent.StallEntrance(market.Site, slot)),
            item => item with { Occupancies = item.Occupancies.Append(occupancy).ToArray() });
    }

    /// <summary>Adds a Council-approved extra stall on a free slot that still waits for its materials.</summary>
    internal static WorldRuntimeState WithApprovedStallProject(WorldRuntimeState state, int slot, out string projectId)
    {
        var town = Town(state);
        var market = Market(state);
        var definition = MarketContent.Stall1x1();
        var plan = new TownProjectPayload("Another stall", definition.CanonicalId, MarketContent.StallSite(market.Site, slot),
            MarketContent.StallEntrance(market.Site, slot), definition.BuildCosts);
        var council = town.Council!;
        var approved = council.Proposals.Single(item => item.Project?.DefinitionId == MarketContent.Hall2x2().CanonicalId) with
        {
            Id = town.Id + ":proposal:" + (council.Sequence + 1).ToString(CultureInfo.InvariantCulture),
            RequestKey = TownProjectRules.RequestKey(plan),
            Text = TownProjectRules.ProposalText(plan),
            Project = plan,
        };
        var project = new TownProject(TownProjectRules.ProjectId(approved), approved.Id, plan,
            approved.SettledTick!.Value, "supplying", 0, state.Society.Society.WorldTick, []);
        projectId = project.Id;
        return state with
        {
            Towns = [town with
            {
                Council = council with { Sequence = council.Sequence + 1, Proposals = council.Proposals.Append(approved).ToArray() },
                Projects = town.Projects.Append(project).OrderBy(item => item.Id, StringComparer.Ordinal).ToArray(),
            }],
        };
    }

    private static async Task<byte[]> BuildAsync()
    {
        var policy = new TownProjectPolicy
        {
            ProjectLocalId = MarketContent.Hall2x2().LocalId,
            ProjectName = "First Market",
            Supply = true,
            PersonalSupply = false,
        };
        // The same ordinary first-Town site as the full construction test; only the Town's starting stock is controlled.
        WorldRuntimeState start;
        using (var generated = NormalPathWorld.CreateGenerated(TownProjectScenario.PlayableSeed, policy.CreateProvider, new GridPoint(108, 53)))
            start = generated.ExportState();
        var warehouse = start.WorldSimulation!.Buildings.Single(building => building.InstanceId == "first-town-warehouse");
        var inventory = start.Society.Society.Inventory;
        foreach (var cost in MarketContent.Hall2x2().BuildCosts)
            inventory = InventoryAuthority.AddLot(inventory, "market-rules-town-" + cost.ResourceId, cost.ResourceId,
                TownBorderRules.FirstTownId, cost.Amount, storageBuildingId: warehouse.InstanceId);
        using var world = WorldRuntime.Restore(WithInventory(start, inventory), policy.CreateProvider);
        for (var tick = 0; tick < 600 && world.Towns[0].Markets.Count == 0; tick++)
            Assert.True((await world.AdvanceOneTickAsync()).Advanced);
        Assert.Equal("completed", Assert.Single(world.Towns[0].Projects).Stage);
        Assert.Equal(2, Assert.Single(world.Towns[0].Markets).Stalls.Count);
        world.Validate();
        return WorldRuntimeCodec.Encode(world.ExportState());
    }
}

/// <summary>Records what each agent is offered and answers from a per-test rule, else stays idle.</summary>
internal sealed class MarketRulesPolicy(DecisionProviderKind kind = DecisionProviderKind.LargeLanguageModel)
{
    internal ConcurrentQueue<(string Actor, long Tick, IReadOnlyList<CognitionCandidate> Candidates)> Offered { get; } = new();
    internal ConcurrentQueue<(string Actor, string Id, long Tick)> Chosen { get; } = new();

    /// <summary>Returns the wanted candidate, or null to take needed food and warmth first and otherwise stay idle.</summary>
    internal Func<string, IReadOnlyList<CognitionCandidate>, CognitionCandidate?> Choose { get; set; } = (_, _) => null;

    internal IEnumerable<CognitionCandidate> OfferedTo(string actor) =>
        Offered.Where(item => item.Actor == actor).SelectMany(item => item.Candidates);

    internal IDecisionProvider CreateProvider(string actor) => new Provider(this, actor, kind);

    private sealed class Provider(MarketRulesPolicy policy, string actor, DecisionProviderKind kind) : IDecisionProvider
    {
        public DecisionProviderKind Kind => kind;
        public long ProviderEpoch => kind == DecisionProviderKind.LargeLanguageModel ? 1 : 0;

        public ValueTask<CognitionDecisionResponse> DecideAsync(CognitionDecisionRequest request, CancellationToken cancellationToken = default)
        {
            var candidates = request.Observation.Candidates;
            policy.Offered.Enqueue((actor, request.Observation.WorldTick, candidates));
            var selected = policy.Choose(actor, candidates) ??
                candidates.Where(candidate => candidate.DeterministicPriority <= 5 && candidate.Id is "consume_food" or
                        "collect_shared_food" or "take_food_from_pot" or "make_room_for_food" or "harvest_food" or "seek_food" or
                        "wear_clothing" or "seek_warmth")
                    .OrderBy(candidate => candidate.DeterministicPriority).FirstOrDefault() ??
                candidates.Single(candidate => candidate.Id == "safe_idle");
            policy.Chosen.Enqueue((actor, selected.Id, request.Observation.WorldTick));
            return ValueTask.FromResult(new CognitionDecisionResponse(request.RequestId, actor, Kind, ProviderEpoch,
                request.Observation.RunEpoch, request.Observation.DecisionGeneration, request.Observation.ObservationDigest,
                selected.Id, 1, candidates.ToDictionary(candidate => candidate.Id,
                    candidate => candidate.Id == selected.Id ? 1d : 0d, StringComparer.Ordinal)));
        }
    }
}
