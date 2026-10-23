using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private bool IsAdult(string actor) => AgentQueries.IsAdult(agentWorld, actor);
}
