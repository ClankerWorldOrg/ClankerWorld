using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

internal sealed partial class TownSystem
{

    private bool settlingTownAdmissions;

    internal static TownProposal? PendingAdmission(TownRuntimeState town, string subject) =>
            town.Council?.Proposals.FirstOrDefault(proposal => proposal.Kind == "admission" &&
                proposal.SubjectId == subject && proposal.Status == "pending");

    internal static TownProposal? AdmissionProposal(TownRuntimeState town, TownAdmissionRecord record) =>
            town.Council?.Proposals.SingleOrDefault(proposal => proposal.Id == record.ProposalId);

    internal static TownAdmissionRecord? ApprovedAdmission(ITownWorld world, TownRuntimeState town, string subject) =>
            town.Admissions?.LastOrDefault(record => record.SubjectId == subject && record.Status == WorldRuntime.CivicSharedRules.AdmissionApproved &&
                TownAdmissionDeadlineRules.IsOpen(AdmissionProposal(town, record), world.WorldTick, CivicDay(world)));

    internal bool HasPendingAdmission(ITownWorld world, string subject) => towns.Any(town => PendingAdmission(town, subject) is not null);

    internal bool HasOpenAdmission(ITownWorld world, string subject, string? townId) =>
            towns.SingleOrDefault(town => town.Id == townId) is { } town &&
            (PendingAdmission(town, subject) is not null || ApprovedAdmission(world, town, subject) is not null);

    internal bool MayRequestOwnAdmission(ITownWorld world, string actor, TownRuntimeState town) =>
            !town.IsAbandoned && world.IsAdult(actor) && TownForResident(world, actor) != town.Id && !HasPendingAdmission(world, actor) &&
            ApprovedAdmission(world, town, actor) is null && !AdmissionRetryWaits(world, town, actor);

    internal bool MayBeSponsoredForAdmission(ITownWorld world, string nominee, TownRuntimeState town) =>
            world.IsAdult(nominee) && TownForResident(world, nominee) is null && !HasPendingAdmission(world, nominee) &&
            ApprovedAdmission(world, town, nominee) is null && !AdmissionRetryWaits(world, town, nominee);

    internal static bool AdmissionRetryWaits(ITownWorld world, TownRuntimeState town, string subject) =>
            town.Council is { } state &&
            state.Proposals.LastOrDefault(proposal => proposal.RequestKey == TownRecordIds.Admission(subject)) is
            { Status: "rejected" or "withdrawn", SettledTick: { } settled } previous &&
            world.WorldTick < settled + CivicDay(world) && previous.CouncilRevision == state.Revision &&
            previous.Circumstances == "council:" + state.Revision;

    internal bool MayVisitAsNewcomer(ITownWorld world, string actor, TownRuntimeState town) =>
            world.IsAdult(actor) && TownForResident(world, actor) is var current && current != town.Id &&
            (current is null || town.BorderTiles.Contains(world.Agents[actor].Position)) && CanWalkToCivicBoard(world, actor, town);

    internal static bool CanWalkToCivicBoard(ITownWorld world, string actor, TownRuntimeState town)
    {
        if (CivicBoard(world, town) is not { } board) return false;
        for (var dy = -WorldRuntime.CivicSharedRules.ResourceInteractionRange; dy <= WorldRuntime.CivicSharedRules.ResourceInteractionRange; dy++)
            for (var dx = -WorldRuntime.CivicSharedRules.ResourceInteractionRange; dx <= WorldRuntime.CivicSharedRules.ResourceInteractionRange; dx++)
            {
                var tile = world.Map.WrapColumn(new GridPoint(board.X + dx, board.Y + dy));
                if (world.Map.Contains(tile) && world.IsWithinInteractionRange(tile, board, WorldRuntime.CivicSharedRules.ResourceInteractionRange) &&
                    world.CanReachTerrain(actor, tile, ReachPurpose.CivicVisit, 0, SwimmingPolicy.WhenCapable))
                    return true;
            }
        return false;
    }

    internal bool MayAcceptAdmission(ITownWorld world, string actor, TownRuntimeState town, TownAdmissionRecord record) =>
            record.Status == WorldRuntime.CivicSharedRules.AdmissionApproved && record.SubjectId == actor && world.IsAdult(actor) &&
            TownAdmissionDeadlineRules.IsOpen(AdmissionProposal(town, record), world.WorldTick, CivicDay(world)) &&
            TownForResident(world, actor) is var current && current == record.PreviousTownId && current != town.Id &&
            // Their own undecided request elsewhere is answered first, so one choice cannot move them twice.
            !towns.Any(other => other.Id != town.Id && PendingAdmission(other, actor) is { } pending && pending.AuthorId == actor) &&
            CivicHistory(town).Knows(actor, "result", record.ProposalId);

