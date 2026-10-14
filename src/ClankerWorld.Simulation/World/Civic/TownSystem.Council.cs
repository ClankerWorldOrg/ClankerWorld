using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Society;
using System.Text.RegularExpressions;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace ClankerWorld.Simulation.World;

internal sealed partial class TownSystem
{
    internal static string[] TownAdults(ICivicHistoryWorld world, TownRuntimeState town) => town.ResidentIds.Where(id => world.Society.Agents.Any(p => p.Id == id && p.Status == SocietyAgentStatus.Active &&
            p.AgeBand is SocietyAgeBand.Adult or SocietyAgeBand.Elder)).Order(StringComparer.Ordinal).ToArray();
    internal static int CivicDay(ICivicHistoryWorld world) => world.CalendarConfig.TicksPerDay;
    internal static long CivicDayNumber(ICivicHistoryWorld world, long tick) => WorldCalendarRules.FromTick(tick, world.CalendarConfig).DayIndex + 1;
    internal static string? CivicNote(ICivicHistoryWorld world, string actor)
    {
        var learned = world.Towns.Where(t => t.Council is not null).SelectMany(t =>
        {
            var history = CivicHistory(t);
            return history.Recent(actor).Select(k => (Receipt: k, Notice: history.Notices[k.NoticeId], Town: t.Name));
        })
            .OrderByDescending(n => n.Receipt.LearnedTick).ThenByDescending(n => n.Notice.PostedTick).Take(3)
            .Select(n => (n.Receipt.SourceAgentId is { } source ? $"Heard from {world.Society.GetAgent(source).Name}: " : "Read Town notice: ") +
                n.Town + ": " + ReadableCivicNotice(world, n.Notice.Text)).ToArray();
        var excerpt = string.Join(" | ", learned);
        return learned.Length == 0 ? null : CivicExcerpt(excerpt, 1024);
    }


    [GeneratedRegex(@"\btick (\d+)", RegexOptions.CultureInvariant)]
    internal static partial Regex CivicTickText();
    internal static string ReadableCivicNotice(ICivicHistoryWorld world, string text)
    {
        foreach (var person in world.Society.Agents.OrderByDescending(p => p.Id.Length))
            text = text.Replace(person.Id, person.Name, StringComparison.Ordinal);
        text = CivicTickText().Replace(text, match =>
        {
            if (!long.TryParse(match.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var tick))
                return match.Value;
            var day = WorldCalendarRules.FromTick(tick, world.CalendarConfig).DayIndex;
            return day < long.MaxValue ? "world day " + (day + 1).ToString(CultureInfo.InvariantCulture) : match.Value;
        });
        return CivicExcerpt(text, 270);
    }

    internal static string CivicExcerpt(string text, int limit)
    {
        var length = Math.Min(limit, text.Length);
        if (length > 0 && length < text.Length &&
            char.IsHighSurrogate(text[length - 1]) && char.IsLowSurrogate(text[length]))
            length--;
        return text[..length];
    }

    internal void AdvanceTownCouncil(ITownWorld world)
    {
        foreach (var town in world.Towns.ToArray())
        {
            if (town.FoundingState != "founded")
            {
                if (town.Government is not null)
                    SetTown(world, town with { Council = TownCouncilState.Create(TownAdults(world, town)) });
                continue;
            }
            var governance = town.Council ?? TownCouncilState.Create(TownAdults(world, town));
            var (council, government) = AdvanceCivic(world, town, governance, town.Government ?? TownGovernmentState.Create());
            SaveTownCouncil(world, town, council, government);
        }
    }

    internal void SaveTownCouncil(ITownWorld world, TownRuntimeState town, TownCouncilState updated, TownGovernmentState? government = null)
    {
        var latest = world.Towns.Single(current => current.Id == town.Id);
        town = town with { LandRecords = latest.LandRecords, NonLandHearings = latest.NonLandHearings };
        government ??= town.Government;
        var priorNotices = town.Council?.Notices.Count ?? 0;
        (town, updated) = ApplyApprovedTownLandClaims(world, town, updated);
        updated = ResolveHouseholdLandRequests(world, town, updated);
        var priorHearings = town.LandRecords;
        var priorNonLand = town.NonLandHearings;
        var transferUpdate = AdvanceTownLandTransfers(world, town with { Council = updated, Government = government }, updated);
        updated = transferUpdate.Council;
        town = town with { LandRecords = transferUpdate.LandRecords };
        var hearingUpdate = AdvanceTownLandHearings(world, town with { Council = updated, Government = government },
            updated, government ?? TownGovernmentState.Create());
        updated = hearingUpdate.Council;
        town = town with { LandRecords = hearingUpdate.LandRecords };
        var nonLandUpdate = AdvanceTownNonLand(world, town with { Council = updated, Government = government }, updated, government ?? TownGovernmentState.Create());
        updated = nonLandUpdate.Council;
        town = town with { NonLandHearings = nonLandUpdate.NonLandHearings };
        if (town.Council == updated && town.Government == government && priorHearings == town.LandRecords && priorNonLand == town.NonLandHearings) return;
        SetTown(world, town with { Council = updated, Government = government });
        foreach (var notice in updated.Notices.Skip(priorNotices))
            world.AppendEvent("town_civic_" + notice.Kind, $"{town.Id}|{notice.SubjectId}|{notice.Text}", CivicBoard(world, town));
    }

