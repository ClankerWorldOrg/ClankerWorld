using System.Globalization;
using ClankerWorld.Simulation.Cognition;

namespace ClankerWorld.Simulation.World;

internal sealed partial class TownSystem
{
    internal LandHearingFilingActionPlan? PlanLandHearingFiling(ITownWorld world, string actor, LandHearingFilingChoice choice, bool bindAuthority = false)
    {
        if (world.Towns.SingleOrDefault(town => town.Id == choice.TownId) is not { Council: not null } town ||
            CivicBoard(world, town) is not { } board) return null;
        var household = world.HouseholdFor(actor);
        var pending = town.LandRecords.Cases.Where(item => item.Status == "pending")
            .SelectMany(item => TownLandHearingRules.CurrentRevision(item).Tiles).ToHashSet();
        var affected = choice.Kind == LandHearingFilingKind.Household
            ? State.HouseholdLandUseRights.Where(right => right.TownId == town.Id && right.HouseholdId == household).SelectMany(right => right.Tiles)
                .Concat(State.HouseholdLandUseRequests.Where(request => request.TownId == town.Id && request.HouseholdId == household && request.Status == "pending")
                    .SelectMany(TownLandRightsRules.UnresolvedRequestTiles))
                .Distinct().OrderBy(tile => world.Map.FootDistance(world.Agents[actor].Position, tile)).ThenBy(tile => tile.Y).ThenBy(tile => tile.X)
                .Where(tile => pending.Contains(tile) || TownLandHearingRules.FilingRefusal(town.LandRecords, town.Id, [tile], State.HouseholdLandUseRights) is null)
                .Take(6).ToArray() : [];
        var office = choice.Kind == LandHearingFilingKind.TownOffice ? town.Government?.Offices.SingleOrDefault(office =>
            office.Mandates == "ordinary" && office.HolderId == actor && office.TermEndTick > world.WorldTick) : null;
        if (bindAuthority) choice = choice with
        {
            AuthorityToken = CivicAgentToken(choice.Kind switch
            {
                LandHearingFilingKind.Household => household ?? "",
                LandHearingFilingKind.TownOffice => office?.ElectionId ?? "",
                _ => DerivedId.Create("council:" + town.Council.Revision.ToString(CultureInfo.InvariantCulture))
            })
        };
        return new(actor, choice, board, household, office, town.Council.Revision, affected, affected.Any(pending.Contains));
    }

    internal static LandHearingFilingActionPlan BindLandHearingFiling(LandHearingFilingActionPlan plan,
        IReadOnlyList<CognitionLandTile>? tiles, CognitionLandHearingChoice? terms, string? proposal)
    {
        if (plan.Bound) return plan;
        var raw = terms?.Statement ?? proposal;
        return plan with
        {
            Bound = true,
            InvalidTerms = !BoundedText.IsValid(raw, TownLandGovernmentFilingRules.MaximumStatementLength),
            Filing = new(TownLandRightsRules.OrderTiles(tiles?.Select(tile => new GridPoint(tile.X, tile.Y)) ?? []),
                new(terms?.RequestedOutcome ?? "", terms?.HouseholdId, terms?.AgreedEndTick), raw?.Trim() ?? "")
        };
    }

