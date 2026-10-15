using System.Collections.ObjectModel;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

internal interface IProductionOrdersWorld
{
    DeclarativeWorldContentState Content { get; }
    WorldContentSimulationState Simulation { get; }
    SocietyCheckpoint Society { get; }
    OrderStore Orders { get; }
    long WorldTick { get; }
    AgentStore PhysicalAgents { get; }
    IReadOnlyDictionary<string, AgentState> Agents { get; }
    bool ReadyForBriefInteraction(string actor);
    bool TryFindRecipeSite(RecipeDefinition recipe, out string siteId, out GridPoint position, string? actorId = null);
    bool NeedsUrgentFood(AgentState person);
    bool NeedsUrgentWarmth(AgentState person);
    void SetOrderStatus(OwnerQueuedInstruction instruction, string status, string? reason, bool waitForDecision = false);
    void MoveToward(string agentId, AgentState state, GridPoint destination, string reason, int interactionRange = 0, int horseMovementBudget = 100);
    bool CanResumeHeldInputs(string actor, string buildingId, IReadOnlyList<string> reservationIds);
    bool TryResumeKnife(string actor, string jobId, out string? knifeId);
    void AppendEvent(string kind, string detail, GridPoint? eventPosition = null);
    bool IsProductionOrderProject(OwnerQueuedInstruction instruction, AgentProject? project);
    void SetProject(string agentId, AgentProject project);
    void ContinueProject(string agentId, AgentState state);
    bool MayFollowOwnerOrder(string actor, OrderTask order);
    bool IsActiveOrder(string status);
    bool IsAdult(string actor);
    bool IsConversationBusy(string agentId);
    ClankerWorld.Simulation.World.OwnerQueuedInstruction? PendingInstructionFor(string agentId);
    string ProductionOrderReceipt(string jobId);
    void CreditOrderEffect(OwnerQueuedInstruction instruction, string effectId, int units);
    void ExtendOrderReservations(IReadOnlyList<string> reservations, long deadline);
    void HoldOrderReservations(IReadOnlyList<string> reservations);
    void ReleaseProductionOrderInputs(WorldProductionJob job);
    void ResumeProductionJob(string job, long deadline, string? knife);
    void PauseProductionJob(string job, long tick);
    void CancelProductionJobs(IReadOnlySet<string> jobs);
    void MarkCheckpointCurrent();
    bool IsHouseholdBuildingTag(string tag);
}
