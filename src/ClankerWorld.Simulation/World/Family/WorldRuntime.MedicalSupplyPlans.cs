using ClankerWorld.Simulation.Cognition;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private GoodsMatch? CarriedMedicalDose(string actor) => MedicalDoseStock(actor).FirstOrDefault(match =>
        match.Lot.OwnerId == actor && PersonalEquipmentRules.IsCarried(match.Lot, actor));

    private MedicalSupplyActionPlan? PlanMedicalSupply(string actor)
    {
        var trip = AuthorizedMedicalSupplyTrip(actor);
        var patient = trip?.PatientId ?? ObservableMedicalNeeds(actor).FirstOrDefault();
        if (patient is null) return null;
        var destination = trip?.LastSeenPosition ?? agents[patient].Position;
        if (trip is not null && CarriedMedicalDose(actor) is { } dose && !CanObserveMedicalPatient(actor, patient))
            return new MedicalSupplyReturnPlan(actor, patient, destination, trip, dose.Lot.Id, dose.Place,
                Reach(actor, destination, ReachPurpose.MedicalSupply, ResourceInteractionRange, 0, SwimmingPolicy.WhenCapable));
        if (HasCarriedOwnItem(actor, "medicine") || FreeCarryCapacity(actor) <= 0 ||
            SharedItem("medicine", actor, destination, ResourceInteractionRange) is not { ContainerLotId: null } stock) return null;
        var pickup = new PlannedEquipmentPickup(stock.Id, stock.OwnerId, stock.ItemKind,
            HouseholdStockPosition(stock), HouseholdStockInteractionRange(stock));
        return new MedicalSupplyPickupPlan(actor, patient, destination, trip, pickup,
            Reach(actor, pickup.Position, ReachPurpose.MedicalSupply, pickup.InteractionRange, 0, SwimmingPolicy.WhenCapable));
    }

    private ActionCheck CheckMedicalSupplyPlan(MedicalSupplyActionPlan plan, ActorCapabilityPolicy? policy = null)
    {
        var capability = ActorCapabilities.Check(CapabilitiesFor(plan.Actor), plan.CandidateId,
            policy ?? MedicalCareCapabilities);
        if (!capability.Allowed) return capability;
        if (!LivingMedicalAdult(plan.Actor) || !agents.TryGetValue(plan.PatientId, out var patient) ||
            !HasMedicalPermission(plan.Actor, plan.PatientId) || agents[plan.Actor].MedicalSupplyTrip != plan.OriginalTrip ||
            plan.OriginalTrip is null && (!ObservableMedicalNeeds(plan.Actor).Contains(plan.PatientId, StringComparer.Ordinal) ||
                patient.Position != plan.LastSeenPosition))
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected patient or recorded care trip is no longer available");
        if (plan is MedicalSupplyReturnPlan returning)
            return !CanObserveMedicalPatient(plan.Actor, plan.PatientId) &&
                RecheckGoods(new(GoodsUse.MedicalDose, plan.Actor, GoodsOwners.One(plan.Actor), GoodsKinds.One("medicine")), returning.StockId) is { } dose &&
                dose.Place.Kind == returning.Place.Kind && dose.Place.CarrierId == returning.Place.CarrierId
                ? new() : ActionCheck.Refuse(ActionRefusalReason.StockUnavailable, "the selected return dose is no longer carried or the patient is already nearby");
        var collecting = (MedicalSupplyPickupPlan)plan;
        if (HasCarriedOwnItem(plan.Actor, "medicine"))
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "they already carry medicine");
        var pickupCheck = CheckEquipmentPickup(plan.Actor, collecting.Pickup, "medicine dose", ReachPurpose.MedicalSupply);
        if (!pickupCheck.Allowed) return pickupCheck;
        var stock = society.Checkpoint.Inventory.GetLot(collecting.Pickup.StockId);
        return CanPrepareSharedPickup(plan.Actor, stock, plan.LastSeenPosition, ResourceInteractionRange)
            ? new() : ActionCheck.Refuse(ActionRefusalReason.OutOfReach, "they cannot carry the selected dose back to the last-seen patient position");
    }

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

    private void ApplyCheckedMedicalSupply(MedicalSupplyActionPlan plan)
    {
        BeginMedicalSupplyTrip(plan);
        if (plan is MedicalSupplyReturnPlan)
        { MoveToward(plan.Actor, agents[plan.Actor], plan.LastSeenPosition, "medical_patient", ResourceInteractionRange); return; }
        ApplyPlannedEquipmentPickup(plan.Actor, ((MedicalSupplyPickupPlan)plan).Pickup);
    }
}
