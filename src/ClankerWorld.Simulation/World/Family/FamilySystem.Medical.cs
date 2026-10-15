using System.Collections.ObjectModel;
using System.Globalization;
using System.Text.Json;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

internal sealed partial class FamilySystem
{
    internal const string MedicalSupplyFailure = "Bring usable, unreserved medicine; another household's stock requires an agreed purchase.";
    internal static ActorCapabilityPolicy MedicalCareCapabilities => new(ActionLifeStages.Adults, AllowUrgentFood: true, AllowUrgentWarmth: true, AllowActiveOrder: true);
    internal static ActorCapabilityPolicy MedicalTreatmentCapabilities(bool allowBoat) => new(ActionLifeStages.Adults, AllowBoat: allowBoat, AllowUrgentFood: true, AllowUrgentWarmth: true, AllowActiveOrder: true);
    internal static bool LivingMedicalAdult(IFamilyMedicalWorld world, string actor) => world.IsAdult(actor) &&
        world.Society.GetAgent(actor).Status == SocietyAgentStatus.Active;

    internal static bool CanObserveMedicalPatient(IFamilyMedicalWorld world, string caregiver, string patient) =>
        world.PhysicalAgents.Living.TryGetValue(caregiver, out var actor) && world.PhysicalAgents.Living.TryGetValue(patient, out var other) &&
        (caregiver == patient || world.IsWithinInteractionRange(actor.Position, other.Position, world.ResourceInteractionRange));

    internal static bool HasMedicalPermission(IFamilyMedicalWorld world, string caregiver, string patient) =>
        MedicalPermission(world.Society, world.PhysicalAgents.Living[patient], caregiver);

    internal static InventoryLot? MedicalSupplyAtHand(IFamilyMedicalWorld world, string caregiver) => world.MedicalDoseStock(caregiver).FirstOrDefault()?.Lot;

    internal static IEnumerable<string> ObservableMedicalNeeds(IFamilyMedicalWorld world, string caregiver) => world.PhysicalAgents.Living.Values
        .Where(person => CanObserveMedicalPatient(world, caregiver, person.AgentId) &&
            HasMedicalPermission(world, caregiver, person.AgentId) && person.MedicalTreatment is null &&
            (person.Survival?.IllnessBasisPoints ?? 0) >= NeedThresholds.MedicalCareIllness)
        .OrderBy(person => person.AgentId == caregiver ? 0 : 1)
        .ThenBy(person => person.AgentId, StringComparer.Ordinal).Select(person => person.AgentId);

    internal static MedicalSupplyTrip? AuthorizedMedicalSupplyTrip(IFamilyMedicalWorld world, string caregiver) =>
        world.PhysicalAgents.Living[caregiver].MedicalSupplyTrip is { } trip && LivingMedicalAdult(world, caregiver) &&
        world.PhysicalAgents.Living.ContainsKey(trip.PatientId) && HasMedicalPermission(world, caregiver, trip.PatientId) ? trip : null;

    internal static void ReconcileMedicalSupplyTrips(IFamilyMedicalWorld world)
    {
        foreach (var person in world.PhysicalAgents.Living.Values.ToArray())
        {
            if (person.MedicalSupplyTrip is not { } trip) continue;
            var observed = CanObserveMedicalPatient(world, person.AgentId, trip.PatientId);
            if (AuthorizedMedicalSupplyTrip(world, person.AgentId) is null ||
                observed && !ObservableMedicalNeeds(world, person.AgentId).Contains(trip.PatientId, StringComparer.Ordinal) ||
                !observed && world.HasCarriedOwnItem(person.AgentId, "medicine") &&
                world.IsWithinInteractionRange(person.Position, trip.LastSeenPosition, world.ResourceInteractionRange))
                world.PhysicalAgents.RecordMedicalSupplyTrip(person.AgentId, person, null);
        }
    }

    internal static bool MedicalSupplyWanted(IFamilyMedicalWorld world, string actor, string kind) => kind == "medicine" && LivingMedicalAdult(world, actor) &&
        (ObservableMedicalNeeds(world, actor).Any() || AuthorizedMedicalSupplyTrip(world, actor) is not null) &&
        MedicalSupplyAtHand(world, actor) is null && world.SharedPreparationItem(kind, actor) is null;

