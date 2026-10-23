using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    public IReadOnlyList<AgentMarriage> Marriages => marriages.OrderBy(item => item.Id, StringComparer.Ordinal).ToArray();

    private int FindMarriageIndex(Predicate<AgentMarriage> predicate) => conversationSystem.MarriageIndex(predicate);

    private void SetMarriageAt(int index, AgentMarriage marriage) => conversationSystem.SetMarriageAt(index, marriage);

    private bool CanProposeMarriage(AgentConversation conversation) =>
        conversationSystem.CanProposeMarriage(conversationWorld, conversation);

    private void MaintainMarriages() =>
        conversationSystem.MaintainMarriages(conversationWorld);

    private SocietyOperationResult RenameSpouses(AgentMarriage marriage, string agentId, string name, bool fromPlayer = false) =>
        ConversationSystem.RenameSpouses(conversationWorld, marriage, agentId, name, fromPlayer);


}
