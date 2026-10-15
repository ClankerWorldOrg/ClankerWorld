using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

internal interface ICommunityDepartureWorld
{
    AgentStore PhysicalAgents { get; }
    SocietyCheckpoint Society { get; }
    long WorldTick { get; }
    GridPoint SharedStoragePosition { get; }
    bool IsAdult(string actor);
    bool HasDependentInPrimaryCare(string actor);
    bool HoldsRelocationNotice(string actor);
    bool MayRelocateFromHousehold(string actor);
    IReadOnlyList<string> MovingCareGroup(string actor);
    IReadOnlyList<DepartureAllowance> PlanDepartureAllowance(string actor, string household);
    string DepartureTransferId(string actor, long tick, int count, string lot);
    bool LeaveHousehold(string actor);
    void CommitDepartureAllowance(InventoryCheckpoint inventory);
    void CancelDepartureOrders(string actor);
    IReadOnlyList<WorldProductionJob> PauseDepartingHouseholdWork(string actor, string household);
    void RestoreDepartureDeliveries(string actor, string household);
    void ReconcileGuardianPlacements();
    void MarkCurrentSchema();
    void AppendEvent(string kind, string detail);
}
