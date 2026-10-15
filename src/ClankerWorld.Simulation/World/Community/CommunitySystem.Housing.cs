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
            world.AppendEvent("housing_request_made", $"{plan.Actor}:{plan.HouseholdId}");
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
        world.AppendEvent("housing_answer_recorded", $"{plan.Actor}:{plan.ApplicantId}");
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
                world.AppendEvent("housing_blocked", $"{actor}:{blocker}");
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
            world.AppendEvent("household_joined", $"{actor}:{request.HouseholdId}");
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
        world.AppendEvent(eventKind, $"{actor}:{request.HouseholdId}");
    }

    internal static void SetHousing(ICommunityHousingWorld world, string actor, HousingState housing)
    {
        var empty = housing.Request is null && housing.Refusals is not { Count: > 0 } && housing.Blocker is null &&
            housing.Relocation is null;
        world.PhysicalAgents.RecordHousing(actor, world.PhysicalAgents.Living[actor], empty ? null : housing with { Refusals = housing.Refusals is { Count: > 0 } ? housing.Refusals : null });
        if (!empty)
            world.MarkCurrentSchema();
    }

}
