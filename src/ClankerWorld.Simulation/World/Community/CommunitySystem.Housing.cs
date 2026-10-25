namespace ClankerWorld.Simulation.World;

internal sealed partial class CommunitySystem
{
    internal const int HousingRequestTicks = 120;
    internal static ActorCapabilityPolicy HouseholdCommunityCapabilities =>
        new(ActionLifeStages.Adults, AllowBoat: true, AllowUrgentFood: true, AllowUrgentWarmth: false, AllowActiveOrder: true);

    internal static void CompleteHousingAction(ICommunityHousingWorld world, HousingActionPlan plan)
    {
        if (plan.Choice.Kind == HousingChoiceKind.Ask)
        {
            SetHousing(world, plan.Actor, (world.PhysicalAgents.Living[plan.Actor].Housing ?? new()) with
            {
                Request = new(plan.HouseholdId, world.WorldTick, world.WorldTick + HousingRequestTicks, plan.AdultMembers, [], []),
            });
            world.AppendEvent(new HousingRequestMadeEventPayload(plan.Actor, plan.HouseholdId));
            return;
        }
        var housing = world.PhysicalAgents.Living[plan.ApplicantId].Housing!;
        var request = housing.Request!;
        SetHousing(world, plan.ApplicantId, housing with
        {
            Request = plan.Choice.Kind == HousingChoiceKind.Admit
                ? request with { Approvals = request.Approvals.Append(plan.Actor).Order(StringComparer.Ordinal).ToArray() }
                : request with { Rejections = request.Rejections.Append(plan.Actor).Order(StringComparer.Ordinal).ToArray() },
        });
        world.AppendEvent(new HousingAnswerRecordedEventPayload(plan.Actor, plan.ApplicantId));
        ResolveHousingRequest(world, plan.ApplicantId);
    }

    internal static void MaintainHousing(ICommunityHousingWorld world)
    {
        foreach (var actor in world.PhysicalAgents.Living.Keys.Order(StringComparer.Ordinal).ToArray())
        {
            ResolveHousingRequest(world, actor);
            var housing = world.PhysicalAgents.Living[actor].Housing;
            var refusals = housing?.Refusals?.Where(refusal => world.WorldTick - refusal.Tick < world.HousingRefusalCooldownTicks).ToArray();
            var blocker = world.HousingBlocker(actor);
            if (housing?.Blocker == blocker && (refusals?.Length ?? 0) == (housing?.Refusals?.Count ?? 0))
                continue;
            SetHousing(world, actor, (housing ?? new()) with { Blocker = blocker, Refusals = refusals });
            if (blocker is not null && blocker != HousingBlockers.AwaitingAnswer && housing?.Blocker != blocker)
                world.AppendEvent(new HousingBlockedEventPayload(actor, blocker));
        }
    }

