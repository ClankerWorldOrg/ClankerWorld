using ClankerWorld.Simulation;
using System.Collections.Concurrent;
using System.Globalization;
using System.Text.RegularExpressions;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.World;

namespace ClankerWorld.Tests;

internal static class NonLandRuntimeFixture
{
    internal const string Judge = NormalPathWorld.Founder1;
    internal const string Stranger = NormalPathWorld.Founder2;
    internal const string Subject = NormalPathWorld.Founder3;
    internal const string Witness = NormalPathWorld.Founder4;
    internal const string ReturnLot = "voluntary-personal-wood";
    internal const int Day = 20;
    private static readonly Lazy<byte[]> Baseline = new(CreateBaseline);
    private static readonly Lazy<Task<byte[]>> Hearing = new(async () => await PrepareReadyHearingAsync(await FiledAsync()));
    private static readonly Lazy<Task<byte[]>> Finding = new(CreateFindingAsync);
    private static readonly Lazy<Task<byte[]>> Accepted = new(async () => WorldRuntimeCodec.Encode(
        await AcceptRemedyAsync(await FindingAsync(), [new("return_goods", Subject, Witness, "wood", 1, ReturnLot)])));

    internal static WorldRuntimeState Prepared() => WorldRuntimeCodec.Decode(Baseline.Value);
    internal static WorldRuntime Create(WorldRuntimeState state, IDecisionProvider provider) =>
        WorldRuntime.Restore(state, _ => provider);

    internal static async Task<WorldRuntimeState> ConductAsync(string? subjectId = null)
    {
        subjectId ??= Subject;
        var provider = new NonLandTestProvider
        {
            Choose = observation => observation.AgentId == subjectId
                ? observation.Candidates.FirstOrDefault(candidate => candidate.Id.Contains("|visit|", StringComparison.Ordinal)) : null
        };
        var prepared = Prepared();
        if (subjectId != Subject)
        {
            prepared = WorldRuntimeCodec.Decode(System.Text.Encoding.UTF8.GetBytes(
                System.Text.Encoding.UTF8.GetString(WorldRuntimeCodec.Encode(prepared))
                    .Replace(Subject, subjectId, StringComparison.Ordinal)));
        }
        using var world = Create(prepared, provider);
        await UntilAsync(world, () => world.Towns[0].NonLandHearings.ConductRecords.Count > 0, 6);
        var conduct = Assert.Single(world.Towns[0].NonLandHearings.ConductRecords);
        Assert.Equal(subjectId, conduct.ActorId);
        Assert.Equal("travel", conduct.ConductKind);
        Assert.Contains(world.ExportState().Events, item => item.Kind == "agent_moved" && item.Detail.StartsWith(subjectId + ":", StringComparison.Ordinal));
        return Strict(world.ExportState());
    }

    internal static async Task<WorldRuntimeState> FiledAsync(string? subjectId = null)
    {
        var provider = FilingProvider();
        using var world = Create(await ConductAsync(subjectId), provider);
        await UntilAsync(world, () => world.Towns[0].NonLandHearings.Cases.Count > 0, 8);
        return Strict(world.ExportState());
    }

    internal static async Task<WorldRuntimeState> ReadyToFindAsync() =>
        WorldRuntimeCodec.Decode(await Hearing.Value);

    internal static async Task<WorldRuntimeState> ReadyToFindAsync(string subjectId) =>
        WorldRuntimeCodec.Decode(await PrepareReadyHearingAsync(await FiledAsync(subjectId)));

    internal static async Task<WorldRuntimeState> AcceptedRemedyAsync() =>
        WorldRuntimeCodec.Decode(await Accepted.Value);

    internal static CognitionNonLandChoice FindingPayload(CognitionCandidate candidate) => new(
        Statement: "The actual observation and recorded rule support a warning, without changing anyone's property.",
        Uncertainty: "The evidence establishes passage, but does not establish the person's motive.",
        EvidenceIds: Regex.Matches(candidate.Description, @"([^\s;]+) \((?:observation|record),")
            .Select(match => match.Groups[1].Value).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray());

    internal static async Task<WorldRuntimeState> FindingAsync() =>
        WorldRuntimeCodec.Decode(await Finding.Value);

