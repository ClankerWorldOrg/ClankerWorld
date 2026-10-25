using System.Collections.ObjectModel;
using System.Globalization;
using System.Text.Json;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;
using ClankerWorld.Simulation.Cognition;

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
        world.AppendEvent(allowed ? new MedicalCareAllowedEventPayload(patient, caregiver) : new MedicalCareRevokedEventPayload(patient, caregiver));
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
        world.PhysicalAgents.RecordMedicalTreatment(patient, person, new(caregiver, supply.Id, supply.OwnerId, Identity.Parse(id), kind, world.WorldTick, world.WorldTick,
                MedicalCareRules.TreatmentTicks));
        if (world.PhysicalAgents.Living[caregiver].MedicalSupplyTrip?.PatientId == patient)
            world.PhysicalAgents.RecordMedicalSupplyTrip(caregiver, world.PhysicalAgents.Living[caregiver], null);
        world.MarkCurrentSchema();
        world.AppendEvent(new MedicalTreatmentStartedEventPayload(patient, caregiver, supply.Id));
        return new(true);
    }

    // Keep the consumed reservation as the authoritative receipt. Closing its
    // medical purpose never releases or refunds goods, and permanently prevents
    // the spent effect from being reattached after permission is renewed.
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
                world.AppendEvent(new MedicalTreatmentInterruptedEventPayload(person.AgentId));
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
            if (remaining == 0) world.AppendEvent(new MedicalTreatmentCompletedEventPayload(person.AgentId));
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
    internal static ActorCapabilityPolicy GuardianTravelCapabilities(GuardianTravelKind kind) =>
        new(kind == GuardianTravelKind.Follow ? ActionLifeStages.Dependents : ActionLifeStages.Adults,
            AllowBoat: true, AllowUrgentFood: false, AllowUrgentWarmth: false, AllowActiveOrder: false);

    internal static void BeginGuardianPlacement(IFamilyGuardianWorld world, string child, string caregiver, string careRelationshipId)
    {
        var person = world.Society.GetAgent(child);
        var adult = world.Society.GetAgent(caregiver);
        if (person.HouseholdId == adult.HouseholdId && world.TownForResident(child) == world.TownForResident(caregiver))
            return;
        var care = world.Society.GetRelationship(careRelationshipId);
        SetGuardianPlacement(world, child, new(caregiver, care.Id, care.Revision, world.WorldTick, "collecting"));
        RefreshGuardianPlacement(world, child);
        world.AppendEvent(new GuardianPlacementPendingEventPayload(child));
    }

    internal static void SetGuardianPlacement(IFamilyGuardianWorld world, string child, GuardianPlacementState? placement)
    {
        if (!world.PhysicalAgents.Living.TryGetValue(child, out var physical) || physical.GuardianPlacement == placement) return;
        world.PhysicalAgents.RecordGuardianPlacement(child, physical, placement);
        world.MarkCurrentSchema();
    }

    internal static bool IsCurrentGuardianPlacement(IFamilyGuardianWorld world, string child, GuardianPlacementState placement) =>
        world.PhysicalAgents.Living.ContainsKey(child) && world.PhysicalAgents.Living.ContainsKey(placement.CaregiverId) &&
        world.Society.Agents.Any(person => person.Id == child &&
            person.Status == SocietyAgentStatus.Active &&
            LifeStageRules.IsDependent(person.AgeBand) &&
            person.PrimaryCaregiverId == placement.CaregiverId) &&
        world.Society.Agents.Any(person => person.Id == placement.CaregiverId &&
            person.Status == SocietyAgentStatus.Active && LifeStageRules.IsAdult(person.AgeBand)) &&
        world.Society.Relationships.Any(edge => edge.Id == placement.CareRelationshipId &&
            edge.Revision == placement.CareRevision && edge.Type == SocietyRelationshipType.Caregiver &&
            edge.State == SocietyRelationshipState.Accepted && edge.ProposerId == placement.CaregiverId && edge.TargetId == child);

    internal static void ReconcileGuardianPlacements(IFamilyGuardianWorld world)
    {
        foreach (var child in world.PhysicalAgents.Living.Values.Where(person => person.GuardianPlacement is not null)
                     .Select(person => person.AgentId).Order(StringComparer.Ordinal).ToArray())
            RefreshGuardianPlacement(world, child);
    }

    internal static void RefreshGuardianPlacement(IFamilyGuardianWorld world, string child)
    {
        if (!world.PhysicalAgents.Living.TryGetValue(child, out var physical) || physical.GuardianPlacement is not { } placement) return;
        if (!IsCurrentGuardianPlacement(world, child, placement))
        {
            SetGuardianPlacement(world, child, null);
            world.AppendEvent(new GuardianPlacementCancelledEventPayload(child));
            return;
        }
        var household = world.Society.GetAgent(placement.CaregiverId).HouseholdId;
        var town = world.TownForResident(placement.CaregiverId);
        var house = household is null || town is null ? null : world.HouseForHousehold(household);
        var changedHome = placement.DestinationHouseholdId != household || placement.DestinationTownId != town ||
            placement.HouseId != house?.InstanceId || placement.HouseDefinitionId != house?.DefinitionId ||
            placement.HousePlacedTick != house?.PlacedTick || placement.HousePosition != house?.Position;
        var refreshed = placement with
        {
            DestinationHouseholdId = household,
            DestinationTownId = town,
            HouseId = house?.InstanceId,
            HouseDefinitionId = house?.DefinitionId,
            HousePlacedTick = house?.PlacedTick,
            HousePosition = house?.Position,
            Stage = changedHome || house is null ? "collecting" : placement.Stage,
        };
        var homeBlocker = GuardianPlacementHomeBlocker(world, child, refreshed);
        var activityBlocker = GuardianPlacementActivityBlocker(world, child, refreshed);
        refreshed = refreshed with
        {
            Blocker = homeBlocker ?? activityBlocker ??
                (changedHome || IsGuardianHomeBlocker(placement.Blocker) ? null : placement.Blocker),
        };
        SetGuardianPlacement(world, child, refreshed);
    }

    internal static bool IsGuardianHomeBlocker(string? blocker) => blocker is not null &&
        (blocker.StartsWith("The guardian needs", StringComparison.Ordinal) ||
         blocker.StartsWith("The guardian's House", StringComparison.Ordinal) ||
         blocker.StartsWith("The guardian's home", StringComparison.Ordinal) ||
         blocker.StartsWith("The guardian is meeting", StringComparison.Ordinal) ||
         blocker.StartsWith("The guardian is finishing", StringComparison.Ordinal) ||
         blocker.StartsWith("The dependent is meeting", StringComparison.Ordinal));

    internal static string? GuardianPlacementActivityBlocker(IFamilyGuardianWorld world, string child, GuardianPlacementState placement)
    {
        var adult = world.PhysicalAgents.Living[placement.CaregiverId];
        if (world.NeedsUrgentFood(adult) || world.NeedsUrgentWarmth(adult))
            return "The guardian is meeting urgent food or warmth needs.";
        if (world.PendingInstructionFor(placement.CaregiverId) is not null || world.IsConversationBusy(placement.CaregiverId))
            return "The guardian is finishing another activity.";
        var dependent = world.PhysicalAgents.Living[child];
        if (placement.Stage == "escorting" && (world.NeedsUrgentFood(dependent) || world.NeedsUrgentWarmth(dependent) ||
            world.PendingInstructionFor(child) is not null || world.IsConversationBusy(child)))
            return "The dependent is meeting needs or finishing another activity.";
        return null;
    }

    internal static string? GuardianPlacementHomeBlocker(IFamilyGuardianWorld world, string child, GuardianPlacementState placement)
    {
        if (placement.DestinationHouseholdId is null) return "The guardian needs a household before the child can move in.";
        if (placement.DestinationTownId is null) return "The guardian needs a recorded Town membership before the child can move in.";
        if (placement.HouseId is null || placement.HousePosition is null)
            return "The guardian needs a completed House before the child can move in.";
        if (world.Society.GetAgent(placement.CaregiverId).HouseholdId != placement.DestinationHouseholdId ||
            world.TownForResident(placement.CaregiverId) != placement.DestinationTownId ||
            !world.Buildings.Any(house => house.InstanceId == placement.HouseId &&
                house.DefinitionId == placement.HouseDefinitionId && house.HouseholdId == placement.DestinationHouseholdId &&
                house.PlacedTick == placement.HousePlacedTick && house.Position == placement.HousePosition))
            return "The guardian's home changed; the journey needs a current destination.";
        return world.CanFitGuardianHousehold(placement.DestinationHouseholdId, placement.CaregiverId, child)
            ? null : "The guardian's House needs a free resident place.";
    }

    internal static bool HasGuardianPlacementTask(IFamilyGuardianWorld world, string actor) =>
        world.PhysicalAgents.Living.TryGetValue(actor, out var person) && person.GuardianPlacement is not null ||
        world.PhysicalAgents.Living.Values.Any(person => person.GuardianPlacement?.CaregiverId == actor);

    internal static string? GuardianPlacementChildFor(IFamilyGuardianWorld world, string caregiver)
    {
        var placements = world.PhysicalAgents.Living.Values
            .Where(person => person.GuardianPlacement is { } placement && placement.CaregiverId == caregiver &&
                IsCurrentGuardianPlacement(world, person.AgentId, placement) && GuardianPlacementHomeBlocker(world, person.AgentId, placement) is null)
            .OrderBy(person => person.GuardianPlacement!.StartedTick).ThenBy(person => person.AgentId, StringComparer.Ordinal).ToArray();
        var origin = world.PhysicalAgents.Living[caregiver].Position;
        // A blocked collection must not starve another accepted child of a move.
        // Keep an escort together, and retry the oldest blocked collection when
        // no reachable collection remains so its normal blocker stays visible.
        return (placements.FirstOrDefault(person => person.GuardianPlacement!.Stage == "escorting") ??
            placements.FirstOrDefault(person => world.IsWithinInteractionRange(origin, person.Position, world.ResourceInteractionRange) ||
            world.Reach(caregiver, person.Position, ReachPurpose.GuardianTravel, world.ResourceInteractionRange, 0, SwimmingPolicy.WhenCapable).Count > 0) ??
            placements.FirstOrDefault())?.AgentId;
    }

    internal static CognitionCandidate? GuardianPlacementCandidate(IFamilyGuardianWorld world, string actor)
    {
        if (!world.PhysicalAgents.Living.TryGetValue(actor, out var person)) return null;
        GuardianTravelChoice? choice = person.GuardianPlacement is { Stage: "escorting" } follow
            ? new(GuardianTravelKind.Follow, follow.CaregiverId)
            : world.IsAdult(actor) && GuardianPlacementChildFor(world, actor) is { } child ? new(GuardianTravelKind.Relocate, child) : null;
        if (choice is null || world.OfferGuardianTravel(actor, choice) is not { } plan) return null;
        return new CognitionCandidate(plan.CandidateId, choice.Kind == GuardianTravelKind.Follow
            ? "Follow your accepted guardian to their House."
            : $"Collect {world.Society.GetAgent(plan.ChildId).Name} and accompany them to your House.", 8, plan.Placement.HouseId)
        { ActionPlan = plan };
    }

    internal static bool ContinueGuardianPlacement(IFamilyGuardianWorld world, string actor)
    {
        if (world.Turn.Contains(actor) || world.PendingInstructionFor(actor) is not null ||
            !HasGuardianPlacementTask(world, actor) || world.IsConversationBusy(actor)) return false;
        var person = world.PhysicalAgents.Living[actor];
        if (world.NeedsUrgentFood(person) || world.NeedsUrgentWarmth(person))
        {
            // Infants still depend on actual nearby care; never grant them an adult food action.
            if (!LifeStageRules.IsInfant(world.Society.GetAgent(actor).AgeBand) &&
                world.UrgentSurvivalCandidateFor(actor, person) is { } urgent)
                world.ApplyCandidate(actor, person, urgent.Id, reportIdle: false);
            return true;
        }
        if (GuardianPlacementCandidate(world, actor) is not { } candidate) return false;
        world.Turn.RecordAction(actor);
        world.TryApplyGuardianPlacementCandidate(actor, candidate.Id, candidate.ActionPlan);
        return true;
    }

    internal static void EscortGuardianChild(IFamilyGuardianWorld world, string caregiver, string child)
    {
        if (!world.PhysicalAgents.Living.TryGetValue(child, out var dependent) || dependent.GuardianPlacement is not { } placement ||
            placement.CaregiverId != caregiver || !IsCurrentGuardianPlacement(world, child, placement) ||
            GuardianPlacementHomeBlocker(world, child, placement) is not null) return;
        var adult = world.PhysicalAgents.Living[caregiver];
        if (placement.Stage == "collecting")
        {
            if (!world.IsWithinInteractionRange(adult.Position, dependent.Position, world.ResourceInteractionRange))
            {
                MoveForGuardianPlacement(world, caregiver, child, dependent.Position, "collect_guardian_child", world.ResourceInteractionRange);
                return;
            }
            placement = placement with { Stage = "escorting", Blocker = null };
            SetGuardianPlacement(world, child, placement);
        }
        if (world.PendingInstructionFor(child) is not null || world.IsConversationBusy(child) ||
            world.NeedsUrgentFood(dependent) || world.NeedsUrgentWarmth(dependent))
        {
            SetGuardianPlacement(world, child, placement with { Blocker = "The dependent is meeting needs or finishing another activity." });
            if (world.NeedsUrgentFood(dependent) || world.NeedsUrgentWarmth(dependent))
            {
                PauseGuardianPlacementWork(world, caregiver);
                if (dependent.FullnessBasisPoints < NeedThresholds.FoodPickupFullness &&
                    world.CaregiverFoodCarryRequirement(caregiver, world.PhysicalAgents.Living[caregiver]) > world.FreeCarryCapacity(caregiver))
                    world.MakeRoomForFood(caregiver, world.PhysicalAgents.Living[caregiver]);
                else
                    world.CareForChild(caregiver, child);
            }
            return;
        }
        if (!world.IsWithinInteractionRange(adult.Position, dependent.Position, 2))
        {
            SetGuardianPlacement(world, child, placement with { Blocker = "The guardian is waiting for the child to catch up." });
            return;
        }
        MoveForGuardianPlacement(world, caregiver, child, placement.HousePosition!.Value, "escort_guardian_child");
    }

    internal static void MoveForGuardianPlacement(IFamilyGuardianWorld world, string actor, string child, GridPoint destination, string reason, int range = 0)
    {
        PauseGuardianPlacementWork(world, actor);
        var before = world.PhysicalAgents.Living[actor];
        world.MoveToward(actor, before, destination, reason, range);
        if (world.PhysicalAgents.Living[child].GuardianPlacement is { } placement)
            SetGuardianPlacement(world, child, placement with
            {
                Blocker = world.PhysicalAgents.Living[actor].MoveWaitTicks > before.MoveWaitTicks
                    ? "The journey is waiting for a clear walking route." : null,
            });
    }

    internal static void PauseGuardianPlacementWork(IFamilyGuardianWorld world, string actor)
    {
        // Release native field claims before leaving for the accepted care task.
        if (world.FarmWorkFor(actor) is { } field) world.CancelFarmWork(field);
        if (world.PhysicalAgents.Living[actor].Equipment?.Repair is not null) world.CancelEquipmentRepair(actor);
        if (world.PhysicalAgents.Living[actor].Project is { Stage: not ("completed" or "cancelled") } project)
            world.SetProject(actor, project with { Stage = "paused", Blocker = "Accompanying an accepted dependent." });
    }

    internal static void FollowPlacementGuardian(IFamilyGuardianWorld world, string child, GuardianTravelActionPlan? originalPlan = null)
    {
        if (!world.PhysicalAgents.Living.TryGetValue(child, out var dependent) || dependent.GuardianPlacement is not { Stage: "escorting" } placement) return;
        var plan = originalPlan ?? world.PlanGuardianTravel(child, new(GuardianTravelKind.Follow, placement.CaregiverId));
        if (plan is null || !world.CheckActionPlan(plan).Allowed) return;
        var adult = world.PhysicalAgents.Living[placement.CaregiverId];
        if (world.NeedsUrgentFood(adult) || world.NeedsUrgentWarmth(adult)) return;
        var home = placement.HousePosition!.Value;
        // Normal following stops beside the guardian; the final leg must enter
        // the actual House before household or Town membership changes.
        var destination = adult.Position == home ? home : adult.Position;
        MoveForGuardianPlacement(world, child, child, destination, "follow_guardian", adult.Position == home ? 0 : world.ResourceInteractionRange);
        CompleteGuardianPlacement(world, child);
    }

    internal static void AdvanceGuardianPlacementFollowers(IFamilyGuardianWorld world, IEnumerable<string> orderActors)
    {
        world.Turn.RecordActions(orderActors);
        foreach (var child in world.PhysicalAgents.Living.Values.Where(person => person.GuardianPlacement is { Stage: "escorting" })
                     .Select(person => person.AgentId).Order(StringComparer.Ordinal).ToArray())
        {
            if (!world.IsActionWithheld(child) && !world.Turn.Contains(child)) FollowPlacementGuardian(world, child);
            CompleteGuardianPlacement(world, child);
        }
    }

    internal static void CompleteGuardianPlacement(IFamilyGuardianWorld world, string child)
    {
        if (!world.PhysicalAgents.Living.TryGetValue(child, out var dependent) || dependent.GuardianPlacement is not { Stage: "escorting" } placement ||
            !IsCurrentGuardianPlacement(world, child, placement) || GuardianPlacementHomeBlocker(world, child, placement) is not null ||
            dependent.Position != placement.HousePosition ||
            !world.IsWithinInteractionRange(dependent.Position, world.PhysicalAgents.Living[placement.CaregiverId].Position, world.ResourceInteractionRange)) return;
        var formerHousehold = world.Society.GetAgent(child).HouseholdId;
        var result = world.PlaceDependentWithGuardian(placement.CaregiverId, child, placement.CareRelationshipId, placement.CareRevision, placement.DestinationHouseholdId!);
        if (result.CreatedId != child) return;
        if (formerHousehold is not null && formerHousehold != placement.DestinationHouseholdId)
            world.RestoreHouseholdDeliveries(child, formerHousehold, "guardian-placement");
        if (!world.MoveDependentToGuardianTown(child, placement.CaregiverId))
            throw new InvalidOperationException("A validated guardian placement could not settle the child's Town.");
        SetGuardianPlacement(world, child, null);
        world.SetHousing(child, (world.PhysicalAgents.Living[child].Housing ?? new()) with { Blocker = world.HousingBlocker(child) });
        world.AppendEvent(new GuardianPlacementCompletedEventPayload(child, placement.CaregiverId));
    }

    internal static bool CanEnterGuardianPlacementHouse(IFamilyGuardianWorld world, string child, PlacedBuilding house) =>
        world.PhysicalAgents.Living.TryGetValue(child, out var person) && person.GuardianPlacement is { Stage: "escorting" } placement &&
        IsCurrentGuardianPlacement(world, child, placement) && GuardianPlacementHomeBlocker(world, child, placement) is null &&
        house.InstanceId == placement.HouseId && house.DefinitionId == placement.HouseDefinitionId &&
        house.HouseholdId == placement.DestinationHouseholdId && house.PlacedTick == placement.HousePlacedTick &&
        house.Position == placement.HousePosition &&
        world.IsWithinInteractionRange(world.PhysicalAgents.Living[placement.CaregiverId].Position, house.Position, world.ResourceInteractionRange);

    internal static GuardianTravelActionPlan? PlanGuardianTravel(IFamilyGuardianWorld world, string actor, GuardianTravelChoice choice)
    {
        if (!world.PhysicalAgents.Living.ContainsKey(actor)) return null;
        var child = choice.Kind == GuardianTravelKind.Relocate ? choice.TargetId : actor;
        if (!world.PhysicalAgents.Living.TryGetValue(child, out var dependent) || dependent.GuardianPlacement is not { } placement ||
            !world.PhysicalAgents.Living.ContainsKey(placement.CaregiverId)) return null;
        var target = choice.Kind == GuardianTravelKind.Relocate
            ? placement.Stage == "collecting" ? dependent.Position : placement.HousePosition ?? dependent.Position
            : world.PhysicalAgents.Living[placement.CaregiverId].Position;
        var range = choice.Kind == GuardianTravelKind.Relocate
            ? placement.Stage == "collecting" ? world.ResourceInteractionRange : 0
            : target == placement.HousePosition ? 0 : world.ResourceInteractionRange;
        return new(actor, choice, child, placement, world.Reach(actor, target, ReachPurpose.GuardianTravel, range, 0, SwimmingPolicy.WhenCapable));
    }

    internal static ActionCheck CheckGuardianTravelPlan(IFamilyGuardianWorld world, GuardianTravelActionPlan plan, ActorCapabilityPolicy? policy = null)
    {
        var following = plan.Choice.Kind == GuardianTravelKind.Follow;
        var capability = ActorCapabilities.Check(world.CapabilitiesFor(plan.Actor), plan.CandidateId,
            policy ?? GuardianTravelCapabilities(plan.Choice.Kind));
        if (!capability.Allowed) return capability;
        if (!world.PhysicalAgents.Living.TryGetValue(plan.ChildId, out var child) || child.GuardianPlacement is not { } current ||
            current with { Stage = plan.Placement.Stage, Blocker = plan.Placement.Blocker } != plan.Placement ||
            !IsCurrentGuardianPlacement(world, plan.ChildId, current))
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected child's guardian, care receipt or home has changed");
        if (GuardianPlacementHomeBlocker(world, plan.ChildId, current) is { } homeBlocker)
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, homeBlocker);
        if (world.PendingInstructionFor(plan.Actor) is not null || world.IsConversationBusy(plan.Actor))
            return ActionCheck.Refuse(ActionRefusalReason.ActiveOrder, "they need to finish the other activity first");
        if (!following)
            return current.CaregiverId == plan.Actor && GuardianPlacementChildFor(world, plan.Actor) == plan.ChildId
                ? new() : ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "another accepted child has the earlier movement turn");
        if (plan.Actor != plan.ChildId || plan.Choice.TargetId != current.CaregiverId || current.Stage != "escorting" ||
            world.PendingInstructionFor(current.CaregiverId) is not null || world.IsConversationBusy(current.CaregiverId))
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected guardian is not ready for this escort");
        var adult = world.PhysicalAgents.Living[current.CaregiverId];
        return !world.NeedsUrgentFood(adult) && !world.NeedsUrgentWarmth(adult) ? new() :
            ActionCheck.Refuse(ActionRefusalReason.UrgentNeed, "the guardian is meeting urgent food or warmth needs");
    }

    internal static void ApplyCheckedGuardianTravel(IFamilyGuardianWorld world, GuardianTravelActionPlan plan)
    {
        // The selected people and home stay bound while collection/escort phases
        // advance. Following reaches the same guardian's current physical position.
        if (plan.Choice.Kind == GuardianTravelKind.Relocate) EscortGuardianChild(world, plan.Actor, plan.ChildId);
        else FollowPlacementGuardian(world, plan.ChildId, plan);
    }
    internal static void ValidateLocalAgentFamily(AgentState person)
    {
        if (person.Parenthood is { } plan)
        {
            if (string.IsNullOrWhiteSpace(plan.PartnerId) || plan.PartnerId == person.AgentId ||
                plan.Stage is not ("requested" or "preparing" or "completed" or "cancelled" or "postponed") ||
                plan.RequestedTick < 0 || plan.LastTransitionTick < plan.RequestedTick ||
                (ActiveParenthood(plan) || plan.Stage is "completed" or "postponed") &&
                    (plan.PrimaryCaregiverId != person.AgentId && plan.PrimaryCaregiverId != plan.PartnerId ||
                     string.IsNullOrWhiteSpace(plan.IntendedHouseholdId)) ||
                plan.Stage == "completed" && (string.IsNullOrWhiteSpace(plan.ChildId) || string.IsNullOrWhiteSpace(plan.BirthHouseholdId)) ||
                plan.Stage != "completed" && (plan.ChildId is not null || plan.BirthHouseholdId is not null))
                throw new InvalidDataException("Family parenthood has invalid local state.");
        }
        if (person.GuardianSearch is { } search && (search.Stage is not ("relatives" or "household" or "town") ||
            search.StartedTick < 0 || search.StageStartedTick < search.StartedTick || search.OfferedAdultIds is null ||
            search.OfferedAdultIds.Any(string.IsNullOrWhiteSpace) ||
            search.OfferedAdultIds.Distinct(StringComparer.Ordinal).Count() != search.OfferedAdultIds.Count))
            throw new InvalidDataException("Family guardian search has invalid local state.");
        if (person.GuardianPlacement is { } placement && (string.IsNullOrWhiteSpace(placement.CaregiverId) ||
            string.IsNullOrWhiteSpace(placement.CareRelationshipId) || placement.CareRevision < 1 || placement.StartedTick < 0 ||
            placement.Stage is not ("collecting" or "escorting")))
            throw new InvalidDataException("Family guardian placement has invalid local state.");
        if (person.MedicalConsent is { } consent && (consent.CaregiverIds.Count is < 1 or > MedicalCareRules.MaximumNamedCaregivers ||
            consent.CaregiverIds.Any(id => string.IsNullOrWhiteSpace(id) || id == person.AgentId) ||
            consent.CaregiverIds.Distinct(StringComparer.Ordinal).Count() != consent.CaregiverIds.Count))
            throw new InvalidDataException("Family medical consent has invalid local state.");
        if (person.MedicalTreatment is { } treatment && (treatment.Kind != "medicine" ||
            string.IsNullOrWhiteSpace(treatment.CaregiverId) || string.IsNullOrWhiteSpace(treatment.SupplyLotId) ||
            string.IsNullOrWhiteSpace(treatment.SupplyOwnerId) || !IdentityRules.Reference(treatment.DoseReservationId.Value) ||
            treatment.StartedTick < 0 || treatment.LastProcessedTick < treatment.StartedTick ||
            treatment.RemainingTicks is < 1 or > MedicalCareRules.TreatmentTicks ||
            treatment.RemainingTicks != MedicalCareRules.TreatmentTicks - (treatment.LastProcessedTick - treatment.StartedTick)))
            throw new InvalidDataException("Family medical treatment has invalid local state.");
        if (person.MedicalSupplyTrip is { } trip && (string.IsNullOrWhiteSpace(trip.PatientId) || trip.StartedTick < 0))
            throw new InvalidDataException("Family medical supply trip has invalid local state.");
    }
}
