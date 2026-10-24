using ClankerWorld.Simulation.Cognition;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{

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
            static (world, actor, choice, _) => FamilySystem.PlanChildChoice(world.FamilyChildWorld, actor, choice),
            static (world, plan, policy) => FamilySystem.CheckChildPlan(world.FamilyChildWorld, plan, policy),
            static (world, plan, _, _) =>
            {
                FamilySystem.ApplyCheckedChildPlan(world.FamilyChildWorld, plan);
                return true;
            });
    }

    private void OfferChildChoice(List<CognitionCandidate> candidates, string actor, ChildChoice choice,
        string description, int priority, string? destinationId = null)
    {
        if (global::ClankerWorld.Simulation.World.WorldRuntime.ChildActions.Definition.Offer(this, actor, choice) is { } plan)
            candidates.Add(new(global::ClankerWorld.Simulation.World.WorldRuntime.ChildActions.Definition.Encode(choice), description, priority, destinationId) { ActionPlan = plan });
    }

}
