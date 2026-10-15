using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Society;
namespace ClankerWorld.Simulation.World;

internal interface ICommunitySkillWorld
{
    SocietyCheckpoint Society { get; }
    AgentStore PhysicalAgents { get; }
    DeclarativeWorldContentState Content { get; }
    long WorldTick { get; }
    void MarkCurrentSchema();
    void AppendEvent(string kind, string detail);
}
