using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.World;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Tests;

internal static class FoodCapacityTestFixture
{
    internal const string Household = "household:first";
    internal const string House = "first-town-house-a";

    internal static async Task<(WorldRuntimeState State, string Actor, GridPoint HousePosition)> Generated(string seed)
    {
        using var setup = NormalPathWorld.CreateGenerated(seed, _ => new Choices());
        for (var tick = 0; tick < 8; tick++) Assert.True((await setup.AdvanceOneTickAsync()).Advanced);
        var state = setup.ExportState();
        var actor = state.Society.Society.Agents.First(person => person.HouseholdId == Household &&
            person.AgeBand is SocietyAgeBand.Adult or SocietyAgeBand.Elder).Id;
        var house = setup.WorldSimulation.Buildings.Single(building => building.InstanceId == House);
        state = WeatherTestFixture.WithWeather(state with
        {
            Agents = state.Agents.Select(person => person with
            {
                Position = person.AgentId == actor ? house.Position : person.Position,
                FullnessBasisPoints = 9_000,
                Survival = new SurvivalCondition(10_000),
                Equipment = person.AgentId == actor ? null : person.Equipment,
                LastDecisionContext = null,
            }).ToArray(),
        }, WeatherKind.Clear);
        return (state, actor, house.Position);
    }

    internal static WorldRuntimeState WithInventory(WorldRuntimeState state, InventoryCheckpoint inventory) =>
        state with { Society = state.Society with { Society = state.Society.Society with { Inventory = inventory } } };

    internal static WorldRuntimeState WithFullness(WorldRuntimeState state, string actor, int fullness) =>
        state with
        {
            Agents = state.Agents.Select(person => person.AgentId == actor
                ? person with { FullnessBasisPoints = fullness } : person).ToArray(),
        };

    internal static InventoryCheckpoint WithoutPersonalCargo(WorldRuntimeState state, string actor) =>
        state.Society.Society.Inventory with
        {
            Lots = state.Society.Society.Inventory.Lots.Where(lot => lot.OwnerId != actor).ToArray(),
        };

    internal static WorldRuntime Restore(WorldRuntimeState state, string actor, Choices choices) =>
        WorldRuntime.Restore(WorldRuntimeCodec.Decode(WorldRuntimeCodec.Encode(state)),
            id => id == actor ? choices : new Choices());

    internal static async Task AssertReplay(WorldRuntime world, string actor, params string[] allowed)
    {
        var bytes = WorldRuntimeCodec.Encode(world.ExportState());
        using var restored = Restore(WorldRuntimeCodec.Decode(bytes), actor, new Choices(allowed));
        Assert.Equal(bytes, WorldRuntimeCodec.Encode(restored.ExportState()));
        Assert.Equal(bytes, WorldRuntimeCodec.Encode(world.ExportState()));
        Assert.True((await world.AdvanceOneTickAsync()).Advanced);
        Assert.True((await restored.AdvanceOneTickAsync()).Advanced);
        Assert.Equal(WorldRuntimeCodec.Encode(world.ExportState()), WorldRuntimeCodec.Encode(restored.ExportState()));
    }

    internal sealed class Choices(params string[] allowed) : IDecisionProvider
    {
        internal List<string[]> Offers { get; } = [];
        public DecisionProviderKind Kind => DecisionProviderKind.Deterministic;
        public long ProviderEpoch => 0;

        public ValueTask<CognitionDecisionResponse> DecideAsync(CognitionDecisionRequest request,
            CancellationToken cancellationToken = default)
        {
            Offers.Add(request.Observation.Candidates.Select(candidate => candidate.Id).ToArray());
            var permitted = request.Observation.Candidates.Where(candidate =>
                allowed.Any(prefix => candidate.Id.StartsWith(prefix, StringComparison.Ordinal))).ToArray();
            return new DeterministicDecisionProvider().DecideAsync(request with
            {
                Observation = request.Observation with
                {
                    Candidates = permitted.Length == 0
                        ? [request.Observation.Candidates.Single(candidate => candidate.Id == "safe_idle")]
                        : permitted,
                },
            }, cancellationToken);
        }
    }
}
