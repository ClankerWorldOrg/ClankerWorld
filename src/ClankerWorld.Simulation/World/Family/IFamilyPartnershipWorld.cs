using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

internal interface IFamilyPartnershipWorld
{
    AgentStore PhysicalAgents { get; }
    SocietyCheckpoint Society { get; }
    long WorldTick { get; }
    int TicksPerDay { get; }
    bool HasSurvivalState { get; }
    bool IsAdult(string actor);
    ActorCapabilityState? CapabilitiesFor(string actor);
    bool ReadyForBriefInteraction(string actor);
    int TrustScore(string owner, string subject);
    string NewPartnershipId(string actor, string partner, long tick);
    void ProposePartnership(SocietyRelationshipProposal relationship);
    void AcceptPartnership(string id, int revision, string actor);
    void RefusePartnership(string id, int revision, string actor);
    void RevokePartnership(string id, string actor);
    void MaintainMarriages();
    void AppendEvent(string kind, string detail);
}
