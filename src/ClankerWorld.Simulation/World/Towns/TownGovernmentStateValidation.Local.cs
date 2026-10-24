namespace ClankerWorld.Simulation.World;

internal static partial class TownGovernmentStateValidation
{
    internal static void ValidateLocal(string townId, TownGovernmentState state, bool allowIntermediateVotes)
    {
        if (state.Laws is null || state.LawDrafts is null || state.Changes is null || state.OfficeHistory is null ||
            state.Consents is null || state.ContestHistory is null || state.Offices is null || state.NonLandGrants is null || state.MayoralRetryCircumstances is null ||
            state.Laws.Any(law => law is null) || state.Changes.Any(change => change is null) || state.ContestHistory.Any(contest => contest is null) || state.NonLandGrants.Any(grant => grant is null) ||
            !TownArrangementRules.IsSupported(state.Arrangement) || state.Sequence < 0 || state.MayoralRetryTick < 0 || !BoundedText.IsWithinLimit(state.MayoralRetryCircumstances, 64))
            Fail("A Town's saved government record is incomplete or declares an unsupported arrangement.");
        var ids = state.Laws.Select(law => law.Id).Concat(state.Changes.Select(change => change.Id))
            .Concat(state.ContestHistory.Select(contest => contest.Id)).Concat(state.Contest is { } currentContest ? [currentContest.Id] : [])
            .Concat(state.NonLandGrants.Select(grant => grant.Id)).ToArray();
        if (ids.Distinct(StringComparer.Ordinal).Count() != ids.Length)
            Fail("A Town's saved law, government-change and election identities must be unique.");
        TownGovernmentValidation.ValidateLocalLaws(townId, state);
        if (state.Offices is null || state.Changes.Any(c => c is null) || state.Consents.Any(c => c is null) ||
            state.OfficeHistory.Any(o => o is null) || state.Offices.Any(o => o is null) || state.ContestHistory.Any(c => c is null) ||
            state.Changes.Count(c => c.Status is "voting" or "handover") > 1 ||
            state.Changes.Where(c => c.Status is "queued" or "voting" or "handover").GroupBy(c => c.RequestKey).Any(g => g.Count() > 1))
            Fail("The Town's government processes or office records are invalid.");
        var forcedElections = new HashSet<string>(StringComparer.Ordinal);
        foreach (var change in state.Changes)
        {
            var opened = change.OpenedTick is not null;
            var approved = change.ApprovedTick is not null;
            if (!TownGovernmentValidation.ValidId(change.Id, townId + ":government:", state.Sequence) ||
                !TownArrangementRules.IsSupported(change.Target) ||
                change.Kind is not ("arrangement" or "replace_mayor") ||
                change.RequestKey != TownGovernmentRules.ChangeRequestKey(change.Kind, change.Target, change.NonLandExtension) ||
                change.Circumstances is not { Length: 64 } || change.SubmittedTick < 0 ||
                change.Status is not ("queued" or "voting" or "handover" or "completed" or "rejected" or "withdrawn" or "cancelled") ||
                change.Voters is null || change.OpeningVoters is null || change.Votes is null ||
                !TownSystem.UniqueCouncilIds(change.OpeningVoters) || !TownSystem.UniqueCouncilIds(change.Voters) || change.Voters.Any(id => !change.OpeningVoters.Contains(id)) ||
                change.Votes.Any(v => v is null || !change.Voters.Contains(v.AgentId)) || !TownSystem.UniqueCouncilIds(change.Votes.Select(v => v.AgentId)) ||
                opened != (change.DeadlineTick is not null) ||
                opened && (change.OpenedTick < change.SubmittedTick || change.DeadlineTick <= change.OpenedTick) ||
                !opened && (change.OpeningVoters.Count != 0 || change.Voters.Count != 0 || change.Votes.Count != 0) ||
                change.Status == "queued" && (opened || change.SettledTick is not null) ||
                change.Status is "voting" or "handover" or "completed" or "rejected" && !opened ||
                change.Status == "voting" && (approved ||
                    !allowIntermediateVotes && change.Votes.Count(v => v.Yes) > change.Voters.Count / 2 || change.SettledTick is not null) ||
                approved != (change.HandoverDeadlineTick is not null) ||
                approved && (change.ApprovedTick < change.OpenedTick || change.ApprovedTick > change.DeadlineTick ||
                    change.Votes.Count(v => v.Yes) <= change.Voters.Count / 2) ||
                change.Status is "handover" or "completed" && !approved ||
                change.Status == "handover" && (change.SettledTick is not null) ||
                change.Status == "completed" && (change.SettledTick < change.ApprovedTick || change.SettledTick > change.HandoverDeadlineTick) ||
                change.Status is "completed" or "rejected" or "withdrawn" or "cancelled" &&
                    (change.SettledTick is null || change.SettledTick < change.SubmittedTick) ||
                change.Reason is { } changeReason && !BoundedText.IsWithinLimit(changeReason, 512))
                Fail("A saved protected resident vote or handover is invalid.");
            if (change.ForcedCouncilElectionId is { } forced && (!TownGovernmentValidation.ValidId(forced, townId + ":election:", long.MaxValue) ||
                !forcedElections.Add(forced) || change.Kind != "arrangement" || change.Status is not ("handover" or "completed" or "cancelled") ||
                change.ApprovedTick is null || change.Target.Ordinary is not (TownArrangementRules.Council or TownArrangementRules.ElectedCouncil)))
                Fail("A forced Council election needs one approved government change in the same Town.");
        }
        var lastArrangement = state.Changes.LastOrDefault(c => c.Status == "completed")?.Target ?? TownArrangementRules.Initial;
        if (state.Arrangement != lastArrangement || !TownSystem.UniqueCouncilIds(state.Offices.Select(o => o.Mandates)) ||
            !state.Offices.Select(o => o.Mandates).ToHashSet(StringComparer.Ordinal).SetEquals(TownGovernmentRules.Mandates(state.Arrangement)))
            Fail("The current government and its mandates need a completed resident handover.");
        if (state.Consents.Any(c => !TownArrangementRules.IsMandates(c.Mandates) || c.Tick < 0) ||
            state.Consents.Select(c => (c.AgentId, c.Mandates)).Distinct().Count() != state.Consents.Count)
            Fail("Saved mayoral willingness must be personal, eligible and specific to its mandates.");
        foreach (var contest in state.ContestHistory.Concat(state.Contest is { } live ? [live] : []))
            ValidateLocalContest(townId, state, contest);
        var completed = state.ContestHistory.Where(c => c.Stage == "completed").ToDictionary(c => c.Id, StringComparer.Ordinal);
        ValidateLocalNonLandGrants(state, completed);
        foreach (var office in state.Offices)
        {
            if (office.Mandates is not ("land" or "ordinary" or "non_land") ||
                office.HolderId is null && (office.TermStartTick is not null || office.TermEndTick is not null || office.ElectionId is not null ||
                    office.VacantSinceTick is null || office.VacantSinceTick < 0 || string.IsNullOrWhiteSpace(office.VacancyReason)) ||
                office.HolderId is not null && (office.TermStartTick is null || office.TermEndTick is null || office.TermEndTick <= office.TermStartTick ||
                    office.VacantSinceTick is not null || office.VacancyReason is not null ||
                    !ValidTerm(state, completed, office.ElectionId, office.HolderId, office.Mandates, office.TermStartTick.Value)))
                Fail("An elected mandate is vacant or must have a valid, unexpired election-backed term.");
        }
        foreach (var term in state.OfficeHistory)
            if (term.Mandates is not ("land" or "ordinary" or "non_land") || term.StartTick < 0 ||
                term.EndTick < term.StartTick ||
                string.IsNullOrWhiteSpace(term.EndReason) || !BoundedText.IsWithinLimit(term.EndReason, 512) ||
                !ValidTerm(state, completed, term.ElectionId, term.HolderId, term.Mandates, term.StartTick))
                Fail("An archived elected mandate has no valid term or election.");
        var terms = state.OfficeHistory.Select(o => (o.Mandates, Start: EffectiveStart(state, o.ElectionId, o.StartTick), End: o.EndTick, o.ElectionId))
            .Concat(state.Offices.Where(o => o.HolderId is not null).Select(o => (o.Mandates,
                Start: EffectiveStart(state, o.ElectionId!, o.TermStartTick!.Value), End: o.TermEndTick!.Value, o.ElectionId!))).ToArray();
        foreach (var group in terms.GroupBy(t => t.Mandates))
        {
            var ordered = group.OrderBy(t => t.Item2).ThenBy(t => t.End).ToArray();
            if (!TownSystem.UniqueCouncilIds(ordered.Select(t => t.Item4)) || ordered.Skip(1).Where((t, i) => t.Item2 < ordered[i].End).Any())
                Fail("Two terms cannot hold the same mandate at once.");
        }
    }

