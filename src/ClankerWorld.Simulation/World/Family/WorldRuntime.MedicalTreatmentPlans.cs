using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private const string MedicalSupplyFailure = "Bring usable, unreserved medicine; another household's stock requires an agreed purchase.";

    private ActionCheck CheckMedicalTreatmentTarget(string caregiver, string patient, int minimumIllness)
    {
        if (!agents.ContainsKey(caregiver) || !agents.TryGetValue(patient, out var person) || !LivingMedicalAdult(caregiver) ||
            !CanObserveMedicalPatient(caregiver, patient) || society.Checkpoint.GetAgent(patient).Status != SocietyAgentStatus.Active ||
            !HasMedicalPermission(caregiver, patient))
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "Provide care to yourself or an accepted patient beside you.");
        if (person.MedicalTreatment is not null)
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "A consumed dose is already being applied gradually.");
        return (person.Survival?.IllnessBasisPoints ?? 0) >= minimumIllness ? new() :
            ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "This patient does not need medicine.");
    }

    private MedicalTreatmentActionPlan? PlanMedicalTreatment(string caregiver, MedicalTreatmentChoice choice,
        int minimumIllness = NeedThresholds.MedicalCareIllness, bool allowBoat = false)
    {
        if (!CheckMedicalTreatmentTarget(caregiver, choice.PatientId, minimumIllness).Allowed || choice.ItemKind != "medicine" ||
            MedicalDoseStock(caregiver).FirstOrDefault() is not { } supply) return null;
        return new(caregiver, choice, supply.Lot.Id, supply.Lot.OwnerId, supply.Place, minimumIllness, allowBoat);
    }

    private ActionCheck CheckMedicalTreatmentPlan(MedicalTreatmentActionPlan plan, ActorCapabilityPolicy? policy = null)
    {
        var capability = ActorCapabilities.Check(CapabilitiesFor(plan.Actor), plan.CandidateId,
            policy ?? MedicalTreatmentCapabilities(plan.AllowBoat));
        if (!capability.Allowed) return capability;
        var target = CheckMedicalTreatmentTarget(plan.Actor, plan.Choice.PatientId, plan.MinimumIllness);
        if (!target.Allowed) return target;
        return RecheckGoods(new(GoodsUse.MedicalDose, plan.Actor, GoodsOwners.One(plan.StockOwner), GoodsKinds.One("medicine")), plan.StockId) is { } supply &&
            supply.Place == plan.Place && InventoryRules.CanReserve(InventoryIndex.For(society.Checkpoint.Inventory), supply.Lot,
                plan.StockOwner, 1, InventoryHoldRules.BorrowedBy(plan.Actor)) ? new() :
            ActionCheck.Refuse(ActionRefusalReason.StockUnavailable, MedicalSupplyFailure);
    }

    private void ApplyMedicalTreatmentPlan(MedicalTreatmentActionPlan plan)
    {
        if (CheckActionPlan(plan).Refusal is { } refusal) { RefuseChoice(plan, refusal); return; }
        CompleteMedicalTreatment(plan.Actor, plan.Choice.PatientId, plan.Choice.ItemKind, society.Checkpoint.Inventory.GetLot(plan.StockId));
    }
}