    private static async Task<byte[]> CreateFindingAsync()
    {
        var provider = new NonLandTestProvider
        {
            Choose = observation => observation.AgentId == Judge
                ? observation.Candidates.FirstOrDefault(candidate => candidate.Id.Contains("|law_case_find|", StringComparison.Ordinal) &&
                    candidate.Id.EndsWith("|warning", StringComparison.Ordinal)) : null,
            Payload = (_, candidate) => candidate.Id.Contains("|law_case_find|", StringComparison.Ordinal) ? FindingPayload(candidate) : null
        };
        WorldRuntimeState state;
        using (var hearing = Create(await ReadyToFindAsync(), provider))
        {
            var plans = new List<NonLandAdjudicationActionPlan>();
            await UntilAsync(hearing, () => hearing.Towns[0].NonLandHearings.Cases[0].Findings.Count > 0, 8, result =>
            {
                plans.AddRange(result.Decisions.Where(decision => decision.ActionPlan is NonLandAdjudicationActionPlan plan && decision.Admission.Accepted && decision.PlanRefusal is null &&
                        result.Events.Any(item => item.Kind == "law_case_finding" && item.Detail == plan.Choice.TownId + "|" + plan.Case.Id + "|" + plan.Actor))
                    .Select(decision => Assert.IsType<NonLandAdjudicationActionPlan>(decision.ActionPlan)));
            });
            var finding = Assert.Single(hearing.Towns[0].NonLandHearings.Cases[0].Findings);
            var plan = Assert.Single(plans);
            Assert.True(plan.Bound);
            Assert.Equal(plan.Choice, NonLandAdjudicationChoice.Decode(plan.CandidateId));
            Assert.Equal((finding.Judge, finding.Reasons, finding.Uncertainty), (plan.Judge, plan.Reasons, plan.Uncertainty));
            Assert.Equal(finding.EvidenceIds, plan.EvidenceIds!.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal));
            Assert.Equal(TownHearingProcedure.Digest(finding.Parties), TownHearingProcedure.Digest(plan.Parties));
            Assert.Equal(ActionRefusalReason.TargetUnavailable, hearing.CheckActionPlan(plan).Refusal!.Reason);
            Assert.Equal("warning", finding.Consequence);
            Assert.Equal(Judge, finding.Judge.AgentId);
            Assert.Equal("non_land_mayor", finding.Judge.Kind);
            state = Strict(hearing.ExportState());
        }
        return WorldRuntimeCodec.Encode(state);
    }

    internal static Task<WorldRuntimeState> AcceptRemedyAsync(WorldRuntimeState state,
        IReadOnlyList<CognitionRemedyTerm> terms) => PrepareRemedyAsync(state, terms, accept: true);

    internal static Task<WorldRuntimeState> OfferRemedyAsync(WorldRuntimeState state,
        IReadOnlyList<CognitionRemedyTerm> terms, long? completionTicks = null) => PrepareRemedyAsync(state, terms, accept: false, completionTicks);

    private static async Task<WorldRuntimeState> PrepareRemedyAsync(WorldRuntimeState state,
        IReadOnlyList<CognitionRemedyTerm> terms, bool accept, long? completionTicks = null)
    {
        var offered = false;
        var consent = new NonLandTestProvider
        {
            Choose = observation =>
            {
                if (observation.AgentId != Subject) return null;
                var choice = observation.Candidates.FirstOrDefault(candidate => candidate.Id.Contains("|read|", StringComparison.Ordinal)) ??
                    observation.Candidates.FirstOrDefault(candidate => candidate.Id.Contains("|law_case_inspect|", StringComparison.Ordinal)) ??
                    observation.Candidates.FirstOrDefault(candidate => candidate.Id.Contains("|remedy_read|", StringComparison.Ordinal)) ??
                    observation.Candidates.FirstOrDefault(candidate => candidate.Id.Contains("|remedy_accept|", StringComparison.Ordinal));
                if (choice is null && !offered)
                {
                    choice = observation.Candidates.FirstOrDefault(candidate => candidate.Id.Contains("|remedy_offer|", StringComparison.Ordinal));
                    if (choice is not null) offered = true;
                }
                return choice;
            },
            Payload = (_, candidate) => candidate.Id.Contains("|remedy_offer|", StringComparison.Ordinal)
                ? new(Statement: "I offer my own named goods or work, voluntarily.", Terms: terms, CompletionTicks: completionTicks) : null
        };
        using var world = Create(state, consent);
        var agreementPlans = new List<NonLandAgreementActionPlan>();
        Wake(world, Subject, "consider-voluntary-return");
        await UntilAsync(world, () => world.Towns[0].NonLandHearings.Offers.Count > 0, 8, Capture);
        var offer = Assert.Single(world.Towns[0].NonLandHearings.Offers);
        var offeredPlan = Assert.Single(agreementPlans, plan => plan.Choice.Verb == "remedy_offer");
        Assert.True(offeredPlan.Bound);
        Assert.Equal((offer.FindingId, offer.CompletionTicks, offer.Reason), (offeredPlan.Finding.Id, offeredPlan.CompletionTicks, offeredPlan.Reason));
        Assert.Equal(MaterialTerms(offer.Terms), MaterialTerms(offeredPlan.Terms!));
        Wake(world, Subject, "read-voluntary-offer");
        await UntilAsync(world, () => world.Towns[0].Council!.Knowledge.Any(receipt =>
            receipt.AgentId == Subject && receipt.NoticeId == offer.NoticeId), 4, Capture);
        if (!accept) return Strict(world.ExportState());
        Wake(world, Subject, "decide-voluntary-consent");
        await UntilAsync(world, () => world.Towns[0].NonLandHearings.Agreements.Count > 0, 4, Capture);
        var acceptedPlan = Assert.Single(agreementPlans, plan => plan.Choice.Verb == "remedy_accept");
        Assert.Equal((offer.Id, offer.TermsHash), (acceptedPlan.Offer!.Id, acceptedPlan.Offer.TermsHash));
        Assert.Equal(acceptedPlan.Choice, NonLandAgreementChoice.Decode(acceptedPlan.CandidateId));
        Assert.Equal(ActionRefusalReason.TargetUnavailable, world.CheckActionPlan(acceptedPlan).Refusal!.Reason);
        Assert.Equal("pending", Assert.Single(world.Towns[0].NonLandHearings.Agreements).Status);
        Assert.Empty(world.Towns[0].NonLandHearings.Effects);
        return Strict(world.ExportState());

        void Capture(WorldStepResult result) => agreementPlans.AddRange(result.Decisions.Where(decision =>
                decision.ActionPlan is NonLandAgreementActionPlan && decision.Admission.Accepted && decision.PlanRefusal is null)
            .Select(decision => Assert.IsType<NonLandAgreementActionPlan>(decision.ActionPlan)));
        static string MaterialTerms(IReadOnlyList<TownRemedyTerm> selected) => TownHearingProcedure.Digest(selected.Select(term =>
            new { term.Kind, term.ContributorId, term.BeneficiaryId, term.ItemKind, term.Quantity, term.TargetId }));
    }

    internal static void Wake(WorldRuntime world, string actor, string key) =>
        world.SubmitInstruction(new(DerivedId.Create(key), "owner:test", actor, OwnerInstructionKind.Suggestion,
            "Consider the current public case and make your own choice."));

    private static async Task<byte[]> PrepareReadyHearingAsync(WorldRuntimeState state)
    {
        var town = state.Towns![0];
        var tick = state.Society.Society.WorldTick;
        var arrangement = new TownArrangement(TownArrangementRules.AllAdultCouncil,
            TownArrangementRules.Mayor, TownArrangementRules.Mayor);
        var (council, government) = TownGovernmentRules.RegisterMayor(town.Council!, town.Government!,
            Judge, "land+non_land", town.ResidentIds, tick);
        (council, government) = TownGovernmentRules.Propose(council, government, town.Id, Judge,
            arrangement, false, town.ResidentIds, tick, Day);
        foreach (var voter in town.ResidentIds.Take(3))
            government = TownGovernmentRules.Vote(government, government.Changes[^1].Id, voter, true, tick);
        (council, government) = TownGovernmentRules.Advance(council, government, town.Id, town.Name,
            state.WorldSeed, town.ResidentIds, tick, Day);
        Assert.Equal(arrangement, government.Arrangement);
        Assert.Equal("completed", Assert.Single(government.Changes).Status);
        // Retained vacant duties need no successor for the ordinary-authority handover.
        // The following advance opens their joint vacancy election after that change completes.
        (council, government) = TownGovernmentRules.Advance(council, government, town.Id, town.Name,
            state.WorldSeed, town.ResidentIds, tick, Day);
        var contest = Assert.IsType<TownMayoralContest>(government.Contest);
        Assert.Equal(("vacancy", "land+non_land", "voting"), (contest.Purpose, contest.Mandates, contest.Stage));
        foreach (var voter in town.ResidentIds)
            government = TownGovernmentRules.VoteMayor(government, TownGovernmentRules.RoundToken(contest), voter, Judge, tick);
        state = state with { Towns = [town with { Council = council, Government = government }] };
        using (var clock = Create(state, new NonLandTestProvider()))
        {
            await UntilAsync(clock, () => clock.Towns[0].Government!.Offices.Any(office => office.HolderId == Judge), Day + 3);
            state = clock.ExportState();
        }
        town = state.Towns![0];
        tick = state.Society.Society.WorldTick;
        government = town.Government!;
        Assert.Equal(arrangement, government.Arrangement);
        var ordinaryChange = Assert.Single(government.Changes);
        Assert.Equal((arrangement, "completed"), (ordinaryChange.Target, ordinaryChange.Status));
        Assert.Equal(2, government.Offices.Count);
        Assert.All(government.Offices, office => Assert.Equal(Judge, office.HolderId));
        Assert.Equal(Judge, TownGovernmentRules.CurrentNonLandAuthority(government, tick)!.HolderId);
        state = Strict(state);
        var provider = new NonLandTestProvider
        {
            Choose = observation => observation.Candidates.FirstOrDefault(candidate => candidate.Id.Contains("|read|", StringComparison.Ordinal)) ??
                observation.Candidates.FirstOrDefault(candidate => candidate.Id.Contains("|law_case_inspect|", StringComparison.Ordinal)) ??
                observation.Candidates.FirstOrDefault(candidate => candidate.Id.Contains("|law_case_answer|", StringComparison.Ordinal)),
            Payload = (_, candidate) => candidate.Id.Contains("|law_case_answer|", StringComparison.Ordinal)
                ? new(Statement: "I answer for myself and acknowledge the reported passage, without promising goods.") : null
        };
        using var prepared = Create(state, provider);
        foreach (var (actor, index) in town.ResidentIds.Select((actor, index) => (actor, index)))
            Wake(prepared, actor, "inspect-elected-mayor-case:" + index.ToString(CultureInfo.InvariantCulture));
        var materialPlans = new List<NonLandEvidenceActionPlan>();
        await UntilAsync(prepared, () => TownNonLandRules.ReadCurrent(prepared.Towns[0].NonLandHearings.Cases[0], Judge, prepared.WorldTick) &&
            prepared.Towns[0].NonLandHearings.Cases[0].Responses.Any(response => response.AgentId == prepared.Towns[0].NonLandHearings.Cases[0].Allegation.SubjectId), 16, result =>
        {
            materialPlans.AddRange(result.Decisions.Where(decision => decision.ActionPlan is NonLandEvidenceActionPlan plan && decision.Admission.Accepted && decision.PlanRefusal is null &&
                    result.Events.Any(item => item.Kind == (plan.Choice.Verb == "law_case_answer" ? "law_case_response" : "law_case_inspected") &&
                        item.Detail == town.Id + "|" + plan.CaseId + "|" + plan.Actor))
                .Select(decision => Assert.IsType<NonLandEvidenceActionPlan>(decision.ActionPlan)));
        });
        var actual = prepared.Towns[0].NonLandHearings.Cases[0];
        Assert.Contains(materialPlans, plan => plan.Choice.Verb == "law_case_inspect" && plan.LawEvidence is not null &&
            actual.Reads.Any(read => read.AgentId == plan.Actor && read.Revision == plan.Revision.Number));
        var response = Assert.Single(actual.Responses, response => response.AgentId == actual.Allegation.SubjectId);
        var answered = Assert.Single(materialPlans, plan => plan.Choice.Verb == "law_case_answer" && plan.Actor == response.AgentId);
        Assert.True(answered.Bound);
        Assert.Equal((response.PartyId, response.RepresentedAgentId, response.Text), (answered.Party!.Id, answered.Party.SubjectId, answered.Statement));
        Assert.Equal(answered.Choice, NonLandEvidenceChoice.Decode(answered.CandidateId));
        Assert.Equal(ActionRefusalReason.TargetUnavailable, prepared.CheckActionPlan(answered).Refusal!.Reason);
        Wake(prepared, Judge, "fresh-reasoned-choice");
        return WorldRuntimeCodec.Encode(Strict(prepared.ExportState()));
    }

    internal static NonLandTestProvider FilingProvider(DecisionProviderKind kind = DecisionProviderKind.LargeLanguageModel,
        bool includePayload = true) => new(kind)
        {
            Choose = observation => observation.AgentId == Witness
                ? observation.Candidates.FirstOrDefault(candidate => candidate.Id.Contains("|law_case_file|", StringComparison.Ordinal)) : null,
            Payload = (_, choice) => includePayload && choice.Id.Contains("|law_case_file|", StringComparison.Ordinal)
                ? new(Statement: "I saw this person travel through the public notice place under the recorded rule.") : null
        };

    internal static async Task UntilAsync(WorldRuntime world, Func<bool> complete, int limit = 80, Action<WorldStepResult>? observe = null)
    {
        for (var tick = 0; tick < limit && !complete(); tick++)
        {
            var result = await world.AdvanceOneTickAsync();
            Assert.True(result.Advanced);
            observe?.Invoke(result);
        }
        Assert.True(complete(), "The expected native legal boundary was not reached within " + limit + " ticks.");
    }

    internal static WorldRuntimeState Strict(WorldRuntimeState state)
    {
        var bytes = WorldRuntimeCodec.Encode(state);
        var decoded = WorldRuntimeCodec.Decode(bytes);
        using var restored = Create(decoded, new NonLandTestProvider());
        Assert.Equal(bytes, WorldRuntimeCodec.Encode(restored.ExportState()));
        return decoded;
    }

    private static byte[] CreateBaseline()
    {
        using var generated = NormalPathWorld.CreateGenerated("government-personal-path", _ => new NonLandTestProvider(), ticksPerDay: Day);
        var state = generated.ExportState();
        var town = state.Towns![0];
        var board = town.OriginSite!.Value;
        var map = state.Map;
        var start = map.FootNeighbors(board).SelectMany(map.FootNeighbors).Distinct()
            .Where(point => map.FootDistance(point, board) == 2)
            .OrderBy(point => point.Y).ThenBy(point => point.X).First();
        var far = Enumerable.Range(0, map.Height).SelectMany(y => Enumerable.Range(0, map.Width).Select(x => new GridPoint(x, y)))
            .First(point => map.IsBuildable(point) && map.FootDistance(point, board) >= 8);
        var council = town.Council!;
        var government = town.Government!;
        (council, government) = TownLawRules.ProposeAdoption(council, government, town.Id, Judge,
            "Paths: Take care when passing the public notice place.", TownLawRules.ResidentDuty, [], town.ResidentIds, 0, Day);
        foreach (var voter in town.ResidentIds.Take(3))
            council = TownCouncilRules.VoteProposal(council, council.Proposals[^1].Id, voter, true, 0);
        (council, government) = TownLawRules.Enact(council, government, town.Id, town.Name, 0);
        foreach (var actor in new[] { Subject, Witness, Judge })
            council = TownCouncilRules.LearnNotices(council, actor, council.Notices.Select(notice => notice.Id), 0);
        var society = state.Society.Society;
        var inventory = InventoryAuthority.AddLot(society.Inventory, ReturnLot, "wood", Subject, 2);
        state = state with
        {
            Towns = [town with { Council = council, Government = government }],
            Agents = state.Agents.Select(person => person with
            {
                Position = person.AgentId == Subject ? start : person.AgentId == Stranger ? far : board,
                FullnessBasisPoints = 9_000,
                LastDecisionContext = null
            }).ToArray(),
            Society = state.Society with
            {
                Society = society with
                {
                    Inventory = inventory,
                }
            }
        };
        using var validated = Create(state, new NonLandTestProvider());
        return WorldRuntimeCodec.Encode(validated.ExportState());
    }
}

