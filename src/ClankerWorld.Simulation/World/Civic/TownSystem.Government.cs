using System.Globalization;
using ClankerWorld.Simulation.Cognition;

namespace ClankerWorld.Simulation.World;

internal sealed partial class TownSystem
{
    internal static (TownCouncilState Council, TownGovernmentState Government) AdvanceCivic(ITownWorld world, TownRuntimeState town,
        TownCouncilState council, TownGovernmentState government)
    {
        var adults = TownAdults(world, town);
        var result = TownGovernmentRules.Advance(council, government, town.Id, town.Name, world.WorldSeed, adults, world.WorldTick, CivicDay(world), TownLandHearingElectionBusy(town) || TownNonLandElectionBusy(town));
        if (SystemInvariantChecks.Enabled)
        {
            TownGovernmentStateValidation.ValidateLocal(town.Id, result.Government, allowIntermediateVotes: false);
            ValidateCompletedGovernmentAuthority(result.Council, result.Government);
        }
        return result;
    }

    internal static void AddTownLawCandidates(ITownWorld world, List<CognitionCandidate> candidates, string actor, TownRuntimeState town)
    {
        const string format = "Write civic_proposal as 'subject: rule'. It needs the current council's votes, applies only from adoption " +
            "and records a social rule; it does not block actions, change ownership or create offices.";
        OfferTownLaw(world, candidates, actor, town, TownLawChoiceKind.Adopt, TownLawRules.Jurisdiction, "",
            $"Propose a law for everyone on {town.Name}'s claimed land, visitors included. {format}", 190);
        OfferTownLaw(world, candidates, actor, town, TownLawChoiceKind.Adopt, TownLawRules.ResidentDuty, "",
            $"Propose a duty for {town.Name}'s residents that applies wherever they are. {format}", 191);
        var here = world.Agents[actor].Position;
        if (TownLawRules.SiteAround(world.Map, here, town.Id, world.TownLandTitles).Length > 0)
            OfferTownLaw(world, candidates, actor, town, TownLawChoiceKind.Adopt, SiteToken(here), "",
                $"Propose a law for the claimed land within {TownLawRules.SiteRadius} tiles of where you stand in {town.Name}, visitors included. {format}", 192);
        var history = CivicHistory(town);
        var estateLaw = town.Government?.Laws.SingleOrDefault(law => TownLawRules.IsInForce(law) && TownLawRules.Current(law).EstateDefault is not null);
        var estateTarget = estateLaw is null ? "adopt" : estateLaw.Id + ":" + TownLawRules.Current(estateLaw).Version.ToString(CultureInfo.InvariantCulture);
        foreach (var share in new[] { 0, 25, 50, 75, 100 })
            OfferTownPolicy(world, candidates, actor, new(town.Id, TownPolicyKind.EstateDefault, estateTarget, share.ToString(CultureInfo.InvariantCulture)),
                $"Propose a supported default estate law: {TownEstateDefaultRules.Text(new(share))} " +
                "It needs the ordinary Council vote. No living person's belongings move; a valid will always wins. Whole vessels stay together, and undeliverable Town shares follow the household default.", 195);
        foreach (var law in town.Government?.Laws.Where(l => TownLawRules.IsInForce(l) && town.Council!.Notices.LastOrDefault(n => n.SubjectId == l.Id) is { } latest &&
                history.Known(actor).Contains(latest.Id))
            ?? [])
        {
            var number = TownLawRules.Number(law.Id).ToString(CultureInfo.InvariantCulture);
            var current = TownLawRules.Current(law);
            OfferTownLaw(world, candidates, actor, town, TownLawChoiceKind.Amend, law.Id, current.Version.ToString(CultureInfo.InvariantCulture),
                $"Propose amending {town.Name}'s law {number} ({current.Subject}); write the complete new wording in civic_proposal as 'subject: rule'. " +
                    "It keeps the law's scope, needs the council's votes and applies only from adoption.", 193);
            OfferTownLaw(world, candidates, actor, town, TownLawChoiceKind.Repeal, law.Id, current.Version.ToString(CultureInfo.InvariantCulture),
                $"Propose repealing {town.Name}'s law {number} ({current.Subject}). It needs the council's votes; earlier conduct stays under the law.", 194);
        }
    }

