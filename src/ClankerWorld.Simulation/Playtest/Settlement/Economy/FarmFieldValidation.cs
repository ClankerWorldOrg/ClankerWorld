using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Harness;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;
using ClankerWorld.Simulation.World;

namespace ClankerWorld.Simulation.Playtest;

public sealed partial class PrivateWorldRuntime
{
    private static void ValidateFarmFields(FarmFieldState[] fields, SeededMap map, string seed,
        SocietyCheckpoint society, WorldContentSimulationState simulation, DeclarativeWorldContentState content,
        IReadOnlyList<GridPoint> roads) =>
        FarmingSystem.ValidateFarmFields(fields, map, seed, society, simulation, content, roads);

}