    internal static GridPoint? CivicBoard(ITownWorld world, TownRuntimeState town) => world.WorldSimulation.Buildings.Where(b => b.TownId == town.Id && b.HouseholdId is null && world.WorldContent.Buildings.Any(d => d.CanonicalId == b.DefinitionId && d.Tags.Contains(TownHallContent.HallTag, StringComparer.Ordinal)))
            .OrderBy(b => b.PlacedTick).ThenBy(b => b.InstanceId, StringComparer.Ordinal)
            .Select(b => (GridPoint?)(b.Entrance ?? TownHallContent.Entrance(b.Position))).FirstOrDefault() ?? town.OriginSite ?? world.WorldSimulation.Buildings.FirstOrDefault(b => b.TownId == town.Id && b.HouseholdId is null)?.Position ?? world.Map.MapObjects.FirstOrDefault(p => p.Kind == "storage" && town.BorderTiles.Contains(p.Position))?.Position ??
        town.BorderTiles.Where(world.Map.IsBuildable).OrderBy(p => p.Y).ThenBy(p => p.X).Select(p => (GridPoint?)p).FirstOrDefault();
    internal static bool NearCivicBoard(ITownWorld world, string actor, TownRuntimeState town) => CivicBoard(world, town) is { } board && world.IsWithinInteractionRange(world.Agents[actor].Position, board, WorldRuntime.CivicSharedRules.ResourceInteractionRange);
    internal static string CivicAgentToken(string id) => id.Length == 0 ? id : DerivedId.Create(id, "agent-sha256:");
    internal static string ResolveCivicAgentToken(ICivicHistoryWorld world, string token) => world.Society.Agents.FirstOrDefault(p =>
        p.Id == token || CivicAgentToken(p.Id) == token)?.Id ?? token;
    internal static string CivicAction(ICivicHistoryWorld world, string town, string kind, string subject = "", string choice = "") =>
        $"civic|{town}|{kind}|{(world.Society.Agents.Any(p => p.Id == subject) ? CivicAgentToken(subject) : subject)}|{CivicAgentToken(choice)}";
    internal static string CivicRoundToken(TownElection election) =>
        DerivedId.Create($"{election.Id}:{election.Stage}:{election.OpenedTick.ToString(CultureInfo.InvariantCulture)}");
    private void AddTownCivicCandidateOptions(ITownWorld world, List<CognitionCandidate> candidates, string actor,
        IReadOnlyList<string>? buildingsWithoutSites = null)
    {
        // Civic agreement and votes are explicit personal choices, never a built-in idle alternative.
        if (world.NeedsUrgentWarmth(world.Agents[actor])) return;
        var siteRecovery = buildingsWithoutSites is { Count: > 0 }
            ? $"No legal building site is available for {string.Join(", ", buildingsWithoutSites)}. "
            : string.Empty;
        if (TownFoundingLayout(world, actor) is { } founding)
            OfferTownSettlement(world, candidates, actor, founding, false,
                "Explicitly found a new Town here on unclaimed land. You leave your previous Town and your dependent children join without moving or changing care. The new council begins with its adult residents. Existing household property stays owned; no House, goods or household membership are supplied.");
        foreach (var town in world.Towns.Where(t => t.Council is not null))
        {
            OfferTownSettlement(world, candidates, actor, town, true, ResettlementText(world, actor, town));
            var state = town.Council!;
            var history = CivicHistory(town);
            var adults = TownAdults(world, town);
            var resident = adults.Contains(actor, StringComparer.Ordinal);
            OfferCivicNotice(world, candidates, actor, town, null, $"Read the actual civic notices posted at {town.Name}.", 155);
            if (resident)
            {
                if (!NearCivicBoard(world, actor, town) && CivicBoard(world, town) is not null)
                    candidates.Add(new(CivicAction(world, town.Id, "visit"), $"Visit {town.Name}\'s public notice place to see what has been posted.", 175));
                OfferCouncilConsent(world, candidates, actor, town, CouncilConsentKind.Register, "",
                    $"Personally agree to stand for full council terms in {town.Name}.", 185);
                if (state.TermEndTick is { } end)
                    OfferCouncilConsent(world, candidates, actor, town, CouncilConsentKind.Remainder, end.ToString(System.Globalization.CultureInfo.InvariantCulture),
                        $"Agree only to fill a vacancy until this council term ends in {town.Name}; this does not enter the next full-term contest.", 186);
                OfferCouncilConsent(world, candidates, actor, town, CouncilConsentKind.WithdrawCandidate, "",
                    $"Withdraw your willingness to stand in {town.Name}.", 190);
                foreach (var nearby in world.Agents.Values.Where(p => p.AgentId != actor && world.IsAdult(p.AgentId) && world.IsWithinInteractionRange(p.Position, world.Agents[actor].Position, WorldRuntime.CivicSharedRules.ResourceInteractionRange)))
                {
                    var nominee = nearby.AgentId;
                    OfferCouncilConsent(world, candidates, actor, town, CouncilConsentKind.Nominate, nominee,
                        $"Nominate {world.Society.GetAgent(nominee).Name} for full council terms in {town.Name}; they must personally agree before becoming a candidate.", 188);
                    OfferTownAdmission(world, candidates, actor, town, TownAdmissionChoiceKind.Sponsor, nominee,
                        $"Ask {town.Name}'s council to approve admission of {world.Society.GetAgent(nominee).Name}, the adult nearby. A request grants no membership or Warehouse access.", 188);
                }
                AddTownLawCandidates(world, candidates, actor, town);
                if (town.Government is not null) AddTownGovernmentCandidates(world, candidates, actor, town);
                world.AddTownProjectProposalCandidates(candidates, actor, town);
                world.AddBoatAccessCandidates(candidates, actor, town);
                var here = world.Agents[actor].Position;
                // The model sees no map grid, so name real claimable tiles it can choose from.
                OfferTownLandClaim(world, candidates, actor, town, siteRecovery);
                if (world.Society.GetAgent(actor).HouseholdId is not null && RequestableLandNear(world, town, here, 6) is { Length: > 0 } free)
                    candidates.Add(new(CivicAction(world, town.Id, "request_land_use"), siteRecovery + $"Ask {town.Name}'s council for household use of a connected plot of Town-titled land; include exact coordinates in civic_land_tiles. " +
                        FormattableString.Invariant($"You stand at ({here.X}, {here.Y}); free Town land nearest you: {string.Join("; ", free.Select(tile => FormattableString.Invariant($"({tile.X}, {tile.Y})")))}. ") +
                        "Filing grants nothing and supplies no household acceptance.", 190));
            }
            else if (NearCivicBoard(world, actor, town))
            {
                OfferTownAdmission(world, candidates, actor, town, TownAdmissionChoiceKind.Request, "", AdmissionRequestText(world, actor, town), 170);
            }
            else if (CivicBoard(world, town) is not null && MayVisitAsNewcomer(world, actor, town))
                candidates.Add(new(CivicAction(world, town.Id, "visit"), $"Walk to {town.Name}'s public notice place to read what is posted. Visiting grants no membership.", 175));
            AddTownAdmissionCandidates(world, candidates, actor, town);
            AddHouseholdLandCandidates(world, candidates, actor, town);
            AddTownLandHearingCandidates(world, candidates, actor, town);
            AddTownNonLandCandidates(world, candidates, actor, town);
            AddNonLandRemedyCandidates(world, candidates, actor, town);
            AddTownLandTransferCandidates(world, candidates, actor, town);
            foreach (var proposal in state.Proposals.Where(p => p.Status == "pending" && history.Knows(actor, p.Id)))
            {
                var draft = town.Government?.LawDrafts.SingleOrDefault(d => d.ProposalId == proposal.Id);
                var voteText = draft is not null ? TownLawRules.VoteText(draft) : proposal.Text;
                OfferCivicProposalVote(world, candidates, actor, town, proposal, CivicProposalVoteKind.Withdraw,
                    $"Withdraw your pending proposal: {voteText}", 190);
                var plot = proposal.LandClaimTiles is { } tiles ? " Exact tiles: " + TownLandClaimRules.DescribeTiles(tiles) + "." :
                    proposal.Kind == "land_use" && world.HouseholdLandUseRequests.SingleOrDefault(r => r.Id == proposal.SubjectId) is { } request
                        ? " " + LandUseTerms(world, request) : proposal.LandHearingRequest is { } hearingRequest
                            ? " " + TownLandGovernmentFilingRules.Describe(hearingRequest) : proposal.NonLandRequest is { } nonLandRequest
                                ? " " + TownNonLandGovernmentFilingRules.Describe(nonLandRequest) : "";
                OfferCivicProposalVote(world, candidates, actor, town, proposal, CivicProposalVoteKind.Yes,
                    $"Cast your final yes vote on {voteText} in {town.Name}. {proposal.RequiredYes} yes votes required.{plot}", 165);
                OfferCivicProposalVote(world, candidates, actor, town, proposal, CivicProposalVoteKind.No,
                    $"Cast your final no vote on {voteText} in {town.Name}.{plot}", 166);
            }
            if (state.Election is { } election)
            {
                OfferCouncilBallot(world, candidates, actor, town, null, $"Submit or revise your {election.Stage} ballot in {town.Name}; choose up to {election.Seats} distinct IDs via civic_ballot. " +
                    $"Willing candidates: {string.Join(", ", election.Candidates.Select(id => world.Society.GetAgent(id).Name + " (" + CivicAgentToken(id) + ")"))}. Self-voting is allowed. " +
                    $"Voting closes on world day {CivicDayNumber(world, election.DeadlineTick)}.", 165);
                foreach (var id in election.Candidates)
                    OfferCouncilBallot(world, candidates, actor, town, id, $"Submit or revise your ballot to support only {world.Society.GetAgent(id).Name} in {town.Name}. Other previous choices are replaced.", 167);
            }
            foreach (var recipient in world.Agents.Values.Where(p => p.AgentId != actor && world.IsWithinInteractionRange(p.Position, world.Agents[actor].Position, WorldRuntime.CivicSharedRules.ResourceInteractionRange)))
            {
                OfferCivicNotice(world, candidates, actor, town, recipient.AgentId,
                    $"Relay the civic notices you actually learned in {town.Name} to {world.Society.GetAgent(recipient.AgentId).Name} nearby.", 180);
            }
        }
    }

