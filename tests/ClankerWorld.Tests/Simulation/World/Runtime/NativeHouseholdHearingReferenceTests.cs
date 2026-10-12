using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using ClankerWorld.Simulation;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.World;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Tests;

public sealed partial class GrownAgentHelperMemoryTests
{
    private static readonly Lazy<Task<byte[]>> FirstHearingHousehold = new(() => CreateHearingHouseholdAsync(false));
    private static readonly Lazy<Task<byte[]>> ThirdHearingHousehold = new(() => CreateHearingHouseholdAsync(true));
    private static readonly Lazy<Task<byte[]>> ShortExpiryHearing = new(() => CreateHouseholdExpiryAsync(null));
    private static readonly Lazy<Task<byte[]>> FirstExpiryHearing = new(() => CreateHouseholdExpiryAsync(false));
    private static readonly Lazy<Task<byte[]>> ThirdExpiryHearing = new(() => CreateHouseholdExpiryAsync(true));

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NativeFoundedHouseholdReceivesAnActuallyRecoveredHouse(bool later)
    {
        var state = WorldRuntimeCodec.Decode(await (later ? ThirdHearingHousehold : FirstHearingHousehold).Value);
        var actor = HearingHouseholdActor(state, later);
        var household = state.Society.Society.GetAgent(actor).HouseholdId!;
        var choices = new NativeLandHearingChoices { Request = true, Consent = true, Rule = true };
        using var world = RestoreHouseholdHearing(state, choices);
        world.Resume();
        using var host = new NativeConversationCheckpointHost(world);
        await HouseholdHearingUntil(world, host, () => world.Towns[0].LandRecords.Cases.Any(item =>
            item.Property?.Transfer is { ResultBuilding.HouseholdId: null }), NativeHearingDay * 3);
        choices.Target = household;
        choices.Rule = false;
        await HouseholdHearingUntil(world, host, () => world.Towns[0].LandRecords.Cases.Any(item =>
            item.Property?.Request.TargetHouseholdId == household), NativeHearingDay * 2);
        var pending = world.Towns[0].LandRecords.Cases.Single(item => item.Property?.Request.TargetHouseholdId == household);
        Assert.Null(pending.Property!.Transfer);
        Assert.Contains(choices.Selected, choice => choice.Choice.Contains("|hearing_property_request|", StringComparison.Ordinal) &&
            choice.Choice.EndsWith("|" + household, StringComparison.Ordinal));
        AssertInvalidHouseholdProperty(world.ExportState(), pending);
        var before = host.Saved();
        Assert.False((await world.AdvanceOneTickAsync(() => false)).Advanced);
        Assert.Equal(before, WorldRuntimeCodec.Encode(world.ExportState()));
        choices.Request = false;
        choices.Rule = true;
        // Reload restarts external calls. Start both continuations after the
        // public cancellation boundary, with identical fresh provider inputs.
        await AwaitConversationRequestsAsync(world);
        world.CancelPendingHostedDecisions();
        var bytes = host.Checkpoint();
        using var replay = RestoreHouseholdHearing(WorldRuntimeCodec.Decode(bytes), new NativeLandHearingChoices
        { Target = household, Consent = true, Rule = true });
        replay.Resume();
        using var replayHost = new NativeConversationCheckpointHost(replay);
        await HouseholdHearingPair(world, host, replay, replayHost, () => world.Towns[0].LandRecords.Cases
            .Single(item => item.Id == pending.Id).Property!.Transfer is not null, NativeHearingDay * 2);
        var granted = world.Towns[0].LandRecords.Cases.Single(item => item.Id == pending.Id);
        var transfer = granted.Property!.Transfer!;
        Assert.Equal(household, transfer.ResultBuilding.HouseholdId);
        Assert.Equal(household, world.WorldSimulation.Buildings.Single(item => item.InstanceId == NativeHearingHouse).HouseholdId);
        Assert.Contains(granted.Property.Consents, consent => consent.AgentId == actor && consent.Agreed);
        Assert.Equal((NativeHearingJudge, "grant", household), (Assert.Single(granted.Rulings).Judge.AgentId,
            granted.Rulings[0].Outcome.Kind, granted.Rulings[0].Outcome.HouseholdId));
        Assert.Equal(transfer.PriorLots.Select(lot => (lot.Id, lot.Quantity, lot.StorageBuildingId)),
            transfer.ResultLots.Select(lot => (lot.Id, lot.Quantity, lot.StorageBuildingId)));
        Assert.All(transfer.ResultLots, lot => Assert.Equal(household, lot.OwnerId));
        AssertInvalidHouseholdProperty(world.ExportState(), granted);
    }

