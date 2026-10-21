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
    internal static OrderTask? ParseGuardianOrder(IGuardianOrdersWorld world, string text, string actor)
    {
        const string prefix = "become guardian for ";
        if (!world.IsAdult(actor) || !text.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return null;
        var target = text[prefix.Length..].Trim();
        if (target.Length == 0) return null;
        var active = world.Society.Agents.Where(person => person.Status == SocietyAgentStatus.Active);
        // Bind the complete, unique name (or exact ID) once. Renames cannot
        // redirect a queued order, and leftover words are never guessed away.
        var named = active.Where(person => person.Id == target).ToArray();
        if (named.Length == 0 && AgentNameRules.CanonicalKey(target) is { } targetName)
            named = active.Where(person => string.Equals(AgentNameRules.CanonicalKey(person.Name), targetName,
                StringComparison.OrdinalIgnoreCase)).ToArray();
        if (named.Length != 1 || !world.NeedsCaregiver(named[0].Id) || world.Agents[named[0].Id].GuardianSearch is null)
            return null;
        return OrderStore.CreateOrderTask(OrderStore.CreateAcceptGuardianshipOrderDefinition(TargetAgentId: (named[0].Id)!), "queued", 1, 0, "guardianships", false);
    }


    internal static CognitionCandidate? GuardianOrderCandidate(IGuardianOrdersWorld world, string adult, string child) => world.PlanDependentCare(adult, new(DependentCareChoiceKind.Accept, child)) is { } plan && world.CheckDependentCarePlan(plan).Allowed
            ? new CognitionCandidate(plan.CandidateId, "Accept primary care of the child named in this order.", 0) { ActionPlan = plan }
            : null;

    internal static void ExecuteGuardianOrder(IGuardianOrdersWorld world, OwnerQueuedInstruction instruction, CognitionCandidate candidate)
    {
        var adult = instruction.TargetAgentId;
        var child = instruction.Task!.TargetAgentId!;
        if (candidate.Id != "guardian_accept:" + child || !world.CanAcceptGuardian(adult, child))
        {
            world.SetOrderStatus(instruction, OrderStatus.Blocked, world.GuardianOrderBlockedReason(adult, child));
            return;
        }
        world.ApplyDependentCareCandidate(adult, candidate.Id, candidate.ActionPlan);
        if (world.Society.GetAgent(child).PrimaryCaregiverId == adult && !world.NeedsCaregiver(child))
            world.CreditOrderEffect(instruction, Identity.Parse(world.GuardianOrderEffectId(adult, child)), 1);
        else
            world.SetOrderStatus(instruction, OrderStatus.Blocked, world.GuardianOrderBlockedReason(adult, child));
    }


    internal static string GuardianOrderBlockedReason(IGuardianOrdersWorld world, string adult, string child)
    {
        if (!world.IsAdult(adult)) return "Only an adult can become a guardian.";
        if (!world.NeedsCaregiver(child) || world.Agents[child].GuardianSearch is null)
            return "The named child is no longer looking for a guardian.";
        if (!world.ReadyForBriefInteraction(adult)) return "The adult needs to finish their current activity before accepting care.";
        return "Waiting for this adult's turn in the search for the child's guardian.";
    }

}