    internal void ContinueTownCivicVisit(ITownWorld world, string actor, string candidate)
    {
        if (CivicVisitChoice.Decode(candidate) is not { } choice || PlanCivicVisit(world, actor, choice) is not { } plan) return;
        if (CheckCivicVisit(world, plan).Refusal is { } refusal) { world.RefuseChoice(plan, refusal); return; }
        world.MoveToward(actor, world.Agents[actor], plan.NoticePlace, "town_notices", WorldRuntime.CivicSharedRules.ResourceInteractionRange);
    }

    internal void ApplyTownCivicCandidate(ITownWorld world, string actor, string candidate, string? proposalText = null, IReadOnlyList<string>? ballot = null,
        IReadOnlyList<CognitionLandTile>? landTiles = null, CognitionLandHearingChoice? hearingChoice = null, CognitionNonLandChoice? nonLandChoice = null,
        ActionPlan? originalPlan = null)
    {
        var parts = candidate.Split('|');
        if (parts.Length != 5) return;
        if (parts[2] == "found")
        {
            var founding = originalPlan as TownFoundingActionPlan ??
                (TownFoundingChoice.Decode(candidate) is { } choice ? PlanTownFounding(world, actor, choice) : null);
            if (founding is null) return;
            FoundTown(world, actor, candidate, founding);
            return;
        }
        var selectedId = candidate;
        if (parts[2] is "nominate" or "relay" or "request_admission") parts[3] = ResolveCivicAgentToken(world, parts[3]);
        if (parts[2] == "single") parts[4] = ResolveCivicAgentToken(world, parts[4]);
        var town = world.Towns.SingleOrDefault(t => t.Id == parts[1]);
        if (town?.Council is not { } state) return;
        var government = town.Government ?? TownGovernmentState.Create();
        var visitPlan = originalPlan as CivicVisitActionPlan;
        var consentPlan = originalPlan as CouncilConsentActionPlan;
        var ballotPlan = originalPlan as CouncilBallotActionPlan;
        var admissionPlan = originalPlan as TownAdmissionActionPlan;
        var resettlementPlan = originalPlan as TownFoundingActionPlan;
        var noticePlan = originalPlan as CivicNoticeActionPlan;
        var proposalVotePlan = originalPlan as CivicProposalVoteActionPlan;
        var lawPlan = originalPlan as TownLawActionPlan;
        var policyPlan = originalPlan as TownPolicyActionPlan;
        var governmentProposalPlan = originalPlan as GovernmentProposalActionPlan;
        var officePlan = originalPlan as GovernmentOfficeActionPlan;
        var mayoralBallotPlan = originalPlan as MayoralBallotActionPlan;
        var landClaimPlan = originalPlan as TownLandClaimActionPlan;
        // Current authority, live notice knowledge and exact contest IDs are checked again when delayed responses arrive.
        if (parts[2] == "visit")
        {
            visitPlan ??= CivicVisitChoice.Decode(selectedId) is { } visit ? PlanCivicVisit(world, actor, visit) : null;
            if (visitPlan is null) return;
            if (CheckCivicVisit(world, visitPlan).Refusal is { } refusal) { world.RefuseChoice(visitPlan, refusal); return; }
        }
        else if (CouncilConsentChoice.Decode(selectedId) is { } consent)
        {
            consentPlan ??= PlanCouncilConsent(world, actor, consent);
            if (consentPlan is null) return;
            if (CheckCouncilConsent(world, consentPlan).Refusal is { } refusal) { world.RefuseChoice(consentPlan, refusal); return; }
        }
        else if (CouncilBallotChoice.Decode(selectedId) is { } electionChoice)
        {
            ballotPlan ??= PlanCouncilBallot(world, actor, electionChoice);
            if (ballotPlan is null) return;
            ballotPlan = BindCouncilBallot(world, ballotPlan, ballot);
            if (CheckCouncilBallot(world, ballotPlan).Refusal is { } refusal) { world.RefuseChoice(ballotPlan, refusal); return; }
        }
        else if (TownAdmissionChoice.Decode(selectedId) is { } admissionChoice)
        {
            admissionPlan ??= PlanTownAdmission(world, actor, admissionChoice);
            if (admissionPlan is null) return;
            if (CheckTownAdmissionPlan(world, admissionPlan).Refusal is { } refusal) { world.RefuseChoice(admissionPlan, refusal); return; }
        }
        else if (TownFoundingChoice.Decode(selectedId) is { Resettle: true } resettlement)
        {
            resettlementPlan ??= PlanTownFounding(world, actor, resettlement);
            if (resettlementPlan is null) return;
            if (CheckTownFoundingPlan(world, resettlementPlan).Refusal is { } refusal) { world.RefuseChoice(resettlementPlan, refusal); return; }
        }
        else if (CivicNoticeChoice.Decode(selectedId) is { } noticeChoice)
        {
            noticePlan ??= PlanCivicNotice(world, actor, noticeChoice);
            if (noticePlan is null) return;
            if (CheckCivicNoticePlan(world, noticePlan).Refusal is { } refusal) { world.RefuseChoice(noticePlan, refusal); return; }
        }
        else if (CivicProposalVoteChoice.Decode(selectedId) is { } proposalResponse)
        {
            proposalVotePlan ??= PlanCivicProposalVote(world, actor, proposalResponse);
            if (proposalVotePlan is null) return;
            if (CheckCivicProposalVote(world, proposalVotePlan).Refusal is { } refusal) { world.RefuseChoice(proposalVotePlan, refusal); return; }
        }
        else if (TownLandClaimChoice.Decode(selectedId) is { } claimChoice)
        {
            landClaimPlan ??= PlanTownLandClaim(world, actor, claimChoice);
            if (landClaimPlan is null) return;
            landClaimPlan = BindTownLandClaim(landClaimPlan, landTiles);
            if (CheckTownLandClaim(world, landClaimPlan).Refusal is { } refusal) { world.RefuseChoice(landClaimPlan, refusal); return; }
        }
        else if (MayoralBallotChoice.Decode(selectedId) is { } mayorChoice)
        {
            mayoralBallotPlan ??= PlanMayoralBallot(world, actor, mayorChoice);
            if (mayoralBallotPlan is null) return;
            if (CheckMayoralBallot(world, mayoralBallotPlan).Refusal is { } refusal) { world.RefuseChoice(mayoralBallotPlan, refusal); return; }
        }
        else if (GovernmentOfficeChoice.Decode(selectedId) is { } officeChoice)
        {
            officePlan ??= PlanGovernmentOffice(world, actor, officeChoice);
            if (officePlan is null) return;
            if (CheckGovernmentOffice(world, officePlan).Refusal is { } refusal) { world.RefuseChoice(officePlan, refusal); return; }
        }
        else if (GovernmentProposalChoice.Decode(selectedId) is { } governmentChoice)
        {
            governmentProposalPlan ??= PlanGovernmentProposal(world, actor, governmentChoice);
            if (governmentProposalPlan is null) return;
            if (CheckGovernmentProposal(world, governmentProposalPlan).Refusal is { } refusal) { world.RefuseChoice(governmentProposalPlan, refusal); return; }
        }
        else if (TownPolicyChoice.Decode(selectedId) is { } policyChoice)
        {
            policyPlan ??= PlanTownPolicy(world, actor, policyChoice);
            if (policyPlan is null) return;
            if (CheckTownPolicyPlan(world, policyPlan).Refusal is { } refusal) { world.RefuseChoice(policyPlan, refusal); return; }
        }
        else if (TownLawChoice.Decode(selectedId) is { } lawChoice)
        {
            lawPlan ??= PlanTownLaw(world, actor, lawChoice);
            if (lawPlan is null) return;
            lawPlan = BindTownLawText(lawPlan, proposalText);
            if (CheckTownLawPlan(world, lawPlan).Refusal is { } refusal) { world.RefuseChoice(lawPlan, refusal); return; }
        }
        else
        {
            var current = new List<CognitionCandidate>();
            if (world.IsChild(actor)) AddChildNonLandCandidates(world, current, actor);
            else AddTownCivicCandidates(world, current, actor);
            if (!current.Any(c => c.Id == selectedId)) return;
        }
        if (parts[2] == "resettle")
        {
            ResettleTown(world, actor, town, resettlementPlan);
            return;
        }
        if (parts[2] == "accept_admission")
        {
            ApplyTownAdmission(world, town.Id, admissionPlan!.Approval!.ProposalId, actor, admissionPlan.Approval, admissionPlan.CareGroup);
            SettleTownAdmissions(world);
            return;
        }
        try
        {
            (state, government) = AdvanceCivic(world, town, state, government);
            if (parts[2].StartsWith("law_case_", StringComparison.Ordinal) || parts[2].StartsWith("remedy_", StringComparison.Ordinal))
            {
                var legalUpdate = ApplyTownNonLandAction(world, town with { Council = state, Government = government }, actor, parts[2], parts[3], parts[4], nonLandChoice, state, government);
                state = legalUpdate.Council;
                town = town with { NonLandHearings = legalUpdate.NonLandHearings };
                SetTown(world, town with { Council = state, Government = government });
            }
            else if (parts[2].StartsWith("hearing_", StringComparison.Ordinal))
            {
                var hearingUpdate = ApplyTownLandHearingAction(world, town with { Council = state, Government = government },
                    actor, parts[2], parts[3], parts[4],
                    proposalText, landTiles, hearingChoice, state, government);
                state = hearingUpdate.Council;
                town = town with { LandRecords = hearingUpdate.LandRecords };
                SetTown(world, town with { Council = state, Government = government });
            }
            else if (parts[2].StartsWith("land_transfer_", StringComparison.Ordinal))
            {
                var transferUpdate = ApplyTownLandTransferAction(world, town with { Council = state, Government = government },
                    actor, parts[2], parts[3], parts[4], landTiles, hearingChoice, state);
                state = transferUpdate.Council;
                town = town with { LandRecords = transferUpdate.LandRecords };
                SetTown(world, town with { Council = state, Government = government });
            }
            if (ballotPlan is not null && CheckCouncilBallot(world, ballotPlan, town with { Council = state, Government = government }).Refusal is { } ballotRefusal)
            {
                world.RefuseChoice(ballotPlan, ballotRefusal);
                return;
            }
            if (proposalVotePlan is not null && CheckCivicProposalVote(world, proposalVotePlan, town with { Council = state, Government = government }).Refusal is { } proposalRefusal)
            {
                world.RefuseChoice(proposalVotePlan, proposalRefusal);
                return;
            }
            switch (parts[2])
            {
                case "visit":
                    world.MoveToward(actor, world.Agents[actor], visitPlan!.NoticePlace, "town_notices", WorldRuntime.CivicSharedRules.ResourceInteractionRange);
                    break;
                case "read":
                    state = TownCouncilRules.LearnNotices(state, actor, noticePlan!.ReadsCurrentBoard
                        ? state.Notices.Select(notice => notice.Id) : noticePlan.Notices.Select(notice => notice.Id), world.WorldTick);
                    break;
                case "relay":
                    state = TownCouncilRules.LearnNotices(state, noticePlan!.Recipient, noticePlan.Notices.Select(notice => notice.Id), world.WorldTick, actor);
                    break;
                case "nominate": state = TownCouncilRules.Nominate(state, actor, consentPlan!.Nominee!, TownAdults(world, town), world.WorldTick); break;
                case "request_admission":
                    state = TownCouncilRules.SubmitProposal(state, town.Id, actor, "admission", admissionPlan!.Subject,
                        $"Admit {world.Society.GetAgent(admissionPlan.Subject).Name} as a Town resident.", "council:" + state.Revision,
                        TownAdults(world, town), world.WorldTick, CivicDay(world));
                    break;
                case "register": state = TownCouncilRules.Register(state, actor, true, null, TownAdults(world, town), world.WorldTick); break;
                case "remainder": state = TownCouncilRules.Register(state, actor, false, consentPlan!.RemainderDeadline, TownAdults(world, town), world.WorldTick); break;
                case "withdraw_candidate": state = TownCouncilRules.WithdrawCandidate(state, actor, world.WorldTick); break;
                case "propose" or "amend" or "repeal":
                    if (lawPlan!.Text is null && lawPlan.Choice.Kind != TownLawChoiceKind.Repeal) return;
                    if (CheckTownLawPlan(world, lawPlan, town with { Council = state, Government = government }).Refusal is { } lawRefusal)
                    { world.RefuseChoice(lawPlan, lawRefusal); return; }
                    (state, government) = lawPlan.Choice.Kind switch
                    {
                        TownLawChoiceKind.Adopt => TownLawRules.ProposeAdoption(state, government, town.Id, actor, lawPlan.Text!,
                            lawPlan.Scope, lawPlan.SiteTiles, TownAdults(world, town), world.WorldTick, CivicDay(world)),
                        TownLawChoiceKind.Amend => TownLawRules.ProposeAmendment(state, government, town.Id, actor, lawPlan.Choice.Target,
                            lawPlan.Text!, TownAdults(world, town), world.WorldTick, CivicDay(world), lawPlan.Version!.Version),
                        _ => TownLawRules.ProposeRepeal(state, government, town.Id, actor, lawPlan.Choice.Target,
                            TownAdults(world, town), world.WorldTick, CivicDay(world), lawPlan.Version!.Version),
                    };
                    break;
                case "boat_access" or "estate_default":
                    if (CheckTownPolicyPlan(world, policyPlan!, town with { Council = state, Government = government }).Refusal is { } policyRefusal)
                    { world.RefuseChoice(policyPlan!, policyRefusal); return; }
                    (state, government) = policyPlan!.Choice.Kind == TownPolicyKind.BoatAccess
                        ? TownLawRules.ProposeBoatAccess(state, government, town.Id, actor,
                            policyPlan.Visitor, TownAdults(world, town), world.WorldTick, CivicDay(world))
                        : TownLawRules.ProposeEstateDefault(state, government, town.Id, actor,
                            policyPlan.TownShare!.Value, TownAdults(world, town), world.WorldTick, CivicDay(world));
                    break;
                case "boat_project":
                    var boatPort = world.Port(parts[3]);
                    if (boatPort is null || boatPort.TownId != town.Id) return;
                    var boatPlan = WorldRuntime.CivicSharedRules.BoatProjectPlan(boatPort, string.IsNullOrWhiteSpace(proposalText) ? "Communal boat" : proposalText.Trim());
                    if (boatPlan.Name.Length > TownProjectRules.MaximumNameLength || boatPlan.Name.Any(char.IsControl)) return;
                    if (world.TownProjectSiteFailure(town, boatPlan, actor: actor) is not null) return;
                    state = TownCouncilRules.SubmitProposal(state, town.Id, actor, "project", null,
                        TownProjectRules.ProposalText(boatPlan), "council:" + state.Revision,
                        TownAdults(world, town), world.WorldTick, CivicDay(world), project: boatPlan);
                    break;
                case "project":
                    var plan = world.OfferedTownProjectPlan(town, actor, parts[3], parts[4], proposalText);
                    if (plan is null) return;
                    state = TownCouncilRules.SubmitProposal(state, town.Id, actor, "project", null,
                        TownProjectRules.ProposalText(plan), "council:" + state.Revision,
                        TownAdults(world, town), world.WorldTick, CivicDay(world), project: plan);
                    break;
                case "claim_land":
                    if (landClaimPlan!.SelectedTiles is null) return;
                    if (CheckTownLandClaim(world, landClaimPlan, town with { Council = state, Government = government }).Refusal is { } claimRefusal)
                    { world.RefuseChoice(landClaimPlan, claimRefusal); return; }
                    state = SubmitTownLandClaim(world, town, state, actor, landTiles, landClaimPlan);
                    break;
                case "request_land_use": state = SubmitPersonalLandUseRequest(world, town, state, actor, landTiles); break;
                case "request_expansion_land": state = SubmitExpansionLandRequest(world, town, state, actor, parts[3]); break;
                case "accept_land_use":
                case "decline_land_use":
                case "withdraw_land_use":
                    state = DecideHouseholdLandUse(world, state, actor, parts[3], parts[2]); break;
                case "admission":
                    state = TownCouncilRules.SubmitProposal(state, town.Id, actor, "admission", actor,
                        $"Admit {world.Society.GetAgent(actor).Name} as a Town resident.", "council:" + state.Revision,
                        TownAdults(world, town), world.WorldTick, CivicDay(world));
                    break;
                case "yes": case "no": state = TownCouncilRules.VoteProposal(state, proposalVotePlan!.Proposal.Id, actor, proposalVotePlan.Choice.Kind == CivicProposalVoteKind.Yes, world.WorldTick); break;
                case "withdraw_proposal": state = TownCouncilRules.WithdrawProposal(state, proposalVotePlan!.Proposal.Id, actor, world.WorldTick); break;
                case "ballot":
                    if (ballotPlan!.Selected is null) return;
                    state = TownCouncilRules.VoteElection(state, ballotPlan.ElectionId, actor, ballotPlan.Selected, world.WorldTick); break;
                case "government_propose" or "government_replace" or "government_yes" or "government_no" or "government_withdraw":
                    if (CheckGovernmentProposal(world, governmentProposalPlan!, town with { Council = state, Government = government }).Refusal is { } governmentRefusal)
                    { world.RefuseChoice(governmentProposalPlan!, governmentRefusal); return; }
                    if (governmentProposalPlan!.Choice.Kind is GovernmentProposalKind.Propose or GovernmentProposalKind.ReplaceMayor)
                        (state, government) = TownGovernmentRules.Propose(state, government, town.Id, actor,
                            governmentProposalPlan.Target!, governmentProposalPlan.Choice.Kind == GovernmentProposalKind.ReplaceMayor,
                            TownAdults(world, town), world.WorldTick, CivicDay(world));
                    else if (governmentProposalPlan.Choice.Kind == GovernmentProposalKind.Withdraw)
                        (state, government) = TownGovernmentRules.Withdraw(state, government, governmentProposalPlan.Change!.Id, actor, world.WorldTick);
                    else government = TownGovernmentRules.Vote(government, governmentProposalPlan.Change!.Id, actor,
                        governmentProposalPlan.Choice.Kind == GovernmentProposalKind.Yes, world.WorldTick);
                    break;
                case "government_extend_non_land" or "government_accept_non_land" or "mayor_register" or "mayor_withdraw" or "mayor_resign":
                    if (CheckGovernmentOffice(world, officePlan!, town with { Council = state, Government = government }).Refusal is { } officeRefusal)
                    { world.RefuseChoice(officePlan!, officeRefusal); return; }
                    (state, government) = officePlan!.Choice.Kind switch
                    {
                        GovernmentOfficeKind.Register => TownGovernmentRules.RegisterMayor(state, government, actor, officePlan.Choice.Subject, TownAdults(world, town), world.WorldTick),
                        GovernmentOfficeKind.Withdraw => TownGovernmentRules.WithdrawMayor(state, government, actor, officePlan.Choice.Subject, world.WorldTick),
                        GovernmentOfficeKind.Resign => TownGovernmentRules.Resign(state, government, actor, officePlan.Office!.Mandates, world.WorldTick),
                        GovernmentOfficeKind.ExtendNonLand => TownGovernmentRules.Propose(state, government, town.Id, actor, officePlan.Arrangement!, false,
                            TownAdults(world, town), world.WorldTick, CivicDay(world), officePlan.Office!.Mandates, officePlan.Office.ElectionId),
                        _ => TownGovernmentRules.AcceptNonLandDuties(state, government, officePlan.Change!.Id, actor, TownAdults(world, town), world.WorldTick),
                    };
                    break;
                case "mayor_vote":
                    if (CheckMayoralBallot(world, mayoralBallotPlan!, town with { Council = state, Government = government }).Refusal is { } mayorRefusal)
                    { world.RefuseChoice(mayoralBallotPlan!, mayorRefusal); return; }
                    government = TownGovernmentRules.VoteMayor(government, mayoralBallotPlan!.Choice.RoundToken, actor, mayoralBallotPlan.Candidate, world.WorldTick);
                    break;
                case "single": state = TownCouncilRules.VoteElection(state, ballotPlan!.ElectionId, actor, ballotPlan.Selected!, world.WorldTick); break;
            }
            (state, government) = AdvanceCivic(world, town, state, government);
            // Submitting and registering are actual notice interactions, so the actor knows their own posted notice.
            state = TownCouncilRules.LearnNotices(state, actor,
                state.Notices.Skip(town.Council.Notices.Count).Select(n => n.Id), world.WorldTick);
            SaveTownCouncil(world, town, state, government);
            world.MaintainTownProjects();
            world.AppendEvent("town_civic_action", $"{town.Id}|{actor}|{parts[2]}", world.Agents[actor].Position);
            SettleTownAdmissions(world);
        }
        catch (InvalidOperationException)
        {
            world.AppendEvent("town_civic_action_rejected", $"{town.Id}|{actor}|{parts[2]}");
        }
    }
}