    [Theory]
    [InlineData(null, "end")]
    [InlineData(false, "end")]
    [InlineData(false, "renew")]
    [InlineData(false, "amend")]
    [InlineData(true, "end")]
    [InlineData(true, "renew")]
    [InlineData(true, "amend")]
    public async Task NativeFoundedHouseholdExpiryAcceptsTheActualJudgeRuling(bool? later, string outcome)
    {
        var state = WorldRuntimeCodec.Decode(await (later is null ? ShortExpiryHearing : later.Value ? ThirdExpiryHearing : FirstExpiryHearing).Value);
        var household = later is null ? "household:camp-alpha" : state.Society.Society.GetAgent(HearingHouseholdActor(state, later.Value)).HouseholdId!;
        var actor = later is null ? state.Society.Society.GetHousehold(household).MemberIds[0] : HearingHouseholdActor(state, later.Value);
        var choices = new NativeHouseholdExpiryChoices(household, outcome);
        using var world = RestoreHouseholdHearing(state, choices);
        world.Resume();
        using var host = new NativeConversationCheckpointHost(world);
        var item = Assert.Single(world.Towns[0].LandRecords.Cases, item => item.Kind == "expiry");
        await HouseholdHearingUntil(world, host, () => world.Towns[0].LandRecords.Cases.Single(saved => saved.Id == item.Id).Status == "settled", 20);
        var settled = world.Towns[0].LandRecords.Cases.Single(saved => saved.Id == item.Id);
        var ruling = Assert.Single(settled.Rulings);
        Assert.Equal((NativeHearingJudge, outcome, household), (ruling.Judge.AgentId, ruling.Outcome.Kind, ruling.Outcome.HouseholdId));
        Assert.Contains(choices.Selected, choice => choice.Contains("|hearing_rule|", StringComparison.Ordinal) &&
            choice.EndsWith("|" + outcome, StringComparison.Ordinal));
        var tiles = TownLandHearingRules.CurrentRevision(settled).Tiles;
        var rights = world.ExportState().HouseholdLandUseRights!.Where(right => right.Tiles.Any(tiles.Contains)).ToArray();
        if (outcome == "end") Assert.Empty(rights);
        else
        {
            Assert.NotEmpty(rights);
            Assert.All(rights, right => { Assert.Equal(household, right.HouseholdId); Assert.True(right.AgreedEndTick > world.WorldTick); });
        }
        Assert.NotEmpty(ruling.EvidenceIds);
        Assert.Contains(settled.Responses, response => response.AgentId == actor);
        AssertInvalidHouseholdRuling(world.ExportState(), settled);
        await AwaitConversationRequestsAsync(world);
        world.CancelPendingHostedDecisions();
        var bytes = host.Checkpoint();
        using var replay = RestoreHouseholdHearing(WorldRuntimeCodec.Decode(bytes), new NativeHouseholdExpiryChoices(household, outcome));
        replay.Resume();
        using var replayHost = new NativeConversationCheckpointHost(replay);
        await HouseholdHearingPair(world, host, replay, replayHost, () => true, 0);
    }

    private static string HearingHouseholdActor(WorldRuntimeState state, bool later) =>
        state.Society.Society.Births.OrderBy(birth => birth.CommittedTick).ElementAt(later ? 2 : 0).ChildId;

    // This fixture prompts every participant each tick. Dispatch the whole
    // population so repeated prompts cannot starve the last queued adult.
    private static WorldRuntime RestoreHouseholdHearing(WorldRuntimeState state, IDecisionProvider choices)
    {
        var budget = state.Agents.Count;
        state = state with { Society = state.Society with { Cognition = state.Society.Cognition with { MaxDispatchPerCycle = budget } } };
        return WorldRuntime.Restore(state, _ => choices, maxCognitionDispatchPerCycle: budget);
    }

    private static async Task<byte[]> CreateHearingHouseholdAsync(bool later)
    {
        var state = WorldRuntimeCodec.Decode(await LaterHearingGovernment.Value);
        var actor = HearingHouseholdActor(state, later);
        var choices = new NativeHouseholdFormationChoices(actor);
        using var world = RestoreHouseholdHearing(state, choices);
        // Match the reported preparation: clear the two original homes through
        // actual adult departures, so another available home does not postpone founding.
        var housed = world.Society.Agents.Where(person => person.HouseholdId is not null &&
            world.WorldSimulation.Buildings.Any(building => building.HouseholdId == person.HouseholdId &&
                world.WorldContent.Buildings.Any(definition => definition.CanonicalId == building.DefinitionId && definition.Tags.Contains("house"))))
            .Select(person => person.Id).ToArray();
        foreach (var member in housed) Assert.True(world.DisplaceAdult(member));
        world.Resume();
        using var host = new NativeConversationCheckpointHost(world);
        await HouseholdHearingUntil(world, host, () => world.Society.GetAgent(actor).HouseholdId is { } home &&
            world.Society.GetHousehold(home).MemberIds.SequenceEqual([actor]), 40);
        var household = world.Society.GetAgent(actor).HouseholdId!;
        Assert.InRange(household.Length, 1, DerivedId.MaximumLength);
        Assert.Contains("household_found", choices.Selected);
        Assert.Equal([actor], world.Society.GetHousehold(household).MemberIds);
        return host.Saved();
    }

