namespace ClankerWorld.Simulation.World;

public sealed class AgentPlacementChangedException : InvalidOperationException
{
    public AgentPlacementChangedException()
        : base("The placement changed after its preview. Check the tile and try again.") { }

    public AgentPlacementChangedException(string detail)
        : base("The placement changed after its preview. " + detail) { }
}