    internal static void ResolveHousingRequest(ICommunityHousingWorld world, string actor)
    {
        // Care, family or a completed expansion can change between answers,
        // including earlier in this decision batch. Cancel stale notices first.
        if (world.PhysicalAgents.Living.TryGetValue(actor, out var applicant) && applicant.Housing is { Request: not null, Relocation: { } notice })
            world.ReconcileRelocation(notice.HouseholdId);
        if (!world.PhysicalAgents.Living.TryGetValue(actor, out var state) || state.Housing is not { Request: { } request } housing)
            return;
        var living = world.HouseholdAdults(request.HouseholdId);
        var members = request.Members.Union(living, StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        if (!request.Members.SequenceEqual(members, StringComparer.Ordinal))
        {
            // Existing answers stay recorded, while adults added or grown into
            // the household must answer before this request can be accepted.
            request = request with { Members = members };
            housing = housing with { Request = request };
            SetHousing(world, actor, housing);
        }
        var current = world.IsAdult(actor) ? world.Society.GetAgent(actor).HouseholdId : null;
        // A member's request ends with their move-out notice: the move is no longer needed.
        if (!world.IsAdult(actor) || current is not null && (current == request.HouseholdId || !world.MayRelocateFromHousehold(actor)) ||
            world.HouseForHousehold(request.HouseholdId) is null || living.Length == 0)
        {
            EndHousingRequest(world, actor, request, "housing_request_cancelled", remember: false);
        }
        else if (world.WorldTick > request.ExpiryTick)
        {
            // Restored answers, or the loss of the last unanswered adult,
            // cannot grant membership after the request's world-tick deadline.
            EndHousingRequest(world, actor, request, "housing_request_expired", remember: true);
        }
        else if (request.Rejections.Any(id => living.Contains(id, StringComparer.Ordinal)))
        {
            EndHousingRequest(world, actor, request, "housing_request_refused", remember: true);
        }
        else if (living.All(id => request.Approvals.Contains(id, StringComparer.Ordinal)) &&
            !world.CanFitCareGroup(request.HouseholdId, actor))
        {
            EndHousingRequest(world, actor, request, "housing_request_blocked_capacity", remember: true);
        }
        else if (living.All(id => request.Approvals.Contains(id, StringComparer.Ordinal)))
        {
            // A member moves out through the ordinary departure first, so the
            // allowance, collection right and care group follow the same rules.
            if (current is not null &&
                !world.DepartHousehold(actor, world.HoldsRelocationNotice(actor) ? "displaced" : "voluntary"))
            {
                EndHousingRequest(world, actor, request, "housing_request_cancelled", remember: false);
                return;
            }
            world.JoinHouseholdCareGroup(actor, request.HouseholdId);
            if (world.Society.GetAgent(actor).HouseholdId != request.HouseholdId)
            {
                EndHousingRequest(world, actor, request, "housing_request_cancelled", remember: false);
                return;
            }
            foreach (var id in world.MovingCareGroup(actor))
                SetHousing(world, id, (world.PhysicalAgents.Living[id].Housing ?? new()) with { Request = null, Refusals = null, Blocker = null });
            world.AppendEvent(new HouseholdJoinedEventPayload(actor, request.HouseholdId));
        }
    }

    internal static void EndHousingRequest(ICommunityHousingWorld world, string actor, HousingRequest request, string eventKind, bool remember)
    {
        var housing = world.PhysicalAgents.Living[actor].Housing!;
        var refusals = (housing.Refusals ?? []).Where(refusal => refusal.HouseholdId != request.HouseholdId);
        if (remember)
            refusals = refusals.Append(new HousingRefusal(request.HouseholdId, world.WorldTick));
        SetHousing(world, actor, housing with
        {
            Request = null,
            Refusals = refusals.OrderBy(refusal => refusal.HouseholdId, StringComparer.Ordinal).ToArray(),
            Blocker = null,
        });
        // The blocker is recomputed now, so the owner never sees a stale "waiting" line.
        SetHousing(world, actor, (world.PhysicalAgents.Living[actor].Housing ?? new()) with { Blocker = world.HousingBlocker(actor) });
        world.AppendEvent(WorldEvents.Housing(eventKind, actor, request.HouseholdId));
    }

    internal static void SetHousing(ICommunityHousingWorld world, string actor, HousingState housing)
    {
        var empty = housing.Request is null && housing.Refusals is not { Count: > 0 } && housing.Blocker is null &&
            housing.Relocation is null;
        if (!empty && SystemInvariantChecks.Enabled) ValidateLocalHousing(housing);
        world.PhysicalAgents.RecordHousing(actor, world.PhysicalAgents.Living[actor], empty ? null : housing with { Refusals = housing.Refusals is { Count: > 0 } ? housing.Refusals : null });
        if (!empty)
            world.MarkCurrentSchema();
    }


    internal static void ValidateLocalHousing(HousingState housing)
    {
        if (housing.Request is null && housing.Refusals is not { Count: > 0 } && housing.Blocker is null && housing.Relocation is null ||
            housing.Blocker is not null && !HousingBlockers.All.Contains(housing.Blocker, StringComparer.Ordinal) ||
            housing.Blocker == HousingBlockers.AwaitingAnswer && housing.Request is null ||
            housing.Refusals is { } refusals && (refusals.Any(item => item is null || string.IsNullOrWhiteSpace(item.HouseholdId) || item.Tick < 0) ||
                refusals.Select(item => item.HouseholdId).Distinct(StringComparer.Ordinal).Count() != refusals.Count))
            throw new InvalidDataException("Community housing has invalid local state.");
        if (housing.Request is { } request)
        {
            var answers = request.Approvals is null || request.Rejections is null ? null : request.Approvals.Concat(request.Rejections).ToArray();
            if (string.IsNullOrWhiteSpace(request.HouseholdId) || request.RequestedTick < 0 ||
                request.ExpiryTick - request.RequestedTick != HousingRequestTicks || request.Members is null || answers is null ||
                request.Members.Count == 0 || request.Members.Distinct(StringComparer.Ordinal).Count() != request.Members.Count ||
                request.Members.Any(string.IsNullOrWhiteSpace) || answers.Distinct(StringComparer.Ordinal).Count() != answers.Length ||
                answers.Any(id => !request.Members.Contains(id, StringComparer.Ordinal)))
                throw new InvalidDataException("Community housing request has invalid local state.");
        }
        if (housing.Relocation is { } notice && (string.IsNullOrWhiteSpace(notice.HouseholdId) || notice.NoticeTick < 0 ||
            notice.DeadlineTick <= notice.NoticeTick || !HouseRelocationRules.Reasons.Contains(notice.Reason, StringComparer.Ordinal)))
            throw new InvalidDataException("Community relocation notice has invalid local state.");
    }
    internal static HousingActionPlan? PlanHousingAction(ICommunityHousingWorld world, string actor, HousingChoice choice)
    {
        var applicant = choice.Kind == HousingChoiceKind.Ask ? actor : choice.TargetId;
        if (!world.PhysicalAgents.Living.ContainsKey(actor) || !world.PhysicalAgents.Living.TryGetValue(applicant, out var person)) return null;
        var request = person.Housing?.Request;
        var household = choice.Kind == HousingChoiceKind.Ask ? choice.TargetId : request?.HouseholdId;
        if (household is null || world.HouseForHousehold(household) is not { } house) return null;
        return new(actor, choice, applicant, household, house, request, world.HouseholdAdults(household));
    }

    internal static ActionCheck CheckHousingActionPlan(ICommunityHousingWorld world, HousingActionPlan plan, ActorCapabilityPolicy? policy = null)
    {
        var capability = ActorCapabilities.Check(world.CapabilitiesFor(plan.Actor), plan.CandidateId,
            policy ?? HouseholdCommunityCapabilities);
        if (!capability.Allowed) return capability;
        if (!world.PhysicalAgents.Living.TryGetValue(plan.ApplicantId, out var applicant) ||
            world.HouseForHousehold(plan.HouseholdId) is not { } house ||
            house.InstanceId != plan.House.InstanceId || house.HouseholdId != plan.House.HouseholdId || house.TownId != plan.House.TownId ||
            house.DefinitionId != plan.House.DefinitionId || house.PlacedTick != plan.House.PlacedTick ||
            !world.HouseholdAdults(plan.HouseholdId).SequenceEqual(plan.AdultMembers, StringComparer.Ordinal))
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected House or its adult members have changed");
        if (plan.Choice.Kind == HousingChoiceKind.Ask)
            return applicant.Housing?.Request is null && world.AskableHouseholds(plan.Actor).Any(home => home.Id == plan.HouseholdId)
                ? new() : ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "that household is no longer available for this housing request");
        // Answers by other members may accumulate in one decision batch. They
        // do not replace the selected request or change who must answer it.
        var current = applicant.Housing?.Request;
        var sameRequest = current is not null && plan.OriginalRequest is { } original &&
            current.HouseholdId == original.HouseholdId && current.RequestedTick == original.RequestedTick &&
            current.ExpiryTick == original.ExpiryTick && current.Members.SequenceEqual(original.Members, StringComparer.Ordinal);
        return sameRequest && world.PendingHousingRequestsFor(plan.Actor).Contains(plan.ApplicantId, StringComparer.Ordinal)
            ? new() : ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected housing request has ended or this member has already answered");
    }
    internal static RelocationActionPlan? PlanRelocationAction(ICommunityRelocationWorld world, string actor)
    {
        if (!world.PhysicalAgents.Living.ContainsKey(actor) || world.HouseholdFor(actor) is not { } household || !MayVolunteerToRelocate(world, actor) ||
            world.HouseForHousehold(household) is not { } house || RelocationSelection(world, household, actor) is not { } selection) return null;
        var kept = selection.Chosen.Select(item => item.Id).ToHashSet(StringComparer.Ordinal);
        var replaced = RelocationNotices(world, household).Where(item => !kept.Contains(item.Id))
            .Select(item => new PlannedRelocationNotice(item.Id, item.Notice)).ToArray();
        return new(actor, new(), household, house, selection.Chosen.ToArray(), replaced,
            replaced.Select(item => (long?)item.Notice.DeadlineTick).Min());
    }

    internal static ActionCheck CheckRelocationActionPlan(ICommunityRelocationWorld world, RelocationActionPlan plan, ActorCapabilityPolicy? policy = null)
    {
        var capability = ActorCapabilities.Check(world.CapabilitiesFor(plan.Actor), plan.CandidateId,
            policy ?? HouseholdCommunityCapabilities);
        if (!capability.Allowed) return capability;
        if (world.HouseholdFor(plan.Actor) != plan.HouseholdId || world.HouseForHousehold(plan.HouseholdId) != plan.House ||
            !MayVolunteerToRelocate(world, plan.Actor) || RelocationSelection(world, plan.HouseholdId, plan.Actor) is not { } selection ||
            !selection.Chosen.SequenceEqual(plan.Selected))
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected House or adults who need to move have changed");
        var kept = plan.Selected.Select(item => item.Id).ToHashSet(StringComparer.Ordinal);
        var replaced = RelocationNotices(world, plan.HouseholdId).Where(item => !kept.Contains(item.Id))
            .Select(item => new PlannedRelocationNotice(item.Id, item.Notice));
        return replaced.SequenceEqual(plan.Replaced) ? new() :
            ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected move-out notices or inherited deadline have changed");
    }
}
