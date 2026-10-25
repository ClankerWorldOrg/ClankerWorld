using PendingWillDecision = ClankerWorld.Simulation.World.FamilyWillService.PendingWillDecision;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private readonly FamilyWillService familyWills = new();
    private IReadOnlyDictionary<string, FamilyWillService.PendingWillDecision> pendingWills => familyWills.Pending;
    private IReadOnlyDictionary<string, string> pendingWillCancellationReasons => familyWills.CancellationReasons;

    private static string BoundedWillHeirKey(string prefix, string id, HashSet<string> occupiedKeys) =>
        FamilyWillService.BoundedWillHeirKey(prefix, id, occupiedKeys);

    private bool IsWillDecisionProviderCurrent(PendingWillDecision pending) =>
        FamilyWillService.IsWillDecisionProviderCurrent(FamilyWillWorld, pending);
}
