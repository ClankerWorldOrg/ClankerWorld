using System.Collections.Frozen;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private delegate bool ChoiceDecoder<TChoice>(string candidate, out TChoice choice);

    private abstract record ActionDefinition
    {
        public abstract Type PlanType { get; }
        public abstract bool Matches(string candidate);
        public abstract ActionCheck Check(WorldRuntime world, ActionPlan plan);
        public abstract bool Apply(WorldRuntime world, ActionContext context, ActionPhase phase);
    }

    private sealed record ActionDefinition<TChoice, TPlan>(
        ChoiceDecoder<TChoice> Decode,
        Func<TChoice, string> Encode,
        ActorCapabilityPolicy Capabilities,
        Func<WorldRuntime, string, TChoice, ActorCapabilityPolicy, TPlan?> PlanChoice,
        Func<WorldRuntime, TPlan, ActorCapabilityPolicy, ActionCheck> CheckPlan,
        Func<WorldRuntime, TPlan, ActionContext, ActionPhase, bool> ApplyPlan,
        Func<TPlan, ActorCapabilityPolicy>? PlanCapabilities = null,
        Func<WorldRuntime, ActionContext, bool>? NoPlanFallback = null) : ActionDefinition
        where TPlan : ActionPlan
    {
        public override Type PlanType => typeof(TPlan);
        public override bool Matches(string candidate) => Decode(candidate, out _);

        public TPlan? Plan(WorldRuntime world, string actor, TChoice choice) => PlanChoice(world, actor, choice, Capabilities);

        public TPlan? Offer(WorldRuntime world, string actor, TChoice choice)
        {
            var plan = Plan(world, actor, choice);
            return plan is not null && CheckPlan(world, plan, PolicyFor(plan)).Allowed ? plan : null;
        }

        public override ActionCheck Check(WorldRuntime world, ActionPlan plan) =>
            CheckPlan(world, (TPlan)plan, PolicyFor((TPlan)plan));

        private ActorCapabilityPolicy PolicyFor(TPlan plan) => PlanCapabilities?.Invoke(plan) ?? Capabilities;

        public override bool Apply(WorldRuntime world, ActionContext context, ActionPhase phase)
        {
            var plan = context.Plan as TPlan ?? (Decode(context.CandidateId, out var choice)
                ? Plan(world, context.AgentId, choice) : null);
            if (plan is null) return NoPlanFallback?.Invoke(world, context) ?? true;
            if (CheckPlan(world, plan, PolicyFor(plan)).Refusal is { } refusal)
            {
                world.RefuseChoice(plan, refusal);
                return true;
            }
            return ApplyPlan(world, plan, context, phase);
        }
    }

    private static readonly Lazy<FrozenDictionary<Type, ActionDefinition[]>> RegisteredActionDefinitions = new(BuildRegisteredActionDefinitions);

    private static FrozenDictionary<Type, ActionDefinition[]> BuildRegisteredActionDefinitions() =>
        ActionRegistrations.Where(row => row.Definition is not null).Select(row => row.Definition!)
            .Distinct().GroupBy(definition => definition.PlanType)
            .ToFrozenDictionary(group => group.Key, group => group.ToArray());

    private static ActionDefinition? DefinitionFor(ActionPlan plan) =>
        RegisteredActionDefinitions.Value.TryGetValue(plan.GetType(), out var definitions)
            ? definitions.FirstOrDefault(definition => definition.Matches(plan.CandidateId)) : null;
}
