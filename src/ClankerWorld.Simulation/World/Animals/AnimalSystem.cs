using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

internal sealed partial class AnimalSystem(AnimalWorldState state)
{
    internal AnimalWorldState State { get; private set; } = state;
}
