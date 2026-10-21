using System.Collections.ObjectModel;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

internal interface IFamilyGuardianWorld
{
    AgentStore PhysicalAgents { get; }
    SocietyCheckpoint Society { get; }
    FamilyTurnState Turn { get; }
    IReadOnlyList<PlacedBuilding> Buildings { get; }
    long WorldTick { get; }
    int ResourceInteractionRange { get; }
    string? TownForResident(string actor);
    PlacedBuilding? HouseForHousehold(string household);
    bool IsAdult(string actor);
    bool NeedsUrgentFood(AgentState person);
    bool NeedsUrgentWarmth(AgentState person);
    OwnerQueuedInstruction? PendingInstructionFor(string actor);
    bool IsConversationBusy(string actor);
    bool CanFitGuardianHousehold(string household, string adult, string child);
    bool IsWithinInteractionRange(GridPoint origin, GridPoint destination, int range);
    ReadOnlyCollection<GridPoint> Reach(string actor, GridPoint target, ReachPurpose purpose, int range, int load, SwimmingPolicy swimming);
    ActorCapabilityState? CapabilitiesFor(string actor);
    GuardianTravelActionPlan? OfferGuardianTravel(string actor, GuardianTravelChoice choice);
    GuardianTravelActionPlan? PlanGuardianTravel(string actor, GuardianTravelChoice choice);
    ActionCheck CheckActionPlan(ActionPlan plan);
    CognitionCandidate? UrgentSurvivalCandidateFor(string actor, AgentState person);
    void ApplyCandidate(string actor, AgentState person, string candidate, bool reportIdle);
    bool TryApplyGuardianPlacementCandidate(string actor, string candidate, ActionPlan? plan);
    int CaregiverFoodCarryRequirement(string actor, AgentState person);
    int FreeCarryCapacity(string actor);
    void MakeRoomForFood(string actor, AgentState person);
    void CareForChild(string actor, string child);
    void MoveToward(string actor, AgentState person, GridPoint destination, string reason, int range);
    FarmFieldState? FarmWorkFor(string actor);
    void CancelFarmWork(FarmFieldState field);
    void CancelEquipmentRepair(string actor);
    void SetProject(string actor, AgentProject project);
    bool IsActionWithheld(string actor);
    SocietyOperationResult PlaceDependentWithGuardian(string adult, string child, string care, int revision, string household);
    void RestoreHouseholdDeliveries(string actor, string household, string transition);
    bool MoveDependentToGuardianTown(string child, string adult);
    void SetHousing(string child, HousingState housing);
    string? HousingBlocker(string child);
    void MarkCurrentSchema();
    void AppendEvent(WorldEventPayload payload);
}
