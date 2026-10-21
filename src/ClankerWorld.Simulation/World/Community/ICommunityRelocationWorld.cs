using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

internal interface ICommunityRelocationWorld
{
    AgentStore PhysicalAgents { get; }
    SocietyCheckpoint Society { get; }
    DeclarativeWorldContentState Content { get; }
    ICommunityHousingWorld HousingWorld { get; }
    long WorldTick { get; }
    long RelocationNoticeTicks { get; }
    string HouseholdFor(string actor);
    ActorCapabilityState? CapabilitiesFor(string actor);
    bool IsAdult(string actor);
    PlacedBuilding? HouseForHousehold(string household);
    HouseResidentCapacityRules.Capacity? HouseResidentCapacity(string household);
    bool DepartHousehold(string actor, string cause);
    void EndHousingRequest(string actor, HousingRequest request, string eventKind, bool remember);
    void AppendEvent(WorldEventPayload payload);
}