    internal static string SiteToken(GridPoint center) =>
        "site:" + center.X.ToString(CultureInfo.InvariantCulture) + "." + center.Y.ToString(CultureInfo.InvariantCulture);
    internal static void AddTownGovernmentCandidates(ITownWorld world, List<CognitionCandidate> candidates, string actor, TownRuntimeState town)
    {
        var government = town.Government!;
        // Each choice costs prompt space on every model call, so protected proposals are
        // made at the notice place, like admission requests.
        foreach (var target in TownArrangementRules.Supported)
            OfferGovernmentProposal(world, candidates, actor, town, GovernmentProposalKind.Propose, TownArrangementRules.Key(target),
                $"Initiate a protected resident vote in {town.Name}: {TownArrangementRules.Declaration(target)} " +
                "More than half the eligible adult residents must approve; incumbent permission is not needed. No power changes before a valid handover.", 195);
        foreach (var office in government.Offices.Where(office => office.HolderId is not null))
            OfferGovernmentOffice(world, candidates, actor, town, GovernmentOfficeKind.ExtendNonLand,
                TownArrangementRules.Key(government.Arrangement with { NonLand = TownArrangementRules.Mayor }), NonLandExtensionToken(office),
                "Propose adding nonviolent civil adjudication to " + world.Society.GetAgent(office.HolderId!).Name +
                "'s existing " + TownArrangementRules.MandateLabel(office.Mandates) + " term. Resident majority and their separate personal consent are required; the existing term deadline stays unchanged.", 195);
        foreach (var change in government.Changes.Where(change => change.Status is "queued" or "voting" or "handover" &&
                     change.NonLandExtension is { ConsentTick: null } extension && extension.HolderId == actor))
            OfferGovernmentOffice(world, candidates, actor, town, GovernmentOfficeKind.AcceptNonLand, change.Id, "",
                "Personally accept these proposed additional nonviolent adjudication duties for your existing term. This supplies no resident vote, and does not restart your term.", 185);
        OfferGovernmentProposal(world, candidates, actor, town, GovernmentProposalKind.ReplaceMayor, TownArrangementRules.Key(government.Arrangement),
                $"Ask {town.Name}'s adult residents to approve early replacement of the elected mayoral mandates. Approval requires a resident majority and a valid successor election.", 196);
        foreach (var change in government.Changes.Where(change => change.Status is "queued" or "voting"))
        {
            OfferGovernmentProposal(world, candidates, actor, town, GovernmentProposalKind.Withdraw, change.Id,
                "Withdraw your unfinished protected government proposal.", 197);
            OfferGovernmentProposal(world, candidates, actor, town, GovernmentProposalKind.Yes, change.Id,
                $"Cast your final yes vote on the protected resident proposal in {town.Name}. {TownArrangementRules.Declaration(change.Target)}", 165);
            OfferGovernmentProposal(world, candidates, actor, town, GovernmentProposalKind.No, change.Id,
                $"Cast your final no vote on the protected resident proposal in {town.Name}. {TownArrangementRules.Declaration(change.Target)}", 166);
        }
        foreach (var mandates in TownArrangementRules.SupportedMandates)
        {
            OfferGovernmentOffice(world, candidates, actor, town, GovernmentOfficeKind.Register, mandates, "",
                $"Personally agree to seek election for {TownArrangementRules.MandateLabel(mandates)} in {town.Name}, if residents authorize that office. This grants no authority or Council candidacy. To add duties to a current office, use its protected extension; this registration cannot restart that term.", 190);
            OfferGovernmentOffice(world, candidates, actor, town, GovernmentOfficeKind.Withdraw, mandates, "",
                $"Withdraw willingness to seek election for {TownArrangementRules.MandateLabel(mandates)} in {town.Name}; this does not resign a held office.", 190);
        }
        foreach (var office in government.Offices)
            OfferGovernmentOffice(world, candidates, actor, town, GovernmentOfficeKind.Resign, office.Mandates, "",
                $"Resign only your mandate for {TownArrangementRules.MandateLabel(office.Mandates)} in {town.Name}. Any separate mandate continues.", 198);
        if (government.Contest is { RoundDeadlineTick: { } closes } contest)
            foreach (var id in contest.Candidates)
                OfferMayoralBallot(world, candidates, actor, town, contest, id,
                    $"Submit or revise your mayoral ballot for {world.Society.GetAgent(id).Name} in {town.Name}, for {TownArrangementRules.MandateLabel(contest.Mandates)}. " +
                    $"Round {contest.Round} closes on world day {CivicDayNumber(world, closes)}. A tie requires another vote, never a draw.", 167);
    }