    internal static void BeginMedicalSupplyTrip(IFamilyMedicalWorld world, MedicalSupplyActionPlan plan)
    {
        if (plan.OriginalTrip is null)
            world.PhysicalAgents.RecordMedicalSupplyTrip(plan.Actor, world.PhysicalAgents.Living[plan.Actor], new(plan.PatientId, plan.LastSeenPosition, world.WorldTick));
    }

    internal static bool MedicalPermission(SocietyCheckpoint checkpoint, AgentState patient,
        string caregiver)
    {
        var recipient = checkpoint.GetAgent(patient.AgentId);
        if (LifeStageRules.IsAdult(recipient.AgeBand))
            return caregiver == patient.AgentId ||
                patient.MedicalConsent?.CaregiverIds.Contains(caregiver, StringComparer.Ordinal) == true;
        return checkpoint.Relationships.Any(edge => edge.Type == SocietyRelationshipType.Caregiver &&
            edge.State == SocietyRelationshipState.Accepted && edge.EffectiveTick <= checkpoint.WorldTick &&
            edge.ProposerId == caregiver && edge.TargetId == patient.AgentId);
    }

    internal static string MedicalDosePurpose(string caregiver, string patient, string kind, string owner,
        long startedTick) => "medical_treatment:" + JsonSerializer.Serialize(new[]
        { caregiver, patient, kind, owner, startedTick.ToString(CultureInfo.InvariantCulture) });

    internal static void CompleteMedicalConsent(IFamilyTreatmentWorld world, MedicalConsentActionPlan plan)
    {
        var patient = plan.Actor;
        var caregiver = plan.Choice.CaregiverId;
        var allowed = plan.Choice.Allow;
        var person = world.PhysicalAgents.Living[patient];
        var ids = plan.ResultingCaregivers;
        if (!allowed && person.MedicalTreatment is { } interrupted && interrupted.CaregiverId == caregiver)
            CloseMedicalDose(world, patient, interrupted);
        world.PhysicalAgents.RecordMedicalConsent(patient, person, ids.Count == 0 ? null : new(ids), !allowed && person.MedicalTreatment?.CaregiverId == caregiver
                ? null : person.MedicalTreatment);
        world.MarkCurrentSchema();
        world.AppendEvent(allowed ? "medical_care_allowed" : "medical_care_revoked", $"{patient}:medical_caregiver:{caregiver}");
    }

    internal static MedicalCareResult TreatPatientCore(IFamilyTreatmentWorld world, string caregiver, string patient, string kind)
    {
        if (kind != "medicine") return new(false, "Injury treatment is not available yet; medicine supports illness recovery.");
        var target = CheckMedicalTreatmentTarget(world, caregiver, patient, 1);
        if (target.Refusal is { } refusal) return new(false, refusal.Explanation);
        var plan = PlanMedicalTreatment(world, caregiver, new(patient, kind), 1, allowBoat: true);
        if (plan is null) return new(false, MedicalSupplyFailure);
        var check = world.CheckActionPlan(plan);
        if (check.Refusal is { } unavailable) return new(false, unavailable.Explanation);
        return CompleteMedicalTreatment(world, caregiver, patient, kind, world.Society.Inventory.GetLot(plan.StockId));
    }

    internal static MedicalCareResult CompleteMedicalTreatment(IFamilyTreatmentWorld world, string caregiver, string patient, string kind, InventoryLot supply)
    {
        var person = world.PhysicalAgents.Living[patient];
        var id = world.MedicalDoseId(world.WorldTick, world.NextEventId, caregiver, patient);
        world.ConsumeMedicalDose(id, supply, caregiver, patient, kind);
        world.PhysicalAgents.RecordMedicalTreatment(patient, person, new(caregiver, supply.Id, supply.OwnerId, id, kind, world.WorldTick, world.WorldTick,
                MedicalCareRules.TreatmentTicks));
        if (world.PhysicalAgents.Living[caregiver].MedicalSupplyTrip?.PatientId == patient)
            world.PhysicalAgents.RecordMedicalSupplyTrip(caregiver, world.PhysicalAgents.Living[caregiver], null);
        world.MarkCurrentSchema();
        world.AppendEvent("medical_treatment_started", $"{patient}:medicine:{caregiver}:{supply.Id}");
        return new(true);
    }

