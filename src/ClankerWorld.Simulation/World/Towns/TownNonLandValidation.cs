using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

/// <summary>Validates the durable civil procedure, consent and performance file before it is used.</summary>
public static partial class TownNonLandValidation
{
    public static void Validate(WorldMap map, SocietyCheckpoint society, TownRuntimeState town,
        IReadOnlyList<TownLandTitleRecord> titles, int day)
    {
        var state = town.NonLandHearings;
        var tick = society.WorldTick;
        ValidateLocal(town.Id, state);
        Check(day > 0, "The Town must retain its nonviolent case and remedy records.");
        var people = society.Agents.ToDictionary(p => p.Id, StringComparer.Ordinal);
        var households = society.Households.Select(h => h.Id).ToHashSet(StringComparer.Ordinal);
        foreach (var item in state.Cases) ValidateCase(map, society, town, item, people, households, titles, day);
        foreach (var offer in state.Offers) ValidateOffer(state, town, offer, people, tick, day);
        foreach (var agreement in state.Agreements) ValidateAgreement(state, agreement, tick);
        foreach (var effect in state.Effects)
            Check(people.ContainsKey(effect.ActorId) && effect.Tick <= tick,
                "A performance receipt must be the agreed contributor's actual later effect, with exact goods or work.");
    }

