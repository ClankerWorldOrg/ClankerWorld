namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private ICartCargoOrdersWorld CartCargoOrdersWorld => new CartCargoOrdersAdapter(this);

    private sealed class CartCargoOrdersAdapter(WorldRuntime runtime) : GoodsRuntimeAdapter(runtime), ICartCargoOrdersWorld
    {
        public string FoodKnowledgeKind(global::ClankerWorld.Simulation.World.MapResource resource) => WorldRuntime.FoodKnowledgeKind(resource);
        public bool IsCartCargoKind(string? kind) => WorldRuntime.IsCartCargoKind(kind);
        public global::ClankerWorld.Simulation.World.OrderTask? ParseCartOrder(string text, string actor) => Runtime.ParseCartOrder(text, actor);
        public bool HasActiveContainerReservation(global::ClankerWorld.Simulation.Kernel.InventoryCheckpoint inventory, string containerId) => WorldRuntime.HasActiveContainerReservation(inventory, containerId);
        public int CargoQuantity(string cartId) => Runtime.CargoQuantity(cartId);
        public global::System.Collections.Generic.IEnumerable<global::ClankerWorld.Simulation.Kernel.InventoryLot> CartCargoLots(string actor, string cartId) => Runtime.CartCargoLots(actor, cartId);
        public global::ClankerWorld.Simulation.World.GridPoint CartPosition(global::ClankerWorld.Simulation.Kernel.InventoryLot cart) => WorldRuntime.CartPosition(cart);
        public bool CanLoadCartLot(string actor, global::ClankerWorld.Simulation.World.AgentState person, global::ClankerWorld.Simulation.Kernel.InventoryLot lot) => TransportSystem.CanLoadCartLot(Runtime.HandcartWorld, actor, person, lot);
        public global::ClankerWorld.Simulation.Kernel.InventoryLot? CargoLotForOrder(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person, global::ClankerWorld.Simulation.Kernel.InventoryLot cart) => Runtime.CargoLotForOrder(instruction, person, cart);
        public int ContainerContentsQuantity(global::ClankerWorld.Simulation.Kernel.InventoryCheckpoint inventory, string containerId) => WorldRuntime.ContainerContentsQuantity(inventory, containerId);
        public string LoadCartPrefix => global::ClankerWorld.Simulation.World.HandcartCargoChoice.LoadPrefix;
        public string UnloadCartGroundPrefix => global::ClankerWorld.Simulation.World.HandcartCargoChoice.GroundPrefix;
        public string UnloadCartPrefix => global::ClankerWorld.Simulation.World.HandcartCargoChoice.UnloadPrefix;
        public string CartCargoOrderEffectId(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, int completed) => WorldRuntime.CartCargoOrderEffectId(instruction, completed);
    }
}