    internal static string NonLandExtensionToken(TownOffice office) => NonLandToken(office.Mandates + "|" + office.ElectionId);
    internal static ActorCapabilityPolicy GovernanceCapabilities =>
        new(ActionLifeStages.Adults, AllowBoat: true, AllowUrgentFood: true, AllowUrgentWarmth: false, AllowActiveOrder: true);
    internal static GovernmentOfficeActionPlan? PlanGovernmentOffice(ITownWorld world, string actor, GovernmentOfficeChoice choice)
    {
        if (world.Towns.SingleOrDefault(town => town.Id == choice.TownId) is not { Government: { } government } town) return null;
        var extending = choice.Kind == GovernmentOfficeKind.ExtendNonLand;
        var office = extending ? government.Offices.SingleOrDefault(office => CivicAgentToken(NonLandExtensionToken(office)) == choice.Value) :
            government.Offices.SingleOrDefault(office => office.Mandates == choice.Subject && office.HolderId == actor);
        var change = choice.Kind == GovernmentOfficeKind.AcceptNonLand ? government.Changes.SingleOrDefault(change => change.Id == choice.Subject) : null;
        return new(actor, choice, government.Consents.SingleOrDefault(consent => consent.AgentId == actor && consent.Mandates == choice.Subject),
            office, change, extending ? TownArrangementRules.Parse(choice.Subject) : null, extending ? CivicBoard(world, town) : null);
    }

