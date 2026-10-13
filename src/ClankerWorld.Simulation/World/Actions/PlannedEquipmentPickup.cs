namespace ClankerWorld.Simulation.World;

public sealed record PlannedEquipmentPickup(string StockId, string OwnerId, string ItemKind,
    GridPoint Position, int InteractionRange);
