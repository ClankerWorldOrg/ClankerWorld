using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.World;

namespace ClankerWorld.Tests;

public sealed class TownNonLandPublicRecordTests
{
    [Fact]
    public async Task AnActuallyReadPublicPermissionCanGroundARehearingButAnUnreadRecordCannot()
    {
        var state = await NonLandRuntimeFixture.FindingAsync();
        var town = state.Towns![0];
        var original = Assert.Single(town.NonLandHearings.Cases);
        var position = original.Allegation.Position;
        var actor = NonLandRuntimeFixture.Subject;
        var household = state.Society.Society.GetAgent(actor).HouseholdId!;
        Assert.Contains(state.TownLandTitles!, title => title.TownId == town.Id && title.Tiles.Contains(position));
        var groups = state.HouseholdLandUseRights!.Where(right => right.TownId == town.Id)
            .GroupBy(right => right.HouseholdId).Select(group => (HouseholdId: group.Key,
                Tiles: (IEnumerable<GridPoint>)group.SelectMany(right => right.Tiles).Where(tile => tile != position)
                    .Concat(group.Key == household ? [position] : []).Distinct().ToArray())).ToArray();
        var rights = TownLandRightsRules.InitialUseRights(state.Map, town.Id, groups, 0);
        var right = Assert.Single(rights, right => right.HouseholdId == household && right.Tiles.Contains(position));
        state = state with { HouseholdLandUseRights = state.HouseholdLandUseRights!.Where(right => right.TownId != town.Id).Concat(rights).OrderBy(right => right.Id, StringComparer.Ordinal).ToArray() };
        NonLandRuntimeFixture.Strict(state);

        var stage = 0;
        string? importedId = null;
        var provider = new NonLandTestProvider
        {
            Choose = observation =>
            {
                var action = observation.AgentId == actor ? stage switch
                {
                    0 => "hearing_file",
                    1 => "hearing_inspect",
                    2 => "law_case_evidence",
                    3 => "law_case_inspect",
                    4 => "law_case_reopen",
                    _ => null,
                } : observation.AgentId == NonLandRuntimeFixture.Judge ? stage switch
                {
                    5 => "law_case_inspect",
                    6 => "law_case_assess_reopen",
                    _ => null,
                } : null;
                return action is null ? null : observation.Candidates.FirstOrDefault(candidate =>
                    candidate.Id.Contains("|" + action + "|", StringComparison.Ordinal) &&
                    (stage != 4 || candidate.Id.EndsWith("|material_evidence", StringComparison.Ordinal)) &&
                    (stage != 6 || candidate.Id.EndsWith(":accept", StringComparison.Ordinal)));
            },
            LandTiles = (_, candidate) => candidate.Id.Contains("|hearing_file|", StringComparison.Ordinal) ? [new(position.X, position.Y)] : null,
            LandPayload = (_, candidate) => candidate.Id.Contains("|hearing_file|", StringComparison.Ordinal)
                ? new(Statement: "Please inspect my recorded permission on this exact plot.", RequestedOutcome: "confirm") : null,
            Payload = (_, candidate) => candidate.Id.Contains("|law_case_reopen|", StringComparison.Ordinal)
                ? new(Grounds: "This newly inspected public permission was not in the earlier hearing.", EvidenceIds: [importedId!])
                : candidate.Id.Contains("|law_case_assess_reopen|", StringComparison.Ordinal)
                    ? new(Statement: "The actual public permission is new material evidence requiring a fresh hearing.") : null,
        };
        using var world = NonLandRuntimeFixture.Create(state, provider);
        NonLandRuntimeFixture.Wake(world, actor, "file-for-real-public-record");
        await NonLandRuntimeFixture.UntilAsync(world, () => world.Towns[0].LandRecords.Cases.Count > 0, 8);
        Assert.DoesNotContain(world.Towns[0].NonLandHearings.Cases[0].Evidence, evidence => evidence.SourceRecordId == right.Id);
        stage = 1;
        NonLandRuntimeFixture.Wake(world, actor, "inspect-real-public-record");
        await NonLandRuntimeFixture.UntilAsync(world, () => world.Towns[0].LandRecords.Cases.Any(item =>
            item.Reads.Any(read => read.AgentId == actor && read.EvidenceIds.Any(id => item.Evidence.Any(evidence => evidence.Id == id && evidence.SourceRecordId == right.Id)))), 8);
        stage = 2;
        NonLandRuntimeFixture.Wake(world, actor, "submit-only-known-public-record");
        await NonLandRuntimeFixture.UntilAsync(world, () => world.Towns[0].NonLandHearings.Cases[0].Evidence.Any(evidence => evidence.SourceRecordId == right.Id), 8);
        var imported = Assert.Single(world.Towns[0].NonLandHearings.Cases[0].Evidence, evidence => evidence.SourceRecordId == right.Id);
        importedId = imported.Id;
        Assert.Equal("record", imported.Kind);
        Assert.Equal("record_inspection", imported.Acquisition);
        Assert.Equal(TownLandHearingRules.Version(right), imported.SourceVersion);
        Assert.Equal(actor, imported.SourceAgentId);
        var submitted = NonLandRuntimeFixture.Strict(world.ExportState());
        var withoutRead = submitted with
        {
            Towns = submitted.Towns!.Select(item => item with
            { LandRecords = item.LandRecords with { Cases = item.LandRecords.Cases.Select(file => file with { Reads = [] }).ToArray() } }).ToArray()
        };
        Assert.Throws<InvalidDataException>(() => WorldRuntimeCodec.Encode(withoutRead));
        var forged = submitted with
        {
            Towns = submitted.Towns!.Select(item => item with
            {
                NonLandHearings = item.NonLandHearings with
                {
                    Cases = item.NonLandHearings.Cases.Select(file => file with
                    { Evidence = file.Evidence.Select(evidence => evidence.Id == imported.Id ? evidence with { SourceVersion = "invented" } : evidence).ToArray() }).ToArray()
                }
            }).ToArray()
        };
        Assert.Throws<InvalidDataException>(() => WorldRuntimeCodec.Encode(forged));
        stage = 3;
        NonLandRuntimeFixture.Wake(world, actor, "read-submitted-public-record");
        await NonLandRuntimeFixture.UntilAsync(world, () => world.Towns[0].NonLandHearings.Cases[0].Reads.Any(read => read.AgentId == actor && read.EvidenceIds.Contains(imported.Id)), 8);
        stage = 4;
        NonLandRuntimeFixture.Wake(world, actor, "request-public-record-rehearing");
        var requestPlans = new List<NonLandAdjudicationActionPlan>();
        await NonLandRuntimeFixture.UntilAsync(world, () => world.Towns[0].NonLandHearings.Cases[0].ReopenRequests.Count > 0, 8, Capture);
        stage = 5;
        NonLandRuntimeFixture.Wake(world, NonLandRuntimeFixture.Judge, "inspect-actual-reopening-request");
        await NonLandRuntimeFixture.UntilAsync(world, () => world.Towns[0].NonLandHearings.Cases[0].Reads.Any(read =>
            read.AgentId == NonLandRuntimeFixture.Judge && read.ReopenRequestIds.Count > 0), 8);
        stage = 6;
        NonLandRuntimeFixture.Wake(world, NonLandRuntimeFixture.Judge, "assess-actual-public-record");
        await NonLandRuntimeFixture.UntilAsync(world, () => world.Towns[0].NonLandHearings.Cases[0].Revisions.Count == 2, 8, Capture);
        var reopened = world.Towns[0].NonLandHearings.Cases[0];
        var request = Assert.Single(reopened.ReopenRequests);
        Assert.Equal("accepted", request.Status);
        var requested = Assert.Single(requestPlans, plan => plan.Choice.Verb == "law_case_reopen");
        var assessed = Assert.Single(requestPlans, plan => plan.Choice.Verb == "law_case_assess_reopen");
        Assert.Equal(request.Reasons, requested.Reasons);
        Assert.Equal(request.EvidenceIds, requested.EvidenceIds!.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal));
        Assert.Equal(request.Id, assessed.Request!.Id);
        Assert.True(assessed.Accept);
        Assert.Equal((request.AssessedBy, request.Assessment), (assessed.Judge, assessed.Reasons));
        Assert.All(requestPlans, plan =>
        {
            Assert.True(plan.Bound);
            Assert.Equal(plan.Choice, NonLandAdjudicationChoice.Decode(plan.CandidateId));
            Assert.Equal(ActionRefusalReason.TargetUnavailable, world.CheckActionPlan(plan).Refusal!.Reason);
        });
        Assert.Equal(original.Findings[0], Assert.Single(reopened.Findings));
        Assert.Equal(NonLandRuntimeFixture.Day, reopened.Revisions[^1].DeadlineTick - reopened.Revisions[^1].PublishedTick);
        NonLandRuntimeFixture.Strict(world.ExportState());

        void Capture(WorldStepResult result) => requestPlans.AddRange(result.Decisions.Where(decision => decision.ActionPlan is NonLandAdjudicationActionPlan plan &&
                decision.Admission.Accepted && decision.PlanRefusal is null && result.Events.Any(item => item.Detail == plan.Choice.TownId + "|" + plan.Case.Id + "|" + plan.Actor &&
                    item.Kind is "law_case_reopen_requested" or "law_case_reopened"))
            .Select(decision => Assert.IsType<NonLandAdjudicationActionPlan>(decision.ActionPlan)));
    }
}