    internal static ActionCheck CheckGovernmentOffice(ITownWorld world, GovernmentOfficeActionPlan plan, TownRuntimeState? currentTown = null, ActorCapabilityPolicy? policy = null)
    {
        var capability = ActorCapabilities.Check(world.CapabilitiesFor(plan.Actor), plan.CandidateId,
            policy ?? GovernanceCapabilities);
        if (!capability.Allowed) return capability;
        if ((currentTown ?? world.Towns.SingleOrDefault(town => town.Id == plan.Choice.TownId)) is not
            { Council: not null, Government: { } government } town || !TownAdults(world, town).Contains(plan.Actor, StringComparer.Ordinal))
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected personal office choice is no longer available");
        var consent = government.Consents.SingleOrDefault(consent => consent.AgentId == plan.Actor && consent.Mandates == plan.Choice.Subject);
        if (plan.Choice.Kind is GovernmentOfficeKind.Register or GovernmentOfficeKind.Withdraw)
        {
            var officeInPlay = TownArrangementRules.HasOffice(government.Arrangement) || government.Contest is not null ||
                government.Changes.Any(change => change.Status is "queued" or "voting" or "handover" && TownArrangementRules.HasOffice(change.Target));
            var firstVacancy = government.Offices.Count > 0 && government.Offices.All(office => office.HolderId is null) &&
                !government.Changes.Any(change => change.Status is "queued" or "voting" or "handover");
            return TownArrangementRules.IsMandates(plan.Choice.Subject) && consent == plan.Consent &&
                (plan.Choice.Kind == GovernmentOfficeKind.Withdraw ? consent is not null : consent is null && officeInPlay &&
                    (!firstVacancy || plan.Choice.Subject == TownArrangementRules.Mandates(government.Arrangement)))
                ? new() : ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected office consent or vacancy has changed");
        }
        if (plan.Choice.Kind == GovernmentOfficeKind.Resign)
            return plan.Office is { } held && government.Offices.Contains(held) && held.HolderId == plan.Actor
                ? new() : ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "that selected office term has ended");
        if (plan.Choice.Kind == GovernmentOfficeKind.ExtendNonLand)
            return plan.Office is { HolderId: not null, Mandates: "land" or "ordinary" } original &&
                government.Offices.Contains(original) && original.TermEndTick > world.WorldTick && government.Arrangement.NonLand == "none" &&
                plan.Arrangement == government.Arrangement with { NonLand = TownArrangementRules.Mayor } &&
                CivicBoard(world, town) == plan.NoticePlace && NearCivicBoard(world, plan.Actor, town)
                ? new() : ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected incumbent term or additional duties have changed");
        var change = government.Changes.SingleOrDefault(change => change.Id == plan.Choice.Subject);
        return plan.Change?.NonLandExtension is { ConsentTick: null } selected && change?.NonLandExtension == selected &&
            change.Target == plan.Change.Target && change.RequestKey == plan.Change.RequestKey &&
            TownGovernmentRules.CanAcceptNonLandDuties(town.Council, government, change.Id, plan.Actor, TownAdults(world, town), world.WorldTick)
            ? new() : ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected additional duties or current term are no longer available");
    }

    private static void OfferGovernmentOffice(ITownWorld world, List<CognitionCandidate> candidates, string actor,
        TownRuntimeState town, GovernmentOfficeKind kind, string subject, string value, string text, int priority)
    {
        var id = CivicAction(world, town.Id, GovernmentOfficeChoice.Verb(kind), subject, value);
        if (GovernmentOfficeChoice.Decode(id) is { } choice && PlanGovernmentOffice(world, actor, choice) is { } plan && CheckGovernmentOffice(world, plan).Allowed)
            candidates.Add(new(id, text, priority) { ActionPlan = plan });
    }
    internal static GovernmentProposalActionPlan? PlanGovernmentProposal(ITownWorld world, string actor, GovernmentProposalChoice choice)
    {
        if (world.Towns.SingleOrDefault(town => town.Id == choice.TownId) is not { Government: { } government } town) return null;
        if (choice.Kind is GovernmentProposalKind.Propose or GovernmentProposalKind.ReplaceMayor)
            return TownArrangementRules.Parse(choice.Subject) is { } target
                ? new(actor, choice, government.Arrangement, target, CivicBoard(world, town), null) : null;
        var change = government.Changes.SingleOrDefault(change => change.Id == choice.Subject);
        return change is null ? null : new(actor, choice, government.Arrangement, null, null,
            change with { Voters = change.Voters.ToArray(), OpeningVoters = change.OpeningVoters.ToArray(), Votes = change.Votes.ToArray() });
    }

    internal static ActionCheck CheckGovernmentProposal(ITownWorld world, GovernmentProposalActionPlan plan, TownRuntimeState? currentTown = null, ActorCapabilityPolicy? policy = null)
    {
        var capability = ActorCapabilities.Check(world.CapabilitiesFor(plan.Actor), plan.CandidateId,
            policy ?? GovernanceCapabilities);
        if (!capability.Allowed) return capability;
        if ((currentTown ?? world.Towns.SingleOrDefault(town => town.Id == plan.Choice.TownId)) is not
            { Council: not null, Government: { } government } town || !TownAdults(world, town).Contains(plan.Actor, StringComparer.Ordinal))
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected resident government proposal is no longer available");
        if (plan.Choice.Kind is GovernmentProposalKind.Propose or GovernmentProposalKind.ReplaceMayor)
            return government.Arrangement == plan.OriginalArrangement && plan.Target is { } target && TownArrangementRules.IsSupported(target) &&
                (plan.Choice.Kind == GovernmentProposalKind.Propose ? target != government.Arrangement :
                    target == government.Arrangement && TownArrangementRules.HasOffice(target)) &&
                CivicBoard(world, town) == plan.NoticePlace && NearCivicBoard(world, plan.Actor, town)
                ? new() : ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected government arrangement or notice place has changed");
        var current = government.Changes.SingleOrDefault(change => change.Id == plan.Choice.Subject);
        if (current is null || plan.Change is not { } selected ||
            current.Status is not ("queued" or "voting") || !CivicHistory(town).Knows(plan.Actor, current.Id))
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected protected proposal or voting window has changed");
        // Withdrawal follows the same request as its vote opens; its writer uses current progress.
        if (plan.Choice.Kind == GovernmentProposalKind.Withdraw)
            return current.Id == selected.Id && current.RequestKey == selected.RequestKey && current.Kind == selected.Kind &&
                current.Target == selected.Target && current.SubmittedTick == selected.SubmittedTick &&
                current.AuthorId == selected.AuthorId && current.AuthorId == plan.Actor
                ? new() : ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected protected proposal or author has changed");
        // Personal acceptance progresses this request without changing the terms residents vote on.
        var extension = current.NonLandExtension;
        if (extension is { ConsentTick: not null } && selected.NonLandExtension is { ConsentTick: null })
            extension = extension with { ConsentTick = null };
        if (current with
        {
            Votes = selected.Votes,
            Voters = selected.Voters,
            OpeningVoters = selected.OpeningVoters,
            NonLandExtension = extension
        } != selected ||
            !current.Voters.SequenceEqual(selected.Voters) || !current.OpeningVoters.SequenceEqual(selected.OpeningVoters))
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected protected proposal or voting window has changed");
        var allowed = current.Status == "voting" && world.WorldTick < current.DeadlineTick &&
            CivicHistory(town).Knows(plan.Actor, TownGovernmentRules.VoteNoticeToken(current)) &&
            current.Voters.Contains(plan.Actor, StringComparer.Ordinal) && !current.Votes.Any(vote => vote.AgentId == plan.Actor);
        return allowed ? new() : ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "that protected response is final or no longer eligible");
    }

    private static void OfferGovernmentProposal(ITownWorld world, List<CognitionCandidate> candidates, string actor,
        TownRuntimeState town, GovernmentProposalKind kind, string subject, string text, int priority)
    {
        if (PlanGovernmentProposal(world, actor, new(town.Id, kind, subject)) is { } plan && CheckGovernmentProposal(world, plan).Allowed)
            candidates.Add(new(plan.CandidateId, text, priority) { ActionPlan = plan });
    }
    internal static TownProjectProposalActionPlan? PlanTownProjectProposal(ITownWorld world, string actor,
        TownProjectProposalChoice choice, TownProjectPayload? selected = null)
    {
        if (world.Towns.SingleOrDefault(town => town.Id == choice.TownId) is not { Council: not null } town) return null;
        var project = selected ?? (choice.Boat ? world.Port(choice.DefinitionOrPort) is { } port ?
            WorldRuntime.CivicSharedRules.BoatProjectPlan(port, "Communal boat") : null :
            world.OfferedTownProjectPlan(town, actor, choice.DefinitionOrPort, choice.Coordinates, null));
        return project is null ? null : new(actor, choice, project, town.Council.Revision);
    }

    internal static TownProjectProposalActionPlan BindTownProjectName(TownProjectProposalActionPlan plan, string? name) =>
        plan.Bound ? plan : plan with { Bound = true, Project = plan.Project with { Name = string.IsNullOrWhiteSpace(name) ? plan.Project.Name : name.Trim() } };

    internal static ActionCheck CheckTownProjectProposal(ITownWorld world, TownProjectProposalActionPlan plan, TownRuntimeState? currentTown = null, ActorCapabilityPolicy? policy = null)
    {
        var capability = ActorCapabilities.Check(world.CapabilitiesFor(plan.Actor), plan.CandidateId,
            policy ?? GovernanceCapabilities);
        if (!capability.Allowed) return capability;
        if ((currentTown ?? world.Towns.SingleOrDefault(town => town.Id == plan.Choice.TownId)) is not { Council: not null } town ||
            !TownAdults(world, town).Contains(plan.Actor, StringComparer.Ordinal) || town.Council.Revision != plan.CouncilRevision)
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "their selected Town project proposal authority has changed");
        if (TownProjectRules.PayloadRefusal(plan.Project) is { } payload)
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, payload);
        if (!world.TownProjectDemandIsCurrent(town, plan.Project))
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected Market stall or boat project is no longer needed");
        if (world.TownProjectSiteFailure(town, plan.Project, actor: plan.Actor) is { } site)
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, site);
        if (TownCouncilRules.ProposalRetryRefusal(town.Council, TownProjectRules.RequestKey(plan.Project),
            "council:" + plan.CouncilRevision.ToString(System.Globalization.CultureInfo.InvariantCulture), world.WorldTick, CivicDay(world)) is { } retry)
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, retry);
        return new();
    }
}
