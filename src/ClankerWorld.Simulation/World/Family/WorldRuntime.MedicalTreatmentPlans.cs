using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private const string MedicalSupplyFailure = "Bring usable, unreserved medicine; another household's stock requires an agreed purchase.";

    private ActionCheck CheckMedicalTreatmentTarget(string caregiver, string patient, int minimumIllness) =>
        FamilySystem.CheckMedicalTreatmentTarget(FamilyMedicalWorld, caregiver, patient, minimumIllness);

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
