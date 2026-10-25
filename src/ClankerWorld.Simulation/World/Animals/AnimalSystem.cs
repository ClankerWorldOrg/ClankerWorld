
namespace ClankerWorld.Simulation.World;

internal sealed partial class AnimalSystem(AnimalWorldState state)
{
    internal AnimalWorldState State { get; private set; } = state;
}
