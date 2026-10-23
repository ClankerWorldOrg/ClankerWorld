namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private IOrderBlockersWorld OrderBlockersWorld => new OrderBlockersAdapter(this);

    private sealed class OrderBlockersAdapter(WorldRuntime runtime) : GoodsRuntimeAdapter(runtime), IOrderBlockersWorld
    {
        public string? KnowledgeReadOrderBlocker(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction) => Runtime.KnowledgeReadOrderBlocker(instruction);
        public string? TalkOrderBlocker(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person) => Runtime.TalkOrderBlocker(instruction, person);
        public bool IsCartOrder(string action) => WorldRuntime.IsCartOrder(action);
        public bool IsAnimalOrder(string action) => WorldRuntime.IsAnimalOrder(action);
        public string AnimalOrderBlockedReason(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction) => Runtime.AnimalOrderBlockedReason(instruction);
        public string ShelterOrderBlockedReason(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person) => Runtime.ShelterOrderBlockedReason(instruction, person);
        public string ConstructionOrderBlockedReason(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person) => OrderSystem.ConstructionOrderBlockedReason(Runtime.ConstructionOrdersWorld, instruction, person);
        public string CollectionOrderBlockedReason(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person) => Runtime.CollectionOrderBlockedReason(instruction, person);
        public string StorageOrderBlockedReason(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person) => Runtime.StorageOrderBlockedReason(instruction, person);
        public string ReturnOrderBlockedReason(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person) => Runtime.ReturnOrderBlockedReason(instruction, person);
        public int ComfortableFullness => WorldRuntime.ComfortableFullness;
        public int FoodHarvestCarryUnits(global::ClankerWorld.Simulation.World.MapResource source) => WorldRuntime.FoodHarvestCarryUnits(source);
        public global::System.Collections.Generic.IEnumerable<global::ClankerWorld.Simulation.World.PlacedBuilding> BuildingsWithTag(string tag) => Runtime.BuildingsWithTag(tag);
        public bool HasHouseGuestInvitation(string actor, string houseId) => Runtime.HasHouseGuestInvitation(actor, houseId);
    }
}
