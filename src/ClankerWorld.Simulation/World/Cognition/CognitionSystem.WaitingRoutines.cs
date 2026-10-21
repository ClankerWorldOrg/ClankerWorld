using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;
using System.Collections.ObjectModel;
using System.Security.Cryptography;
using System.Text.Json.Serialization;
using System.Text.Json;
using System.Text;

namespace ClankerWorld.Simulation.World;

internal sealed partial class CognitionSystem
{
    internal static void ApplySafeRoutinesWhileWaiting(ICognitionWaitingRoutinesWorld world,
        IEnumerable<string> waitingIds,
        IEnumerable<string> orderActorsHandledThisTick)
    {
        var handledOrders = orderActorsHandledThisTick.ToHashSet(StringComparer.Ordinal);
        foreach (var id in waitingIds.OrderBy(item => item, StringComparer.Ordinal))
        {
            if (!world.Agents.TryGetValue(id, out var state)) continue;
            if (handledOrders.Contains(id) || world.HasGuardianPlacementTask(id) && world.GuardianPlacementActions.Contains(id)) continue;
            if (world.PendingInstructionFor(id) is not null) continue;
            world.TryApplyWaitingRoutine(id, state);
        }
    }


    internal static bool TryApplyWaitingRoutine(ICognitionWaitingRoutinesWorld world, string id, AgentState state)
    {
        if (world.WithheldActions?.Contains(id) == true) return false;
        if (world.NeedsUrgentFood(state) || world.NeedsUrgentWarmth(state))
        {
            var candidate = world.CreateCandidates(id, state, restrictForOrder: false)
                .Where(item => world.IsWaitingSurvivalCandidate(item.Id))
                .OrderBy(item => item.DeterministicPriority)
                .ThenBy(item => item.Id, StringComparer.Ordinal)
                .FirstOrDefault();
            if (candidate is not null) world.ApplyCandidate(id, state, candidate.Id, reportIdle: false);
            return true;
        }
        if (!world.IsAdult(id) || !world.ReadyForBriefInteraction(id)) return false;
        var nearby = world.ChildrenNeedingCare(id).Select(child => child.AgentId).Concat(world.IllDependentsNeedingCare(id))
            .Where(dependent => world.IsWithinInteractionRange(state.Position, world.Agents[dependent].Position, world.ResourceInteractionRange))
            .ToHashSet(StringComparer.Ordinal);
        if (nearby.Count == 0) return false;
        var care = world.CreateCandidates(id, state, restrictForOrder: false).Where(candidate =>
                candidate.Id.StartsWith("care:", StringComparison.Ordinal) && nearby.Contains(candidate.Id[5..]) ||
                candidate.Id.StartsWith("guardian_tend:", StringComparison.Ordinal) && nearby.Contains(candidate.Id[14..]))
            .OrderBy(candidate => candidate.DeterministicPriority).ThenBy(candidate => candidate.Id, StringComparer.Ordinal).FirstOrDefault();
        if (care is null) return false;
        // Use the ordinary physical care boundary without cancelling an existing
        // repair or starting, ending or accepting a caregiving relationship.
        if (care.Id.StartsWith("care:", StringComparison.Ordinal)) world.CareForChild(id, care.Id[5..], care.ActionPlan);
        else world.TendToIllDependent(id, care.Id[14..], care.ActionPlan);
        return true;
    }


    internal static bool CanContinuePendingTask(ICognitionWaitingRoutinesWorld world, AgentState state, string id) => world.IsWaitingSurvivalCandidate(id) || world.IsTownProjectCandidate(id) || id is "knowledge_continue" or "haul_household_stock" or
            "store_household_food" or "store_town_resources" or "haul_farm_grain" or "haul_farm_flour" or
            "haul_smith_input" or "gather_smith_ore" or "deliver_smith_ore" or "collect_water_jug" or "return_water_jug" or
            "store_food_in_pot" ||
        world.KnowledgeWritingFor(state.AgentId) is { OrderInstructionId: null } writing && writing.CandidateId == id ||
        id == "explore" && state.Exploration?.OutingPath.Count > 0 ||
        state.Exploration is { Returning: false, OutingPath.Count: > 0, Goal: { OrderInstructionId: null } goal } &&
            world.ExplorationGoalCandidateId(goal) == id ||
        id.StartsWith("assist:", StringComparison.Ordinal) ||
        id.StartsWith(world.GatherBlacksmithInputPrefix, StringComparison.Ordinal) ||
        id.StartsWith(world.GatherBuildingMaterialPrefix, StringComparison.Ordinal) ||
        id.StartsWith(world.RareMiningPrefix, StringComparison.Ordinal) ||
        id.StartsWith(world.SupplyWorkstationPrefix, StringComparison.Ordinal) ||
        id.StartsWith(world.ReturnEmptyVesselPrefix, StringComparison.Ordinal) ||
        id.StartsWith(world.FillWaterJugPrefix, StringComparison.Ordinal) ||
        id.StartsWith(world.PullCartPrefix, StringComparison.Ordinal) ||
        id.StartsWith("boat_queue:", StringComparison.Ordinal) ||
        id.StartsWith("animal:supply_trip:", StringComparison.Ordinal) ||
        id.StartsWith("animal:care:", StringComparison.Ordinal) ||
        id.StartsWith("animal:collect:", StringComparison.Ordinal) ||
        id.StartsWith("animal:lead_home:", StringComparison.Ordinal);
}
