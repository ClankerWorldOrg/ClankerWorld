using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private static void ValidateFarmFields(FarmFieldState[] fields, WorldMap map, string seed,
        SocietyCheckpoint society, WorldContentSimulationState simulation, DeclarativeWorldContentState content,
        IReadOnlyList<GridPoint> roads) =>
        FarmingSystem.ValidateFarmFields(fields, map, seed, society, simulation, content, roads);

}