    internal static void CloseMedicalDose(IFamilyTreatmentWorld world, string patient, MedicalTreatmentState treatment)
    {
        var purpose = MedicalDosePurpose(treatment.CaregiverId, patient, treatment.Kind,
            treatment.SupplyOwnerId, treatment.StartedTick);
        world.CloseMedicalDoseReceipt(treatment.DoseReservationId, purpose);
    }

    internal static void AdvanceMedicalTreatments(IFamilyTreatmentWorld world)
    {
        foreach (var person in world.PhysicalAgents.Living.Values.OrderBy(person => person.AgentId, StringComparer.Ordinal).ToArray())
        {
            if (person.MedicalTreatment is not { } treatment) continue;
            if (world.Society.GetAgent(person.AgentId).Status != SocietyAgentStatus.Active ||
                !LivingMedicalAdult(world, treatment.CaregiverId) || !HasMedicalPermission(world, treatment.CaregiverId, person.AgentId))
            {
                CloseMedicalDose(world, person.AgentId, treatment);
                world.PhysicalAgents.RecordMedicalTreatment(person.AgentId, person, null);
                world.AppendEvent("medical_treatment_interrupted", person.AgentId);
                continue;
            }
            if (treatment.LastProcessedTick >= world.WorldTick) continue;
            var survival = person.Survival ?? new();
            var remaining = treatment.RemainingTicks - 1;
            if (remaining == 0) CloseMedicalDose(world, person.AgentId, treatment);
            world.PhysicalAgents.RecordMedicalRecovery(person.AgentId, person, survival with
            {
                IllnessBasisPoints = Math.Max(0,
                    survival.IllnessBasisPoints - MedicalCareRules.MedicineIllnessPerTick)
            }, remaining == 0 ? null : treatment with
            { LastProcessedTick = world.WorldTick, RemainingTicks = remaining });
            if (remaining == 0) world.AppendEvent("medical_treatment_completed", person.AgentId);
        }
    }

    internal static ReadOnlyCollection<string> MedicalConsentResult(IFamilyMedicalWorld world, string patient, MedicalConsentChoice choice) =>
        Array.AsReadOnly((world.PhysicalAgents.Living[patient].MedicalConsent?.CaregiverIds ?? []).Where(id =>
                id != choice.CaregiverId && (!choice.Allow || LivingMedicalAdult(world, id)))
            .Concat(choice.Allow ? [choice.CaregiverId] : Array.Empty<string>()).Order(StringComparer.Ordinal).ToArray());

    internal static MedicalConsentActionPlan PlanMedicalConsent(IFamilyMedicalWorld world, string patient, MedicalConsentChoice choice)
    {
        var person = world.PhysicalAgents.Living[patient];
        return new(patient, choice, Array.AsReadOnly((person.MedicalConsent?.CaregiverIds ?? []).ToArray()),
            MedicalConsentResult(world, patient, choice), !choice.Allow && person.MedicalTreatment?.CaregiverId == choice.CaregiverId
                ? person.MedicalTreatment.DoseReservationId : null);
    }

    internal static ActionCheck CheckMedicalConsentPlan(IFamilyMedicalWorld world, MedicalConsentActionPlan plan, ActorCapabilityPolicy? policy = null)
    {
        var capability = ActorCapabilities.Check(world.CapabilitiesFor(plan.Actor), plan.CandidateId,
            policy ?? MedicalCareCapabilities);
        if (!capability.Allowed) return capability;
        if (world.PendingInstructionFor(plan.Actor)?.Kind == OwnerInstructionKind.Order)
            return ActionCheck.Refuse(ActionRefusalReason.ActiveOrder, "their current order comes first");
        if (!LivingMedicalAdult(world, plan.Actor) || plan.Choice.CaregiverId == plan.Actor)
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected medical permission is no longer available");
        var person = world.PhysicalAgents.Living[plan.Actor];
        var existing = person.MedicalConsent?.CaregiverIds ?? [];
        if (!existing.SequenceEqual(plan.OriginalCaregivers) || existing.Contains(plan.Choice.CaregiverId, StringComparer.Ordinal) == plan.Choice.Allow ||
            !MedicalConsentResult(world, plan.Actor, plan.Choice).SequenceEqual(plan.ResultingCaregivers))
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected medical permissions have changed");
        if (plan.Choice.Allow && (!world.PhysicalAgents.Living.ContainsKey(plan.Choice.CaregiverId) || !LivingMedicalAdult(world, plan.Choice.CaregiverId) ||
            !CanObserveMedicalPatient(world, plan.Actor, plan.Choice.CaregiverId)))
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected caregiver is no longer beside them");
        if (plan.ResultingCaregivers.Count > MedicalCareRules.MaximumNamedCaregivers)
            return ActionCheck.Refuse(ActionRefusalReason.Capacity, "there is no room for another named caregiver");
        var dose = !plan.Choice.Allow && person.MedicalTreatment?.CaregiverId == plan.Choice.CaregiverId
            ? person.MedicalTreatment.DoseReservationId : null;
        return dose == plan.InterruptedDoseId ? new() :
            ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected caregiver's medicine dose has changed");
    }

