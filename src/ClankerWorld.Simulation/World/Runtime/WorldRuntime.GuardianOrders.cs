using System.Security.Cryptography;
using System.Text;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private OrderTask? ParseGuardianOrder(string text, string actor)
    {
        const string prefix = "become guardian for ";
        if (!IsAdult(actor) || !text.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return null;
        var target = text[prefix.Length..].Trim();
        if (target.Length == 0) return null;
        var active = society.Checkpoint.Agents.Where(person => person.Status == SocietyAgentStatus.Active);
        // Bind the complete, unique name (or exact ID) once. Renames cannot
        // redirect a queued order, and leftover words are never guessed away.
        var named = active.Where(person => person.Id == target).ToArray();
        if (named.Length == 0 && AgentNameRules.CanonicalKey(target) is { } targetName)
            named = active.Where(person => string.Equals(AgentNameRules.CanonicalKey(person.Name), targetName,
                StringComparison.OrdinalIgnoreCase)).ToArray();
        if (named.Length != 1 || !NeedsCaregiver(named[0].Id) || agents[named[0].Id].GuardianSearch is null)
            return null;
        return new OrderTask(new AcceptGuardianshipOrderDefinition(TargetAgentId: (named[0].Id)!),
    "queued",
    1,
    0,
    "guardianships", false);
    }

    private CognitionCandidate? GuardianOrderCandidate(string adult, string child) =>
        CanAcceptGuardian(adult, child)
            ? new("guardian_accept:" + child, "Accept primary care of the child named in this order.", 0)
            : null;

    private void ExecuteGuardianOrder(OwnerQueuedInstruction instruction, CognitionCandidate candidate)
    {
        var adult = instruction.TargetAgentId;
        var child = instruction.Task!.TargetAgentId!;
        if (candidate.Id != "guardian_accept:" + child || !CanAcceptGuardian(adult, child))
        {
            SetOrderStatus(instruction, OrderStatus.Blocked, GuardianOrderBlockedReason(adult, child));
            return;
        }
        ApplyDependentCareCandidate(adult, candidate.Id);
        if (society.Checkpoint.GetAgent(child).PrimaryCaregiverId == adult && !NeedsCaregiver(child))
            CreditOrderEffect(instruction, Identity.Parse(GuardianOrderEffectId(adult, child)), 1);
        else
            SetOrderStatus(instruction, OrderStatus.Blocked, GuardianOrderBlockedReason(adult, child));
    }

    private string GuardianOrderBlockedReason(string adult, string child)
    {
        if (!IsAdult(adult)) return "Only an adult can become a guardian.";
        if (!NeedsCaregiver(child) || agents[child].GuardianSearch is null)
            return "The named child is no longer looking for a guardian.";
        if (!ReadyForBriefInteraction(adult)) return "The adult needs to finish their current activity before accepting care.";
        return "Waiting for this adult's turn in the search for the child's guardian.";
    }

    internal static string GuardianOrderEffectId(string adult, string child) =>
        DerivedId.Hash($"{adult}|{child}", "guardianship:");
}
