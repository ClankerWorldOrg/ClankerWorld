using System.Globalization;
using System.Text.Json;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private const string MedicalAllowPrefix = MedicalConsentChoice.AllowPrefix;
    private const string MedicalRevokePrefix = MedicalConsentChoice.RevokePrefix;
    private const string MedicalTreatPrefix = MedicalTreatmentChoice.Prefix;

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
        if (MedicalConsentChoice.Decode(selected.CandidateId) is { } choice)
            ApplyMedicalConsentPlan(decision.ActionPlan as MedicalConsentActionPlan ?? PlanMedicalConsent(decision.AgentId, choice));
        return true;
    }

    private void CompleteMedicalConsent(MedicalConsentActionPlan plan)
    {
        var patient = plan.Actor;
        var caregiver = plan.Choice.CaregiverId;
        var allowed = plan.Choice.Allow;
        var person = agents[patient];
        var ids = plan.ResultingCaregivers;
        if (!allowed && person.MedicalTreatment is { } interrupted && interrupted.CaregiverId == caregiver)
            CloseMedicalDose(patient, interrupted);
        agents[patient] = person with
        {
            MedicalConsent = ids.Count == 0 ? null : new(ids),
            MedicalTreatment = !allowed && person.MedicalTreatment?.CaregiverId == caregiver
                ? null : person.MedicalTreatment,
        };
        checkpointSchemaVersion = StateSchemaVersion;
        AppendEvent(allowed ? "medical_care_allowed" : "medical_care_revoked", $"{patient}:medical_caregiver:{caregiver}");
    }

    private InventoryLot? MedicalSupplyAtHand(string caregiver) => MedicalDoseStock(caregiver).FirstOrDefault()?.Lot;

    public MedicalCareResult TreatPatient(string caregiver, string patient, string kind)
    {
        gate.Wait();
        try { return TreatPatientCore(caregiver, patient, kind); }
        finally { gate.Release(); }
    }

    private MedicalCareResult TreatPatientCore(string caregiver, string patient, string kind)
    {
        if (kind != "medicine") return new(false, "Injury treatment is not available yet; medicine supports illness recovery.");
        var target = CheckMedicalTreatmentTarget(caregiver, patient, 1);
        if (target.Refusal is { } refusal) return new(false, refusal.Explanation);
        var plan = PlanMedicalTreatment(caregiver, new(patient, kind), 1, allowBoat: true);
        if (plan is null) return new(false, MedicalSupplyFailure);
        var check = CheckActionPlan(plan);
        if (check.Refusal is { } unavailable) return new(false, unavailable.Explanation);
        return CompleteMedicalTreatment(caregiver, patient, kind, society.Checkpoint.Inventory.GetLot(plan.StockId));
    }

    private MedicalCareResult CompleteMedicalTreatment(string caregiver, string patient, string kind, InventoryLot supply)
    {
        var person = agents[patient];
        var id = MedicalDoseId(WorldTick, nextEventId, caregiver, patient);
        ApplyInventoryTransition(inventory => InventoryAuthority.ConsumeReservation(InventoryAuthority.Reserve(inventory,
            id, supply.OwnerId, supply.Id, 1,
            MedicalDosePurpose(caregiver, patient, kind, supply.OwnerId, WorldTick), WorldTick, holdAccess: InventoryHoldRules.BorrowedBy(caregiver)), id));
        agents[patient] = person with
        {
            MedicalTreatment = new(caregiver, supply.Id, supply.OwnerId, id, kind, WorldTick, WorldTick,
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
            (person.Survival?.IllnessBasisPoints ?? 0) >= NeedThresholds.MedicalCareIllness)
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
        AddMedicalConsentCandidates(candidates, actor, allow: false);
        foreach (var patient in ObservableMedicalNeeds(actor))
            if (MedicalTreatmentDefinition.Offer(this, actor, new(patient)) is { } plan)
                candidates.Add(new(plan.CandidateId, "Apply one dose of medicine to support gradual illness recovery.", 7,
                    patient, society.Checkpoint.GetAgent(patient).Name)
                { ActionPlan = plan });
        AddMedicalSupplyCandidate(candidates, actor);
        AddMedicalConsentCandidates(candidates, actor, allow: true);
    }

    private void ApplyMedicalCandidate(string actor, AgentState person, string candidate, ActionPlan? originalPlan = null)
    {
        if (originalPlan is MedicalTreatmentActionPlan treatment) { ApplyMedicalTreatmentPlan(treatment); return; }
        if (originalPlan is MedicalSupplyActionPlan supply) { ApplyMedicalSupplyPlan(supply); return; }
        if (!LivingMedicalAdult(actor)) return;
        if (MedicalSupplyChoice.Decode(candidate) is not null)
        {
            if (PlanMedicalSupply(actor) is { } collection) ApplyMedicalSupplyPlan(collection);
            return;
        }
        if (MedicalTreatmentChoice.Decode(candidate) is { } choice && PlanMedicalTreatment(actor, choice) is { } plan)
            ApplyMedicalTreatmentPlan(plan);
    }

    private void BeginMedicalSupplyTrip(MedicalSupplyActionPlan plan)
    {
        if (plan.OriginalTrip is null)
            agents[plan.Actor] = agents[plan.Actor] with
            { MedicalSupplyTrip = new(plan.PatientId, plan.LastSeenPosition, WorldTick) };
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
                string.IsNullOrWhiteSpace(treatment.DoseReservationId) || treatment.DoseReservationId.Length > DerivedId.MaximumLength ||
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
