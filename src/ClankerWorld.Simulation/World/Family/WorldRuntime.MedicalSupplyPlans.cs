using ClankerWorld.Simulation.Cognition;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{

    private MedicalSupplyActionPlan? PlanMedicalSupply(string actor) =>
        FamilySystem.PlanMedicalSupply(FamilySupplyWorld, actor);

    private ActionCheck CheckMedicalSupplyPlan(MedicalSupplyActionPlan plan, ActorCapabilityPolicy? policy = null) =>
        FamilySystem.CheckMedicalSupplyPlan(FamilySupplyWorld, plan, policy);

    private void AddMedicalSupplyCandidate(List<CognitionCandidate> candidates, string actor)
    {
        if (MedicalSupplyDefinition.Offer(this, actor, new()) is not { } plan) return;
        candidates.Add(new(plan.CandidateId, plan is MedicalSupplyReturnPlan
            ? "Return with the medicine to where you last saw the consenting patient."
            : "Visit accessible care stock and collect one real dose of medicine.", 8,
            plan is MedicalSupplyPickupPlan pickup ? society.Checkpoint.Inventory.GetLot(pickup.Pickup.StockId).StorageBuildingId : plan.PatientId)
        { ActionPlan = plan });
    }

    private void ApplyMedicalSupplyPlan(MedicalSupplyActionPlan plan)
    {
        if (CheckActionPlan(plan).Refusal is { } refusal) { RefuseChoice(plan, refusal); return; }
        ApplyCheckedMedicalSupply(plan);
    }

    private void ApplyCheckedMedicalSupply(MedicalSupplyActionPlan plan) =>
        FamilySystem.ApplyCheckedMedicalSupply(FamilySupplyWorld, plan);
}