    private static void ValidateLocalContest(string townId, TownGovernmentState state, TownMayoralContest contest)
    {
        var live = ReferenceEquals(state.Contest, contest);
        if (!TownGovernmentValidation.ValidId(contest.Id, townId + ":mayor:", state.Sequence) ||
            !TownArrangementRules.IsMandates(contest.Mandates) || contest.Purpose is not ("handover" or "vacancy" or "renewal") ||
            contest.Stage is not ("waiting" or "voting" or "ready" or "completed" or "failed" or "cancelled") ||
            live != (contest.Stage is "waiting" or "voting" or "ready") || contest.Round < 0 || contest.Interruptions < 0 ||
            contest.OpenedTick < 0 || contest.Rounds is null || contest.Rounds.Any(r => r is null) ||
            contest.Voters is null || contest.Candidates is null || contest.Ballots is null || contest.TiedCandidates is null ||
            !TownSystem.UniqueCouncilIds(contest.Voters) || !TownSystem.UniqueCouncilIds(contest.Candidates) || !TownSystem.UniqueCouncilIds(contest.TiedCandidates) ||
            !Ballots(contest.Ballots, contest.Voters, contest.Candidates) ||
            contest.Purpose == "handover" != (contest.ChangeId is not null) ||
            contest.ChangeId is { } changeId && !state.Changes.Any(c => c.Id == changeId && c.ApprovedTick is not null &&
                contest.OpenedTick >= c.ApprovedTick && (!live || c.Status == "handover") &&
                (contest.Stage != "completed" || c.Status == "completed") && contest.Mandates.Split('+').All(m => TownGovernmentRules.Mandates(c.Target).Contains(m))) ||
            live && contest.Candidates.Any(id => !state.Consents.Any(c => c.AgentId == id && c.Mandates == contest.Mandates)) ||
            live && contest.Candidates.Any(id => !TownGovernmentRules.CanStandForMayoralContest(state, id, contest.Mandates, contest.ChangeId)) ||
            contest.Stage == "voting" && (contest.Round < 1 || contest.RoundOpenedTick is null ||
                contest.RoundDeadlineTick is null || contest.RoundDeadlineTick <= contest.RoundOpenedTick || contest.Candidates.Count == 0) ||
            contest.Stage == "waiting" && (contest.RoundOpenedTick is not null || contest.RoundDeadlineTick is not null ||
                contest.Voters.Count > 0 || contest.Candidates.Count > 0 || contest.Ballots.Count > 0) ||
            !live && (contest.SettledTick is null || contest.SettledTick < contest.OpenedTick) ||
            live && contest.SettledTick is not null || contest.Reason is { } contestReason && !BoundedText.IsWithinLimit(contestReason, 512))
            Fail("A saved mayoral contest, mandate, electorate or ballot is invalid.");
        string[]? priorTie = null;
        foreach (var (round, index) in contest.Rounds.Select((r, i) => (r, i)))
        {
            if (round.Number != index + 1 || round.OpenedTick < contest.OpenedTick || round.ClosedTick < round.OpenedTick ||
                index > 0 && round.OpenedTick < contest.Rounds[index - 1].ClosedTick ||
                round.Result is not ("tie" or "winner" or "interrupted" or "failed" or "cancelled") ||
                round.Voters is null || round.Candidates is null || round.Ballots is null || round.TiedCandidates is null ||
                !TownSystem.UniqueCouncilIds(round.Voters) || !TownSystem.UniqueCouncilIds(round.Candidates) || !TownSystem.UniqueCouncilIds(round.TiedCandidates) ||
                !Ballots(round.Ballots, round.Voters, round.Candidates) ||
                priorTie is not null && round.Candidates.Any(id => !priorTie.Contains(id)))
                Fail("Saved mayoral rounds must preserve their electorate, ballots and previous top tie.");
            var top = Top(round.Candidates, round.Ballots);
            if (round.Result == "tie")
            {
                if (top is not [_, _, ..]) Fail("A recorded mayoral tie needs tied candidates with actual votes.");
                priorTie = top;
            }
            if (round.Result == "winner" && (top.Length != 1 || top[0] != contest.WinnerId))
                Fail("A mayoral winner needs the unique highest positive vote count in the deciding round.");
        }
        if (contest.Interruptions != contest.Rounds.Count(r => r.Result == "interrupted") ||
            contest.Round != contest.Rounds.Count + (contest.Stage == "voting" ? 1 : 0) ||
            contest.Stage is "ready" or "completed" && (contest.WinnerId is null || (contest.Rounds.Count == 0 || contest.Rounds[^1].Result != "winner")) ||
            priorTie is not null && contest.Candidates.Any(id => !priorTie.Contains(id)))
            Fail("A saved mayoral result or interrupted round is inconsistent.");
    }

