using ClankerWorld.Simulation.Content;
namespace ClankerWorld.Simulation.World;

internal interface ICommunitySkillWorld
{
    AgentStore PhysicalAgents { get; }
    DeclarativeWorldContentState Content { get; }
    long WorldTick { get; }
    void MarkCurrentSchema();
    void AppendEvent(string kind, string detail);
}
