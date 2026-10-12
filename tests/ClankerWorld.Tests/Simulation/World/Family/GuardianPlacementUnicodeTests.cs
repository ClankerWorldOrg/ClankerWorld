using System.Collections.Concurrent;
using System.Text.Json;
using System.Text;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.World;

namespace ClankerWorld.Tests;

public sealed class GuardianPlacementUnicodeTests
{
    private static readonly Lazy<Task<byte[]>> Escorting = new(CreateEscorting);

    [Theory]
    [InlineData(34, true, 256)]
    [InlineData(35, false, 255)]
    [InlineData(36, false, 256)]
    public async Task NativeGuardianContextKeepsWholeRenamedCharactersAndAcceptedCare(int namePrefix,
        bool includesEmoji, int noteLength)
    {
        var scenario = GuardianPlacementTestFixture.Prepared();
        var child = scenario.Children[0];
        var state = WorldRuntimeCodec.Decode(await Escorting.Value);
        var placement = state.Agents.Single(person => person.AgentId == child).GuardianPlacement!;
        var actor = placement.CaregiverId;
        // A controlled cold guardian interrupts its accepted escort through the
        // runtime's own warmth blocker. No derived note or blocker is injected.
        state = state with
        {
            Agents = state.Agents.Select(person => person.AgentId == child
                ? person with { LastDecisionContext = null }
                : person.AgentId == actor ? person with
                {
                    LastDecisionContext = null,
                    Survival = person.Survival! with { WarmthBasisPoints = 0 },
                } : person).ToArray(),
        };
        state = WeatherTestFixture.WithWeather(state, WeatherKind.Storm);
        var provider = new Capture(actor);
        using var world = WorldRuntime.Restore(
            WorldRuntimeCodec.Decode(WorldRuntimeCodec.Encode(state)), _ => provider);
        var name = new string('a', namePrefix) + "😀 Vale";
        Assert.True(world.RenameAgent(child, name));
        for (var tick = 0; tick < 6 && provider.Observations.IsEmpty; tick++)
            Assert.True((await world.AdvanceOneTickAsync()).Advanced);
        var observation = Assert.Single(provider.Observations);
        observation.Validate();
        var note = Assert.IsType<string>(observation.Self!.HousingNote);
        Assert.Contains("The guardian is meeting urgent food or warmth needs.", note, StringComparison.Ordinal);
        var childStart = note.IndexOf(" Child: ", StringComparison.Ordinal) + 8;
        Assert.Equal(220, childStart); // Keep these rows at, before and after the actual cutoff.
        Assert.Equal(noteLength, note.Length);
        Assert.Equal(new string('a', namePrefix) + (includesEmoji ? "😀" : string.Empty), note[childStart..]);
        var wire = JsonSerializer.Deserialize<AgentObservation>(JsonSerializer.Serialize(observation))!;
        Assert.Equal(note, wire.Self!.HousingNote);
        _ = new UTF8Encoding(false, true).GetBytes(note);
        Assert.Equal(name, world.Society.GetAgent(child).Name);
        _ = new UTF8Encoding(false, true).GetBytes(name);
        var bytes = WorldRuntimeCodec.Encode(world.ExportState());
        using var reload = WorldRuntime.Restore(WorldRuntimeCodec.Decode(bytes), _ => new Capture(actor));
        Assert.Equal(bytes, WorldRuntimeCodec.Encode(reload.ExportState()));
        var after = Assert.IsType<GuardianPlacementState>(GuardianPlacementTestFixture.Person(world, child).GuardianPlacement);
        Assert.Equal(placement, after with { Blocker = placement.Blocker });
        Assert.Equal(placement.CaregiverId, world.Society.GetAgent(child).PrimaryCaregiverId);
        Assert.Equal(state.Society.Society.GetAgent(child).HouseholdId, world.Society.GetAgent(child).HouseholdId);
        Assert.Equal(scenario.OriginalTownId, GuardianPlacementTestFixture.TownOf(world, child));
    }

    private static async Task<byte[]> CreateEscorting()
    {
        var scenario = GuardianPlacementTestFixture.Prepared();
        using var world = GuardianPlacementTestFixture.Restore(scenario.State,
            GuardianPlacementTestFixture.Accepting(scenario));
        await GuardianPlacementTestFixture.Accepted(world, scenario);
        await GuardianPlacementTestFixture.Until(world,
            () => GuardianPlacementTestFixture.Person(world, scenario.Children[0]).GuardianPlacement?.Stage == "escorting");
        return WorldRuntimeCodec.Encode(world.ExportState());
    }

    private sealed class Capture(string actor) : IDecisionProvider
    {
        internal ConcurrentQueue<AgentObservation> Observations { get; } = new();
        public DecisionProviderKind Kind => DecisionProviderKind.LargeLanguageModel;
        public long ProviderEpoch => 0;
        public ValueTask<CognitionDecisionResponse> DecideAsync(CognitionDecisionRequest request,
            CancellationToken cancellationToken = default)
        {
            if (request.Observation.AgentId == actor && request.Observation.Self?.HousingNote is not null)
                Observations.Enqueue(request.Observation);
            return ValueTask.FromResult(new CognitionDecisionResponse(request.RequestId,
                request.Observation.AgentId, Kind, ProviderEpoch, request.Observation.RunEpoch,
                request.Observation.DecisionGeneration, request.Observation.ObservationDigest, "safe_idle", 1,
                request.Observation.Candidates.ToDictionary(item => item.Id, item => item.Id == "safe_idle" ? 1d : 0d)));
        }
    }
}