    private static void ValidateLocalNonLandGrants(TownGovernmentState state, Dictionary<string, TownMayoralContest> completed)
    {
        foreach (var change in state.Changes.Where(change => change.NonLandExtension is not null))
        {
            var extension = change.NonLandExtension!;
            if (change.Kind != "arrangement" || change.Target.NonLand != TownArrangementRules.Mayor ||
                extension.BaseMandate is not ("land" or "ordinary") ||
                !TownGovernmentRules.Mandates(change.Target).Contains(extension.BaseMandate) ||
                extension.TermStartTick < 0 || extension.TermStartTick > change.SubmittedTick ||
                extension.TermEndTick <= change.SubmittedTick || string.IsNullOrWhiteSpace(extension.BaseElectionId) ||
                !completed.TryGetValue(extension.BaseElectionId, out var election) || election.WinnerId != extension.HolderId ||
                election.SettledTick != extension.TermStartTick || !election.Mandates.Split('+').Contains(extension.BaseMandate) ||
                BaseEnd(state, extension) is not { } ended || ended < change.SubmittedTick ||
                extension.ConsentTick is { } accepted && (accepted < change.SubmittedTick || accepted >= extension.TermEndTick ||
                    accepted > ended) ||
                change.Status == "completed" && (extension.ConsentTick is null ||
                    !state.NonLandGrants.Any(grant => grant.ChangeId == change.Id)))
                Fail("Added duties need the exact retained elected term and the named holder's informed personal consent.");
        }
        foreach (var grant in state.NonLandGrants)
        {
            var change = state.Changes.SingleOrDefault(change => change.Id == grant.ChangeId);
            var baseEnd = change?.NonLandExtension is { } target ? BaseEnd(state, target) : null;
            if (change is not { Status: "completed", NonLandExtension: { } extension } ||
                grant.Id != TownGovernmentRules.NonLandGrantId(change.Id) || grant.HolderId != extension.HolderId ||
                grant.BaseMandate != extension.BaseMandate || grant.BaseElectionId != extension.BaseElectionId ||
                grant.ConsentTick != extension.ConsentTick || grant.EffectiveTick != change.SettledTick ||
                grant.EffectiveTick < grant.ConsentTick || grant.EffectiveTick >= grant.TermEndTick ||
                grant.TermStartTick != extension.TermStartTick || grant.TermEndTick != extension.TermEndTick ||
                change.SuccessorId != grant.HolderId || baseEnd is null || baseEnd < grant.EffectiveTick)
                Fail("A non-land grant must preserve the approved extension, consent, handover and original term.");
            var active = state.Offices.Where(office => office.ElectionId == grant.Id).ToArray();
            var ended = state.OfficeHistory.Where(term => term.ElectionId == grant.Id).ToArray();
            if (active.Length + ended.Length != 1 ||
                active.Any(office => office.Mandates != "non_land" || office.HolderId != grant.HolderId ||
                    office.TermStartTick != grant.TermStartTick || office.TermEndTick != grant.TermEndTick) ||
                ended.Any(term => term.Mandates != "non_land" || term.HolderId != grant.HolderId ||
                    term.StartTick != grant.TermStartTick || term.EndTick < grant.EffectiveTick || term.EndTick > baseEnd!.Value))
                Fail("Added duties must end with their base term and retain exactly one current or historical office record.");
        }
    }

}