    private static void ValidateCase(WorldMap map, SocietyCheckpoint society, TownRuntimeState town,
        TownNonLandCase item, Dictionary<string, SocietyAgent> people, HashSet<string> households,
        IReadOnlyList<TownLandTitleRecord> titles, int day)
    {
        var tick = society.WorldTick;
        Check(item.FiledTick <= tick &&
            map.Contains(item.Allegation.Position) && people.ContainsKey(item.Allegation.SubjectId), "A saved allegation must identify its actual Town, person, place and time.");
        var law = town.Government?.Laws.SingleOrDefault(l => l.Id == item.Allegation.LawId);
        // Native conduct captures the actual wording before another action can amend
        // the law in the same tick. Live applicability still uses InForceAt.
        var version = law?.Versions.SingleOrDefault(v => v.Version == item.Allegation.LawVersion);
        Check(version is not null && version.AdoptedTick <= item.Allegation.ConductTick &&
            (version.EndedTick is null || version.EndedTick >= item.Allegation.ConductTick),
            "A report must use the law wording in force at the time of conduct.");
        if (version.Scope is TownLawRules.Jurisdiction or TownLawRules.Site)
            Check(titles.Any(t => t.TownId == town.Id && t.RecordedTick <= item.Allegation.ConductTick && t.Tiles.Contains(item.Allegation.Position)) &&
                (version.Scope != TownLawRules.Site || version.SiteTiles.Contains(item.Allegation.Position)),
                "A later Town claim cannot create earlier territorial jurisdiction.");
        // Resident-duty context and actual source acquisition are checked against the
        // native conduct snapshot by the runtime's physical-source validator.
        foreach (var revision in item.Revisions)
        {
            Check(revision.PublishedTick >= item.FiledTick && revision.PublishedTick <= tick &&
                revision.DeadlineTick >= revision.PublishedTick && revision.DeadlineTick - revision.PublishedTick == day && Id(revision.NoticeId) &&
                town.Council?.Notices.Any(n => n.Id == revision.NoticeId && n.Kind == "law_case" &&
                    n.SubjectId == DerivedId.Create(item.Id + ":" + revision.Number.ToString(CultureInfo.InvariantCulture)) && n.PostedTick == revision.PublishedTick) == true,
                "A hearing needs its actual published notice and a complete response day.");
            ValidateParties(revision.Parties, people, households, society);
            ValidateTownParty(town, item, revision.Parties, revision.PublishedTick);
        }
        Check(item.DirectStakeIds.All(people.ContainsKey), "Direct stakes must name known people.");
        foreach (var filing in item.Filings)
            Check(people.ContainsKey(filing.AgentId) && filing.Tick <= tick &&
                (filing.Kind != "town" || ValidTownFiling(town, item, filing)), "A filing must retain a specific informed reporter and source.");
        foreach (var evidence in item.Evidence)
            Check(people.ContainsKey(evidence.SourceAgentId) && people.ContainsKey(evidence.SubmittedByAgentId) &&
                evidence.SubmittedTick <= tick && item.Revisions.Any(r => r.Number == evidence.Revision &&
                    TownNonLandRules.HasNoticeReceipt(r, evidence.SubmittedByAgentId, evidence.SubmittedTick, town.Council!.Knowledge)),
                "Evidence must preserve its source and actual informed submission.");
        foreach (var response in item.Responses)
        {
            var revision = item.Revisions.SingleOrDefault(r => r.Number == response.Revision);
            var party = revision?.Parties.SingleOrDefault(p => p.Id == response.PartyId);
            Check(response.Tick <= tick &&
                TownNonLandRules.HasNoticeReceipt(revision!, response.AgentId, response.Tick, town.Council!.Knowledge),
                "A response must belong to the noticed person or their recorded caregiver support.");
        }
        foreach (var read in item.Reads)
            Check(people.ContainsKey(read.AgentId) && read.ReadTick <= tick, "A file read cannot contain unknown or future facts.");
        foreach (var consent in item.JudgeConsents)
            Check(people.ContainsKey(consent.AgentId) && consent.Tick <= tick && (consent.WithdrawnTick is null || consent.WithdrawnTick <= tick), "A case judge needs their own recorded willingness.");
        foreach (var contest in item.ContestHistory.Concat(item.Contest is { } live ? [live] : []))
            ValidateContest(contest, item, people, tick, day, ReferenceEquals(contest, item.Contest));
        if (item.Judge is { } judge)
        {
            ValidateJudge(judge, item, town.Government, people, tick);
            Check(town.Government is { Arrangement.NonLand: TownArrangementRules.Mayor } currentGovernment &&
                (judge.Kind == "case_elected" || TownGovernmentRules.CurrentNonLandAuthority(currentGovernment, tick) is { } authority &&
                    authority.HolderId == judge.AgentId && authority.AuthorityId == judge.AuthorityId) &&
                people[judge.AgentId].Status == SocietyAgentStatus.Active &&
                LifeStageRules.IsAdult(people[judge.AgentId].AgeBand) && town.ResidentIds.Contains(judge.AgentId) &&
                !TownNonLandRules.JudgeConflict(judge.AgentId, people[judge.AgentId].HouseholdId,
                    TownNonLandPartyRules.CurrentParties(town, item, society, tick), item.DirectStakeIds.ToHashSet(StringComparer.Ordinal)),
                "A current case judge must remain an independent eligible Town adult.");
        }
        foreach (var term in item.JudgeHistory)
        {
            Check(term.EndedTick <= tick, "An ended assignment must retain its actual end and reason.");
            ValidateJudge(term.Judge, item, town.Government, people, term.Judge.AssignedTick);
        }
        foreach (var finding in item.Findings) ValidateFinding(town, item, finding, people, households, society);
        foreach (var request in item.ReopenRequests)
        {
            Check(people.ContainsKey(request.AgentId) && request.Tick <= tick && (request.AssessedTick is null || request.AssessedTick <= tick),
                "A reopening request must identify real submitted grounds.");
            if (request.AssessedBy is { } assessor)
            {
                ValidateJudge(assessor, item, town.Government, people, request.AssessedTick!.Value);
            }
        }
    }

    private static void ValidateParties(IReadOnlyList<TownCaseParty> parties, Dictionary<string, SocietyAgent> people,
        HashSet<string> households, SocietyCheckpoint society)
    {
        Check(TownNonLandRules.ValidParties(parties), "Case parties must be complete and unique.");
        foreach (var party in parties)
            Check(people.ContainsKey(party.SubjectId) && (party.RespondingAdultId is null || people.ContainsKey(party.RespondingAdultId)) &&
                (party.HouseholdId is null || households.Contains(party.HouseholdId)) &&
                (party.CareRelationshipId is null ? party.Role == "town" || party.RespondingAdultId is null || party.RespondingAdultId == party.SubjectId :
                    society.Relationships.Any(r => r.Id == party.CareRelationshipId && r.Type == SocietyRelationshipType.Caregiver &&
                        r.ProposerId == party.RespondingAdultId && r.TargetId == party.SubjectId && r.Revision >= party.CareRevision)),
                "A party's caregiver support must have actual care provenance, not shared housing.");
    }

