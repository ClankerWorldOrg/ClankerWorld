using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

/// <summary>Rejects civic authority that is not backed by the saved resident and election decisions.</summary>
internal static partial class TownGovernmentStateValidation
{
    public static void Validate(TownRuntimeState town, TownGovernmentState state, TownCouncilState council,
        SocietyCheckpoint society, int day)
    {
        var tick = society.WorldTick;
        var known = society.Agents.Select(p => p.Id).ToHashSet(StringComparer.Ordinal);
        var adults = town.ResidentIds.Where(id => society.Agents.Any(p => p.Id == id &&
            p.Status == SocietyAgentStatus.Active && LifeStageRules.IsAdult(p.AgeBand))).ToHashSet(StringComparer.Ordinal);
        foreach (var change in state.Changes)
        {
            if (!known.Contains(change.AuthorId) || change.SubmittedTick > tick ||
                change.OpeningVoters.Concat(change.Voters).Any(id => !known.Contains(id)) ||
                change.OpenedTick is not null && (change.OpenedTick > tick || change.DeadlineTick != change.OpenedTick + day) ||
                change.Status == "voting" && (change.DeadlineTick <= tick || change.Voters.Any(id => !adults.Contains(id))) ||
                change.ApprovedTick is not null && (change.ApprovedTick > tick || change.HandoverDeadlineTick != change.ApprovedTick + (long)TownArrangementRules.HandoverDays * day) ||
                change.Status == "handover" && change.HandoverDeadlineTick <= tick || change.SettledTick > tick ||
                change.SuccessorId is { } successor && !known.Contains(successor))
                Fail("A saved protected resident vote or handover is invalid.");
            ValidateForcedCouncilElection(town.Id, council, change);
        }
        if (state.Consents.Any(consent => !adults.Contains(consent.AgentId) || consent.Tick > tick))
            Fail("Saved mayoral willingness must be personal, eligible and specific to its mandates.");
        foreach (var contest in state.ContestHistory.Concat(state.Contest is { } live ? [live] : []))
            ValidateContest(town, state, council, contest, known, adults, tick, day);
        var completed = state.ContestHistory.Where(contest => contest.Stage == "completed").ToDictionary(contest => contest.Id, StringComparer.Ordinal);
        ValidateNonLandGrants(state, council, completed, known, tick, day);
        foreach (var office in state.Offices)
            if (office.HolderId is null ? office.VacantSinceTick > tick :
                !adults.Contains(office.HolderId) || office.TermStartTick > tick || office.TermEndTick <= tick ||
                    office.TermEndTick != office.TermStartTick + (long)TownArrangementRules.MayorTermDays * day)
                Fail("An elected mandate is vacant or must have a valid, unexpired election-backed term.");
        foreach (var term in state.OfficeHistory)
            if (!known.Contains(term.HolderId) || term.EndTick > tick || term.EndTick > term.StartTick + (long)TownArrangementRules.MayorTermDays * day)
                Fail("An archived elected mandate has no valid term or election.");
    }

    private static void ValidateForcedCouncilElection(string townId, TownCouncilState council,
        TownGovernmentChange change)
    {
        if (change.ForcedCouncilElectionId is not { } electionId) return;
        if (!TownGovernmentValidation.ValidId(electionId, townId + ":election:", council.Sequence))
            Fail("A forced Council election needs one approved government change in the same Town.");
        var elections = council.ElectionHistory.Concat(council.Election is { } live ? [live] : [])
            .Where(election => election.Id == electionId).ToArray();
        if (elections.Length != 1)
            Fail("A government change's forced Council election must exist exactly once.");
        var election = elections[0];
        // A runoff updates OpenedTick, so it can be later than approval or the first round.
        if (election.Kind != "initial" || election.OpenedTick < change.ApprovedTick ||
            change.SettledTick is { } settled && election.OpenedTick > settled ||
            council.Election?.Id == electionId && change.Status != "handover" ||
            election.Stage == "completed" && change.Status != "completed")
            Fail("A forced Council election does not match its creating handover's lifecycle.");
    }

    private static long EffectiveStart(TownGovernmentState state, string electionId, long start) =>
        state.NonLandGrants.SingleOrDefault(grant => grant.Id == electionId)?.EffectiveTick ?? start;

    private static bool ValidTerm(TownGovernmentState state, Dictionary<string, TownMayoralContest> completed, string? electionId,
        string holder, string mandate, long start) => electionId is not null &&
        (completed.TryGetValue(electionId, out var election) && election.WinnerId == holder &&
            election.Mandates.Split('+').Contains(mandate) && election.SettledTick == start ||
         mandate == "non_land" && state.NonLandGrants.Any(grant => grant.Id == electionId &&
             grant.HolderId == holder && grant.TermStartTick == start));

