using System.Security.Cryptography;
using System.Text;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private const int ChildSocialCooldownTicks = 120;

    private bool IsChild(string actor) => AgentQueries.IsChild(agentWorld, actor);

    // This is the final world-side gate, in addition to the legal candidate list
    // used by cognition admission. It also protects continuing intentions after
    // an age transition and any future direct action caller.
    private bool AgePermitsCandidate(string actor, string candidate)
    {
        var age = society.Checkpoint.GetAgent(actor).AgeBand;
        if (age == SocietyAgeBand.Infant) return false;
        if (candidate == "make_room_for_food" && IsChild(actor)) return true;
        if (age is SocietyAgeBand.Adult or SocietyAgeBand.Elder)
            return !candidate.StartsWith("child_", StringComparison.Ordinal);
        return candidate is "safe_idle" or "consume_food" or "drink_milk" or "collect_shared_food" or
            "seek_food" or "harvest_food" or "talk_to" or "move_to" or "wear_clothing" or "seek_warmth" or "seek_shelter" or "inspect_shelter_site" ||
            candidate.StartsWith("guardian_accept:", StringComparison.Ordinal) ||
            candidate.StartsWith("guardian_refuse:", StringComparison.Ordinal) ||
            candidate.StartsWith("guardian_end:", StringComparison.Ordinal) ||
            candidate.StartsWith("child_converse:", StringComparison.Ordinal) ||
            candidate.StartsWith("child_play:", StringComparison.Ordinal) ||
            candidate.StartsWith("child_learn:", StringComparison.Ordinal) ||
            candidate.StartsWith("talk:", StringComparison.Ordinal) ||
            candidate.StartsWith("conversation_", StringComparison.Ordinal) ||
            candidate.StartsWith(TownSalvagePrefix, StringComparison.Ordinal) ||
            candidate == "child_help_food" || candidate.StartsWith("child_gather:", StringComparison.Ordinal) ||
            candidate.StartsWith("child_carry:", StringComparison.Ordinal);
    }

    private bool ChildSocialAvailable(string actor, string target, string kind) =>
        IsChild(actor) && actor != target && agents.ContainsKey(target) &&
        society.Checkpoint.GetAgent(target).AgeBand != SocietyAgeBand.Infant &&
        !society.Checkpoint.Memories.Any(memory => memory.OwnerId == actor && memory.SubjectId == target &&
            memory.Id.Value.StartsWith($"child-social:{kind}:", StringComparison.Ordinal) &&
            memory.SourceTick > WorldTick - ChildSocialCooldownTicks);

    private void AddChildCandidates(List<CognitionCandidate> candidates, string actor, AgentState state)
    {
        var availableTargets = 0;
        foreach (var target in agents.Keys.Where(id => id != actor &&
                     society.Checkpoint.GetAgent(id).AgeBand != SocietyAgeBand.Infant)
                 .OrderBy(id => map.FootDistance(state.Position, agents[id].Position))
                 .ThenBy(id => id, StringComparer.Ordinal))
        {
            if (NeedsUrgentFood(state) && !IsWithinInteractionRange(state.Position, agents[target].Position, ResourceInteractionRange))
                continue;
            if (FindUnoccupiedRoute(actor, state.Position, agents[target].Position, ResourceInteractionRange).Count == 0)
                continue;
            var candidateCount = candidates.Count;
            if (ChildSocialAvailable(actor, target, "converse"))
                candidates.Add(new($"child_converse:{target}", "Talk with a nearby person and remember the encounter.", 38, target));
            if (ChildSocialAvailable(actor, target, "play"))
                candidates.Add(new($"child_play:{target}", "Play together and build a friendship.", 34, target));
            if (IsAdult(target) && ChildSocialAvailable(actor, target, "learn"))
                candidates.Add(new($"child_learn:{target}", "Learn a simple observation from an adult without taking an adult work role.", 42, target));
            if (candidates.Count > candidateCount && ++availableTargets == 3) break;
        }

        var householdId = society.Checkpoint.GetAgent(actor).HouseholdId;
        AddChildHelpingCandidates(candidates, actor, state);
        var house = householdId is null ? null : HouseForHousehold(householdId);
        if (state.FullnessBasisPoints >= 6_000 && PersonalSpareFood(actor) is not null &&
            (house is null || DestinationRoom(house.InstanceId) > 0) &&
            FindUnoccupiedRoute(actor, state.Position,
                house?.Position ?? SharedStoragePosition,
                house is null ? ResourceInteractionRange : 0).Count > 0)
            candidates.Add(new("child_help_food", "Carry one spare food serving back to the household store.", 26));
    }

    private void ApplyChildCandidate(string actor, AgentState state, string candidate)
    {
        if (!IsChild(actor) || NeedsUrgentWarmth(state)) return;
        if (candidate.StartsWith("child_gather:", StringComparison.Ordinal) ||
            candidate.StartsWith("child_carry:", StringComparison.Ordinal))
        {
            ApplyChildHelpingCandidate(actor, state, candidate);
            return;
        }
        if (candidate == "child_help_food")
        {
            if (state.FullnessBasisPoints < 6_000 ||
                PersonalSpareFood(actor) is not { } lot)
                return;
            var householdId = society.Checkpoint.GetAgent(actor).HouseholdId;
            var house = householdId is null ? null : HouseForHousehold(householdId);
            if (house is not null && DestinationRoom(house.InstanceId) == 0)
                return;
            var store = house?.Position ?? SharedStoragePosition;
            var interactionRange = house is null ? ResourceInteractionRange : 0;
            if (!IsWithinInteractionRange(state.Position, store, interactionRange))
            {
                MoveToward(actor, state, store, "child_help", interactionRange);
                return;
            }
            ApplyInventoryTransition(inventory => InventoryAuthority.Transfer(inventory,
                $"child-help:{WorldTick}:{actor}", actor, HouseholdFor(actor), lot.Id, 1,
                "child_household_help", house?.InstanceId, holdAccess: InventoryHoldRules.DeliveryFor(actor, house?.InstanceId)));
            AppendEvent(new ChildHelpedHouseholdEventPayload(actor));
            return;
        }

        var separator = candidate.IndexOf(':', StringComparison.Ordinal);
        if (separator < 0) return;
        var kind = candidate[6..separator];
        var target = candidate[(separator + 1)..];
        if (kind is not ("converse" or "play" or "learn") || !ChildSocialAvailable(actor, target, kind) ||
            kind == "learn" && !IsAdult(target)) return;
        var destination = agents[target].Position;
        if (!IsWithinInteractionRange(state.Position, destination, ResourceInteractionRange))
        {
            MoveToward(actor, state, destination, "child_social", ResourceInteractionRange);
            return;
        }

        // Keep short keys and cooldown prefixes; hash the complete identity when
        // ancestry makes it exceed the cognition memory boundary.
        var memoryId = ChildSocialMemoryId(kind, actor, target, WorldTick);
        society.Apply(checkpoint => SocietyRules.RecordSocialMemory(checkpoint,
            new(Identity.Parse(memoryId), actor, target,
                kind switch
                {
                    "converse" => "Talked with another person.",
                    "play" => "Played with another person.",
                    _ => "Learned by observing an adult.",
                }, "public", WorldTick)));
        IncreaseTrust(actor, target, 1, "child_" + kind);
        AppendEvent(WorldEvents.Child(kind, actor, target));
    }
}
