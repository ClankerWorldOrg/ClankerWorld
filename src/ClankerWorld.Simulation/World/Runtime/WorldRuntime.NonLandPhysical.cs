using ClankerWorld.Simulation.Kernel;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private const string NonLandRemedyPrefix = TownSystem.NonLandRemedyPrefix;
    private const string NonLandRelayPrefix = TownSystem.NonLandRelayPrefix;
    private (string TownId, string AgreementId, string TermId)? nonLandRemedySelection;
    // This runs at the native successful action, never by searching the public event log.
    // A relevant law makes the act reportable, not illegal; only an agent may make that allegation.
    private void RecordNonLandConduct(string actor, string kind, GridPoint position, string? targetId, string? itemKind, int quantity, string nativeActId) => townSystem.RecordNonLandConduct(civicWorld, actor, kind, position, targetId, itemKind, quantity, nativeActId);
    private bool NonLandEquipmentRepairPaused(string actor) => TownSystem.NonLandEquipmentRepairPaused(civicWorld, actor);
    private bool ApplyNonLandRemedyAction(TownRuntimeState town, string actor, string candidateId, ActionPlan? plan = null)
    {
        if (!TownSystem.TryPlanNonLandPhysical(civicWorld, town, actor, candidateId, plan, out var selected)) return false;
        if (selected is null) return true;
        return TryRegisteredAction(ActionPhase.FreshAdmission, new(actor, agents[actor], candidateId, false, selected));
    }
    private void RecordNonLandRepairCompletion(string actor, InventoryLot originalTarget, string repairedLotId, string nativeReceiptId, GridPoint position, IReadOnlyList<Identity> reservationIds) => townSystem.RecordNonLandRepairCompletion(civicWorld, actor, originalTarget, repairedLotId, nativeReceiptId, position, reservationIds);
    private void RecordNonLandGoodsCompletion(string actor, InventoryLot sourceLot, string recipientId, int quantity, string? storageBuildingId, string nativeReceiptId, string kind, long? carriedAvailableBefore = null) => townSystem.RecordNonLandGoodsCompletion(civicWorld, actor, sourceLot, recipientId, quantity, storageBuildingId, nativeReceiptId, kind, carriedAvailableBefore);
}