    internal string AdmissionRequestText(ITownWorld world, string actor, TownRuntimeState town)
    {
        var dependents = TownCareGroup(world, actor, TownForResident(world, actor)).Length - 1;
        var children = dependents > 0 ? " Your dependent children would join with you." : string.Empty;
        return TownForResident(world, actor) is { } current
            ? $"Ask {town.Name}'s council to approve your move from {TownName(world, current)}. If approved, you leave {TownName(world, current)} and become a resident at once; until then the request grants no membership or Warehouse access. It never gives a House or household place.{children}"
            : $"Ask {town.Name}'s council to approve your admission. If approved, you become a resident at once; until then the request grants no membership or Warehouse access. It never gives a House or household place.{children}";
    }

    internal void AddTownAdmissionCandidates(ITownWorld world, List<CognitionCandidate> candidates, string actor, TownRuntimeState town)
    {
        if (ApprovedAdmission(world, town, actor) is not { } record ||
            TownAdmissionDeadlineRules.AcceptanceDeadline(AdmissionProposal(town, record), CivicDay(world)) is not { } deadline) return;
        var previous = record.PreviousTownId is { } from ? $" You would leave {TownName(world, from)}." : string.Empty;
        OfferTownAdmission(world, candidates, actor, town, TownAdmissionChoiceKind.Accept, record.ProposalId,
            $"Accept {town.Name}'s approved admission and become a resident.{previous} You must accept before {TownAdmissionDeadlineRules.DescribeWindow(deadline, world.WorldTick, CivicDay(world), world.CalendarConfig.CalendarOffsetTicks)}. This gives no House or household place.", 169);
    }

    internal void AcceptTownAdmission(ITownWorld world, string actor, string townId, string proposalId)
    {
        var town = towns.Single(item => item.Id == townId);
        if (ApprovedAdmission(world, town, actor) is not { } record || record.ProposalId != proposalId ||
            !MayAcceptAdmission(world, actor, town, record)) return;
        ApplyTownAdmission(world, townId, proposalId, actor, record);
        SettleTownAdmissions(world);
    }

    internal void SettleTownAdmissions(ITownWorld world)
    {
        // Applying an admission changes rosters; the running loop picks up anything that changes.
        if (settlingTownAdmissions) return;
        settlingTownAdmissions = true;
        try
        {
            while (SettleNextTownAdmission(world)) { }
        }
        finally
        {
            settlingTownAdmissions = false;
        }
    }

    internal bool SettleNextTownAdmission(ITownWorld world)
    {
        foreach (var town in towns.OrderBy(item => item.Id, StringComparer.Ordinal))
        {
            foreach (var record in town.Admissions ?? [])
            {
                if (record.Status != WorldRuntime.CivicSharedRules.AdmissionApproved) continue;
                var current = TownForResident(world, record.SubjectId);
                var reason = !world.IsAdult(record.SubjectId) ? "unavailable"
                    : current == town.Id ? "already_resident"
                    : current != record.PreviousTownId ? "affiliation_changed"
                    : TownAdmissionDeadlineRules.IsExpired(AdmissionProposal(town, record), world.WorldTick, CivicDay(world)) ? "acceptance_expired" : null;
                if (reason is null) continue;
                LapseTownAdmission(world, town.Id, record, reason);
                return true;
            }
            if (town.Council is not { } council) continue;
            foreach (var proposal in council.Proposals)
            {
                if (proposal is not { Kind: "admission", Status: "passed", SubjectId: { } subject } ||
                    (town.Admissions ?? []).Any(record => record.ProposalId == proposal.Id))
                    continue;
                // Asking for oneself is consent; an approval someone else asked for waits for the newcomer.
                if (proposal.AuthorId == subject)
                    ApplyTownAdmission(world, town.Id, proposal.Id, subject, approved: null);
                else
                {
                    var record = new TownAdmissionRecord(proposal.Id, subject, WorldRuntime.CivicSharedRules.AdmissionApproved, world.WorldTick, TownForResident(world, subject));
                    // A newcomer who died or joined this Town while the vote was open has nothing to accept.
                    var lapse = !world.IsAdult(subject) ? "unavailable" : record.PreviousTownId == town.Id ? "already_resident"
                        : TownAdmissionDeadlineRules.IsExpired(proposal, world.WorldTick, CivicDay(world)) ? "acceptance_expired" : null;
                    if (lapse is not null) LapseTownAdmission(world, town.Id, record, lapse);
                    else
                    {
                        SetTownAdmission(world, town.Id, record);
                        world.AppendEvent("town_admission_approved", $"{town.Id}|{subject}|{proposal.Id}");
                    }
                }
                return true;
            }
        }
        return false;
    }

