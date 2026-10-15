using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

internal interface IFamilyContinuityWorld
{
    AgentStore PhysicalAgents { get; }
    SocietyCheckpoint Society { get; }
    long WorldTick { get; }
    long ContinuityPostponeTicks { get; }
    bool Partners(string actor, string other);
    void SetParenthood(string actor, ParenthoodPlan plan);
    string HouseholdFor(string actor);
    void MarkCurrentSchema();
    void AppendEvent(string kind, string detail);
}
