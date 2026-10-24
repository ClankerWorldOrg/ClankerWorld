namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private static ActorCapabilityPolicy ToolRequestCapabilities =>
        new(ActionLifeStages.Adults, AllowBoat: true, AllowUrgentFood: true, AllowUrgentWarmth: true, AllowActiveOrder: true);
    private static ActorCapabilityPolicy ToolRequestVisitCapabilities =>
        new(ActionLifeStages.Adults, AllowBoat: true, AllowUrgentFood: false, AllowUrgentWarmth: false, AllowActiveOrder: true);
    private static ActorCapabilityPolicy ToolRequestConsentCapabilities(ToolRequestConsentKind kind) =>
        new(ActionLifeStages.Adults, AllowBoat: true, AllowUrgentFood: kind != ToolRequestConsentKind.Place,
            AllowUrgentWarmth: kind != ToolRequestConsentKind.Place, AllowActiveOrder: true);

    private static class ToolRequestActions
    {
        internal static readonly ActionDefinition<ToolRequestConsentChoice, ToolRequestConsentActionPlan> Consent = new(
            DecodeReferenceChoice(ToolRequestConsentChoice.Decode), static choice => choice.Encode(), ToolRequestConsentCapabilities(ToolRequestConsentKind.Place),
            static (world, actor, choice, _) => world.PlanToolRequestConsent(actor, choice),
            static (world, plan, policy) => world.CheckToolRequestConsentPlan(plan, policy),
            static (world, plan, _, _) => { world.CompleteToolRequestConsent(plan); return true; },
            PlanCapabilities: static plan => ToolRequestConsentCapabilities(plan.Choice.Kind));

        internal static readonly ActionDefinition<ToolRequestVisitChoice, ToolRequestVisitActionPlan> Visit = new(
            DecodeReferenceChoice(ToolRequestVisitChoice.Decode), static choice => choice.Encode(), ToolRequestVisitCapabilities,
            static (world, actor, choice, _) => world.PlanToolRequestVisit(actor, choice),
            static (world, plan, policy) => world.CheckToolRequestVisitPlan(plan, policy),
            static (world, plan, _, _) => { world.MoveToward(plan.Actor, world.agents[plan.Actor], plan.Smith.Position, "tool_request", WorldRuntime.ResourceInteractionRange); return true; });

        internal static readonly ActionDefinition<ToolRequestWorkChoice, ToolRequestWorkActionPlan> Work = new(
            DecodeReferenceChoice(ToolRequestWorkChoice.Decode), static choice => choice.Encode(), ToolRequestCapabilities,
            static (world, actor, choice, _) => world.PlanToolRequestWork(actor, choice),
            static (world, plan, policy) => world.CheckToolRequestWorkPlan(plan, policy),
            static (world, plan, action, _) => { world.ApplyCheckedToolRequestWork(plan, action.ReportIdle); return true; });

        internal static readonly ActionDefinition<ToolRequestCollectionChoice, ToolRequestCollectionActionPlan> Collection = new(
            DecodeReferenceChoice(ToolRequestCollectionChoice.Decode), static choice => choice.Encode(), ToolRequestCapabilities,
            static (world, actor, choice, _) => world.PlanToolRequestCollection(actor, choice),
            static (world, plan, policy) => world.CheckToolRequestCollectionPlan(plan, policy),
            static (world, plan, _, _) => { world.CompleteToolRequestCollection(plan); return true; });
    }
}
