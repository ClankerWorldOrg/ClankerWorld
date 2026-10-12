using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private sealed class EnvironmentWorldAdapter(WorldRuntime runtime) : IEnvironmentResidentWorld
    {
        public WorldMap Map => runtime.map;
        public GridPoint WeatherAnchor => runtime.WeatherAnchor;
        public SocietyCheckpoint Society => runtime.society.Checkpoint;
        public IReadOnlyDictionary<string, InhabitantState> Inhabitants => runtime.inhabitants;
        public void SetResourceAvailability(string id, bool available) =>
            runtime.resources[id] = available ? ResourceState.Available : ResourceState.Depleted;
        public void AppendEvent(string kind, string detail) => runtime.AppendEvent(kind, detail);
        public void AddInhabitant(string id, InhabitantState state) => runtime.inhabitants.Add(id, state);
        public void SetInhabitant(string id, InhabitantState state) => runtime.inhabitants[id] = state;
        public void RemoveInhabitant(string id) => runtime.inhabitants.Remove(id);
        public void AddDeceasedInhabitant(string id, DeceasedInhabitantState state) => runtime.deceasedInhabitants.Add(id, state);
        public OwnerQueuedInstruction? PendingInstructionFor(string id) => runtime.PendingInstructionFor(id);
        public void CancelConstructionForOrder(OwnerQueuedInstruction instruction) => runtime.CancelConstructionForOrder(instruction);
        public void CancelExpansionForOrder(OwnerQueuedInstruction instruction) => runtime.CancelExpansionForOrder(instruction);
        public void CancelProductionForOrder(OwnerQueuedInstruction instruction) => runtime.CancelProductionForOrder(instruction);
        public void SetOrderStatus(OwnerQueuedInstruction instruction, string status, string? reason) => runtime.SetOrderStatus(instruction, status, reason);
        public void ApplyInventoryTransition(Func<InventoryCheckpoint, InventoryCheckpoint> transition) => runtime.ApplyInventoryTransition(transition);
        public void FinishIdentityMoment(string id, AgentIdentityMomentKind kind, string outcome) => runtime.FinishIdentityMoment(id, kind, outcome);
        public string? TownForResident(string id) => runtime.TownForResident(id);
        public string? RecordBoatDeath(string id) => runtime.RecordBoatDeath(id);
        public void RemoveTownResident(string id) => runtime.RemoveTownResident(id);
    }
}