    private static bool OrdinaryMayorAt(TownRuntimeState town, string actor, string? authority, long tick) =>
        town.Government is { } government && (government.Offices.Any(o => o.Mandates == "ordinary" && o.HolderId == actor &&
            (authority is null || o.ElectionId == authority) && o.TermStartTick <= tick && o.TermEndTick > tick) ||
            government.OfficeHistory.Any(o => o.Mandates == "ordinary" && o.HolderId == actor &&
                (authority is null || o.ElectionId == authority) && o.StartTick <= tick && o.EndTick >= tick));

    private static bool ValidTownFiling(TownRuntimeState town, TownNonLandCase item, TownViolationFiling filing) =>
        Id(filing.AuthorityId) && (OrdinaryMayorAt(town, filing.AgentId, filing.AuthorityId, filing.Tick) ||
            town.Council?.Proposals.Any(p => p.Id == filing.AuthorityId && p.Kind == "law_case" && p.Status == "passed" &&
                p.AuthorId == filing.AgentId && p.SettledTick <= filing.Tick && p.NonLandRequest is { } request &&
                TownNonLandRules.CaseKey(town.Id, request.Allegation) == item.Key) == true);

    private static void ValidateTownParty(TownRuntimeState town, TownNonLandCase item, IReadOnlyList<TownCaseParty> parties, long tick)
    {
        foreach (var party in parties.Where(p => p.Role == "town"))
            Check(party.Id == TownRecordIds.TownParty(town.Id) && party.CareRelationshipId is null &&
                item.Filings.Any(f => f.Kind == "town" && f.AgentId == party.SubjectId && f.Tick <= tick) &&
                (party.RespondingAdultId is null || OrdinaryMayorAt(town, party.RespondingAdultId, null, tick) ||
                    item.Filings.Any(f => f.Kind == "town" && f.AgentId == party.RespondingAdultId && f.Tick <= tick &&
                        town.Council!.Proposals.Any(p => p.Id == f.AuthorityId && p.AuthorId == f.AgentId && p.Kind == "law_case" && p.Status == "passed"))),
                "The Town party needs its actual public filing and institutional representative, never household liability.");
    }

    private static void ValidateFinding(TownRuntimeState town, TownNonLandCase item, TownViolationFinding finding,
        Dictionary<string, SocietyAgent> people, HashSet<string> households, SocietyCheckpoint society)
    {
        Check(finding.Tick <= society.WorldTick, "A finding requires its noticed case, civil standard, sources, reasons and uncertainty.");
        ValidateParties(finding.Parties, people, households, society);
        ValidateTownParty(town, item, finding.Parties, finding.Tick);
        ValidateJudge(finding.Judge, item, town.Government, people, finding.Tick);
        if (finding.Consequence == "censure")
            Check(town.NonLandHearings.ConductRecords?.Any(c => c.Id == item.Allegation.IncidentId && c.ActorId == item.Allegation.SubjectId &&
                c.Laws?.Any(l => l.LawId == item.Allegation.LawId && l.Version == item.Allegation.LawVersion && l.PriorNoticeIds is { Count: > 0 }) == true) == true,
                "Censure must retain credible prior law notice; explanation and warning remain available without it.");
    }

    private static void ValidateJudge(TownCaseJudge judge, TownNonLandCase item, TownGovernmentState? government,
        Dictionary<string, SocietyAgent> people, long at)
    {
        Check(people.ContainsKey(judge.AgentId) && judge.AssignedTick <= at &&
            government is not null && TownGovernmentRules.NonLandScopeAt(government, at, includeEndingTick: true) &&
            (judge.Kind == "non_land_mayor" ? TownGovernmentRules.NonLandAuthorityAt(government, judge.AgentId, judge.AuthorityId, at) :
                item.JudgeConsents.Any(c => c.AgentId == judge.AgentId &&
                        c.Tick <= at && (c.WithdrawnTick is null || c.WithdrawnTick >= at))), "The judge must have exact historical non-land authority for this decision.");
    }

