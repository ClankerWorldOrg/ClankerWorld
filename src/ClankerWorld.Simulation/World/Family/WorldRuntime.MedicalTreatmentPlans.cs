
namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{

    private void ApplyMedicalTreatmentPlan(MedicalTreatmentActionPlan plan)
    {
        if (CheckActionPlan(plan).Refusal is { } refusal) { RefuseChoice(plan, refusal); return; }
        CompleteMedicalTreatment(plan.Actor, plan.Choice.PatientId, plan.Choice.ItemKind, society.Checkpoint.Inventory.GetLot(plan.StockId));
    }
}
