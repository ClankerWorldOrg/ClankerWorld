using ClankerWorld.Simulation.Cognition;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private const string TownSalvagePrefix = "town_salvage:";

    private static string TownSalvageId(string townId, string lotId) => TownSystem.TownSalvageId(townId, lotId);
}
