using System.Collections.Concurrent;
using System.Net;
using System.Text;
using System.Text.Json;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.World;
using ClankerWorld.Simulation.Society;
using ClankerWorld.Server.Observation;
using ClankerWorld.Server.Runtime;
using ClankerWorld.Server.Saves;
using GodotSnapshot = ClankerWorld.GodotClient.Protocol.OwnerWorldSnapshot;

namespace ClankerWorld.Tests;

public sealed class TownCouncilRuntimeTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private const string FirstAuthor = NormalPathWorld.Founder1;
    private const string SecondAuthor = NormalPathWorld.Founder3;
    private const string FirstText = "Harvest dates: Publish harvest dates for our Town.";
    private const string SecondText = "Storm notices: Share notices about storms in our Town.";

    [Fact]
    public async Task OrdinaryFourFounderWorldOpensCouncilElectionOnDayOneWithoutBuiltInConsentAndReplays()
    {
        using var world = NormalPathWorld.CreateGenerated("four-founder-day-one-council",
            _ => new ActionCoverageRecorder(chooseIdle: true));
        var initial = world.ExportState();
        var town = Assert.Single(initial.Towns!);
        var adults = initial.Society.Society.Agents.Where(person =>
            town.ResidentIds.Contains(person.Id) && person.Status == SocietyAgentStatus.Active &&
            person.AgeBand is SocietyAgeBand.Adult or SocietyAgeBand.Elder).Select(person => person.Id).Order().ToArray();
        Assert.Equal(4, adults.Length);
        Assert.True((await world.AdvanceOneTickAsync()).Advanced);
        var council = world.Towns[0].Council!;
        var election = Assert.Single(council.ElectionHistory);
        Assert.Equal("initial", election.Kind);
        Assert.Equal(1, WorldCalendarRules.FromTick(election.OpenedTick, world.WorldSystems.Config).DayIndex + 1);
        Assert.Equal(adults, election.Voters);
        Assert.Equal("failed", election.Stage);
        Assert.Empty(election.Candidates);
        Assert.Empty(election.Ballots);
        Assert.Empty(council.Candidates);
        Assert.Equal("all_adult", council.Form);
        world.Validate();
        var bytes = WorldRuntimeCodec.Encode(world.ExportState());
        using var replay = WorldRuntime.Restore(WorldRuntimeCodec.Decode(bytes),
            _ => new ActionCoverageRecorder(chooseIdle: true));
        Assert.Equal(bytes, WorldRuntimeCodec.Encode(replay.ExportState()));
        Assert.True((await world.AdvanceOneTickAsync()).Advanced);
        Assert.True((await replay.AdvanceOneTickAsync()).Advanced);
        Assert.Equal(WorldRuntimeCodec.Encode(world.ExportState()), WorldRuntimeCodec.Encode(replay.ExportState()));
    }

    [Theory]
    [InlineData("built_in")]
    [InlineData("built_in_yes")]
    [InlineData("built_in_register")]
    [InlineData("failed_personal")]
    [InlineData("silent_personal")]
    public async Task CivicKnowledgeNeverTurnsBuiltInRulesFailureOrSilenceIntoConsent(string mode)
    {
        var provider = new CivicBoundaryProvider(mode);
        using var source = NormalPathWorld.CreateGenerated("council-personal-consent-boundary", _ => provider);
        var initial = source.ExportState();
        var town = initial.Towns![0];
        var governance = TownCouncilRules.SubmitProposal(town.Council!, town.Id, FirstAuthor, "law", null,
            FirstText, "same", town.ResidentIds, 0, initial.WorldSystems!.Config.TicksPerDay);
        // These controlled readers are at the actual notice place and have recorded reading this notice.
        foreach (var actor in town.ResidentIds)
            foreach (var notice in governance.Notices)
                governance = TownCouncilRules.LearnNotice(governance, actor, notice.Id, 0);
        using var world = WorldRuntime.Restore(initial with
        {
            Towns = [town with { Council = governance }],
            Agents = initial.Agents.Select(actor => actor with { Position = town.OriginSite!.Value }).ToArray(),
        }, _ => provider);
        var fellBack = false;
        for (var tick = 0; tick < 4; tick++)
        {
            var result = await world.AdvanceOneTickAsync();
            fellBack |= result.Decisions.Any(decision => decision.Admission.FellBack);
        }
        Assert.NotEmpty(provider.Observations);
        Assert.Contains(provider.Observations, observation => observation.Candidates.Any(candidate => candidate.Id.Contains("|yes|", StringComparison.Ordinal)));
        Assert.Empty(world.Towns[0].Council!.Candidates);
        var proposal = Assert.Single(world.Towns[0].Council!.Proposals);
        Assert.Empty(proposal.Votes);
        Assert.Equal("pending", proposal.Status);
        Assert.Equal(mode == "failed_personal", fellBack);
        world.Validate();
    }

    [Theory]
    [InlineData("Numbers: Discuss tick 999999999999999999999999999999.")]
    public async Task OrdinaryProposalTextWithLargeNumbersDoesNotStopLaterCivicObservations(string text)
    {
        var provider = new CivicProvider(FirstAuthor, proposalOverride: text);
        using var world = NormalPathWorld.CreateGenerated("council-large-proposal-number", id =>
            id == FirstAuthor ? provider : new ActionCoverageRecorder(chooseIdle: true));
        for (var tick = 0; tick < 40; tick++) Assert.True((await world.AdvanceOneTickAsync()).Advanced);
        Assert.Equal(text, Assert.Single(world.Towns[0].Council!.Proposals).Text);
        Assert.Contains(provider.Observations, observation => observation.Self?.CivicNote?.Contains("Discuss", StringComparison.Ordinal) == true);
        world.Validate();
    }

    [Fact]
    public async Task TwoGeneratedTownsUseOrdinaryPersonalTurnsAndClientProjectionAndRoundtripIndependently()
    {
        var providers = new ConcurrentDictionary<string, CivicProvider>(StringComparer.Ordinal);
        using var generated = NormalPathWorld.CreateGenerated("council-two-normal-towns", id => providers.GetOrAdd(id, key => new CivicProvider(key)));
        var initial = generated.ExportState();
        var ids = initial.Towns![0].ResidentIds.ToArray();
        var first = initial.Towns[0];
        var secondSite = initial.Map.Tiles.Select(t => t.Position).First(p => initial.Map.IsBuildable(p) && !first.BorderTiles.Contains(p));
        var second = new TownRuntimeState("town:second", "Second Town", "founded", 0, ids[2..], [], [secondSite], secondSite,
            TownCouncilState.Create(ids[2..]), TownGovernmentState.Create());
        var split = initial with
        {
            Towns = [first with { ResidentIds = ids[..2], Council = TownCouncilState.Create(ids[..2]) }, second],
            Agents = initial.Agents.Select(p => p with
            {
                Position = ids.AsSpan(0, 2).Contains(p.AgentId) ? first.OriginSite!.Value : secondSite,
                FullnessBasisPoints = 8_000,
            }).ToArray(),
        };
        using var world = WorldRuntime.Restore(split, id => providers.GetOrAdd(id, key => new CivicProvider(key)));
        var directory = Directory.CreateTempSubdirectory("clankerworld-civic-client-");
        try
        {
            var presence = new OwnerClientPresenceLease(TimeSpan.FromMinutes(1));
            presence.RecordAuthenticatedReconnect("test-device");
            var file = new WorldStateFile(Path.Combine(directory.FullName, "world.json"));
            var logger = new RecordingLogger<WorldRuntimeService>();
            using var service = new WorldRuntimeService(world, file, presence, logger);
            for (var tick = 0; tick < 80 && world.Towns.Any(t => !t.Council!.Proposals.Any(p => p.Status == "passed")); tick++)
            {
                Assert.True(await service.TryAdvanceOnceAsync());
                await Task.Delay(2);
            }
            Assert.All(world.Towns, town =>
            {
                var proposal = Assert.Single(town.Council!.Proposals);
                Assert.Equal("passed", proposal.Status);
                Assert.Equal(2, proposal.Votes.Count);
                Assert.All(proposal.Votes, vote => Assert.Contains(vote.AgentId, town.ResidentIds));
                Assert.Equal(town.Id == "town:first" ? FirstText : SecondText, proposal.Text);
                Assert.NotEmpty(town.Council.Knowledge);
            });
            foreach (var provider in providers.Values.Where(provider => provider.Observations.Count > 0))
            {
                if (provider.Observations.First().AgentId is not (FirstAuthor or SecondAuthor))
                    Assert.Contains(provider.Observations, o => o.Candidates.Any(c => c.Id.Contains("|read|", StringComparison.Ordinal)));
                Assert.Contains(provider.Observations, o => o.Self?.CivicNote?.Contains("Town", StringComparison.Ordinal) == true);
                Assert.DoesNotContain(provider.Observations, o => o.Self?.CivicNote?.Contains("tick ", StringComparison.Ordinal) == true);
            }
            Assert.Contains(logger.Messages, m => m.Contains("town_civic", StringComparison.Ordinal) && m.Contains("transition=DecisionRecorded", StringComparison.Ordinal));
            Assert.DoesNotContain(logger.Messages, m => m.Contains(FirstText, StringComparison.Ordinal) || m.Contains(SecondText, StringComparison.Ordinal));
            Assert.DoesNotContain(logger.Messages, m => m.Contains("town_civic_action", StringComparison.Ordinal) || m.Contains("Read Town notice", StringComparison.Ordinal));
            world.Pause();
            var bytes = WorldRuntimeCodec.Encode(world.ExportState());
            file.Save(world);
            using var restored = file.LoadOrCreate(world.ExportState().WorldSeed);
            Assert.Equal(bytes, WorldRuntimeCodec.Encode(restored.ExportState()));
            var snapshot = new OwnerWorldObservationStore(restored).GetSnapshot();
            var client = JsonSerializer.Deserialize<GodotSnapshot>(JsonSerializer.Serialize(snapshot, JsonOptions), JsonOptions)!;
            Assert.Equal(2, client.Towns.Count);
            Assert.All(client.Towns, town =>
            {
                Assert.Equal(2, town.Council!.MemberNames.Count);
                Assert.Equal("passed", Assert.Single(town.Council.Proposals).Status);
                Assert.Null(town.Council.Election);
            });
            var savedClock = restored.WorldTick;
            Assert.False((await restored.AdvanceOneTickAsync()).Advanced);
            Assert.Equal(savedClock, restored.WorldTick);
        }
        finally { directory.Delete(recursive: true); }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NormalTurnsRegisterCandidatesAndCastStructuredBallotsWhoseConsentAndChoicesReload(bool longDescendantId)
    {
        var observed = new ConcurrentQueue<AgentObservation>();
        using var source = NormalPathWorld.CreateGenerated("council-normal-election", _ => new ElectionProvider(observed));
        var firstCandidate = longDescendantId ? "child:" + FirstAuthor + ":" + new string('a', 250) : FirstAuthor;
        // Descendant IDs retain ancestry and may be longer than a model action token.
        var initialJson = Encoding.UTF8.GetString(WorldRuntimeCodec.Encode(source.ExportState()))
            .Replace(FirstAuthor, firstCandidate, StringComparison.Ordinal);
        using var generated = WorldRuntime.Restore(WorldRuntimeCodec.Decode(Encoding.UTF8.GetBytes(initialJson)),
            _ => new ElectionProvider(observed));
        await generated.AdvanceOneTickAsync();
        Assert.Equal(4, generated.Towns[0].Council!.Candidates.Count);
        var opening = generated.Towns[0].Council!.Election!;
        Assert.Equal(4, opening.Voters.Count);
        Assert.Equal(3, opening.Candidates.Count); // Voting opens as soon as three of the four founders consent.
        Assert.Equal(1, WorldCalendarRules.FromTick(opening.OpenedTick, generated.WorldSystems.Config).DayIndex + 1);
        var positioned = generated.ExportState();
        using var world = WorldRuntime.Restore(positioned with
        {
            Agents = positioned.Agents.Select(p => p with { Position = positioned.Towns![0].OriginSite!.Value }).ToArray(),
        }, _ => new ElectionProvider(observed));
        for (var tick = 0; tick < 40 && world.Towns[0].Council!.Election!.Ballots.Count < 4; tick++)
            await world.AdvanceOneTickAsync();
        var election = world.Towns[0].Council!.Election!;
        Assert.NotEmpty(election.Ballots);
        Assert.All(election.Ballots, b => Assert.Equal(3, b.Choices.Count));
        Assert.Contains(firstCandidate, election.Candidates);
        Assert.Contains(election.Ballots, b => b.Choices.Contains(firstCandidate));
        Assert.Equal(3, election.Candidates.Count); // Later personal registrations stay in the continuing register.
        Assert.Equal(4, world.Towns[0].Council!.Candidates.Count);
        Assert.Contains(observed, o => o.Candidates.Any(c => c.Id.Contains("|ballot|", StringComparison.Ordinal)) && o.Self?.CivicNote is not null);
        var encoded = WorldRuntimeCodec.Encode(world.ExportState());
        using var restored = WorldRuntime.Restore(WorldRuntimeCodec.Decode(encoded));
        Assert.Equal(encoded, WorldRuntimeCodec.Encode(restored.ExportState()));
        Assert.Equal(election.Ballots.Select(b => b.AgentId + ":" + string.Join(",", b.Choices)),
            restored.Towns[0].Council!.Election!.Ballots.Select(b => b.AgentId + ":" + string.Join(",", b.Choices)));
    }

    [Fact]
    public async Task CompletedWorldDrawAndAcceptedEventsReplayWithoutRerolling()
    {
        using var generated = NormalPathWorld.CreateGenerated("council-private-saved-draw", _ => new ActionCoverageRecorder(chooseIdle: true));
        for (var ordinal = 5; ordinal <= 8; ordinal++)
        {
            var setup = generated.ExportState();
            var site = setup.Towns![0].BorderTiles.First(p => setup.Map.IsBuildable(p) &&
                !setup.Map.Resources.Any(r => r.Position == p) && !setup.Map.MapObjects.Any(o => o.Position == p) &&
                !setup.Agents.Any(i => i.Position == p));
            generated.AddAgent($"agent:{ordinal:D32}", site);
        }
        var initial = generated.ExportState();
        var society = initial.Society.Society;
        var oldDay = society.Config.TicksPerWorldDay;
        const int shortDay = 10;
        var town = initial.Towns![0];
        var ids = town.ResidentIds.ToArray();
        var governance = TownCouncilState.Create(ids);
        foreach (var id in ids.Take(5)) governance = TownCouncilRules.Register(governance, id, true, null, ids, 0);
        governance = TownCouncilRules.Advance(governance, town.Id, initial.WorldSeed, ids, 0, shortDay);
        governance = TownCouncilRules.VoteElection(governance, governance.Election!.Id, ids[0], [ids[0], ids[1], ids[2]], 0);
        governance = TownCouncilRules.VoteElection(governance, governance.Election!.Id, ids[1], [ids[0], ids[3]], 0);
        var shortened = initial with
        {
            Towns = [town with { Council = governance }],
            WorldSystems = RegionalWeatherRules.Initialize(initial.WorldSystems! with
            {
                Config = initial.WorldSystems.Config with { TicksPerDay = shortDay, CalendarOffsetTicks = 0 },
                RegionalWeather = null,
            }, initial.Map),
            Society = initial.Society with
            {
                Society = society with
                {
                    Config = society.Config with { TicksPerWorldDay = shortDay },
                    Agents = society.Agents.Select(p => p with
                    {
                        BirthTick = p.BirthTick / oldDay * shortDay,
                        BirthLifeTick = p.BirthLifeTick is { } birth ? birth / oldDay * shortDay : null,
                    }).ToArray(),
                }
            },
        };
        using var world = WorldRuntime.Restore(shortened, _ => new ActionCoverageRecorder(chooseIdle: true));
        for (var tick = 0; tick < shortDay * 2; tick++) await world.AdvanceOneTickAsync();
        var accepted = Assert.Single(world.Towns[0].Council!.ElectionHistory);
        Assert.Equal("completed", accepted.Stage);
        Assert.Equal(3, accepted.DrawOrder.Count);
        Assert.DoesNotContain(ids[4], accepted.DrawOrder);
        var encoded = WorldRuntimeCodec.Encode(world.ExportState());
        using var replay = WorldRuntime.Restore(WorldRuntimeCodec.Decode(encoded), _ => new ActionCoverageRecorder(chooseIdle: true));
        Assert.Equal(encoded, WorldRuntimeCodec.Encode(replay.ExportState()));
        await world.AdvanceOneTickAsync();
        await replay.AdvanceOneTickAsync();
        Assert.Equal(WorldRuntimeCodec.Encode(world.ExportState()), WorldRuntimeCodec.Encode(replay.ExportState()));
        Assert.Equal(accepted.DrawOrder, replay.Towns[0].Council!.ElectionHistory[0].DrawOrder);
    }

    [Fact]
    public async Task TravelersGainNoUnseenCivicInformationAndKeepRecordedCouncilMembership()
    {
        var recorder = new ActionCoverageRecorder(chooseIdle: true);
        using var generated = NormalPathWorld.CreateGenerated("council-unseen-travel", _ => recorder);
        var initial = generated.ExportState();
        var town = initial.Towns![0];
        var governance = TownCouncilRules.SubmitProposal(town.Council!, town.Id, FirstAuthor, "law", null,
            FirstText, "same", town.ResidentIds, 0, initial.WorldSystems!.Config.TicksPerDay);
        var far = initial.Map.Tiles.Select(t => t.Position).First(p => initial.Map.IsBuildable(p) && !town.BorderTiles.Contains(p));
        using var world = WorldRuntime.Restore(initial with
        {
            Towns = [town with { Council = governance }],
            Agents = initial.Agents.Select(p => p with { Position = far }).ToArray(),
        }, _ => recorder);
        await world.AdvanceOneTickAsync();
        Assert.All(recorder.OfferedByAgent.Values, offered => Assert.DoesNotContain(offered.Keys, id =>
            id.Contains("|yes|", StringComparison.Ordinal) || id.Contains("|no|", StringComparison.Ordinal)));
        Assert.All(world.Towns[0].Council!.Knowledge, _ => Assert.Fail("A traveler was given an unseen notice."));
        Assert.Equal(4, world.Towns[0].Council!.Members.Count);
        Assert.Equal("pending", world.Towns[0].Council!.Proposals[0].Status);
    }

    [Fact]
    public async Task PendingProposalRetainsWindowAcrossPauseReloadAndRejectedPreparedTick()
    {
        var provider = new CivicProvider(FirstAuthor);
        using var generated = NormalPathWorld.CreateGenerated("council-window-rollback", id => id == FirstAuthor ? provider : new ActionCoverageRecorder(chooseIdle: true));
        var before = WorldRuntimeCodec.Encode(generated.ExportState());
        var refused = await generated.AdvanceOneTickAsync(() => false);
        Assert.False(refused.Advanced);
        Assert.Equal(before, WorldRuntimeCodec.Encode(generated.ExportState()));
        await generated.AdvanceOneTickAsync();
        var proposal = Assert.Single(generated.Towns[0].Council!.Proposals);
        generated.Pause();
        var paused = generated.ExportState();
        for (var attempt = 0; attempt < 3; attempt++) Assert.False((await generated.AdvanceOneTickAsync()).Advanced);
        Assert.Equal(proposal.DeadlineTick, generated.Towns[0].Council!.Proposals[0].DeadlineTick);
        using var restored = WorldRuntime.Restore(WorldRuntimeCodec.Decode(WorldRuntimeCodec.Encode(paused)),
            _ => new ActionCoverageRecorder(chooseIdle: true));
        Assert.Equal(proposal.DeadlineTick, restored.Towns[0].Council!.Proposals[0].DeadlineTick);
        restored.Resume();
        await restored.AdvanceOneTickAsync();
        Assert.Equal(proposal.DeadlineTick, restored.Towns[0].Council!.Proposals[0].DeadlineTick);
    }

    [Fact]
    public async Task ConcurrentCouncilChangeDiscardsDelayedProposalVote()
    {
        var provider = new CivicProvider(FirstAuthor, holdVote: true);
        using var generated = NormalPathWorld.CreateGenerated("council-stale-held-vote", id => id == FirstAuthor ? provider : new ActionCoverageRecorder(chooseIdle: true));
        for (var tick = 0; tick < 80 && !provider.VoteStarted.Task.IsCompleted; tick++)
        {
            await generated.AdvanceOneTickNonBlockingAsync();
            await Task.Delay(2);
        }
        await provider.VoteStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var original = Assert.Single(generated.Towns[0].Council!.Proposals);
        var state = generated.ExportState();
        var site = state.Towns![0].BorderTiles.First(p => state.Map.IsBuildable(p) &&
            !state.Map.Resources.Any(r => r.Position == p) && !state.Map.MapObjects.Any(o => o.Position == p) &&
            !state.Agents.Any(i => i.Position == p));
        generated.AddAgent("agent:00000000000000000000000000000099", site);
        Assert.Equal("cancelled", generated.Towns[0].Council!.Proposals[0].Status);
        provider.ReleaseVote.TrySetResult(true);
        for (var tick = 0; tick < 8; tick++)
        {
            await generated.AdvanceOneTickNonBlockingAsync();
            await Task.Delay(2);
        }
        var cancelled = generated.Towns[0].Council!.Proposals.Single(p => p.Id == original.Id);
        Assert.Empty(cancelled.Votes);
        Assert.Equal("cancelled", cancelled.Status);
        generated.Validate();
    }

    [Theory]
    [InlineData("[7]")]
    [InlineData("[{}]")]
    [InlineData("[\"a\",\"b\",\"c\",\"d\"]")]
    public async Task MalformedStructuredCivicBallotFailsAsProviderData(string ballot)
    {
        using var handler = new BallotHandler(ballot);
        using var client = new HttpClient(handler);
        var provider = new OpenAiCompatibleDecisionProvider(client, () => "synthetic-key", new Uri("https://model.test/v1/chat/completions"), "synthetic-model");
        var observation = new AgentObservation("a", 0, 0, 0, "digest", 8_000, [new("safe_idle", "Wait safely.")]);
        await Assert.ThrowsAsync<InvalidDataException>(async () => await provider.DecideAsync(new("request", provider.ProviderEpoch, observation)));
    }

    private sealed class ElectionProvider(ConcurrentQueue<AgentObservation> observed) : IDecisionProvider
    {
        public DecisionProviderKind Kind => DecisionProviderKind.LargeLanguageModel;
        public long ProviderEpoch => 1;
        public ValueTask<CognitionDecisionResponse> DecideAsync(CognitionDecisionRequest request, CancellationToken cancellationToken = default)
        {
            var o = request.Observation;
            observed.Enqueue(o);
            var selected = o.Candidates.FirstOrDefault(c => c.Id.Contains("|register|", StringComparison.Ordinal)) ??
                o.Candidates.FirstOrDefault(c => c.Id.Contains("|read|", StringComparison.Ordinal)) ??
                o.Candidates.FirstOrDefault(c => c.Id.Contains("|ballot|", StringComparison.Ordinal)) ??
                o.Candidates.Single(c => c.Id == "safe_idle");
            var choices = selected.Id.Contains("|ballot|", StringComparison.Ordinal)
                ? o.Candidates.Where(c => c.Id.Contains("|single|", StringComparison.Ordinal)).Select(c => c.Id.Split('|')[4]).Take(3).ToArray() : null;
            return ValueTask.FromResult(new CognitionDecisionResponse(request.RequestId, o.AgentId, Kind, ProviderEpoch,
                o.RunEpoch, o.DecisionGeneration, o.ObservationDigest, selected.Id, 1,
                o.Candidates.ToDictionary(c => c.Id, c => c.Id == selected.Id ? 1d : 0d, StringComparer.Ordinal), CivicBallot: choices));
        }
    }

    private sealed class CivicBoundaryProvider(string mode) : IDecisionProvider
    {
        public ConcurrentQueue<AgentObservation> Observations { get; } = new();
        public DecisionProviderKind Kind => mode.StartsWith("built_in", StringComparison.Ordinal)
            ? DecisionProviderKind.Deterministic : DecisionProviderKind.LargeLanguageModel;
        public long ProviderEpoch => 1;
        public ValueTask<CognitionDecisionResponse> DecideAsync(CognitionDecisionRequest request, CancellationToken cancellationToken = default)
        {
            var observation = request.Observation;
            Observations.Enqueue(observation);
            if (mode == "failed_personal") throw new HttpRequestException("Recorded provider outage.");
            if (mode == "built_in") return new DeterministicDecisionProvider().DecideAsync(request, cancellationToken);
            var action = mode == "built_in_yes" ? "|yes|" : mode == "built_in_register" ? "|register|" : null;
            var selected = observation.Candidates.FirstOrDefault(candidate => action is not null && candidate.Id.Contains(action, StringComparison.Ordinal)) ??
                observation.Candidates.Single(candidate => candidate.Id == "safe_idle");
            return ValueTask.FromResult(new CognitionDecisionResponse(request.RequestId, observation.AgentId, Kind, ProviderEpoch,
                observation.RunEpoch, observation.DecisionGeneration, observation.ObservationDigest, selected.Id, 1,
                observation.Candidates.ToDictionary(candidate => candidate.Id, candidate => candidate.Id == selected.Id ? 1d : 0d, StringComparer.Ordinal)));
        }
    }

    private sealed class BallotHandler(string ballot) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var answer = "{\"selected_candidate_id\":\"safe_idle\",\"confidence\":1,\"civic_ballot\":" + ballot + "}";
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(new { choices = new[] { new { message = new { content = answer } } } }), Encoding.UTF8, "application/json"),
            });
        }
    }

    private sealed class CivicProvider(string actor, bool holdVote = false, string? proposalOverride = null) : IDecisionProvider
    {
        public ConcurrentQueue<AgentObservation> Observations { get; } = new();
        public TaskCompletionSource<bool> VoteStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<bool> ReleaseVote { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public DecisionProviderKind Kind => DecisionProviderKind.LargeLanguageModel;
        public long ProviderEpoch => 1;
        public async ValueTask<CognitionDecisionResponse> DecideAsync(CognitionDecisionRequest request, CancellationToken cancellationToken = default)
        {
            var observation = request.Observation;
            Observations.Enqueue(observation);
            var ownTown = actor is FirstAuthor or NormalPathWorld.Founder2 ? "town:first" : "town:second";
            var legal = observation.Candidates.Where(c => c.Id.StartsWith("civic|" + ownTown + "|", StringComparison.Ordinal)).ToArray();
            var text = proposalOverride ?? (ownTown == "town:first" ? FirstText : SecondText);
            var author = actor is FirstAuthor or SecondAuthor;
            var selected = legal.FirstOrDefault(c => c.Id.Contains("|yes|", StringComparison.Ordinal)) ??
                legal.FirstOrDefault(c => c.Id.Contains("|read|", StringComparison.Ordinal)) ??
                (author && observation.Self?.CivicNote?.Contains(text, StringComparison.Ordinal) != true
                    ? legal.FirstOrDefault(c => c.Id.Contains("|propose|", StringComparison.Ordinal)) : null) ??
                observation.Candidates.Single(c => c.Id == "safe_idle");
            if (holdVote && selected.Id.Contains("|yes|", StringComparison.Ordinal))
            {
                VoteStarted.TrySetResult(true);
                // Ignore cancellation deliberately: delayed provider replies must still be rejected by authority checks.
                await ReleaseVote.Task;
            }
            return new(request.RequestId, observation.AgentId, Kind, ProviderEpoch,
                observation.RunEpoch, observation.DecisionGeneration, observation.ObservationDigest,
                selected.Id, 1, observation.Candidates.ToDictionary(c => c.Id, c => c.Id == selected.Id ? 1d : 0d, StringComparer.Ordinal),
                CivicProposal: selected.Id.Contains("|propose|", StringComparison.Ordinal) ? text : null);
        }
    }
}
