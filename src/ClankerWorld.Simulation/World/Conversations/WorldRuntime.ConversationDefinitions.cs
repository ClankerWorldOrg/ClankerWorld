namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{

    private static class ConversationActions
    {
        internal static readonly ActionDefinition<ConversationChoice, ConversationActionPlan> Definition = new(
            DecodeReferenceChoice(ConversationChoice.Decode), static choice => choice.Encode(), ConversationSystem.ActionCapabilities(ConversationChoiceKind.Talk),
            static (world, actor, choice, _) => world.conversationSystem.PlanConversationAction(world.conversationWorld, actor, choice),
            static (world, plan, policy) => world.conversationSystem.CheckConversationPlan(world.conversationWorld, plan, policy),
            static (world, plan, action, _) =>
            {
                if (world.conversationSystem.ApplyConversationPlan(world.conversationWorld, plan) &&
                    world.agents[action.AgentId].Equipment?.Repair is not null) world.CancelEquipmentRepair(action.AgentId);
                return true;
            },
            PlanCapabilities: static plan => ConversationSystem.ActionCapabilities(plan.Choice.Kind));
    }
}
