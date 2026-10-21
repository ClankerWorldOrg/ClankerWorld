using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;
using System.Collections.ObjectModel;
using System.Security.Cryptography;
using System.Text;

namespace ClankerWorld.Simulation.World;

internal sealed partial class OrderSystem
{
    internal static bool ShouldInterruptOrder(IOrderSurvivalWorld world,
        AgentState person,
        OwnerQueuedInstruction instruction,
        CognitionCandidate? orderCandidate,
        CognitionCandidate? urgentCandidate)
    {
        if (IsShelterOrder(instruction.Task!.Action))
            return world.NeedsUrgentFood(person) && urgentCandidate is not null && world.IsFoodSurvivalCandidate(person.AgentId, urgentCandidate.Id) ||
                orderCandidate is null && urgentCandidate is not null;
        if (world.NeedsUrgentWarmth(person))
            return urgentCandidate is not null &&
                orderCandidate?.Id is not ("wear_clothing" or "tend_fire" or "seek_warmth");
        if (!world.NeedsUrgentFood(person)) return false;
        if (urgentCandidate is null) return false;
        return orderCandidate is null || orderCandidate.Id != urgentCandidate.Id ||
            orderCandidate.DestinationId != urgentCandidate.DestinationId;
    }


    internal static CognitionCandidate? UrgentSurvivalCandidateFor(IOrderSurvivalWorld world,
        string agentId, AgentState person, OwnerQueuedInstruction? instruction = null) => world.SelectOrderSurvivalCandidate(world.CreateCandidates(agentId, person, restrictForOrder: false), person, instruction);

    internal static CognitionCandidate? SelectOrderSurvivalCandidate(IOrderSurvivalWorld world,
        IEnumerable<CognitionCandidate> candidates, AgentState person, OwnerQueuedInstruction? instruction)
    {
        var available = candidates.Where(candidate => world.IsSurvivalCandidate(person.AgentId, candidate.Id))
            .OrderBy(candidate => candidate.DeterministicPriority)
            .ThenBy(candidate => candidate.Id, StringComparer.Ordinal).ToArray();
        if (instruction?.Task is { } order && IsShelterOrder(order.Action) && world.NeedsUrgentFood(person) &&
            available.FirstOrDefault(candidate => world.IsFoodSurvivalCandidate(person.AgentId, candidate.Id)) is { } food)
            return food;
        return available.FirstOrDefault();
    }


    internal static bool IsFoodSurvivalCandidate(IOrderSurvivalWorld world, string actor, string candidateId) => candidateId is
        "consume_food" or "drink_milk" or "collect_shared_food" or "take_food_from_pot" or "make_room_for_food" or
        "harvest_food" or "seek_food" || candidateId.StartsWith(world.TownProjectReturnPrefix, StringComparison.Ordinal) ||
        world.NeedsUrgentFood(world.Agents[actor]) &&
        (world.IsShopFoodCandidate(actor, candidateId) || world.IsMarketFoodCandidate(actor, candidateId));

    internal static bool IsSurvivalCandidate(IOrderSurvivalWorld world, string actor, string candidateId) => world.IsFoodSurvivalCandidate(actor, candidateId) || candidateId is
        "recover_household_delivery" or "wear_clothing" or "tend_fire" or "seek_warmth";
}
