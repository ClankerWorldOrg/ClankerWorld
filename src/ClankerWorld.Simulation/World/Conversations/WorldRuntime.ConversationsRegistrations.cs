namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private static ActionRegistration RegisterConversationActions() => new(ActionPhase.BeforeAge, "talk:", ["talk:", "conversation_"], [], static (runtime, action) =>
    {
        if (!runtime.ApplyConversationCandidate(action.AgentId, action.CandidateId))
            runtime.AppendEvent("conversation_action_rejected", $"{action.AgentId}:{action.CandidateId.Split(':')[0]}");
        else if (runtime.agents[action.AgentId].Equipment?.Repair is not null)
            runtime.CancelEquipmentRepair(action.AgentId);
        return true;
    });
    private static ChoiceRegistration RegisterConversationChoices() => new(ChoiceGroup.Final, "conversations", static (runtime, context) => context.Candidates.AddRange(runtime.ConversationCandidates(context.AgentId)), static (_, context) => !context.HasConversation);
}
