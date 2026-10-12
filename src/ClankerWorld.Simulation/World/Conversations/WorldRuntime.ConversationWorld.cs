using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private sealed class ConversationWorldAdapter(WorldRuntime runtime) : IConversationWorld
    {
        public long WorldTick => runtime.WorldTick;
        public string WorldSeed => runtime.worldSeed;
        public SocietyCheckpoint Society => runtime.society.Checkpoint;
        public IReadOnlyDictionary<string, InhabitantState> Inhabitants => runtime.inhabitants;
        public long WorldDayAt(long tick) => WorldCalendarRules.FromTick(tick, runtime.worldSystems.Config).DayIndex;
        public bool NeedsUrgentFood(InhabitantState person) => WorldRuntime.NeedsUrgentFood(person);
        public bool NeedsUrgentWarmth(InhabitantState person) => runtime.NeedsUrgentWarmth(person);
        public bool IsAboardBoat(string agentId) => runtime.PassengerBoat(agentId) is not null;
        public int FootDistance(GridPoint from, GridPoint to) => runtime.map.FootDistance(from, to);
        public bool IsWithinInteractionRange(GridPoint from, GridPoint to, int range) =>
                runtime.IsWithinInteractionRange(from, to, range);
        public bool HasExplicitConversationProvider(string agentId) => runtime.HasExplicitConversationProvider(agentId);
        public bool ActiveTalkOrderUses(string conversationId) => runtime.ActiveTalkOrderUses(conversationId);
        public void IncreaseTrust(string from, string to, int amount, string reason) => runtime.IncreaseTrust(from, to, amount, reason);
        public void AppendEvent(string kind, string detail, GridPoint? position) => runtime.AppendEvent(kind, detail, position);
        public SocietyOperationResult Apply(Func<SocietyCheckpoint, SocietyOperationResult> operation) => runtime.society.Apply(operation);
        public SocietyOperationResult RenameFromPlayer(SocietyCheckpoint checkpoint, string agentId, string name) =>
                runtime.RenameFromPlayer(checkpoint, agentId, name);
    }
}
