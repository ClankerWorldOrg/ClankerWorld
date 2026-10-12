using System.Text;
using System.Text.Json.Nodes;
using ClankerWorld.Simulation.World;

namespace ClankerWorld.Tests;

public sealed class TownCouncilSaveValidationTests
{
    [Fact]
    public void ReorderedNoticeIdsAreRefused()
    {
        var state = NewCheckpoint();
        var town = state.Towns![0];
        var council = TownCouncilRules.Register(town.Council!, town.ResidentIds[0], true, null, town.ResidentIds, 0);
        council = TownCouncilRules.Nominate(council, town.ResidentIds[0], town.ResidentIds[1], town.ResidentIds, 0);

        AssertDamagedCheckpointRefused(WithCouncil(state, council), saved =>
        {
            saved["notices"]![0]!["id"] = "notice:2";
            saved["notices"]![1]!["id"] = "notice:1";
        });
    }

    [Theory]
    [InlineData("proposal")]
    [InlineData("election")]
    public void SavedSequenceCannotPrecedeAnExistingCivicRecord(string kind)
    {
        var state = CheckpointWithCivicRecord(kind);
        AssertDamagedCheckpointRefused(state, saved => saved["sequence"] = 0);
    }

    [Theory]
    [InlineData("proposal", "0")]
    [InlineData("proposal", "01")]
    [InlineData("proposal", "not-a-number")]
    public void CivicRecordIdsRequireAPositiveCanonicalNumberWithinTheSequence(string kind, string suffix)
    {
        var state = CheckpointWithCivicRecord(kind);
        var id = state.Towns![0].Id + ":" + kind + ":" + suffix;
        AssertDamagedCheckpointRefused(state, saved =>
        {
            var record = kind == "proposal" ? saved["proposals"]![0]! : saved["election"]!;
            record["id"] = id;
        });
    }

    [Fact]
    public void HistoricalElectionIdsAlsoRequireTheSavedSequence()
    {
        var state = CheckpointWithCivicRecord("election");
        var council = state.Towns![0].Council!;
        council = council with
        {
            Election = null,
            ElectionHistory = [council.Election! with { Stage = "cancelled" }],
        };
        AssertDamagedCheckpointRefused(WithCouncil(state, council), saved => saved["sequence"] = 0);
    }

