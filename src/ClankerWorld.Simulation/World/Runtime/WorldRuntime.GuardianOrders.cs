using System.Security.Cryptography;
using System.Text;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Society;

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

    private static string GuardianOrderEffectId(string adult, string child) =>
        DerivedId.Hash($"{adult}|{child}", "guardianship:");
}