    private static async Task<byte[]> CreateHouseholdExpiryAsync(bool? later)
    {
        var state = WorldRuntimeCodec.Decode(await (later is null ? LaterHearingGovernment : later.Value ? ThirdHearingHousehold : FirstHearingHousehold).Value);
        var household = later is null ? "household:camp-alpha" : state.Society.Society.GetAgent(HearingHouseholdActor(state, later.Value)).HouseholdId!;
        var farm = state.WorldSimulation!.Buildings.Single(building => building.InstanceId == "first-town-farmhouse");
        var definition = state.WorldContent!.Buildings.Single(building => building.CanonicalId == farm.DefinitionId);
        var plot = WorldContentSimulationRules.Footprint(definition, farm).ToHashSet();
        state = state with
        {
            HouseholdLandUseRights = state.HouseholdLandUseRights!.Select(right =>
            right.Tiles.Any(plot.Contains) ? right with { AgreedEndTick = state.Society.Society.WorldTick + 8 } : right).ToArray()
        };
        var choices = new NativeHouseholdExpiryChoices(household, null);
        using var world = RestoreHouseholdHearing(state, choices);
        if (farm.HouseholdId != household)
        {
            var reassigned = world.ReassignBuilding(farm.InstanceId, farm.TownId, farm.HouseholdId, null, household);
            Assert.True(reassigned.Applied, reassigned.Failure);
        }
        world.Resume();
        using var host = new NativeConversationCheckpointHost(world);
        await HouseholdHearingUntil(world, host, () => choices.Offered.Any(id => id.Contains("|hearing_rule|", StringComparison.Ordinal)), NativeHearingDay * 2);
        Assert.Single(world.Towns[0].LandRecords.Cases, item => item.Kind == "expiry");
        return host.Saved();
    }

    private static async Task HouseholdHearingStep(WorldRuntime world, NativeConversationCheckpointHost host)
    {
        await AwaitConversationRequestsAsync(world);
        NativeHearingPromptAll(world);
        await host.Advance();
        await AwaitConversationRequestsAsync(world);
        var bytes = host.Saved();
        using var restored = WorldRuntime.Restore(WorldRuntimeCodec.Decode(bytes), _ => new NativeLandHearingChoices());
        Assert.Equal(bytes, WorldRuntimeCodec.Encode(restored.ExportState()));
    }

    private static async Task HouseholdHearingUntil(WorldRuntime world, NativeConversationCheckpointHost host, Func<bool> done, int limit)
    {
        for (var tick = 0; tick < limit && !done(); tick++) await HouseholdHearingStep(world, host);
        Assert.True(done(), "The actual native household hearing boundary was not reached: " + string.Join("; ", world.ExportState().Events.TakeLast(5).Select(item => item.Kind + ":" + item.Detail)));
    }

    private static async Task HouseholdHearingPair(WorldRuntime world, NativeConversationCheckpointHost host,
        WorldRuntime replay, NativeConversationCheckpointHost replayHost, Func<bool> done, int limit)
    {
        for (var tick = 0; tick < limit && !done(); tick++)
        {
            await HouseholdHearingStep(world, host);
            await HouseholdHearingStep(replay, replayHost);
            Assert.Equal(host.Saved(), replayHost.Saved());
        }
        Assert.True(done(), "The actual property hearing did not complete.");
        for (var tick = 0; tick < 2; tick++)
        {
            await HouseholdHearingStep(world, host);
            await HouseholdHearingStep(replay, replayHost);
            Assert.Equal(host.Saved(), replayHost.Saved());
        }
    }

    private static void AssertInvalidHouseholdRuling(WorldRuntimeState state, TownLandCase item)
    {
        var ruling = Assert.Single(item.Rulings);
        foreach (var invalid in new[] { ruling.Outcome.HouseholdId + ":unknown", " " + ruling.Outcome.HouseholdId, ruling.Outcome.HouseholdId + "\0" })
        {
            var damaged = item with { Rulings = [ruling with { Outcome = ruling.Outcome with { HouseholdId = invalid } }] };
            var changed = state with
            {
                Towns = state.Towns!.Select(town => town with
                { LandRecords = town.LandRecords with { Cases = town.LandRecords.Cases.Select(saved => saved.Id == item.Id ? damaged : saved).ToArray() } }).ToArray()
            };
            Assert.Throws<InvalidDataException>(() => WorldRuntimeCodec.Encode(changed));
        }
    }

