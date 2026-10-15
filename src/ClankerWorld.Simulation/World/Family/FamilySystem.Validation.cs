namespace ClankerWorld.Simulation.World;

internal sealed partial class FamilySystem
{
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
            string.IsNullOrWhiteSpace(treatment.SupplyOwnerId) || string.IsNullOrWhiteSpace(treatment.DoseReservationId) ||
            treatment.StartedTick < 0 || treatment.LastProcessedTick < treatment.StartedTick ||
            treatment.RemainingTicks is < 1 or > MedicalCareRules.TreatmentTicks ||
            treatment.RemainingTicks != MedicalCareRules.TreatmentTicks - (treatment.LastProcessedTick - treatment.StartedTick)))
            throw new InvalidDataException("Family medical treatment has invalid local state.");
        if (person.MedicalSupplyTrip is { } trip && (string.IsNullOrWhiteSpace(trip.PatientId) || trip.StartedTick < 0))
            throw new InvalidDataException("Family medical supply trip has invalid local state.");
    }
}
