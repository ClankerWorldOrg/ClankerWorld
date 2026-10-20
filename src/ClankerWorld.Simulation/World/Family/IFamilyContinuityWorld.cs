using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

internal interface IFamilyContinuityWorld
{
    AgentStore PhysicalAgents { get; }
    SocietyCheckpoint Society { get; }
    long WorldTick { get; }
    long ContinuityPostponeTicks { get; }
    bool Partners(string actor, string other);
    bool CanHaveChildren(string actor);
    bool EligibleContinuityParents(string actor, string other);
    IEnumerable<SocietyRelationship> Partnerships(string actor);
    int TrustScore(string actor, string other);
    SocietyRelationship ProposePartnership(string actor, string partner);
    void AcceptPartnership(SocietyRelationship proposal);
    void RevokePartnership(string id, string actor);
    void SetParenthood(string actor, ParenthoodPlan plan);
    string HouseholdFor(string actor);
    void MarkCurrentSchema();
    void AppendEvent(WorldEventPayload payload);
}
