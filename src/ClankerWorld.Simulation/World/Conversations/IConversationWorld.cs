using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

/// <summary>The coordinator services needed by conversation and marriage rules.</summary>
public interface IConversationWorld
{
    long WorldTick { get; }
    string WorldSeed { get; }
    SocietyCheckpoint Society { get; }
    IReadOnlyDictionary<string, AgentState> Agents { get; }
    long WorldDayAt(long tick);
    bool NeedsUrgentFood(AgentState person);
    bool NeedsUrgentWarmth(AgentState person);
    bool IsAboardBoat(string agentId);
    int FootDistance(GridPoint from, GridPoint destination);
    bool IsWithinInteractionRange(GridPoint from, GridPoint destination, int range);
    bool HasExplicitConversationProvider(string agentId);
    bool ActiveTalkOrderUses(string conversationId);
    void IncreaseTrust(string from, string subject, int amount, string reason);
    void AppendEvent(string kind, string detail, GridPoint? position = null);
    SocietyOperationResult Apply(Func<SocietyCheckpoint, SocietyOperationResult> operation);
    SocietyOperationResult RenameFromPlayer(SocietyCheckpoint checkpoint, string agentId, string name);
}
