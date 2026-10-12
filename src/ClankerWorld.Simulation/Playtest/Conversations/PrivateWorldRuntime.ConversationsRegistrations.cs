namespace ClankerWorld.Simulation.Playtest;

public sealed partial class PrivateWorldRuntime
{
    private static ActionRegistration RegisterConversationActions() => new(ActionPhase.BeforeAge, "talk:", ["talk:", "conversation_"], [], static (runtime, action) =>
    {
        if (!runtime.ApplyConversationCandidate(action.InhabitantId, action.CandidateId))
            runtime.AppendEvent("conversation_action_rejected", $"{action.InhabitantId}:{action.CandidateId.Split(':')[0]}");
        else if (runtime.inhabitants[action.InhabitantId].Equipment?.Repair is not null)
            runtime.CancelEquipmentRepair(action.InhabitantId);
        return true;
    });
    private static ChoiceRegistration RegisterConversationChoices() => new(ChoiceGroup.Final, "conversations", static (runtime, context) => context.Candidates.AddRange(runtime.ConversationCandidates(context.InhabitantId)), static (_, context) => !context.HasConversation);
}