    internal void ApplyTownAdmission(ITownWorld world, string townId, string proposalId, string subject, TownAdmissionRecord? approved,
        IReadOnlyList<string>? selectedCareGroup = null)
    {
        var previous = TownForResident(world, subject);
        var reason = !world.IsAdult(subject) ? "unavailable"
            : previous == townId ? "already_resident"
            : approved is not null && previous != approved.PreviousTownId ? "affiliation_changed"
            : approved is not null && TownAdmissionDeadlineRules.IsExpired(
                AdmissionProposal(towns.Single(town => town.Id == townId), approved), world.WorldTick, CivicDay(world)) ? "acceptance_expired" : null;
        if (reason is not null)
        {
            LapseTownAdmission(world, townId, approved ?? new TownAdmissionRecord(proposalId, subject, WorldRuntime.CivicSharedRules.AdmissionApproved, world.WorldTick), reason);
            return;
        }
        var group = selectedCareGroup ?? TownCareGroup(world, subject, previous);
        SetTownAdmission(world, townId, new TownAdmissionRecord(proposalId, subject, WorldRuntime.CivicSharedRules.AdmissionAdmitted, world.WorldTick, previous, group));
        foreach (var other in towns.Where(item => item.Id != townId).ToArray())
            foreach (var open in (other.Admissions ?? []).Where(record => record.SubjectId == subject &&
                         record.Status == WorldRuntime.CivicSharedRules.AdmissionApproved).ToArray())
                LapseTownAdmission(world, other.Id, open, "joined_elsewhere");
        var moving = group.ToHashSet(StringComparer.Ordinal);
        if (previous is not null)
        {
            var left = towns.Single(item => item.Id == previous);
            SetTown(world, left with { ResidentIds = left.ResidentIds.Where(id => !moving.Contains(id)).ToArray() });
        }
        var joined = towns.Single(item => item.Id == townId);
        SetTown(world, joined with
        {
            ResidentIds = joined.ResidentIds.Concat(group).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray(),
        });
        world.AppendEvent("town_admission_accepted", $"{townId}|{subject}|{previous ?? "none"}|{group.Count}");
        // Council rosters follow recorded adult residents in both Towns.
        AdvanceTownCouncil(world);
    }

    internal bool MoveDependentToGuardianTown(ITownWorld world, string child, string guardian)
    {
        var person = world.Society.GetAgent(child);
        var caregiver = world.Society.GetAgent(guardian);
        if (!world.Agents.ContainsKey(child) || !world.Agents.ContainsKey(guardian) ||
            person.Status != SocietyAgentStatus.Active ||
            !LifeStageRules.IsDependent(person.AgeBand) ||
            caregiver.Status != SocietyAgentStatus.Active ||
            !LifeStageRules.IsAdult(caregiver.AgeBand) ||
            person.PrimaryCaregiverId != guardian || !SocietyRules.HasActivePrimaryCaregiver(world.Society, child) ||
            TownForResident(world, guardian) is not { } destination)
            return false;
        if (TownForResident(world, child) == destination) return true;

        // Finish both roster updates before governance or admission reconciliation
        // observes the move. House.TownId is deliberately not a membership source.
        foreach (var town in towns.ToArray())
        {
            if (town.Id == destination)
                SetTown(world, town with
                {
                    ResidentIds = town.ResidentIds.Append(child).Distinct(StringComparer.Ordinal)
                        .Order(StringComparer.Ordinal).ToArray(),
                });
            else if (town.ResidentIds.Contains(child, StringComparer.Ordinal))
                SetTown(world, town with { ResidentIds = town.ResidentIds.Where(id => id != child).ToArray() });
        }
        AdvanceTownCouncil(world);
        SettleTownAdmissions(world);
        return true;
    }

    internal void LapseTownAdmission(ITownWorld world, string townId, TownAdmissionRecord record, string reason)
    {
        SetTownAdmission(world, townId, record with { Status = WorldRuntime.CivicSharedRules.AdmissionLapsed, DecidedTick = world.WorldTick, PreviousTownId = null, MemberIds = null, Reason = reason });
        world.AppendEvent("town_admission_lapsed", $"{townId}|{record.SubjectId}|{record.ProposalId}|{reason}");
    }