    private static void AssertInvalidHouseholdProperty(WorldRuntimeState state, TownLandCase item)
    {
        var household = item.Property!.Request.TargetHouseholdId!;
        foreach (var invalid in new[] { household + ":unknown", " " + household, household + "\0" })
        {
            var damaged = item with { Property = item.Property with { Request = item.Property.Request with { TargetHouseholdId = invalid } } };
            var changed = state with
            {
                Towns = state.Towns!.Select(town => town with
                { LandRecords = town.LandRecords with { Cases = town.LandRecords.Cases.Select(saved => saved.Id == item.Id ? damaged : saved).ToArray() } }).ToArray()
            };
            Assert.Throws<InvalidDataException>(() => WorldRuntimeCodec.Encode(changed));
        }
    }

    private sealed class NativeHouseholdFormationChoices(string actor) : IDecisionProvider
    {
        public DecisionProviderKind Kind => DecisionProviderKind.LargeLanguageModel;
        public long ProviderEpoch => 1;
        public ConcurrentQueue<string> Selected { get; } = new();
        public ValueTask<CognitionDecisionResponse> DecideAsync(CognitionDecisionRequest request, CancellationToken cancellationToken = default)
        {
            var observation = request.Observation;
            var selected = observation.Candidates.FirstOrDefault(candidate => observation.AgentId == actor && candidate.Id == "household_found") ??
                observation.Candidates.FirstOrDefault(candidate => observation.AgentId == actor && candidate.Id.StartsWith("household_ask:", StringComparison.Ordinal)) ??
                observation.Candidates.FirstOrDefault(candidate => observation.AgentId != actor && candidate.Id == "household_refuse:" + actor) ??
                observation.Candidates.Single(candidate => candidate.Id == "safe_idle");
            Selected.Enqueue(selected.Id);
            return ValueTask.FromResult(new CognitionDecisionResponse(request.RequestId, observation.AgentId, Kind, ProviderEpoch,
                observation.RunEpoch, observation.DecisionGeneration, observation.ObservationDigest, selected.Id, 1,
                new Dictionary<string, double> { [selected.Id] = 1 }));
        }
    }

    private sealed class NativeHouseholdExpiryChoices(string household, string? outcome) : IDecisionProvider
    {
        public DecisionProviderKind Kind => DecisionProviderKind.LargeLanguageModel;
        public long ProviderEpoch => 1;
        public ConcurrentQueue<string> Selected { get; } = new();
        public ConcurrentQueue<string> Offered { get; } = new();
        public ValueTask<CognitionDecisionResponse> DecideAsync(CognitionDecisionRequest request, CancellationToken cancellationToken = default)
        {
            var observation = request.Observation;
            foreach (var candidate in observation.Candidates) if (observation.AgentId == NativeHearingJudge) Offered.Enqueue(candidate.Id);
            CognitionCandidate? Find(string action) => observation.Candidates.FirstOrDefault(candidate => candidate.Id.Contains("|" + action + "|", StringComparison.Ordinal));
            var selected = Find("read") ?? Find("hearing_inspect") ?? Find("hearing_answer");
            if (outcome is not null && observation.AgentId == NativeHearingJudge)
                selected ??= observation.Candidates.FirstOrDefault(candidate => candidate.Id.Contains("|hearing_rule|", StringComparison.Ordinal) && candidate.Id.EndsWith("|" + outcome, StringComparison.Ordinal));
            selected ??= observation.Candidates.Single(candidate => candidate.Id == "safe_idle");
            Selected.Enqueue(selected.Id);
            var evidence = selected.Description.Split([' ', ';', ','], StringSplitOptions.RemoveEmptyEntries)
                .Where(token => token.StartsWith("land-evidence:", StringComparison.Ordinal)).Select(token => token.Split('=')[0]).Distinct(StringComparer.Ordinal).ToArray();
            var ruling = selected.Id.Contains("|hearing_rule|", StringComparison.Ordinal);
            return ValueTask.FromResult(new CognitionDecisionResponse(request.RequestId, observation.AgentId, Kind, ProviderEpoch,
                observation.RunEpoch, observation.DecisionGeneration, observation.ObservationDigest, selected.Id, 1,
                new Dictionary<string, double> { [selected.Id] = 1 }, CivicLandHearing: new(
                    Statement: "Resolve this exact expired permission using its inspected record and actual personal responses.",
                    HouseholdId: ruling ? household : null, AgreedEndTick: ruling && outcome != "end" ? observation.WorldTick + NativeHearingDay * 4 : null,
                    EvidenceIds: evidence, RequestedOutcome: ruling ? outcome : null)));
        }
    }
}
