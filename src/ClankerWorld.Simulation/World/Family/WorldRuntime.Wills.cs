using PendingWillDecision = ClankerWorld.Simulation.World.FamilyWillService.PendingWillDecision;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private readonly FamilyWillService familyWills = new();
    private IReadOnlyDictionary<string, FamilyWillService.PendingWillDecision> pendingWills => familyWills.Pending;
    private IReadOnlyDictionary<string, string> pendingWillCancellationReasons => familyWills.CancellationReasons;

    private void StartWillDecisions() =>
        familyWills.StartWillDecisions(FamilyWillWorld);

    private static string BoundedWillHeirKey(string prefix, string id, HashSet<string> occupiedKeys) =>
        FamilyWillService.BoundedWillHeirKey(prefix, id, occupiedKeys);

    /// <summary>
    /// Where each Town can keep goods an estate leaves it this tick. Only built
    /// when an estate is due, so ordinary ticks do no storage scan.
    /// </summary>
    private SocietyTownStore[]? TownStoresForDueEstates(long targetTick) =>
        FamilyWillService.TownStoresForDueEstates(FamilyWillWorld, targetTick);

    private SocietyDefaultEstateDivision[] DefaultEstateDivisionsForDueEstates(long targetTick) =>
        FamilyWillService.DefaultEstateDivisionsForDueEstates(FamilyWillWorld, targetTick);

    private void ProcessWillDecisions(
        IReadOnlyList<PendingWillDecision> completed, IReadOnlyList<string> activeIds,
        IReadOnlyDictionary<string, string> inactiveReasons,
        Func<PendingWillDecision, bool> providerIsCurrent) =>
        FamilyWillService.ProcessWillDecisions(FamilyWillWorld, completed, activeIds, inactiveReasons, providerIsCurrent);

    private bool IsWillDecisionProviderCurrent(PendingWillDecision pending) =>
        FamilyWillService.IsWillDecisionProviderCurrent(FamilyWillWorld, pending);

    private void CancelPendingWill(string estateId, string reason = "interrupted", bool underRuntimeGate = true) =>
        familyWills.CancelPendingWill(FamilyWillWorld, estateId, reason, underRuntimeGate);
}
