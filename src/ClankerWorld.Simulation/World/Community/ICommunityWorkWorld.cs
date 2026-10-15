using System.Collections.ObjectModel;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

internal interface ICommunityWorkWorld
{
    AgentStore PhysicalAgents { get; }
    SocietyCheckpoint Society { get; }
    long WorldTick { get; }
    IReadOnlyList<PlacedBuilding> Buildings { get; }
    IReadOnlyList<WorldProductionJob> ProductionJobs { get; }
    IReadOnlyList<BuildingExpansionJob>? Expansions { get; }
    string HouseholdFor(string actor);
    ActorCapabilityState? CapabilitiesFor(string actor);
    bool IsActiveProductionOrderJob(string job);
    bool CanFitExpansion(PlacedBuilding building, GridPoint position, BuildingFootprintRevision footprint, string job);
    ReadOnlyCollection<GridPoint> Reach(string actor, GridPoint target, ReachPurpose purpose, int range, int load, SwimmingPolicy swimming);
    void MoveToward(string actor, AgentState person, GridPoint destination, string reason, int range = 0);
    void SetReservationDeadline(IReadOnlyList<string> ids, long deadline);
    void ResumeProduction(string job, long deadline, string? knife, string actor);
    void ResumeExpansion(string job, long deadline, string actor);
    void ReleaseUnavailablePausedInputs(IReadOnlyList<string> ids);
    void CancelProduction(IReadOnlySet<string> ids);
    void CancelPausedExpansions(IReadOnlySet<string> ids, string reason);
    void AppendEvent(string kind, string detail);
}
