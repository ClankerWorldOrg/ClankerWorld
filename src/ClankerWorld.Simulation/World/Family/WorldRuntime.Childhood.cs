using ClankerWorld.Simulation.Cognition;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{

    private bool IsChild(string actor) => AgentQueries.IsChild(agentWorld, actor);
}