    internal static ActionCheck CheckMedicalTreatmentTarget(IFamilyMedicalWorld world, string caregiver, string patient, int minimumIllness)
    {
        if (!world.PhysicalAgents.Living.ContainsKey(caregiver) || !world.PhysicalAgents.Living.TryGetValue(patient, out var person) || !LivingMedicalAdult(world, caregiver) ||
            !CanObserveMedicalPatient(world, caregiver, patient) || world.Society.GetAgent(patient).Status != SocietyAgentStatus.Active ||
            !HasMedicalPermission(world, caregiver, patient))
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "Provide care to yourself or an accepted patient beside you.");
        if (person.MedicalTreatment is not null)
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "A consumed dose is already being applied gradually.");
        return (person.Survival?.IllnessBasisPoints ?? 0) >= minimumIllness ? new() :
            ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "This patient does not need medicine.");
    }

    internal static MedicalTreatmentActionPlan? PlanMedicalTreatment(IFamilyMedicalWorld world, string caregiver, MedicalTreatmentChoice choice,
        int minimumIllness = NeedThresholds.MedicalCareIllness, bool allowBoat = false)
    {
        if (!CheckMedicalTreatmentTarget(world, caregiver, choice.PatientId, minimumIllness).Allowed || choice.ItemKind != "medicine" ||
            world.MedicalDoseStock(caregiver).FirstOrDefault() is not { } supply) return null;
        return new(caregiver, choice, supply.Lot.Id, supply.Lot.OwnerId, supply.Place, minimumIllness, allowBoat);
    }

    internal static ActionCheck CheckMedicalTreatmentPlan(IFamilyMedicalWorld world, MedicalTreatmentActionPlan plan, ActorCapabilityPolicy? policy = null)
    {
        var capability = ActorCapabilities.Check(world.CapabilitiesFor(plan.Actor), plan.CandidateId,
            policy ?? MedicalTreatmentCapabilities(plan.AllowBoat));
        if (!capability.Allowed) return capability;
        var target = CheckMedicalTreatmentTarget(world, plan.Actor, plan.Choice.PatientId, plan.MinimumIllness);
        if (!target.Allowed) return target;
        return world.RecheckGoods(new(GoodsUse.MedicalDose, plan.Actor, GoodsOwners.One(plan.StockOwner), GoodsKinds.One("medicine")), plan.StockId) is { } supply &&
            supply.Place == plan.Place && InventoryRules.CanReserve(InventoryIndex.For(world.Society.Inventory), supply.Lot,
                plan.StockOwner, 1, InventoryHoldRules.BorrowedBy(plan.Actor)) ? new() :
            ActionCheck.Refuse(ActionRefusalReason.StockUnavailable, MedicalSupplyFailure);
    }

    internal static GoodsMatch? CarriedMedicalDose(IFamilySupplyWorld world, string actor) => world.MedicalDoseStock(actor).FirstOrDefault(match =>
        match.Lot.OwnerId == actor && PersonalEquipmentRules.IsCarried(match.Lot, actor));

    internal static MedicalSupplyActionPlan? PlanMedicalSupply(IFamilySupplyWorld world, string actor)
    {
        var trip = AuthorizedMedicalSupplyTrip(world, actor);
        var patient = trip?.PatientId ?? ObservableMedicalNeeds(world, actor).FirstOrDefault();
        if (patient is null) return null;
        var destination = trip?.LastSeenPosition ?? world.PhysicalAgents.Living[patient].Position;
        if (trip is not null && CarriedMedicalDose(world, actor) is { } dose && !CanObserveMedicalPatient(world, actor, patient))
            return new MedicalSupplyReturnPlan(actor, patient, destination, trip, dose.Lot.Id, dose.Place,
                world.Reach(actor, destination, ReachPurpose.MedicalSupply, world.ResourceInteractionRange, 0, SwimmingPolicy.WhenCapable));
        if (world.HasCarriedOwnItem(actor, "medicine") || world.FreeCarryCapacity(actor) <= 0 ||
            world.SharedItem("medicine", actor, destination, world.ResourceInteractionRange) is not { ContainerLotId: null } stock) return null;
        var pickup = new PlannedEquipmentPickup(stock.Id, stock.OwnerId, stock.ItemKind,
            world.HouseholdStockPosition(stock), world.HouseholdStockInteractionRange(stock));
        return new MedicalSupplyPickupPlan(actor, patient, destination, trip, pickup,
            world.Reach(actor, pickup.Position, ReachPurpose.MedicalSupply, pickup.InteractionRange, 0, SwimmingPolicy.WhenCapable));
    }

    internal static ActionCheck CheckMedicalSupplyPlan(IFamilySupplyWorld world, MedicalSupplyActionPlan plan, ActorCapabilityPolicy? policy = null)
    {
        var capability = ActorCapabilities.Check(world.CapabilitiesFor(plan.Actor), plan.CandidateId,
            policy ?? MedicalCareCapabilities);
        if (!capability.Allowed) return capability;
        if (!LivingMedicalAdult(world, plan.Actor) || !world.PhysicalAgents.Living.TryGetValue(plan.PatientId, out var patient) ||
            !HasMedicalPermission(world, plan.Actor, plan.PatientId) || world.PhysicalAgents.Living[plan.Actor].MedicalSupplyTrip != plan.OriginalTrip ||
            plan.OriginalTrip is null && (!ObservableMedicalNeeds(world, plan.Actor).Contains(plan.PatientId, StringComparer.Ordinal) ||
                patient.Position != plan.LastSeenPosition))
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected patient or recorded care trip is no longer available");
        if (plan is MedicalSupplyReturnPlan returning)
            return !CanObserveMedicalPatient(world, plan.Actor, plan.PatientId) &&
                world.RecheckGoods(new(GoodsUse.MedicalDose, plan.Actor, GoodsOwners.One(plan.Actor), GoodsKinds.One("medicine")), returning.StockId) is { } dose &&
                dose.Place.Kind == returning.Place.Kind && dose.Place.CarrierId == returning.Place.CarrierId
                ? new() : ActionCheck.Refuse(ActionRefusalReason.StockUnavailable, "the selected return dose is no longer carried or the patient is already nearby");
        var collecting = (MedicalSupplyPickupPlan)plan;
        if (world.HasCarriedOwnItem(plan.Actor, "medicine"))
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "they already carry medicine");
        var pickupCheck = world.CheckEquipmentPickup(plan.Actor, collecting.Pickup, "medicine dose", ReachPurpose.MedicalSupply);
        if (!pickupCheck.Allowed) return pickupCheck;
        var stock = world.Society.Inventory.GetLot(collecting.Pickup.StockId);
        return world.CanPrepareSharedPickup(plan.Actor, stock, plan.LastSeenPosition, world.ResourceInteractionRange)
            ? new() : ActionCheck.Refuse(ActionRefusalReason.OutOfReach, "they cannot carry the selected dose back to the last-seen patient position");
    }

    internal static void ApplyCheckedMedicalSupply(IFamilySupplyWorld world, MedicalSupplyActionPlan plan)
    {
        BeginMedicalSupplyTrip(world, plan);
        if (plan is MedicalSupplyReturnPlan)
        { world.MoveToward(plan.Actor, world.PhysicalAgents.Living[plan.Actor], plan.LastSeenPosition, "medical_patient", world.ResourceInteractionRange); return; }
        world.ApplyPlannedEquipmentPickup(plan.Actor, ((MedicalSupplyPickupPlan)plan).Pickup);
    }
}
