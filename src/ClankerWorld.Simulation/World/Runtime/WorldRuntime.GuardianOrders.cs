using ClankerWorld.Simulation.Cognition;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private OrderTask? ParseGuardianOrder(string text, string actor) =>
        OrderSystem.ParseGuardianOrder(GuardianOrdersWorld, text, actor);

    private CognitionCandidate? GuardianOrderCandidate(string adult, string child) =>
        OrderSystem.GuardianOrderCandidate(GuardianOrdersWorld, adult, child);

    private void ExecuteGuardianOrder(OwnerQueuedInstruction instruction, CognitionCandidate candidate) =>
        OrderSystem.ExecuteGuardianOrder(GuardianOrdersWorld, instruction, candidate);

    private string GuardianOrderBlockedReason(string adult, string child) =>
        OrderSystem.GuardianOrderBlockedReason(GuardianOrdersWorld, adult, child);

    internal static string GuardianOrderEffectId(string adult, string child) =>
        DerivedId.Hash($"{adult}|{child}", "guardianship:");
}
