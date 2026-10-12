using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    /// <summary>The name and its private permanent memory publish together, including for married spouses.</summary>
    private SocietyOperationResult RenameFromPlayer(SocietyCheckpoint checkpoint, string agentId, string name)
    {
        var renamed = SocietyRules.RenameAgent(checkpoint, agentId, name);
        if (renamed.NewEvents is not { Count: > 0 }) return renamed;
        var identity = renamed.Checkpoint.GetAgent(agentId);
        // The saved world-event ordinal distinguishes same-tick renames and survives setup undo and reload.
        var id = RenameMemoryId(agentId, nextEventId);
        var day = CivicDayNumber(checkpoint.WorldTick).ToString(CultureInfo.InvariantCulture);
        var recorded = SocietyRules.RecordSocialMemory(renamed.Checkpoint,
            new(id, agentId, agentId, $"I was renamed on day {day} to {identity.Name}.", "private", checkpoint.WorldTick, Permanent: true));
        return new(recorded.Checkpoint, NewEvents: [.. renamed.NewEvents, .. recorded.NewEvents ?? []]);
    }
}