    private static void ValidateContest(TownCaseJudgeContest contest, TownNonLandCase item,
        Dictionary<string, SocietyAgent> people, long tick, int day, bool live)
    {
        Check(contest.OpenedTick <= tick && contest.Voters.Concat(contest.Candidates).All(people.ContainsKey) &&
            (live || contest.SettledTick <= tick) && (contest.Stage != "voting" ||
                contest.RoundDeadlineTick == contest.RoundOpenedTick + day && contest.RoundDeadlineTick > tick),
            "Case-election timing, ballots or status are inconsistent.");
        foreach (var round in contest.Rounds)
            Check(round.ClosedTick <= tick && round.Voters.Concat(round.Candidates).All(people.ContainsKey) &&
                (round.Result is not ("winner" or "tie") || round.ClosedTick >= round.OpenedTick + day),
                "Case-election rounds must preserve real ballots, time and the prior tie.");
        Check(contest.Stage != "completed" || people.ContainsKey(contest.WinnerId!), "A completed case election needs its actual winner.");
    }

    private static void ValidateOffer(TownNonLandHearingState state, TownRuntimeState town, TownRemedyOffer offer,
        Dictionary<string, SocietyAgent> people, long tick, int day)
    {
        Check(offer.PublishedTick <= tick && offer.ResponseDeadlineTick - offer.PublishedTick == day &&
            offer.Terms.All(t => people.ContainsKey(t.ContributorId)) &&
            town.Council?.Notices.Any(n => n.Id == offer.NoticeId && n.Kind == "remedy" && n.SubjectId == offer.Id && n.PostedTick == offer.PublishedTick) == true,
            "A voluntary offer needs its actual finding, publication, exact terms and response window.");
        foreach (var response in offer.Responses)
            Check(response.Tick <= tick && TownHearingProcedure.HasNotice(offer.NoticeId, offer.PublishedTick, response.AgentId, response.Tick, town.Council!.Knowledge),
                "Each remedy response must be that contributor's informed answer to the exact offer.");
        Check(offer.Status switch
        {
            "pending" => tick < offer.ResponseDeadlineTick,
            "unanswered" => tick >= offer.ResponseDeadlineTick,
            _ => true,
        }, "Offer status cannot invent consent or conceal decline, silence or a counteroffer.");
    }

    private static void ValidateAgreement(TownNonLandHearingState state, TownRestorativeAgreement agreement, long tick)
    {
        Check(agreement.AcceptedTick <= tick, "An agreement needs every contributor's own exact consent and recorded deadline.");
        Check(agreement.Status == "completed" || agreement.Status == (tick > agreement.DeadlineTick ? "overdue" : "pending"),
            "Compliance must reflect actual physical performance and the agreed deadline.");
    }

    private static bool MissingConsent(TownRemedyOffer offer) =>
        offer.Terms.Any(t => !offer.Responses.Any(r => r.AgentId == t.ContributorId && r.Kind == "accept"));
    private static bool Ballots(IReadOnlyList<string> voters, IReadOnlyList<string> candidates, IReadOnlyList<TownMayoralBallot>? ballots) =>
        ballots is not null && ballots.All(b => b is not null && voters.Contains(b.AgentId) && candidates.Contains(b.CandidateId)) &&
        ballots.Select(b => b.AgentId).Distinct(StringComparer.Ordinal).Count() == ballots.Count;
    private static bool Id(string? value) => TownHearingProcedure.Id(value);
    private static bool Text(string? value) => TownHearingProcedure.Text(value);
    private static bool Canonical(IReadOnlyList<string>? values) => values is not null && values.All(Id) && values.SequenceEqual(TownHearingProcedure.Ordered(values));
    private static bool OrderedTimes(IEnumerable<long> values) => values.SequenceEqual(values.Order());
    private static void Unique(IEnumerable<string> values) { var all = values.ToArray(); Check(all.All(Id) && all.Distinct(StringComparer.Ordinal).Count() == all.Length, "Saved civil record identities must be unique."); }
    private static void Check([DoesNotReturnIf(false)] bool valid, string reason) { if (!valid) throw new InvalidDataException(reason); }
}
