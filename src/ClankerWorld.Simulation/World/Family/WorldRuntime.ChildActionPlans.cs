using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Kernel;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private static ActionDefinition<ChildChoice, ChildActionPlan> ChildDefinition => ChildActions.Definition;

    private static class ChildActions
    {
        internal static readonly ActionDefinition<ChildChoice, ChildActionPlan> Definition = new(
            static (string candidate, out ChildChoice choice) =>
            {
                var decoded = ChildChoice.Parse(candidate);
                choice = decoded.GetValueOrDefault();
                return decoded.HasValue;
            },
            static choice => choice.Id,
            new(ActionLifeStages.Children),
            static (world, actor, choice, _) => world.PlanChildChoice(actor, choice),
            static (world, plan, policy) => world.CheckChildPlan(plan, policy),
            static (world, plan, _, _) =>
            {
                world.ApplyCheckedChildPlan(plan);
                return true;
            });
    }

    private void OfferChildChoice(List<CognitionCandidate> candidates, string actor, ChildChoice choice,
        string description, int priority, string? destinationId = null)
    {
        if (ChildDefinition.Offer(this, actor, choice) is { } plan)
            candidates.Add(new(ChildDefinition.Encode(choice), description, priority, destinationId) { ActionPlan = plan });
    }

    private ChildActionPlan? PlanChildCandidate(string actor, string candidate) =>
        ChildDefinition.Decode(candidate, out var choice) ? ChildDefinition.Plan(this, actor, choice) : null;

    private ChildActionPlan? PlanChildChoice(string actor, ChildChoice choice) =>
        FamilySystem.PlanChildChoice(FamilyChildWorld, actor, choice);

    private ActionCheck CheckChildPlan(ChildActionPlan plan, ActorCapabilityPolicy policy) =>
        FamilySystem.CheckChildPlan(FamilyChildWorld, plan, policy);

    private void ApplyChildPlan(ChildActionPlan? plan)
    {
        if (plan is null) return;
        if (CheckActionPlan(plan).Refusal is { } refusal)
        {
            RefuseChoice(plan, refusal);
            return;
        }
        ApplyCheckedChildPlan(plan);
    }

    private void ApplyCheckedChildPlan(ChildActionPlan plan) =>
        FamilySystem.ApplyCheckedChildPlan(FamilyChildWorld, plan);

}
