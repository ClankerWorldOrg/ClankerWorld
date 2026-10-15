using ClankerWorld.Simulation.Content;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private static ActorCapabilityPolicy ProjectInputCapabilities =>
        new(ActionLifeStages.Adults, AllowUrgentFood: true, AllowUrgentWarmth: true, AllowActiveOrder: true);
    private static ProjectInputActionDefinition ProjectInputDefinition => ProjectInputs.Definition;

    private static class ProjectInputs
    {
        internal static readonly ProjectInputActionDefinition Definition = new(
            static (world, actor, input, owner, site) => world.PlanProjectInput(actor, input, owner, site));
    }

    private sealed record ProjectInputActionDefinition(
        Func<WorldRuntime, string, ContentQuantity, string, GridPoint?, ProjectInputActionPlan> Planner) : ActionDefinition
    {
        public override Type PlanType => typeof(ProjectInputActionPlan);
        public override bool Matches(string candidate) => true;
        public override ActionPlan? PlanCandidate(WorldRuntime world, string actor, string candidate) => null;

        public ProjectInputActionPlan Plan(WorldRuntime world, string actor, ContentQuantity input, string owner, GridPoint? site) =>
            Planner(world, actor, input, owner, site);

        public override ActionCheck Check(WorldRuntime world, ActionPlan plan) =>
            world.CheckProjectInput((ProjectInputActionPlan)plan, ProjectInputCapabilities);

        public override bool Apply(WorldRuntime world, ActionContext context, ActionPhase phase)
        {
            if (phase != ActionPhase.ProjectInput || context.Plan is not ProjectInputActionPlan plan) return false;
            if (Check(world, plan).Refusal is { } refusal) world.RefuseChoice(plan, refusal);
            else world.ApplyCheckedProjectInput(plan);
            return true;
        }
    }
}
