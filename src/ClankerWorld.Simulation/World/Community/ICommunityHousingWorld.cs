using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

internal interface ICommunityHousingWorld
{
    AgentStore PhysicalAgents { get; }
    SocietyCheckpoint Society { get; }
    long WorldTick { get; }
    long HousingRefusalCooldownTicks { get; }
    bool IsAdult(string actor);
    ActorCapabilityState? CapabilitiesFor(string actor);
    string[] HouseholdAdults(string household);
    PlacedBuilding? HouseForHousehold(string household);
    IEnumerable<SocietyHousehold> AskableHouseholds(string actor);
    IEnumerable<string> PendingHousingRequestsFor(string member);
    string? HousingBlocker(string actor);
    void ReconcileRelocation(string household);
    bool MayRelocateFromHousehold(string actor);
    bool CanFitCareGroup(string household, string actor);
    bool HoldsRelocationNotice(string actor);
    bool DepartHousehold(string actor, string cause);
    IReadOnlyList<string> MovingCareGroup(string actor);
    void JoinHouseholdCareGroup(string actor, string household);
    void MarkCurrentSchema();
    void AppendEvent(WorldEventPayload payload);
}
