using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Kernel;

namespace ClankerWorld.Simulation.World;

internal interface IExpansionAdmissionWorld
{
    long WorldTick { get; }
    long NextProductionJobSequence { get; }
    DeclarativeWorldContentState Content { get; }
    IReadOnlyDictionary<string, AgentState> Agents { get; }
    OwnerQueuedInstruction? PendingInstructionFor(string actor);
    PlacedBuilding? ExpansionOrderBuilding(OwnerQueuedInstruction instruction);
    bool MayExpandBuilding(string actor, PlacedBuilding building, out string failure);
    ExpansionActionPlan? PlanExpansion(string actor, PlacedBuilding building, OwnerQueuedInstruction? instruction);
    ActionCheck CheckExpansion(ExpansionActionPlan plan);
    bool HasExpansionMaterials(string actor, PlacedBuilding building, IReadOnlyList<ContentQuantity> costs);
    ExpansionMaterialInput[]? PlanExpansionInputs(string actor, PlacedBuilding building, IReadOnlyList<ContentQuantity> costs);
    int SkilledBuildingWorkTicks(string actor, int ticks);
    string ExpansionJobId(long sequence);
    IReadOnlyList<string> ReserveExpansionInputs(IReadOnlyList<ExpansionMaterialInput> inputs, string actor, string id, long expiry);
    void AdvanceExpansionSequence();
    void BindStartedExpansionOrder(OwnerQueuedInstruction instruction, BuildingExpansionJob job);
    void AppendEvent(string kind, string detail, GridPoint? position = null);
}

internal interface IExpansionCompletionWorld
{
    InventoryCheckpoint Inventory { get; }
    bool IsAdult(string actor);
    string HouseholdFor(string actor);
    string? TownForResident(string actor);
    bool CanFitExpansion(PlacedBuilding building, GridPoint position, BuildingFootprintRevision footprint, out string failure, string jobId);
    void CompleteExpansionInputs(IReadOnlyList<string> ids);
    void CancelExpansionInputs(IReadOnlyList<string> ids);
    void RecordExpandedWork(PlacedBuilding building, BuildingExpansionJob job);
    void CreditExpansionOrderJob(BuildingExpansionJob job);
    void AppendEvent(string kind, string detail, GridPoint? position = null);
}