    private static void ValidateNonLandGrants(TownGovernmentState state, TownCouncilState council,
        Dictionary<string, TownMayoralContest> completed, HashSet<string> known, long tick, int day)
    {
        foreach (var change in state.Changes.Where(change => change.NonLandExtension is not null))
        {
            var extension = change.NonLandExtension!;
            if (!known.Contains(extension.HolderId) || extension.TermEndTick != extension.TermStartTick + (long)TownArrangementRules.MayorTermDays * day ||
                extension.ConsentTick is { } accepted && (accepted > tick || !council.Knowledge.Any(receipt => receipt.AgentId == extension.HolderId && receipt.LearnedTick <= accepted &&
                    council.Notices.Any(notice => notice.Id == receipt.NoticeId && notice.Kind == "government" &&
                        (notice.SubjectId == change.Id || notice.SubjectId == TownGovernmentRules.VoteNoticeToken(change))))))
                Fail("Added duties need the exact retained elected term and the named holder's informed personal consent.");
        }
        foreach (var grant in state.NonLandGrants)
        {
            var change = state.Changes.Single(item => item.Id == grant.ChangeId);
            var end = BaseEnd(state, change.NonLandExtension!)!.Value;
            if (state.Offices.Any(office => office.ElectionId == grant.Id) && end <= tick)
                Fail("Added duties must end with their base term and retain exactly one current or historical office record.");
        }
    }

    private static long? BaseEnd(TownGovernmentState state, TownNonLandExtension extension) =>
        state.Offices.SingleOrDefault(office => office.Mandates == extension.BaseMandate && office.HolderId == extension.HolderId &&
            office.ElectionId == extension.BaseElectionId && office.TermStartTick == extension.TermStartTick)?.TermEndTick ??
        state.OfficeHistory.SingleOrDefault(term => term.Mandates == extension.BaseMandate && term.HolderId == extension.HolderId &&
            term.ElectionId == extension.BaseElectionId && term.StartTick == extension.TermStartTick)?.EndTick;

    private static void ValidateContest(TownRuntimeState town, TownGovernmentState state, TownCouncilState council,
        TownMayoralContest contest, HashSet<string> known, HashSet<string> adults, long tick, int day)
    {
        var live = ReferenceEquals(state.Contest, contest);
        if (contest.OpenedTick > tick || contest.Voters.Concat(contest.Candidates).Concat(contest.TiedCandidates).Any(id => !known.Contains(id)) ||
            live && contest.Voters.Concat(contest.Candidates).Any(id => !adults.Contains(id)) ||
            contest.Stage == "voting" && (contest.RoundOpenedTick > tick || contest.RoundDeadlineTick != contest.RoundOpenedTick + day ||
                contest.RoundDeadlineTick <= tick || council.Election is { Stage: "main" or "runoff" }) ||
            !live && contest.SettledTick > tick || contest.WinnerId is { } winner && !known.Contains(winner))
            Fail("A saved mayoral contest, mandate, electorate or ballot is invalid.");
        foreach (var round in contest.Rounds)
            if (round.ClosedTick > tick || round.Voters.Concat(round.Candidates).Concat(round.TiedCandidates).Any(id => !known.Contains(id)) ||
                round.Result is "winner" or "tie" && round.ClosedTick < round.OpenedTick + day)
                Fail("Saved mayoral rounds must preserve their electorate, ballots and previous top tie.");
    }

    private static string[] Top(IReadOnlyList<string> candidates, IReadOnlyList<TownMayoralBallot> ballots)
    {
        var totals = candidates.ToDictionary(id => id, id => ballots.Count(b => b.CandidateId == id));
        var most = totals.Count == 0 ? 0 : totals.Values.Max();
        return most == 0 ? [] : candidates.Where(id => totals[id] == most).ToArray();
    }
    private static bool Ballots(IReadOnlyList<TownMayoralBallot> ballots, IReadOnlyList<string> voters, IReadOnlyList<string> candidates) =>
        ballots.All(b => b is not null && voters.Contains(b.AgentId) && candidates.Contains(b.CandidateId)) && Unique(ballots.Select(b => b.AgentId));
    private static bool Ids(IEnumerable<string> ids, HashSet<string> known) => Unique(ids) && ids.All(known.Contains);
    private static bool Unique(IEnumerable<string> ids) => ids.All(id => !string.IsNullOrWhiteSpace(id)) && ids.Distinct(StringComparer.Ordinal).Count() == ids.Count();
    [System.Diagnostics.CodeAnalysis.DoesNotReturn]
    private static void Fail(string message) => throw new InvalidDataException(message);
}
