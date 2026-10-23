
namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{

    private MedicalTreatmentActionPlan? PlanMedicalTreatment(string caregiver, MedicalTreatmentChoice choice,
        int minimumIllness = NeedThresholds.MedicalCareIllness, bool allowBoat = false) =>
        FamilySystem.PlanMedicalTreatment(FamilyMedicalWorld, caregiver, choice, minimumIllness, allowBoat);

    private ActionCheck CheckMedicalTreatmentPlan(MedicalTreatmentActionPlan plan, ActorCapabilityPolicy? policy = null) =>
        FamilySystem.CheckMedicalTreatmentPlan(FamilyMedicalWorld, plan, policy);

    private void ApplyMedicalTreatmentPlan(MedicalTreatmentActionPlan plan)
    {
        if (CheckActionPlan(plan).Refusal is { } refusal) { RefuseChoice(plan, refusal); return; }
        CompleteMedicalTreatment(plan.Actor, plan.Choice.PatientId, plan.Choice.ItemKind, society.Checkpoint.Inventory.GetLot(plan.StockId));
    }
}
