namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private static ActionRegistration RegisterConversationActions() =>
        new(ActionPhase.BeforeAge, "talk:", ["talk:", "conversation_"], [], Definition: ConversationDefinition);
    private static ChoiceRegistration RegisterConversationChoices() => new(ChoiceGroup.Final, "conversations", static (runtime, context) => context.Candidates.AddRange(runtime.ConversationCandidates(context.AgentId)), static (_, context) => !context.HasConversation);
}
