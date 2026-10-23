using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private sealed class ConversationWorldAdapter(WorldRuntime runtime) : AgentRuntimeAdapter(runtime), IConversationWorld
    {
        public string WorldSeed => Runtime.worldSeed;
        public long WorldDayAt(long tick) => WorldCalendarRules.FromTick(tick, Runtime.worldSystems.Config).DayIndex;
        public bool NeedsUrgentFood(AgentState person) => WorldRuntime.NeedsUrgentFood(person);
        public bool IsAboardBoat(string agentId) => Runtime.PassengerBoat(agentId) is not null;
        public int FootDistance(GridPoint from, GridPoint to) => Runtime.map.FootDistance(from, to);
        public bool IsWithinInteractionRange(GridPoint from, GridPoint to, int range) =>
                Runtime.IsWithinInteractionRange(from, to, range);
        public bool HasExplicitConversationProvider(string agentId) => Runtime.HasExplicitConversationProvider(agentId);
        public bool ActiveTalkOrderUses(string conversationId) => Runtime.ActiveTalkOrderUses(conversationId);
        public void IncreaseTrust(string from, string to, int amount, string reason) => Runtime.IncreaseTrust(from, to, amount, reason);
        public void AppendEvent(WorldEventPayload payload, GridPoint? position) => Runtime.AppendEvent(payload, position);
        public SocietyOperationResult Apply(Func<SocietyCheckpoint, SocietyOperationResult> operation) => Runtime.society.Apply(operation);
        public SocietyOperationResult RenameFromPlayer(SocietyCheckpoint checkpoint, string agentId, string name) =>
                Runtime.RenameFromPlayer(checkpoint, agentId, name);
    }
}
