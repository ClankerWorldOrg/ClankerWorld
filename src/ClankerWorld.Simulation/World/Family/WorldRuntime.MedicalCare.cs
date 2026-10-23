using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private const string MedicalAllowPrefix = MedicalConsentChoice.AllowPrefix;
    private const string MedicalRevokePrefix = MedicalConsentChoice.RevokePrefix;

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
            ApplyMedicalConsentPlan(decision.ActionPlan as MedicalConsentActionPlan ?? FamilySystem.PlanMedicalConsent(this.FamilyMedicalWorld, decision.AgentId, choice));
        return true;
    }

    public MedicalCareResult TreatPatient(string caregiver, string patient, string kind)
    {
        gate.Wait();
        try { return FamilySystem.TreatPatientCore(this.FamilyTreatmentWorld, caregiver, patient, kind); }
        finally { gate.Release(); }
    }

    private MedicalCareResult CompleteMedicalTreatment(string caregiver, string patient, string kind, InventoryLot supply) =>
        FamilySystem.CompleteMedicalTreatment(FamilyTreatmentWorld, caregiver, patient, kind, supply);

    private void AddMedicalCareCandidates(List<CognitionCandidate> candidates, string actor)
    {
        if (!FamilySystem.LivingMedicalAdult(this.FamilyMedicalWorld, actor)) return;
        AddMedicalConsentCandidates(candidates, actor, allow: false);
        foreach (var patient in FamilySystem.ObservableMedicalNeeds(this.FamilyMedicalWorld, actor))
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
        if (!FamilySystem.LivingMedicalAdult(this.FamilyMedicalWorld, actor)) return;
        if (MedicalSupplyChoice.Decode(candidate) is not null)
        {
            if (FamilySystem.PlanMedicalSupply(this.FamilySupplyWorld, actor) is { } collection) ApplyMedicalSupplyPlan(collection);
            return;
        }
        if (MedicalTreatmentChoice.Decode(candidate) is { } choice && FamilySystem.PlanMedicalTreatment(this.FamilyMedicalWorld, actor, choice, 2500, false) is { } plan)
            ApplyMedicalTreatmentPlan(plan);
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
                 !LifeStageRules.IsAdult(caregiver.AgeBand) ||
                 !active.TryGetValue(trip.PatientId, out var patient) ||
                 !FamilySystem.MedicalPermission(checkpoint, patient, person.AgentId) ||
                 trip.StartedTick < 0 || trip.StartedTick > checkpoint.WorldTick || !map.Contains(trip.LastSeenPosition)))
                throw new InvalidDataException("A medicine supply trip must name an accepted patient and a valid last-seen location and time.");
            if (person.MedicalTreatment is not { } treatment) continue;
            if (treatment.Kind != "medicine" || !active.ContainsKey(treatment.CaregiverId) ||
                !people.TryGetValue(treatment.CaregiverId, out var provider) || provider.Status != SocietyAgentStatus.Active ||
                !LifeStageRules.IsAdult(provider.AgeBand) ||
                people[person.AgentId].Status != SocietyAgentStatus.Active ||
                !FamilySystem.MedicalPermission(checkpoint, person, treatment.CaregiverId) || treatment.StartedTick < 0 ||
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
                    reservation.Purpose == FamilySystem.MedicalDosePurpose(treatment.CaregiverId, person.AgentId, treatment.Kind, treatment.SupplyOwnerId, treatment.StartedTick) && reservation.State == InventoryReservationState.Completed))
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
            !LifeStageRules.IsAdult(recipient.AgeBand) ||
            ids.Any(id => id == person.AgentId || !people.TryGetValue(id, out var caregiver) ||
                !LifeStageRules.IsAdult(caregiver.AgeBand)))
            throw new InvalidDataException("Medical permission must name distinct known adult caregivers.");
    }
}
