namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private static ActionRegistration RegisterConversationActions() => new(ActionPhase.BeforeAge, "talk:", ["talk:", "conversation_"], [], static (runtime, action) =>
    {
        var plan = action.Plan as ConversationActionPlan ?? (ConversationChoice.Decode(action.CandidateId) is { } choice
            ? runtime.conversationSystem.PlanConversationAction(runtime.conversationWorld, action.AgentId, choice) : null);
        if (plan is null) return true;
        if (runtime.CheckActionPlan(plan).Refusal is { } refusal) { runtime.RefuseChoice(plan, refusal); return true; }
        if (runtime.conversationSystem.ApplyConversationPlan(runtime.conversationWorld, plan) &&
            runtime.agents[action.AgentId].Equipment?.Repair is not null) runtime.CancelEquipmentRepair(action.AgentId);
        return true;
    });
    private static ChoiceRegistration RegisterConversationChoices() => new(ChoiceGroup.Final, "conversations", static (runtime, context) => context.Candidates.AddRange(runtime.ConversationCandidates(context.AgentId)), static (_, context) => !context.HasConversation);
}