internal sealed class NonLandTestProvider(DecisionProviderKind kind = DecisionProviderKind.LargeLanguageModel) : IDecisionProvider
{
    public DecisionProviderKind Kind => kind;
    public long ProviderEpoch => 1;
    internal Func<AgentObservation, CognitionCandidate?>? Choose { get; init; }
    internal Func<AgentObservation, CognitionCandidate, CognitionNonLandChoice?>? Payload { get; init; }
    internal Func<AgentObservation, CognitionCandidate, CognitionLandHearingChoice?>? LandPayload { get; init; }
    internal Func<AgentObservation, CognitionCandidate, IReadOnlyList<CognitionLandTile>?>? LandTiles { get; init; }
    internal ConcurrentQueue<AgentObservation> Observations { get; } = new();
    internal ConcurrentQueue<string> Selected { get; } = new();

    public ValueTask<CognitionDecisionResponse> DecideAsync(CognitionDecisionRequest request, CancellationToken cancellationToken = default)
    {
        var observation = request.Observation;
        Observations.Enqueue(observation);
        var choice = Choose?.Invoke(observation) ?? observation.Candidates.Single(candidate => candidate.Id == "safe_idle");
        Selected.Enqueue(observation.AgentId + "|" + choice.Id);
        return ValueTask.FromResult(new CognitionDecisionResponse(request.RequestId, observation.AgentId, Kind, ProviderEpoch,
            observation.RunEpoch, observation.DecisionGeneration, observation.ObservationDigest, choice.Id, 1,
            observation.Candidates.ToDictionary(candidate => candidate.Id, candidate => candidate.Id == choice.Id ? 1d : 0d, StringComparer.Ordinal),
            CivicNonLandHearing: Payload?.Invoke(observation, choice),
            CivicLandHearing: LandPayload?.Invoke(observation, choice), CivicLandTiles: LandTiles?.Invoke(observation, choice)));
    }
}