    [Fact]
    public void PassedFourPersonAdmissionCannotClaimApprovalWithOneYesVote()
    {
        var state = NewCheckpoint();
        var town = state.Towns![0];
        var day = state.WorldSystems!.Config.TicksPerDay;
        var subject = town.ResidentIds[3];
        var council = TownCouncilRules.SubmitProposal(town.Council!, town.Id, town.ResidentIds[0],
            "admission", subject, "Approve this admission request.", "same", town.ResidentIds, 0, day);
        foreach (var voter in town.ResidentIds.Take(3))
            council = TownCouncilRules.VoteProposal(council, council.Proposals[0].Id, voter, true, 0);
        Assert.Equal("passed", council.Proposals[0].Status);
        // The subject already lives there, so the passed admission's one outcome is a lapse.
        var withVote = WithCouncil(state, council);
        var healthy = withVote with
        {
            Towns = [withVote.Towns![0] with
            {
                Admissions = [new(council.Proposals[0].Id, subject, "lapsed", 0, Reason: "already_resident")],
            }],
        };
        AssertRoundtrips(healthy);

        AssertDamagedCheckpointRefused(healthy, saved =>
        {
            var proposal = saved["proposals"]![0]!;
            proposal["requiredYes"] = 1;
            var votes = proposal["votes"]!.AsArray();
            while (votes.Count > 1) votes.RemoveAt(votes.Count - 1);
        });
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(0, 2)]
    [InlineData(1, 1)]
    [InlineData(1, 2)]
    [InlineData(3, 2)]
    public void LegitimateHistoricalThresholdsSurviveRosterAndGoverningFormChanges(int voters, int requiredYes)
    {
        var state = NewCheckpoint();
        var town = state.Towns![0];
        var council = TownCouncilRules.SubmitProposal(town.Council!, town.Id, town.ResidentIds[0],
            "law", null, "Post harvest dates.", "same", town.ResidentIds, 0, state.WorldSystems!.Config.TicksPerDay);
        council = council with
        {
            Revision = 1,
            Proposals = [council.Proposals[0] with
            {
                Voters = town.ResidentIds.Take(voters).ToArray(),
                RequiredYes = requiredYes,
                Status = "cancelled",
                SettledTick = 0,
            }],
        };

        AssertRoundtrips(WithCouncil(state, council));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RelayProvenanceRequiresADirectReadOutsideTheSourceCycle(bool mutual)
    {
        var state = CheckpointWithSameTickRelays();
        var ids = state.Towns![0].ResidentIds;
        AssertDamagedCheckpointRefused(state, saved =>
            saved["knowledge"]![0]!["sourceAgentId"] = mutual ? ids[1] : ids[0]);
    }

    [Fact]
    public async Task SameTickRelayChainsReloadInAnyReceiptOrderAndCanAdvanceNormally()
    {
        var state = CheckpointWithSameTickRelays();
        var council = state.Towns![0].Council!;
        state = WithCouncil(state, council with { Knowledge = council.Knowledge.Reverse().ToArray() });
        var encoded = WorldRuntimeCodec.Encode(state);
        using var restored = WorldRuntime.Restore(WorldRuntimeCodec.Decode(encoded),
            _ => new ActionCoverageRecorder(chooseIdle: true));
        Assert.Equal(encoded, WorldRuntimeCodec.Encode(restored.ExportState()));
        Assert.True((await restored.AdvanceOneTickAsync()).Advanced);
        Assert.Equal(council.Knowledge.Count, restored.Towns[0].Council!.Knowledge.Count);
        restored.Validate();
    }

    [Fact]
    public void CanonicalHistoriesReloadAndAllocateDistinctNextRecordsAndNotices()
    {
        var state = CheckpointWithCivicRecord("election");
        var town = state.Towns![0];
        var council = town.Council!;
        council = council with
        {
            Election = null,
            ElectionHistory = [council.Election! with { Stage = "cancelled" }],
        };
        council = TownCouncilRules.SubmitProposal(council, town.Id, town.ResidentIds[0], "law", null,
            "Post harvest dates.", "same", town.ResidentIds, 0, state.WorldSystems!.Config.TicksPerDay);
        state = WithCouncil(state, council);
        AssertRoundtrips(state);

        var decoded = WorldRuntimeCodec.Decode(WorldRuntimeCodec.Encode(state));
        var priorSequence = decoded.Towns![0].Council!.Sequence;
        council = TownCouncilRules.SubmitProposal(decoded.Towns![0].Council!, town.Id, town.ResidentIds[0],
            "law", null, "Post storm notices.", "same", town.ResidentIds, 0, state.WorldSystems!.Config.TicksPerDay);
        var next = WithCouncil(decoded, council);
        AssertRoundtrips(next);
        Assert.Equal(town.Id + ":proposal:" + (priorSequence + 1), council.Proposals[^1].Id);
        Assert.Equal(Enumerable.Range(1, council.Notices.Count).Select(number => "notice:" + number),
            council.Notices.Select(notice => notice.Id));
        Assert.Equal(council.Notices.Count, council.Notices.Select(notice => notice.Id).Distinct().Count());
    }

    private static WorldRuntimeState NewCheckpoint()
    {
        using var world = NormalPathWorld.CreateGenerated("civic-save-validation", _ => new ActionCoverageRecorder(chooseIdle: true));
        return world.ExportState();
    }

    private static WorldRuntimeState CheckpointWithCivicRecord(string kind)
    {
        var state = NewCheckpoint();
        var town = state.Towns![0];
        var day = state.WorldSystems!.Config.TicksPerDay;
        var council = town.Council!;
        if (kind == "proposal")
            council = TownCouncilRules.SubmitProposal(council, town.Id, town.ResidentIds[0], "law", null,
                "Post harvest dates.", "same", town.ResidentIds, 0, day);
        else
        {
            council = council with
            {
                Form = "representative",
                Fallback = "none",
                Members = town.ResidentIds.Take(2).ToArray(),
                TermEndTick = (long)day * TownCouncilRules.TermDays,
            };
            council = TownCouncilRules.Register(council, town.ResidentIds[2], true, null, town.ResidentIds, 0);
            council = TownCouncilRules.Advance(council, town.Id, state.WorldSeed, town.ResidentIds, 0, day);
            Assert.NotNull(council.Election);
        }
        return WithCouncil(state, council);
    }

    private static WorldRuntimeState CheckpointWithSameTickRelays()
    {
        var state = NewCheckpoint();
        var town = state.Towns![0];
        var ids = town.ResidentIds;
        var council = TownCouncilRules.Register(town.Council!, ids[0], true, null, ids, 0);
        var notice = council.Notices[0].Id;
        for (var index = 0; index < ids.Count; index++)
            council = TownCouncilRules.LearnNotice(council, ids[index], notice, 0,
                index == 0 ? null : ids[index - 1]);
        return WithCouncil(state, council);
    }

    private static WorldRuntimeState WithCouncil(WorldRuntimeState state, TownCouncilState council) =>
        state with { Towns = [state.Towns![0] with { Council = council }] };

    private static void AssertDamagedCheckpointRefused(WorldRuntimeState healthy, Action<JsonNode> damage)
    {
        var document = JsonNode.Parse(WorldRuntimeCodec.Encode(healthy))!;
        damage(document["state"]!["towns"]![0]!["council"]!);
        var bytes = Encoding.UTF8.GetBytes(document.ToJsonString());
        Assert.Throws<InvalidDataException>(() => WorldRuntimeCodec.Decode(bytes));
    }

    private static void AssertRoundtrips(WorldRuntimeState state)
    {
        var encoded = WorldRuntimeCodec.Encode(state);
        using var restored = WorldRuntime.Restore(WorldRuntimeCodec.Decode(encoded));
        Assert.Equal(encoded, WorldRuntimeCodec.Encode(restored.ExportState()));
    }
}
