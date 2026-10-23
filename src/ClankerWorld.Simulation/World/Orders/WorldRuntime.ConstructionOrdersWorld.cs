namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private IConstructionOrdersWorld ConstructionOrdersWorld => new ConstructionOrdersAdapter(this);

    private sealed class ConstructionOrdersAdapter(WorldRuntime runtime) : GoodsRuntimeAdapter(runtime), IConstructionOrdersWorld
    {
        public string ConstructionInstanceId(string instructionId) => WorldRuntime.ConstructionInstanceId(instructionId);
        public bool IsConstructionOrderProject(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentProject? project) => WorldRuntime.IsConstructionOrderProject(instruction, project);
        public bool OrdinaryProjectCanYieldToOrder(global::ClankerWorld.Simulation.World.AgentProject project) => Runtime.OrdinaryProjectCanYieldToOrder(project);
        public string? HouseholdBuildingKind(global::ClankerWorld.Simulation.Content.BuildingDefinition definition) => WorldRuntime.HouseholdBuildingKind(definition);
        public bool TownHasRoomForSharedShop(string actor, string? kind) => Runtime.TownHasRoomForSharedShop(actor, kind);
        public bool WorkBuildingHasInputs(string actor, string household, global::ClankerWorld.Simulation.Content.BuildingDefinition building) => Runtime.WorkBuildingHasInputs(actor, household, building);
        public global::ClankerWorld.Simulation.World.TownLayoutContext CreateTownLayoutContext(string actor, global::ClankerWorld.Simulation.World.GridPoint? selectedSite = null, global::ClankerWorld.Simulation.Content.BuildingDefinition? building = null) => Runtime.CreateTownLayoutContext(actor, selectedSite, building);
        public bool TryConstructionOrderSite(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person, out global::ClankerWorld.Simulation.Content.BuildingDefinition? definition, out string? household, out global::ClankerWorld.Simulation.World.GridPoint position, out string failure) => OrderSystem.TryConstructionOrderSite(Runtime.ConstructionOrdersWorld, instruction, person, out definition, out household, out position, out failure);
        public bool NeedsUrgentFood(global::ClankerWorld.Simulation.World.AgentState person) => WorldRuntime.NeedsUrgentFood(person);
        public bool IsActiveOrder(string status) => WorldRuntime.IsActiveOrder(status);
        public void CancelConstructionForOrder(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction) => OrderSystem.CancelConstructionForOrder(Runtime.ConstructionOrdersWorld, instruction);
        public string ConstructionOrderReceipt(string instanceId) => WorldRuntime.ConstructionOrderReceipt(instanceId);
        public void RecordConstructionReceipt(WorldConstructionReceipt receipt) => Runtime.constructionSystem.Buildings.RecordConstructionReceipt(receipt);
    }
}
