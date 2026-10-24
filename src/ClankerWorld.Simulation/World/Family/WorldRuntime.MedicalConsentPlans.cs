using ClankerWorld.Simulation.Cognition;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{

    private void AddMedicalConsentCandidates(List<CognitionCandidate> candidates, string actor, bool allow)
    {
        IEnumerable<string> caregivers = allow ? agents.Keys.Order(StringComparer.Ordinal) : agents[actor].MedicalConsent?.CaregiverIds ?? [];
        var offered = 0;
        foreach (var caregiver in caregivers)
        {
            var plan = global::ClankerWorld.Simulation.World.WorldRuntime.MedicalActions.Consent.Offer(this, actor, new(caregiver, allow));
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
