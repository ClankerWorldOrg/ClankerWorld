using PendingWillDecision = ClankerWorld.Simulation.World.FamilyWillService.PendingWillDecision;
using WillRequestContext = ClankerWorld.Simulation.World.FamilyWillService.WillRequestContext;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private readonly FamilyWillService familyWills = new();
    private IReadOnlyDictionary<string, FamilyWillService.PendingWillDecision> pendingWills => familyWills.Pending;
    private IReadOnlyDictionary<string, string> pendingWillCancellationReasons => familyWills.CancellationReasons;

    private void StartWillDecisions() =>
        familyWills.StartWillDecisions(FamilyWillWorld);

    /// <summary>
    /// The dead agent's own view for their will: what they owned, their family
    /// and household first among up to sixteen living people, and their Town
    /// when it has a Warehouse to keep inherited goods.
    /// </summary>
    private WillRequestContext CreateWillRequestContext(SocietyEstate estate) =>
        FamilyWillService.CreateWillRequestContext(FamilyWillWorld, estate);

    private static string WillDisplayKind(string kind, string itemKey) =>
        FamilyWillService.WillDisplayKind(kind, itemKey);

    private static string BoundedWillHeirKey(string prefix, string id, HashSet<string> occupiedKeys) =>
        FamilyWillService.BoundedWillHeirKey(prefix, id, occupiedKeys);

    /// <summary>A short label of how a living person was related to the dead agent, if at all.</summary>
    private static string? WillRelation(SocietyCheckpoint checkpoint, SocietyAgent deceased, SocietyAgent person) =>
        FamilyWillService.WillRelation(checkpoint, deceased, person);

    /// <summary>Towns with a Warehouse of their own, the only place a Town keeps inherited goods.</summary>
    private HashSet<string> TownsThatMayInherit() =>
        FamilyWillService.TownsThatMayInherit(FamilyWillWorld);

    private PlacedBuilding? TownInheritanceWarehouse(string townId) =>
        FamilyWillService.TownInheritanceWarehouse(FamilyWillWorld, townId);

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

    /// <summary>
    /// Maps a reply's keys back to the heirs and lots this request offered.
    /// Any key the request did not offer makes the whole division unusable.
    /// </summary>
    private static SocietyWillDirective? WillDirective(PendingWillDecision pending, CognitionWillChoice? choice) =>
        FamilyWillService.WillDirective(pending, choice);

    private void ResolveWillDefault(string estateId, string reason, string? finalWords = null) =>
        FamilyWillService.ResolveWillDefault(FamilyWillWorld, estateId, reason, finalWords);

    private void CancelPendingWill(string estateId, string reason = "interrupted", bool underRuntimeGate = true) =>
        familyWills.CancelPendingWill(FamilyWillWorld, estateId, reason, underRuntimeGate);
}
