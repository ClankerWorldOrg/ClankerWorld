using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

internal interface IFamilyParenthoodWorld
{
    AgentStore PhysicalAgents { get; }
    SocietyCheckpoint Society { get; }
    WorldMap Map { get; }
    long WorldTick { get; }
    int TicksPerDay { get; }
    int ResourceInteractionRange { get; }
    int IllnessCareReliefBasisPoints { get; }
    bool IsAdult(string actor);
    bool CloseKin(string actor, string other);
    IEnumerable<SocietyRelationship> Partnerships(string actor);
    bool ReadyForLesson(string actor);
    bool RecentlyCaredForIllness(string child);
    bool NeedsCaregiver(string child);
    bool IsWithinInteractionRange(GridPoint origin, GridPoint destination, int range);
    IEnumerable<PlacedBuilding> AccessibleShelters(string actor, bool includeStormRefuge);
    ParenthoodFood ParenthoodFoodReadiness(string actor);
    IReadOnlyList<TownRuntimeState> Towns { get; }
    GoodsMatch? RecheckGoods(GoodsRequest request, string lot);
    bool ReadyForBriefInteraction(string actor);
    bool NeedsUrgentFood(AgentState person);
    IEnumerable<InventoryLot> PreferredFood(string owner, string actor);
    bool CanReach(string actor, GridPoint target, ReachPurpose purpose, int range, int load, SwimmingPolicy swimming);
    int MinimumFoodPickupCarryUnits(string actor, GridPoint origin, bool forDependent);
    string HouseholdFor(string actor);
    bool ContinuityPlanDue(string actor, string partner);
    ContinuityCouple? ContinuityCoupleOf(string actor);
    ContinuityCouple? ContinuityCoupleFor(string actor, string partner);
    bool ContinuityAllowsPostponement(ContinuityCouple couple);
    ActorCapabilityState? CapabilitiesFor(string actor);
    string ParenthoodRequestId(string actor, long tick);
    void CommitBirth(SocietyBirthRequest request);
    string? HousingBlocker(string child);
    void SetHousing(string child, HousingState housing);
    string? TownForResident(string actor);
    void AddTownResident(string town, string child, string eventKind);
    void ConsumeCareFood(string actor, string lot);
    int FoodNourishment(string kind);
    SurvivalCondition? AfterMeal(AgentState person, InventoryLot food);
    void MarkCurrentSchema();
    void AppendEvent(WorldEventPayload payload);
}
