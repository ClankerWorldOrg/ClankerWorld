using System.Collections.ObjectModel;
using ClankerWorld.Simulation.Cognition;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private ReadOnlyCollection<string> MedicalConsentResult(string patient, MedicalConsentChoice choice) =>
        Array.AsReadOnly((agents[patient].MedicalConsent?.CaregiverIds ?? []).Where(id =>
                id != choice.CaregiverId && (!choice.Allow || LivingMedicalAdult(id)))
            .Concat(choice.Allow ? [choice.CaregiverId] : Array.Empty<string>()).Order(StringComparer.Ordinal).ToArray());

    private MedicalConsentActionPlan PlanMedicalConsent(string patient, MedicalConsentChoice choice)
    {
        var person = agents[patient];
        return new(patient, choice, Array.AsReadOnly((person.MedicalConsent?.CaregiverIds ?? []).ToArray()),
            MedicalConsentResult(patient, choice), !choice.Allow && person.MedicalTreatment?.CaregiverId == choice.CaregiverId
                ? person.MedicalTreatment.DoseReservationId : null);
    }

    private ActionCheck CheckMedicalConsentPlan(MedicalConsentActionPlan plan, ActorCapabilityPolicy? policy = null)
    {
        var capability = ActorCapabilities.Check(CapabilitiesFor(plan.Actor), plan.CandidateId,
            policy ?? MedicalCareCapabilities);
        if (!capability.Allowed) return capability;
        if (PendingInstructionFor(plan.Actor)?.Kind == OwnerInstructionKind.Order)
            return ActionCheck.Refuse(ActionRefusalReason.ActiveOrder, "their current order comes first");
        if (!LivingMedicalAdult(plan.Actor) || plan.Choice.CaregiverId == plan.Actor)
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected medical permission is no longer available");
        var person = agents[plan.Actor];
        var existing = person.MedicalConsent?.CaregiverIds ?? [];
        if (!existing.SequenceEqual(plan.OriginalCaregivers) || existing.Contains(plan.Choice.CaregiverId, StringComparer.Ordinal) == plan.Choice.Allow ||
            !MedicalConsentResult(plan.Actor, plan.Choice).SequenceEqual(plan.ResultingCaregivers))
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected medical permissions have changed");
        if (plan.Choice.Allow && (!agents.ContainsKey(plan.Choice.CaregiverId) || !LivingMedicalAdult(plan.Choice.CaregiverId) ||
            !CanObserveMedicalPatient(plan.Actor, plan.Choice.CaregiverId)))
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected caregiver is no longer beside them");
        if (plan.ResultingCaregivers.Count > MedicalCareRules.MaximumNamedCaregivers)
            return ActionCheck.Refuse(ActionRefusalReason.Capacity, "there is no room for another named caregiver");
        var dose = !plan.Choice.Allow && person.MedicalTreatment?.CaregiverId == plan.Choice.CaregiverId
            ? person.MedicalTreatment.DoseReservationId : null;
        return dose == plan.InterruptedDoseId ? new() :
            ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected caregiver's medicine dose has changed");
    }

    private void AddMedicalConsentCandidates(List<CognitionCandidate> candidates, string actor, bool allow)
    {
        IEnumerable<string> caregivers = allow ? agents.Keys.Order(StringComparer.Ordinal) : agents[actor].MedicalConsent?.CaregiverIds ?? [];
        var offered = 0;
        foreach (var caregiver in caregivers)
        {
            var plan = MedicalConsentDefinition.Offer(this, actor, new(caregiver, allow));
            if (plan is null) continue;
            var name = society.Checkpoint.GetAgent(caregiver).Name;
            candidates.Add(new(plan.CandidateId, allow
                ? $"Allow {name} to provide medical care to you. Your personal choice is required."
                : $"Withdraw {name}'s permission to provide medical care.", 110,
                allow ? caregiver : null, allow ? name : null)
            { ActionPlan = plan });
            if (allow && ++offered == MedicalCareRules.MaximumNamedCaregivers) break;
        }
    }

    private void ApplyMedicalConsentPlan(MedicalConsentActionPlan plan)
    {
        TryRegisteredAction(ActionPhase.FreshAdmission, new(plan.Actor, agents[plan.Actor], plan.CandidateId, false, plan));
    }
}