    internal void SetTownAdmission(ITownWorld world, string townId, TownAdmissionRecord record)
    {
        var town = towns.Single(item => item.Id == townId);
        var records = town.Admissions ?? [];
        SetTown(world, town with
        {
            Admissions = records.Any(item => item.ProposalId == record.ProposalId)
                ? records.Select(item => item.ProposalId == record.ProposalId ? record : item).ToArray()
                : records.Append(record).ToArray(),
        });
    }

    internal string[] TownCareGroup(ITownWorld world, string adult, string? fromTownId) => world.Society.Agents
            .Where(person => person.Status == SocietyAgentStatus.Active && (person.Id == adult ||
                LifeStageRules.IsDependent(person.AgeBand) &&
                person.PrimaryCaregiverId == adult && TownForResident(world, person.Id) == fromTownId))
            .Select(person => person.Id).Order(StringComparer.Ordinal).ToArray();

    internal string TownName(ITownWorld world, string townId) => towns.SingleOrDefault(town => town.Id == townId)?.Name ?? "another Town";

    internal string? TownMembershipNote(ITownWorld world, string actor)
    {
        var known = towns.Where(town => town.Council is not null).ToDictionary(town => town.Id, CivicHistory, StringComparer.Ordinal);
        return TownMembershipText.Describe(towns, world.Society, actor, CivicDay(world),
            TownMembershipText.TownsWithWarehouse(world.WorldSimulation, world.WorldContent),
            (town, kind, subject) => known.TryGetValue(town.Id, out var history) && history.Knows(actor, kind, subject),
            world.CalendarConfig.CalendarOffsetTicks);
    }

    internal static void ValidateTownAdmissions(IReadOnlyList<TownRuntimeState> savedTowns, SocietyCheckpoint society, int schemaVersion)
    {
        var people = society.Agents.Select(person => person.Id).ToHashSet(StringComparer.Ordinal);
        var townIds = savedTowns.Select(town => town.Id).ToHashSet(StringComparer.Ordinal);
        foreach (var town in savedTowns)
        {
            ValidateLocalAdmissions(town);
            var records = town.Admissions ?? [];
            var proposals = (town.Council?.Proposals ?? []).ToDictionary(proposal => proposal.Id, StringComparer.Ordinal);
            // Each passed admission is settled as soon as it passes, so it has exactly one outcome record.
            if ((town.Admissions is not null && schemaVersion < WorldRuntime.TownAdmissionSchemaVersion) ||
                proposals.Values.Any(proposal => proposal is { Kind: "admission", Status: "passed" } &&
                    !records.Any(record => record?.ProposalId == proposal.Id)))
                throw new InvalidDataException("A Town's saved admission records are invalid.");
            foreach (var record in records)
            {
                var valid = record is not null && proposals.TryGetValue(record.ProposalId ?? "", out var proposal) &&
                    proposal is { Kind: "admission", Status: "passed" } && proposal.SubjectId == record.SubjectId &&
                    people.Contains(record.SubjectId) && record.DecidedTick >= proposal.SettledTick &&
                    record.DecidedTick <= society.WorldTick &&
                    (record.PreviousTownId is null || townIds.Contains(record.PreviousTownId) && record.PreviousTownId != town.Id) &&
                    record.Status switch
                    {
                        WorldRuntime.CivicSharedRules.AdmissionApproved => proposal.AuthorId != record.SubjectId &&
                            TownAdmissionDeadlineRules.IsOpen(proposal, society.WorldTick, society.Config.TicksPerWorldDay),
                        WorldRuntime.CivicSharedRules.AdmissionAdmitted => record.MemberIds!.All(people.Contains) &&
                            (proposal.AuthorId == record.SubjectId || TownAdmissionDeadlineRules.IsOpen(proposal, record.DecidedTick, society.Config.TicksPerWorldDay)),
                        WorldRuntime.CivicSharedRules.AdmissionLapsed =>
                            (record.Reason != "acceptance_expired" || TownAdmissionDeadlineRules.IsExpired(proposal, record.DecidedTick, society.Config.TicksPerWorldDay)),
                        _ => false,
                    };
                if (!valid)
                    throw new InvalidDataException("A Town's saved admission record does not match a passed admission proposal.");
            }
        }
        // An approval waiting for acceptance belongs to someone who is not already a resident there.
        if (savedTowns.Any(town => (town.Admissions ?? []).Any(record => record.Status == WorldRuntime.CivicSharedRules.AdmissionApproved &&
                town.ResidentIds.Contains(record.SubjectId, StringComparer.Ordinal))))
            throw new InvalidDataException("A Town's saved admission approval names an existing resident.");
    }

}
