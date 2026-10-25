namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private static ActorCapabilityPolicy ExplorationCapabilities(bool native) =>
        new(ActionLifeStages.Adults, AllowBoat: native, AllowUrgentFood: native, AllowUrgentWarmth: native, AllowActiveOrder: true);

    private static class ExplorationActions
    {
        internal static readonly ActionDefinition<ExplorationChoice, ExplorationActionPlan> Definition = new(
            DecodeReferenceChoice(ExplorationChoice.Decode), static choice => choice.Encode(), ExplorationCapabilities(false),
            static (world, actor, choice, _) => world.PlanExploration(actor, choice),
            static (world, plan, policy) => world.CheckExploration(plan, policy),
            static (world, plan, _, _) => { world.ApplyCheckedExploration(plan); return true; },
            PlanCapabilities: static plan => ExplorationCapabilities(plan.Native));
    }
}
