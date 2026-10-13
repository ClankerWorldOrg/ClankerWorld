using System.Globalization;
using System.Text.Json;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private const string MedicalAllowPrefix = "medical_allow:";
    private const string MedicalRevokePrefix = "medical_revoke:";
    private const string MedicalTreatPrefix = "medical_treat:";
    private const string MedicalCollectPrefix = "medical_collect:";

    private bool LivingMedicalAdult(string actor) => IsAdult(actor) &&
        society.Checkpoint.GetAgent(actor).Status == SocietyAgentStatus.Active;

    private bool CanObserveMedicalPatient(string caregiver, string patient) =>
        agents.TryGetValue(caregiver, out var actor) && agents.TryGetValue(patient, out var other) &&
        (caregiver == patient || IsWithinInteractionRange(actor.Position, other.Position, ResourceInteractionRange));

    private bool HasMedicalPermission(string caregiver, string patient) =>
        MedicalPermission(society.Checkpoint, agents[patient], caregiver);

    private static bool MedicalPermission(SocietyCheckpoint checkpoint, AgentState patient,
        string caregiver)
    {
        var recipient = checkpoint.GetAgent(patient.AgentId);
        if (recipient.AgeBand is SocietyAgeBand.Adult or SocietyAgeBand.Elder)
            return caregiver == patient.AgentId ||
                patient.MedicalConsent?.CaregiverIds.Contains(caregiver, StringComparer.Ordinal) == true;
        return checkpoint.Relationships.Any(edge => edge.Type == SocietyRelationshipType.Caregiver &&
            edge.State == SocietyRelationshipState.Accepted && edge.EffectiveTick <= checkpoint.WorldTick &&
            edge.ProposerId == caregiver && edge.TargetId == patient.AgentId);
    }

    private static string MedicalDosePurpose(string caregiver, string patient, string kind, string owner,
        long startedTick) => "medical_treatment:" + JsonSerializer.Serialize(new[]
        { caregiver, patient, kind, owner, startedTick.ToString(CultureInfo.InvariantCulture) });

    // Permission is handled only at fresh admission. Continuing intentions and
    // ApplyMedicalCandidate deliberately have no permission-changing branch.
    private bool ApplyMedicalConsentDecision(SocietyCognitionDispatchResult decision)
    {
        var selected = decision.Admission.Intention;
        if (selected is null || !(selected.CandidateId.StartsWith(MedicalAllowPrefix, StringComparison.Ordinal) ||
            selected.CandidateId.StartsWith(MedicalRevokePrefix, StringComparison.Ordinal))) return false;
        if (PendingInstructionFor(decision.AgentId)?.Kind == OwnerInstructionKind.Order) return false;
        if (!decision.Admission.Accepted || decision.Admission.FellBack ||
            selected.Provider != DecisionProviderKind.LargeLanguageModel ||
            selected.AgentId != decision.AgentId) return true;
        var allowed = selected.CandidateId.StartsWith(MedicalAllowPrefix, StringComparison.Ordinal);
        var caregiver = selected.CandidateId[(allowed ? MedicalAllowPrefix.Length : MedicalRevokePrefix.Length)..];
        ChangeMedicalConsent(decision.AgentId, caregiver, allowed);
        return true;
    }

    private void ChangeMedicalConsent(string patient, string caregiver, bool allowed)
    {
        if (!LivingMedicalAdult(patient) || patient == caregiver) return;
        var person = agents[patient];
        var existing = person.MedicalConsent?.CaregiverIds ?? [];
        if (allowed)
        {
            if (!LivingMedicalAdult(caregiver) || !CanObserveMedicalPatient(patient, caregiver) ||
                existing.Contains(caregiver, StringComparer.Ordinal)) return;
        }
        else if (!existing.Contains(caregiver, StringComparer.Ordinal)) return;
        var ids = existing.Where(id => id != caregiver && (!allowed || LivingMedicalAdult(id))).ToList();
        if (allowed)
        {
            if (ids.Count >= MedicalCareRules.MaximumNamedCaregivers) return;
            ids.Add(caregiver);
        }
        if (!allowed && person.MedicalTreatment is { } interrupted && interrupted.CaregiverId == caregiver)
            CloseMedicalDose(patient, interrupted);
        agents[patient] = person with
        {
            MedicalConsent = ids.Count == 0 ? null : new(ids.Order(StringComparer.Ordinal).ToArray()),
            MedicalTreatment = !allowed && person.MedicalTreatment?.CaregiverId == caregiver
                ? null : person.MedicalTreatment,
        };
        checkpointSchemaVersion = StateSchemaVersion;
        AppendEvent(allowed ? "medical_care_allowed" : "medical_care_revoked", $"{patient}:medical_caregiver:{caregiver}");
    }

    private InventoryLot? MedicalSupplyAtHand(string caregiver) => society.Checkpoint.Inventory.Lots
        .Where(lot => lot.ItemKind == "medicine" && lot.ContainerLotId is null && lot.GroundPosition is null &&
            lot.DeliveryBuildingId is null && AvailableLotQuantity(lot) > 0 &&
            // Borrowed or in-transit goods are only carried, not owned, so they are never a caregiver's dose.
            (lot.OwnerId == caregiver && PersonalEquipmentRules.IsCarried(lot, caregiver) ||
             lot.OwnerId == HouseholdFor(caregiver) && lot.StorageBuildingId is { } buildingId &&
             worldSimulation.Buildings.Any(building => building.InstanceId == buildingId &&
                 building.HouseholdId == lot.OwnerId) &&
             IsWithinInteractionRange(agents[caregiver].Position, HouseholdStockPosition(lot),
                 HouseholdStockInteractionRange(lot))))
        .OrderBy(lot => lot.OwnerId == caregiver ? 0 : 1)
        .ThenBy(lot => lot.Id, StringComparer.Ordinal).FirstOrDefault();

    public MedicalCareResult TreatPatient(string caregiver, string patient, string kind)
    {
        gate.Wait();
        try { return TreatPatientCore(caregiver, patient, kind); }
        finally { gate.Release(); }
    }

    private MedicalCareResult TreatPatientCore(string caregiver, string patient, string kind)
    {
        if (kind != "medicine") return new(false, "Injury treatment is not available yet; medicine supports illness recovery.");
        if (!LivingMedicalAdult(caregiver) || !CanObserveMedicalPatient(caregiver, patient) ||
            society.Checkpoint.GetAgent(patient).Status != SocietyAgentStatus.Active ||
            !HasMedicalPermission(caregiver, patient))
            return new(false, "Provide care to yourself or an accepted patient beside you.");
        var person = agents[patient];
        if (person.MedicalTreatment is not null) return new(false, "A consumed dose is already being applied gradually.");
        if ((person.Survival?.IllnessBasisPoints ?? 0) == 0) return new(false, "This patient does not need medicine.");
        if (MedicalSupplyAtHand(caregiver) is not { } supply)
            return new(false, "Bring usable, unreserved medicine; another household's stock requires an agreed purchase.");
        var id = MedicalDoseId(WorldTick, nextEventId, caregiver, patient);
        ApplyInventoryTransition(inventory => InventoryAuthority.ConsumeReservation(InventoryAuthority.Reserve(inventory,
            id, supply.OwnerId, supply.Id, 1,
            MedicalDosePurpose(caregiver, patient, kind, supply.OwnerId, WorldTick), WorldTick), id));
        agents[patient] = person with
        {
            MedicalTreatment = new(caregiver, supply.Id, supply.OwnerId, Identity.Parse(id), kind, WorldTick, WorldTick,
                MedicalCareRules.TreatmentTicks),
        };
        if (agents[caregiver].MedicalSupplyTrip?.PatientId == patient)
            agents[caregiver] = agents[caregiver] with { MedicalSupplyTrip = null };
        checkpointSchemaVersion = StateSchemaVersion;
        AppendEvent("medical_treatment_started", $"{patient}:medicine:{caregiver}:{supply.Id}");
        return new(true);
    }

    // Keep the consumed reservation as the authoritative receipt. Closing its
    // medical purpose never releases or refunds goods, and permanently prevents
    // the spent effect from being reattached after permission is renewed.
    private void CloseMedicalDose(string patient, MedicalTreatmentState treatment)
    {
        var purpose = MedicalDosePurpose(treatment.CaregiverId, patient, treatment.Kind,
            treatment.SupplyOwnerId, treatment.StartedTick);
        ApplyInventoryTransition(inventory =>
        {
            var receipt = inventory.GetReservation(treatment.DoseReservationId);
            if (receipt.State != InventoryReservationState.Completed || receipt.Purpose != purpose)
                throw new InvalidDataException("Only the original consumed medical dose can be closed.");
            return inventory with
            {
                Reservations = inventory.Reservations.Select(item => item.Id == receipt.Id
                    ? item with { Purpose = purpose + ":closed" } : item).ToArray(),
            };
        });
    }

    private void AdvanceMedicalTreatments()
    {
        foreach (var person in agents.Values.OrderBy(person => person.AgentId, StringComparer.Ordinal).ToArray())
        {
            if (person.MedicalTreatment is not { } treatment) continue;
            if (society.Checkpoint.GetAgent(person.AgentId).Status != SocietyAgentStatus.Active ||
                !LivingMedicalAdult(treatment.CaregiverId) || !HasMedicalPermission(treatment.CaregiverId, person.AgentId))
            {
                CloseMedicalDose(person.AgentId, treatment);
                agents[person.AgentId] = person with { MedicalTreatment = null };
                AppendEvent("medical_treatment_interrupted", person.AgentId);
                continue;
            }
            if (treatment.LastProcessedTick >= WorldTick) continue;
            var survival = person.Survival ?? new();
            var remaining = treatment.RemainingTicks - 1;
            if (remaining == 0) CloseMedicalDose(person.AgentId, treatment);
            agents[person.AgentId] = person with
            {
                Survival = survival with
                {
                    IllnessBasisPoints = Math.Max(0,
                    survival.IllnessBasisPoints - MedicalCareRules.MedicineIllnessPerTick)
                },
                MedicalTreatment = remaining == 0 ? null : treatment with
                { LastProcessedTick = WorldTick, RemainingTicks = remaining },
            };
            if (remaining == 0) AppendEvent("medical_treatment_completed", person.AgentId);
        }
    }

    private IEnumerable<string> ObservableMedicalNeeds(string caregiver) => agents.Values
        .Where(person => CanObserveMedicalPatient(caregiver, person.AgentId) &&
            HasMedicalPermission(caregiver, person.AgentId) && person.MedicalTreatment is null &&
            (person.Survival?.IllnessBasisPoints ?? 0) >= 2_500)
        .OrderBy(person => person.AgentId == caregiver ? 0 : 1)
        .ThenBy(person => person.AgentId, StringComparer.Ordinal).Select(person => person.AgentId);

    private MedicalSupplyTrip? AuthorizedMedicalSupplyTrip(string caregiver) =>
        agents[caregiver].MedicalSupplyTrip is { } trip && LivingMedicalAdult(caregiver) &&
        agents.ContainsKey(trip.PatientId) && HasMedicalPermission(caregiver, trip.PatientId) ? trip : null;

    private void ReconcileMedicalSupplyTrips()
    {
        foreach (var person in agents.Values.ToArray())
        {
            if (person.MedicalSupplyTrip is not { } trip) continue;
            var observed = CanObserveMedicalPatient(person.AgentId, trip.PatientId);
            if (AuthorizedMedicalSupplyTrip(person.AgentId) is null ||
                observed && !ObservableMedicalNeeds(person.AgentId).Contains(trip.PatientId, StringComparer.Ordinal) ||
                !observed && HasCarriedOwnItem(person.AgentId, "medicine") &&
                IsWithinInteractionRange(person.Position, trip.LastSeenPosition, ResourceInteractionRange))
                agents[person.AgentId] = person with { MedicalSupplyTrip = null };
        }
    }

    private bool MedicalSupplyWanted(string actor, string kind) => kind == "medicine" && LivingMedicalAdult(actor) &&
        (ObservableMedicalNeeds(actor).Any() || AuthorizedMedicalSupplyTrip(actor) is not null) &&
        MedicalSupplyAtHand(actor) is null && SharedPreparationItem(kind, actor) is null;

    private void AddMedicalCareCandidates(List<CognitionCandidate> candidates, string actor)
    {
        if (!LivingMedicalAdult(actor)) return;
        foreach (var caregiver in agents[actor].MedicalConsent?.CaregiverIds ?? [])
            candidates.Add(new(MedicalRevokePrefix + caregiver,
                $"Withdraw {society.Checkpoint.GetAgent(caregiver).Name}'s permission to provide medical care.", 110));
        var supply = MedicalSupplyAtHand(actor);
        foreach (var patient in ObservableMedicalNeeds(actor))
        {
            if (supply is not null)
                candidates.Add(new(MedicalTreatPrefix + patient + ":medicine",
                    "Apply one dose of medicine to support gradual illness recovery.", 7, patient,
                    society.Checkpoint.GetAgent(patient).Name));
        }
        var trip = AuthorizedMedicalSupplyTrip(actor);
        var destination = trip?.LastSeenPosition;
        if (destination is null && ObservableMedicalNeeds(actor).FirstOrDefault() is { } observedPatient)
            destination = agents[observedPatient].Position;
        if (trip is not null && HasCarriedOwnItem(actor, "medicine") && !CanObserveMedicalPatient(actor, trip.PatientId))
            candidates.Add(new(MedicalCollectPrefix + "medicine",
                "Return with the medicine to where you last saw the consenting patient.", 8, trip.PatientId));
        else if (!HasCarriedOwnItem(actor, "medicine") && destination is { } patientPosition && FreeCarryCapacity(actor) > 0 &&
            SharedItem("medicine", actor, patientPosition, ResourceInteractionRange) is { } shared)
            candidates.Add(new(MedicalCollectPrefix + "medicine", "Visit accessible care stock and collect one real dose of medicine.",
                8, shared.StorageBuildingId));
        var existing = agents[actor].MedicalConsent?.CaregiverIds ?? [];
        var remainingPermissions = MedicalCareRules.MaximumNamedCaregivers - existing.Count(LivingMedicalAdult);
        if (remainingPermissions <= 0) return;
        foreach (var other in agents.Keys.Order(StringComparer.Ordinal).Where(other => other != actor &&
            LivingMedicalAdult(other) && CanObserveMedicalPatient(actor, other) && !existing.Contains(other, StringComparer.Ordinal))
            .Take(MedicalCareRules.MaximumNamedCaregivers))
            candidates.Add(new(MedicalAllowPrefix + other,
                $"Allow {society.Checkpoint.GetAgent(other).Name} to provide medical care to you. Your personal choice is required.",
                110, other, society.Checkpoint.GetAgent(other).Name));
    }

    private void ApplyMedicalCandidate(string actor, AgentState person, string candidate)
    {
        if (!LivingMedicalAdult(actor)) return;
        if (candidate == MedicalCollectPrefix + "medicine")
        {
            var trip = AuthorizedMedicalSupplyTrip(actor);
            if (trip is null)
            {
                var patient = ObservableMedicalNeeds(actor).FirstOrDefault();
                if (patient is null) return;
                trip = new(patient, agents[patient].Position, WorldTick);
                agents[actor] = person with { MedicalSupplyTrip = trip };
            }
            if (HasCarriedOwnItem(actor, "medicine"))
            {
                MoveToward(actor, agents[actor], trip.LastSeenPosition, "medical_patient", ResourceInteractionRange);
                return;
            }
            CollectEquipment(actor, agents[actor], "medicine", trip.LastSeenPosition, ResourceInteractionRange);
            return;
        }
        if (!candidate.StartsWith(MedicalTreatPrefix, StringComparison.Ordinal)) return;
        var separator = candidate.LastIndexOf(':');
        if (separator <= MedicalTreatPrefix.Length) return;
        _ = TreatPatientCore(actor, candidate[MedicalTreatPrefix.Length..separator], candidate[(separator + 1)..]);
    }

    public string? MedicalCareNote(string actor)
    {
        gate.Wait();
        try { return MedicalCareNoteCore(actor); }
        finally { gate.Release(); }
    }

    private string? MedicalCareNoteCore(string actor)
    {
        if (!agents.TryGetValue(actor, out var person)) return null;
        return MedicalCareRules.Note(person);
    }

    private static void ValidateMedicalCare(WorldRuntimeState state) =>
        ValidateMedicalCare(state.Agents, state.DeceasedAgents ?? [], state.Society.Society, state.Map);

    private static void ValidateMedicalCare(IEnumerable<AgentState> living,
        IEnumerable<DeceasedAgentState> deceased, SocietyCheckpoint checkpoint, WorldMap map)
    {
        var people = checkpoint.Agents.ToDictionary(person => person.Id, StringComparer.Ordinal);
        var active = living.ToDictionary(person => person.AgentId, StringComparer.Ordinal);
        var doses = new HashSet<string>(StringComparer.Ordinal);
        foreach (var person in active.Values)
        {
            ValidateMedicalConsent(person, people);
            if (person.MedicalSupplyTrip is { } trip &&
                (!people.TryGetValue(person.AgentId, out var caregiver) ||
                 caregiver.Status != SocietyAgentStatus.Active ||
                 caregiver.AgeBand is not (SocietyAgeBand.Adult or SocietyAgeBand.Elder) ||
                 !active.TryGetValue(trip.PatientId, out var patient) ||
                 !MedicalPermission(checkpoint, patient, person.AgentId) ||
                 trip.StartedTick < 0 || trip.StartedTick > checkpoint.WorldTick || !map.Contains(trip.LastSeenPosition)))
                throw new InvalidDataException("A medicine supply trip must name an accepted patient and a valid last-seen location and time.");
            if (person.MedicalTreatment is not { } treatment) continue;
            if (treatment.Kind != "medicine" || !active.ContainsKey(treatment.CaregiverId) ||
                !people.TryGetValue(treatment.CaregiverId, out var provider) || provider.Status != SocietyAgentStatus.Active ||
                provider.AgeBand is not (SocietyAgeBand.Adult or SocietyAgeBand.Elder) ||
                people[person.AgentId].Status != SocietyAgentStatus.Active ||
                !MedicalPermission(checkpoint, person, treatment.CaregiverId) || treatment.StartedTick < 0 ||
                treatment.StartedTick > checkpoint.WorldTick || treatment.LastProcessedTick != checkpoint.WorldTick ||
                treatment.RemainingTicks is < 1 or > MedicalCareRules.TreatmentTicks ||
                treatment.RemainingTicks != MedicalCareRules.TreatmentTicks - (treatment.LastProcessedTick - treatment.StartedTick) ||
                string.IsNullOrWhiteSpace(treatment.SupplyLotId) || string.IsNullOrWhiteSpace(treatment.SupplyOwnerId) ||
                string.IsNullOrWhiteSpace(treatment.DoseReservationId) || treatment.DoseReservationId.Value.Length > DerivedId.MaximumLength ||
                !doses.Add(treatment.DoseReservationId) ||
                checkpoint.Inventory.Lots.Any(lot => lot.Id == treatment.SupplyLotId && lot.ItemKind != "medicine") ||
                !checkpoint.Inventory.Reservations.Any(reservation => reservation.Id == treatment.DoseReservationId &&
                    reservation.OwnerId == treatment.SupplyOwnerId && reservation.LotId == treatment.SupplyLotId &&
                    reservation.Quantity == 1 && reservation.ExpiryTick == treatment.StartedTick &&
                    reservation.Purpose == MedicalDosePurpose(treatment.CaregiverId, person.AgentId, treatment.Kind,
                        treatment.SupplyOwnerId, treatment.StartedTick) && reservation.State == InventoryReservationState.Completed))
                throw new InvalidDataException("Medical treatment must match one consumed dose and its original patient and progress.");
        }
        foreach (var historical in deceased)
        {
            if (historical.LastPhysical is null)
                throw new InvalidDataException("A deceased person must retain a final physical record.");
            ValidateMedicalConsent(historical.LastPhysical, people);
            if (historical.LastPhysical.MedicalTreatment is not null || historical.LastPhysical.MedicalSupplyTrip is not null)
                throw new InvalidDataException("A deceased person cannot retain an active medicine treatment or supply trip.");
        }
    }

    private static void ValidateMedicalConsent(AgentState person,
        Dictionary<string, SocietyAgent> people)
    {
        if (person.MedicalConsent is not { } consent) return;
        var ids = consent.CaregiverIds;
        if (ids is null || ids.Count is < 1 or > MedicalCareRules.MaximumNamedCaregivers ||
            ids.Count != ids.Distinct(StringComparer.Ordinal).Count() ||
            !ids.SequenceEqual(ids.Order(StringComparer.Ordinal), StringComparer.Ordinal) ||
            !people.TryGetValue(person.AgentId, out var recipient) ||
            recipient.AgeBand is not (SocietyAgeBand.Adult or SocietyAgeBand.Elder) ||
            ids.Any(id => id == person.AgentId || !people.TryGetValue(id, out var caregiver) ||
                caregiver.AgeBand is not (SocietyAgeBand.Adult or SocietyAgeBand.Elder)))
            throw new InvalidDataException("Medical permission must name distinct known adult caregivers.");
    }
}
