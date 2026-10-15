using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

internal interface IFamilyCareWorld
{
    AgentStore PhysicalAgents { get; }
    SocietyCheckpoint Society { get; }
    long WorldTick { get; }
    int TicksPerDay { get; }
    long GuardianStageTicks { get; }
    int ResourceInteractionRange { get; }
    int IllnessCareReliefBasisPoints { get; }
    bool IsAdult(string actor);
    bool ReadyForBriefInteraction(string actor);
    ActorCapabilityState? CapabilitiesFor(string actor);
    bool RecentlyCaredForIllness(string child);
    string[] GuardianAdultsForStage(string child, string stage);
    string[] NewGuardianAdultsForStage(string child, string stage);
    string HouseholdFor(string actor);
    string? TownForResident(string actor);
    PlacedBuilding? HouseholdBuildingWithTag(string household, string tag);
    HouseResidentCapacityRules.Capacity? HouseResidentCapacity(string household, SocietyAgent proposed);
    bool NeedsUrgentFood(AgentState person);
    bool IsWithinInteractionRange(GridPoint origin, GridPoint destination, int range);
    PracticalCareActionPlan? OfferPracticalCare(string actor, PracticalCareChoice choice);
    string NewCareRelationshipId(string actor, string target, long tick);
    void ProposeCare(SocietyRelationshipProposal proposal);
    void AcceptCare(string id, int revision, string actor);
    void RefuseCare(string id, int revision, string actor);
    void RevokeCare(string id, string actor);
    SocietyOperationResult AssumePrimaryCare(string adult, string child);
    SocietyOperationResult AssumeInfantCare(string adult, string child);
    SocietyOperationResult AcceptDependentGuardianship(string adult, string child, string? destination);
    void ReconcileGuardianPlacements();
    void BeginGuardianPlacement(string child, string adult, string care);
    void SetHousing(string child, HousingState housing);
    string? HousingBlocker(string child);
    void MarkCurrentSchema();
    void AppendEvent(string kind, string detail);
}