    internal ActionCheck CheckLandHearingFiling(ITownWorld world, LandHearingFilingActionPlan plan, TownRuntimeState? currentTown = null, ActorCapabilityPolicy? policy = null)
    {
        var capability = ActorCapabilities.Check(world.CapabilitiesFor(plan.Actor), plan.CandidateId,
            policy ?? GovernanceCapabilities);
        if (!capability.Allowed) return capability;
        if ((currentTown ?? world.Towns.SingleOrDefault(town => town.Id == plan.Choice.TownId)) is not { Council: not null } town ||
            CivicBoard(world, town) != plan.NoticePlace || !NearCivicBoard(world, plan.Actor, town))
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected filing notice place is unavailable");
        switch (plan.Choice.Kind)
        {
            case LandHearingFilingKind.Household:
                if (plan.Household is null || world.HouseholdFor(plan.Actor) != plan.Household || plan.Choice.AuthorityToken != CivicAgentToken(plan.Household))
                    return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "their selected household no longer has an affected filing plot");
                if (!plan.Bound && PlanLandHearingFiling(world, plan.Actor, plan.Choice) is not { NearbyAffectedTiles.Count: > 0 })
                    return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "no affected filing plot remains available");
                break;
            case LandHearingFilingKind.TownOffice:
                if (!TownAdults(world, town).Contains(plan.Actor, StringComparer.Ordinal) || town.Government?.Arrangement.Ordinary != TownArrangementRules.Mayor ||
                    plan.Office is null || plan.Office.HolderId != plan.Actor || plan.Office.TermEndTick is not { } end || end <= world.WorldTick ||
                    !town.Government.Offices.Contains(plan.Office) || plan.Choice.AuthorityToken != CivicAgentToken(plan.Office.ElectionId ?? ""))
                    return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "their selected governing office can no longer file this case");
                break;
            case LandHearingFilingKind.CouncilProposal:
                if (!TownAdults(world, town).Contains(plan.Actor, StringComparer.Ordinal) ||
                    town.Government?.Arrangement.Ordinary is not (TownArrangementRules.Council or TownArrangementRules.ElectedCouncil or TownArrangementRules.AllAdultCouncil) ||
                    town.Council.Revision != plan.CouncilRevision || !town.Council.Members.Contains(plan.Actor, StringComparer.Ordinal) ||
                    plan.Choice.AuthorityToken != CivicAgentToken(DerivedId.Create("council:" + plan.CouncilRevision.ToString(CultureInfo.InvariantCulture))))
                    return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "their selected Council filing authority has changed");
                break;
        }
        if (!plan.Bound) return new();
        if (plan.InvalidTerms || plan.Filing is not { } filing || !TownLandHearingRules.IsValidOutcome(filing.RequestedOutcome, world.WorldTick) ||
            filing.RequestedOutcome.Kind is "reclaim" or "grant" || filing.RequestedOutcome.HouseholdId is { } target && !world.Society.Households.Any(household => household.Id == target) ||
            !TownLandRightsRules.IsValidPlot(world.Map, filing.Tiles, world.WorldTick, world.WorldTick) ||
            filing.Tiles.Any(tile => !TownLandRightsRules.IsCoveredByTownTitle(tile, town.Id, State.TownLandTitles)))
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected filing statement, plot or requested outcome is unavailable");
        if (TownLandHearingRules.FilingRefusal(town.LandRecords, town.Id, filing.Tiles, State.HouseholdLandUseRights) is { } refusal)
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, refusal);
        if (plan.Choice.Kind == LandHearingFilingKind.Household && !LandHearingParties(world, town, filing.Tiles).Any(party => party.HouseholdId == plan.Household))
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "their selected household is no longer affected by that exact plot");
        if (plan.Choice.Kind == LandHearingFilingKind.CouncilProposal)
        {
            if (!TownLandGovernmentFilingRules.IsValid(filing, world.WorldTick))
                return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected Council filing request is malformed");
            if (TownCouncilRules.ProposalRetryRefusal(town.Council, TownLandGovernmentFilingRules.RequestKey(filing),
                "council:" + plan.CouncilRevision.ToString(CultureInfo.InvariantCulture), world.WorldTick, CivicDay(world)) is { } retry)
                return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, retry);
        }
        else if (town.LandRecords.Cases.Any(item => item.Status == "pending" && item.Property is not null && TownLandHearingRules.CurrentRevision(item).Tiles.SequenceEqual(filing.Tiles)))
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected plot already has a different property hearing");
        return new();
    }
    internal const string LandHearingWaiverStatement = "I choose not to respond. This speaks only for me.";

    internal LandHearingEvidenceActionPlan? PlanLandHearingEvidence(ITownWorld world, string actor, LandHearingEvidenceChoice choice)
    {
        if (world.Towns.SingleOrDefault(town => town.Id == choice.TownId) is not { Council: not null } town ||
            town.LandRecords.Cases.SingleOrDefault(item => CivicAgentToken(TownLandHearingRules.RevisionToken(item)) == choice.RevisionToken) is not { } item) return null;
        var revision = TownLandHearingRules.CurrentRevision(item);
        var party = choice.Kind is LandHearingEvidenceKind.Answer or LandHearingEvidenceKind.Waive
            ? LandHearingParties(world, town, revision.Tiles, item).SingleOrDefault(party => CivicAgentToken(party.Id) == choice.PartyOrRecipientToken) : null;
        var reads = choice.Kind == LandHearingEvidenceKind.Relay
            ? item.Reads.Where(read => read.AgentId == actor && read.Revision == revision.Number && read.ReadTick <= world.WorldTick).ToArray() : [];
        return new(actor, choice, item.Id, revision, choice.Kind == LandHearingEvidenceKind.Inspect ? CivicBoard(world, town) : null,
            party, choice.Kind == LandHearingEvidenceKind.Relay ? ResolveCivicAgentToken(world, choice.PartyOrRecipientToken) : null,
            TownLandHearingRules.Ordered(reads.SelectMany(read => read.EvidenceIds)), TownLandHearingRules.Ordered(reads.SelectMany(read => read.ReopenRequestIds)),
            choice.Kind == LandHearingEvidenceKind.Observe ? PlanLandPlotObservation(world, item, actor) : null);
    }

    internal static LandHearingEvidenceActionPlan BindLandHearingEvidence(LandHearingEvidenceActionPlan plan, CognitionLandHearingChoice? terms, string? proposal)
    {
        if (plan.StatementBound || plan.Choice.Kind is not (LandHearingEvidenceKind.Statement or LandHearingEvidenceKind.Answer)) return plan;
        var raw = terms?.Statement ?? proposal;
        return plan with
        {
            StatementBound = true,
            Statement = raw?.Trim(),
            InvalidStatement = !BoundedText.IsValid(raw, TownLandGovernmentFilingRules.MaximumStatementLength)
        };
    }

    internal ActionCheck CheckLandHearingEvidence(ITownWorld world, LandHearingEvidenceActionPlan plan, TownRuntimeState? currentTown = null, ActorCapabilityPolicy? policy = null)
    {
        var capability = ActorCapabilities.Check(world.CapabilitiesFor(plan.Actor), plan.CandidateId,
            policy ?? GovernanceCapabilities);
        if (!capability.Allowed) return capability;
        if ((currentTown ?? world.Towns.SingleOrDefault(town => town.Id == plan.Choice.TownId)) is not { Council: not null } town ||
            town.LandRecords.Cases.SingleOrDefault(item => item.Id == plan.CaseId) is not { } item ||
            CivicAgentToken(TownLandHearingRules.RevisionToken(item)) != plan.Choice.RevisionToken ||
            TownLandHearingRules.RecordVersion(TownLandHearingRules.CurrentRevision(item)) != TownLandHearingRules.RecordVersion(plan.Revision))
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected land case or noticed revision has changed");
        var revision = TownLandHearingRules.CurrentRevision(item);
        var parties = LandHearingParties(world, town, revision.Tiles, item);
        var isParty = parties.Any(party => party.AdultIds.Contains(plan.Actor, StringComparer.Ordinal) || party.RepresentativeId == plan.Actor);
        var resident = TownAdults(world, town).Contains(plan.Actor, StringComparer.Ordinal);
        var open = item.Status == "pending" || item.ReopenRequests.Any(request => request.Status == "pending");
        if (plan.Choice.Kind == LandHearingEvidenceKind.Inspect)
            return CivicBoard(world, town) == plan.NoticePlace && NearCivicBoard(world, plan.Actor, town) && LandHearingMayInspect(world, town, item, plan.Actor) &&
                (!LandHearingReadCurrent(world, item, plan.Actor) || item.Status == "settled" &&
                    (!LandHearingReadRuling(world, item, plan.Actor) || item.ReopenRequests.Any(request => request.Status == "pending" &&
                        !item.Reads.Any(read => read.AgentId == plan.Actor && read.Revision == revision.Number && read.ReopenRequestIds.Contains(request.Id, StringComparer.Ordinal))) ||
                        LandHearingUnreadRecords(world, town, item) is [_, ..])) ? new()
                : ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected public case file is unavailable or already read");
        if (!TownLandHearingRules.HasNoticeReceipt(revision, plan.Actor, world.WorldTick, town.Council.Knowledge))
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "they have not learned that selected case notice");
        if (plan.StatementBound && (plan.InvalidStatement || plan.Statement is null))
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected personal statement is unavailable");
        switch (plan.Choice.Kind)
        {
            case LandHearingEvidenceKind.Statement:
                return open && (isParty || resident) ? new()
                    : ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "they cannot submit that selected case statement now");
            case LandHearingEvidenceKind.Answer:
            case LandHearingEvidenceKind.Waive:
                return item.Status == "pending" && plan.Party is { } selected && CivicAgentToken(selected.Id) == plan.Choice.PartyOrRecipientToken &&
                    parties.Any(party => party.Id == selected.Id &&
                        (party.Kind == "household" ? party.AdultIds.Contains(plan.Actor, StringComparer.Ordinal) : party.RepresentativeId == plan.Actor)) &&
                    revision.Parties.Any(party => party.Id == selected.Id) ? new()
                    : ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected household or Town response party has changed");
            case LandHearingEvidenceKind.Observe:
                if (!(open && resident || isParty) || plan.Observation is not { VisibleTiles.Count: > 0 } observation)
                    return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "they cannot observe that selected case plot now");
                var currentObservation = PlanLandPlotObservation(world, item, plan.Actor);
                return currentObservation.VisibleTiles.SequenceEqual(observation.VisibleTiles) && currentObservation.Description == observation.Description ? new()
                    : ActionCheck.Refuse(ActionRefusalReason.OutOfReach, "the selected nearby plot view or its physical facts have changed");
            case LandHearingEvidenceKind.Relay:
                if (!open || plan.Recipient is not { } recipient || recipient == plan.Actor || !world.Agents.ContainsKey(recipient) ||
                    CivicAgentToken(recipient) != plan.Choice.PartyOrRecipientToken ||
                    !world.IsWithinInteractionRange(world.Agents[recipient].Position, world.Agents[plan.Actor].Position, WorldRuntime.CivicSharedRules.ResourceInteractionRange) ||
                    !LandHearingReadCurrent(world, item, plan.Actor) || !LandHearingMayInspect(world, town, item, recipient) || LandHearingReadCurrent(world, item, recipient))
                    return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected case relay source or recipient is no longer eligible");
                var reads = item.Reads.Where(read => read.AgentId == plan.Actor && read.Revision == revision.Number && read.ReadTick <= world.WorldTick).ToArray();
                return plan.RelayedEvidence.All(id => reads.Any(read => read.EvidenceIds.Contains(id, StringComparer.Ordinal))) &&
                    plan.RelayedRequests.All(id => reads.Any(read => read.ReopenRequestIds.Contains(id, StringComparer.Ordinal))) ? new()
                    : ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected case material was not actually read by the relay source");
        }
        return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected case evidence action is unavailable");
    }

    private void OfferLandHearingEvidence(ITownWorld world, List<CognitionCandidate> candidates, string actor, TownRuntimeState town,
        TownLandCase item, LandHearingEvidenceKind kind, string target, string text, int priority)
    {
        var id = CivicAction(world, town.Id, LandHearingEvidenceChoice.Verb(kind), TownLandHearingRules.RevisionToken(item), target);
        if (LandHearingEvidenceChoice.Decode(id) is { } choice && PlanLandHearingEvidence(world, actor, choice) is { } plan && CheckLandHearingEvidence(world, plan).Allowed)
            candidates.Add(new(id, text, priority) { ActionPlan = plan });
    }
    private static string LandHearingJudgeActionToken(TownLandCase item, LandHearingJudgeKind kind) =>
        kind == LandHearingJudgeKind.Vote && item.Contest is { } contest
            ? DerivedId.Create(TownLandHearingRules.RevisionToken(item) + "#" + TownLandCaseJudgeRules.RoundToken(contest)) : TownLandHearingRules.RevisionToken(item);

    internal static LandHearingJudgeActionPlan? PlanLandHearingJudge(ITownWorld world, string actor, LandHearingJudgeChoice choice)
    {
        if (world.Towns.SingleOrDefault(town => town.Id == choice.TownId) is not { Council: not null } town ||
            town.LandRecords.Cases.SingleOrDefault(item => LandHearingJudgeActionToken(item, choice.Kind) == choice.RevisionOrRoundToken) is not { } item) return null;
        var contest = item.Contest;
        return new(actor, choice, item.Id, TownLandHearingRules.CurrentRevision(item),
            item.JudgeConsents.SingleOrDefault(consent => consent.AgentId == actor && consent.WithdrawnTick is null), item.Judge,
            contest is null ? null : contest with
            {
                Voters = contest.Voters.ToArray(),
                Candidates = contest.Candidates.ToArray(),
                Ballots = contest.Ballots.ToArray(),
                TiedCandidates = contest.TiedCandidates.ToArray(),
                Rounds = contest.Rounds.ToArray()
            }, choice.Kind == LandHearingJudgeKind.Vote ? ResolveCivicAgentToken(world, choice.CandidateToken) : null);
    }

    internal ActionCheck CheckLandHearingJudge(ITownWorld world, LandHearingJudgeActionPlan plan, TownRuntimeState? currentTown = null, ActorCapabilityPolicy? policy = null)
    {
        var capability = ActorCapabilities.Check(world.CapabilitiesFor(plan.Actor), plan.CandidateId,
            policy ?? GovernanceCapabilities);
        if (!capability.Allowed) return capability;
        if ((currentTown ?? world.Towns.SingleOrDefault(town => town.Id == plan.Choice.TownId)) is not { Council: not null } town ||
            town.LandRecords.Cases.SingleOrDefault(item => item.Id == plan.CaseId) is not { } item ||
            LandHearingJudgeActionToken(item, plan.Choice.Kind) != plan.Choice.RevisionOrRoundToken ||
            TownLandHearingRules.RecordVersion(TownLandHearingRules.CurrentRevision(item)) != TownLandHearingRules.RecordVersion(plan.Revision) ||
            !TownLandHearingRules.HasNoticeReceipt(TownLandHearingRules.CurrentRevision(item), plan.Actor, world.WorldTick, town.Council.Knowledge))
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected case, noticed revision or knowledge has changed");
        if (plan.Choice.Kind == LandHearingJudgeKind.Vote)
        {
            if (plan.Contest is not { } original || item.Contest is not { Stage: "voting", RoundDeadlineTick: { } deadline } current || deadline <= world.WorldTick ||
                TownLandHearingRules.RecordVersion(current with
                { Voters = original.Voters, Candidates = original.Candidates, Ballots = original.Ballots, TiedCandidates = original.TiedCandidates }) != TownLandHearingRules.RecordVersion(original) ||
                !original.Voters.Contains(plan.Actor, StringComparer.Ordinal) || !current.Voters.Contains(plan.Actor, StringComparer.Ordinal) ||
                plan.Candidate is not { } candidate || CivicAgentToken(candidate) != plan.Choice.CandidateToken ||
                !original.Candidates.Contains(candidate, StringComparer.Ordinal) || !current.Candidates.Contains(candidate, StringComparer.Ordinal) ||
                !town.Council.Knowledge.Any(receipt => receipt.AgentId == plan.Actor && town.Council.Notices.Any(notice => notice.Id == receipt.NoticeId &&
                    notice.Kind == "land_hearing" && notice.SubjectId == TownLandCaseJudgeRules.RoundToken(current))))
                return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected case ballot round or candidate is no longer eligible");
            return new();
        }
        if (!TownAdults(world, town).Contains(plan.Actor, StringComparer.Ordinal) ||
            !(item.Status == "pending" || item.ReopenRequests.Any(request => request.Status == "pending")))
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected case no longer needs their personal mandate response");
        var consent = item.JudgeConsents.SingleOrDefault(consent => consent.AgentId == plan.Actor && consent.WithdrawnTick is null);
        return plan.Choice.Kind switch
        {
            LandHearingJudgeKind.Register when plan.Consent is null && consent is null && TownLandCaseJudgeRules.Eligible(item, plan.Actor,
                TownAdults(world, town), LandHearingHouseholds(world), LandHearingParties(world, town, plan.Revision.Tiles, item), LandHearingDirectStakes(world, item)) => new(),
            LandHearingJudgeKind.Withdraw when plan.Consent is not null && consent == plan.Consent => new(),
            LandHearingJudgeKind.Resign when plan.Judge is { Kind: "case_elected" } judge && judge.AgentId == plan.Actor && item.Judge == judge => new(),
            _ => ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected consent or assigned case mandate has changed")
        };
    }

    private void OfferLandHearingJudge(ITownWorld world, List<CognitionCandidate> candidates, string actor, TownRuntimeState town,
        TownLandCase item, LandHearingJudgeKind kind, string target, string text, int priority)
    {
        var id = CivicAction(world, town.Id, LandHearingJudgeChoice.Verb(kind), LandHearingJudgeActionToken(item, kind), target);
        if (LandHearingJudgeChoice.Decode(id) is { } choice && PlanLandHearingJudge(world, actor, choice) is { } plan && CheckLandHearingJudge(world, plan).Allowed)
            candidates.Add(new(id, text, priority) { ActionPlan = plan });
    }
    internal static LandHearingPropertyActionPlan? PlanLandHearingProperty(ITownWorld world, string actor, LandHearingPropertyChoice choice,
        TownPropertyRequest? selectedRequest = null, TownPropertySnapshot? selectedSnapshot = null, IReadOnlyList<GridPoint>? selectedTiles = null)
    {
        if (world.Towns.SingleOrDefault(town => town.Id == choice.TownId) is not { Council: not null, Government: not null } town) return null;
        if (choice.Kind != LandHearingPropertyKind.Request)
        {
            if (town.LandRecords.Cases.SingleOrDefault(item => CivicAgentToken(TownLandHearingRules.RevisionToken(item)) == choice.SubjectToken) is not { Property: { } property } item) return null;
            var revision = TownLandHearingRules.CurrentRevision(item);
            return new(actor, choice, property.Request, property.Snapshots.Single(snapshot => snapshot.Revision == revision.Number), revision.Tiles,
                null, false, null, town.Council.Revision, item.Id, revision,
                property.Consents.LastOrDefault(consent => consent.Revision == revision.Number && consent.AgentId == actor));
        }
        var building = world.WorldSimulation.Buildings.SingleOrDefault(building => CivicAgentToken(building.InstanceId) == choice.SubjectToken);
        if (building is null) return null;
        var target = choice.TargetToken == "town" ? null : world.Society.Households.SingleOrDefault(household => CivicAgentToken(household.Id) == choice.TargetToken)?.Id;
        var request = selectedRequest ?? new(building.InstanceId, building.HouseholdId, target);
        var snapshot = selectedSnapshot ?? world.TryCaptureProperty(town, request, 1, out _);
        if (snapshot is null) return null;
        var tiles = selectedTiles ?? TownLandRightsRules.OrderTiles(WorldContentSimulationRules.Footprint(
            world.WorldContent.Buildings.Single(definition => definition.CanonicalId == building.DefinitionId), building));
        var mayor = town.Government.Arrangement.Ordinary == TownArrangementRules.Mayor;
        var office = mayor ? town.Government.Offices.SingleOrDefault(office => office.Mandates == "ordinary" && office.HolderId == actor && office.TermEndTick > world.WorldTick) : null;
        return new(actor, choice, request, snapshot, tiles, CivicBoard(world, town), mayor, office, town.Council.Revision);
    }

    internal static LandHearingPropertyActionPlan BindLandHearingProperty(LandHearingPropertyActionPlan plan, CognitionLandHearingChoice? terms, string? proposal)
    {
        if (plan.Bound || plan.Choice.Kind != LandHearingPropertyKind.Request) return plan;
        var raw = terms?.Statement ?? proposal;
        return plan with { Bound = true, Statement = raw?.Trim(), InvalidStatement = !BoundedText.IsValid(raw, TownLandGovernmentFilingRules.MaximumStatementLength) };
    }

    internal ActionCheck CheckLandHearingProperty(ITownWorld world, LandHearingPropertyActionPlan plan, TownRuntimeState? currentTown = null, ActorCapabilityPolicy? policy = null)
    {
        var capability = ActorCapabilities.Check(world.CapabilitiesFor(plan.Actor), plan.CandidateId,
            policy ?? GovernanceCapabilities);
        if (!capability.Allowed) return capability;
        if ((currentTown ?? world.Towns.SingleOrDefault(town => town.Id == plan.Choice.TownId)) is not { Council: not null, Government: not null } town)
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected property hearing Town is unavailable");
        if (plan.Choice.Kind != LandHearingPropertyKind.Request)
        {
            if (town.LandRecords.Cases.SingleOrDefault(item => item.Id == plan.CaseId) is not { Status: "pending", Property: { Transfer: null } property } item ||
                plan.Revision is null || CivicAgentToken(TownLandHearingRules.RevisionToken(item)) != plan.Choice.SubjectToken ||
                TownLandHearingRules.RecordVersion(TownLandHearingRules.CurrentRevision(item)) != TownLandHearingRules.RecordVersion(plan.Revision) ||
                property.Request != plan.Request || TownLandHearingRules.RecordVersion(property.Snapshots.Single(snapshot => snapshot.Revision == plan.Revision.Number)) !=
                    TownLandHearingRules.RecordVersion(plan.Snapshot) || !LandHearingReadCurrent(world, item, plan.Actor) ||
                property.Consents.LastOrDefault(consent => consent.Revision == plan.Revision.Number && consent.AgentId == plan.Actor) != plan.PreviousConsent ||
                plan.PreviousConsent?.Agreed == (plan.Choice.Kind == LandHearingPropertyKind.Accept))
                return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected noticed property or personal consent has changed");
            var parties = LandHearingParties(world, town, plan.Tiles, item);
            return TownPropertyRules.ConsentRefusal(item, plan.Actor, world.WorldTick, town.Council.Knowledge, parties) is { } refusal
                ? ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, refusal) : new();
        }
        if (CivicBoard(world, town) != plan.NoticePlace || !NearCivicBoard(world, plan.Actor, town) || !TownAdults(world, town).Contains(plan.Actor, StringComparer.Ordinal) ||
            CivicAgentToken(plan.Request.BuildingId) != plan.Choice.SubjectToken ||
            (plan.Request.TargetHouseholdId is { } target ? CivicAgentToken(target) : "town") != plan.Choice.TargetToken ||
            world.TryCaptureProperty(town, plan.Request, 1, out _) is not { } current ||
            TownPropertyRules.MaterialVersion(current) != TownPropertyRules.MaterialVersion(plan.Snapshot))
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected property, ownership, goods, recipient or notice place has changed");
        if (plan.Mayor)
        {
            if (town.Government.Arrangement.Ordinary != TownArrangementRules.Mayor || plan.Office is null || plan.Office.HolderId != plan.Actor ||
                plan.Office.TermEndTick is not { } end || end <= world.WorldTick || !town.Government.Offices.Contains(plan.Office))
                return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "their selected governing office can no longer file this property request");
        }
        else if (town.Government.Arrangement.Ordinary is not (TownArrangementRules.Council or TownArrangementRules.ElectedCouncil or TownArrangementRules.AllAdultCouncil) ||
            !town.Council.Members.Contains(plan.Actor, StringComparer.Ordinal) || town.Council.Revision != plan.CouncilRevision)
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "their selected Council property-filing authority has changed");
        if (TownPropertyRules.FilingRefusal(town.LandRecords, plan.Tiles, new(plan.Request, [plan.Snapshot], [])) is { } filingFailure)
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, filingFailure);
        if (!plan.Bound) return new();
        if (plan.InvalidStatement || plan.Statement is null)
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected property filing statement is unavailable");
        var filing = new TownLandFilingRequest(plan.Tiles, TownPropertyRules.Outcome(plan.Request), plan.Statement) { Property = plan.Request };
        return !plan.Mayor && TownCouncilRules.ProposalRetryRefusal(town.Council, TownLandGovernmentFilingRules.RequestKey(filing),
            "council:" + plan.CouncilRevision.ToString(CultureInfo.InvariantCulture), world.WorldTick, CivicDay(world)) is { } retry
                ? ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, retry) : new();
    }
    internal static LandHearingReopeningActionPlan? PlanLandHearingReopening(ITownWorld world, string actor, LandHearingReopeningChoice choice)
    {
        if (world.Towns.SingleOrDefault(town => town.Id == choice.TownId) is not { Council: not null } town ||
            town.LandRecords.Cases.SingleOrDefault(item => CivicAgentToken(TownLandHearingRules.RevisionToken(item)) == choice.RevisionToken) is not { Rulings.Count: > 0 } item) return null;
        var request = choice.Assessment ? item.ReopenRequests.SingleOrDefault(request => DerivedId.Create(request.Id + ":accept", "agent-sha256:") == choice.GroundsOrRequestToken ||
            DerivedId.Create(request.Id + ":reject", "agent-sha256:") == choice.GroundsOrRequestToken) : null;
        var accept = request is not null && DerivedId.Create(request.Id + ":accept", "agent-sha256:") == choice.GroundsOrRequestToken;
        return new(actor, choice, item.Id, TownLandHearingRules.CurrentRevision(item), item.Rulings[^1], request, accept, item.Judge,
            accept && item.Property is { Transfer: null } property && world.PropertyMayReopen(town, item)
                ? world.CaptureProperty(town, property.Request, TownLandHearingRules.CurrentRevision(item).Number + 1) : null);
    }

    internal static LandHearingReopeningActionPlan BindLandHearingReopening(LandHearingReopeningActionPlan plan, CognitionLandHearingChoice? terms)
    {
        if (plan.Bound) return plan;
        var raw = plan.Choice.Assessment ? terms?.Statement : terms?.Grounds ?? terms?.Statement;
        return plan with
        {
            Bound = true,
            EvidenceIds = terms?.EvidenceIds?.ToArray() ?? [],
            Reasons = raw?.Trim(),
            InvalidTerms = !BoundedText.IsValid(raw, TownLandGovernmentFilingRules.MaximumStatementLength)
        };
    }

    internal ActionCheck CheckLandHearingReopening(ITownWorld world, LandHearingReopeningActionPlan plan, TownRuntimeState? currentTown = null, ActorCapabilityPolicy? policy = null)
    {
        var capability = ActorCapabilities.Check(world.CapabilitiesFor(plan.Actor), plan.CandidateId,
            policy ?? GovernanceCapabilities);
        if (!capability.Allowed) return capability;
        if ((currentTown ?? world.Towns.SingleOrDefault(town => town.Id == plan.Choice.TownId)) is not { Council: not null } town ||
            town.LandRecords.Cases.SingleOrDefault(item => item.Id == plan.CaseId) is not { Status: "settled", Rulings.Count: > 0 } item ||
            CivicAgentToken(TownLandHearingRules.RevisionToken(item)) != plan.Choice.RevisionToken ||
            TownLandHearingRules.RecordVersion(TownLandHearingRules.CurrentRevision(item)) != TownLandHearingRules.RecordVersion(plan.Revision) ||
            TownLandHearingRules.RecordVersion(item.Rulings[^1]) != TownLandHearingRules.RecordVersion(plan.Ruling) ||
            !TownLandHearingRules.HasNoticeReceipt(plan.Revision, plan.Actor, world.WorldTick, town.Council.Knowledge) ||
            !LandHearingReadCurrent(world, item, plan.Actor))
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected settled ruling, noticed file or personal knowledge has changed");
        if (!plan.Choice.Assessment)
        {
            if (!LandHearingReadRuling(world, item, plan.Actor) || !LandHearingParties(world, town, plan.Revision.Tiles, item)
                    .Any(party => party.AdultIds.Contains(plan.Actor, StringComparer.Ordinal) || party.RepresentativeId == plan.Actor) ||
                plan.Choice.GroundsOrRequestToken is not ("material_evidence" or "procedural_error"))
                return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "they cannot submit those selected reopening grounds now");
            if (!plan.Bound) return new();
            return plan.InvalidTerms || plan.Reasons is null || TownLandHearingRules.RequestReopenRefusal(item,
                plan.Choice.GroundsOrRequestToken, plan.EvidenceIds ?? [], plan.Reasons) is not null ?
                ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected reopening grounds or cited evidence are unavailable") : new();
        }
        if (plan.Judge is not { } judge || judge.AgentId != plan.Actor || item.Judge != judge || !LandHearingJudgeValid(world, town, item, judge) ||
            plan.Request is not { } original || item.ReopenRequests.SingleOrDefault(request => request.Id == original.Id) is not { Status: "pending" } current ||
            TownLandHearingRules.RecordVersion(current) != TownLandHearingRules.RecordVersion(original) ||
            DerivedId.Create(current.Id + (plan.Accept ? ":accept" : ":reject"), "agent-sha256:") != plan.Choice.GroundsOrRequestToken ||
            !item.Reads.Any(read => read.AgentId == plan.Actor && read.Revision == plan.Revision.Number && read.ReadTick >= current.Tick && read.ReadTick <= world.WorldTick &&
                read.ReopenRequestIds.Contains(current.Id, StringComparer.Ordinal)))
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected pending request or informed assessor has changed");
        if (plan.Accept && (!world.PropertyMayReopen(town, item) || item.Property is { Transfer: null } &&
                (plan.PropertySnapshot is not { } snapshot || TownPropertyRules.MaterialVersion(snapshot) !=
                    TownPropertyRules.MaterialVersion(world.CaptureProperty(town, item.Property.Request, plan.Revision.Number + 1)))))
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected property can no longer receive a fresh hearing notice");
        if (TownLandHearingRules.ReopeningRefusal(town.LandRecords, item, current, judge, plan.Accept,
            plan.Bound ? plan.Reasons ?? "" : "Assess the selected request", world.WorldTick, validAuthority: true) is { } refusal)
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, refusal);
        return !plan.Bound || !plan.InvalidTerms ? new()
            : ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected assessment reasons are unavailable");
    }

    private void OfferLandHearingReopening(ITownWorld world, List<CognitionCandidate> candidates, string actor, TownRuntimeState town,
        TownLandCase item, bool assessment, string terms, string text, int priority)
    {
        var id = CivicAction(world, town.Id, assessment ? "hearing_assess_reopen" : "hearing_reopen", TownLandHearingRules.RevisionToken(item), terms);
        if (LandHearingReopeningChoice.Decode(id) is { } choice && PlanLandHearingReopening(world, actor, choice) is { } plan && CheckLandHearingReopening(world, plan).Allowed)
            candidates.Add(new(id, text, priority) { ActionPlan = plan });
    }
    internal LandHearingRulingActionPlan? PlanLandHearingRuling(ITownWorld world, string actor, LandHearingRulingChoice choice)
    {
        if (world.Towns.SingleOrDefault(town => town.Id == choice.TownId) is not { Council: not null } town ||
            town.LandRecords.Cases.SingleOrDefault(item => CivicAgentToken(TownLandHearingRules.RevisionToken(item)) == choice.RevisionToken) is not { } item) return null;
        var revision = TownLandHearingRules.CurrentRevision(item);
        var parties = LandHearingParties(world, town, revision.Tiles, item);
        var lapsed = TownLandHearingRules.RulingChoices(State.HouseholdLandUseRights, revision.Tiles, parties, world.WorldTick).Lapsed;
        return new(actor, choice, item.Id, revision, item.Judge, lapsed,
            State.HouseholdLandUseRights.Where(right => right.Tiles.Any(revision.Tiles.Contains)).ToArray(),
            State.HouseholdLandUseRequests.Where(request => request.TownId == town.Id && request.Status == "pending" &&
                TownLandRightsRules.UnresolvedRequestTiles(request).Any(revision.Tiles.Contains)).ToArray(),
            item.Property?.Request, item.Property is not null && choice.OutcomeKind != "reject" && world.PropertyCanTransfer(town, item)
                ? world.CaptureProperty(town, item.Property.Request, revision.Number) : null);
    }

    internal static LandHearingRulingActionPlan BindLandHearingRuling(LandHearingRulingActionPlan plan, CognitionLandHearingChoice? terms, string? proposal)
    {
        if (plan.Bound) return plan;
        var raw = terms?.Statement ?? proposal;
        var property = plan.PropertyRequest is not null && plan.Choice.OutcomeKind != "reject";
        var outcome = property ? TownPropertyRules.Outcome(plan.PropertyRequest!)
            : new TownLandRequestedOutcome(plan.Choice.OutcomeKind, terms?.HouseholdId, terms?.AgreedEndTick);
        return plan with
        {
            Bound = true,
            Outcome = outcome,
            EvidenceIds = terms?.EvidenceIds?.ToArray() ?? [],
            LawIds = terms?.LawIds?.ToArray() ?? [],
            Reasons = raw?.Trim(),
            InvalidTerms = !BoundedText.IsValid(raw, TownLandGovernmentFilingRules.MaximumStatementLength) ||
                terms?.RequestedOutcome is { } supplied && supplied != plan.Choice.OutcomeKind || property &&
                (outcome.Kind != plan.Choice.OutcomeKind || terms?.HouseholdId is { } household && household != outcome.HouseholdId || terms?.AgreedEndTick is not null)
        };
    }

    internal ActionCheck CheckLandHearingRuling(ITownWorld world, LandHearingRulingActionPlan plan, TownRuntimeState? currentTown = null, ActorCapabilityPolicy? policy = null)
    {
        var capability = ActorCapabilities.Check(world.CapabilitiesFor(plan.Actor), plan.CandidateId,
            policy ?? GovernanceCapabilities);
        if (!capability.Allowed) return capability;
        if ((currentTown ?? world.Towns.SingleOrDefault(town => town.Id == plan.Choice.TownId)) is not { Council: not null } town ||
            town.LandRecords.Cases.SingleOrDefault(item => item.Id == plan.CaseId) is not { Status: "pending" } item ||
            CivicAgentToken(TownLandHearingRules.RevisionToken(item)) != plan.Choice.RevisionToken ||
            TownLandHearingRules.RecordVersion(TownLandHearingRules.CurrentRevision(item)) != TownLandHearingRules.RecordVersion(plan.Revision) ||
            plan.Judge is not { } judge || judge.AgentId != plan.Actor || item.Judge != judge || !LandHearingJudgeValid(world, town, item, judge) ||
            !TownLandHearingRules.HasNoticeReceipt(plan.Revision, plan.Actor, world.WorldTick, town.Council.Knowledge) || !LandHearingReadCurrent(world, item, plan.Actor))
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected noticed case, informed judge or mandate has changed");
        var parties = LandHearingParties(world, town, plan.Revision.Tiles, item);
        var choices = item.Property is null ? TownLandHearingRules.RulingChoices(State.HouseholdLandUseRights, plan.Revision.Tiles, parties, world.WorldTick).Kinds
            : world.PropertyCanTransfer(town, item) ? new[] { TownPropertyRules.Outcome(item.Property.Request).Kind, "reject" } : new[] { "reject" };
        if (!TownLandHearingRules.CanCloseResponses(item, parties, world.WorldTick) || !choices.Contains(plan.Choice.OutcomeKind, StringComparer.Ordinal) ||
            TownLandHearingRules.RecordVersion(State.HouseholdLandUseRights.Where(right => right.Tiles.Any(plan.Revision.Tiles.Contains)).ToArray()) !=
                TownLandHearingRules.RecordVersion(plan.AffectedRights) ||
            TownLandHearingRules.RecordVersion(State.HouseholdLandUseRequests.Where(request => request.TownId == town.Id && request.Status == "pending" &&
                TownLandRightsRules.UnresolvedRequestTiles(request).Any(plan.Revision.Tiles.Contains)).ToArray()) != TownLandHearingRules.RecordVersion(plan.PendingRequests) ||
            item.Property?.Request != plan.PropertyRequest)
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected ruling opportunity, permissions or affected applications have changed");
        if (!plan.Bound) return new();
        if (plan.InvalidTerms || plan.Outcome is not { } outcome || plan.Reasons is null ||
            !TownLandHearingRules.IsValidOutcome(outcome, world.WorldTick) || outcome.HouseholdId is { } target &&
                (!world.Society.Households.Any(household => household.Id == target) || !parties.Any(party => party.HouseholdId == target)))
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected ruling terms or affected household are unavailable");
        if (plan.PropertyRequest is not null && plan.Choice.OutcomeKind != "reject" &&
            (plan.PropertySnapshot is not { } snapshot || item.Property is null || !world.PropertyCanTransfer(town, item) || TownPropertyRules.MaterialVersion(snapshot) !=
                TownPropertyRules.MaterialVersion(item.Property.Snapshots.Single(current => current.Revision == plan.Revision.Number))))
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected noticed property or personal consent has changed");
        var evidence = plan.EvidenceIds ?? [];
        var laws = plan.LawIds ?? [];
        var applicable = LandHearingApplicableLaws(world, town, item).Select(pair => LandHearingLawToken(pair.Law, pair.Version)).ToHashSet(StringComparer.Ordinal);
        if (laws.Any(id => !applicable.Contains(id) || !item.Evidence.Any(record => record.SourceRecordId == id && record.Kind == "record")))
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected cited law version was not actually inspected or is no longer applicable");
        return TownLandHearingRules.RulingRefusal(item, world.Map, judge, world.WorldTick, outcome, evidence, laws, plan.Reasons,
            State.HouseholdLandUseRights, parties, validAuthority: true, State.HouseholdLandUseRequests) is { } refusal
                ? ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, refusal) : new();
    }
}
