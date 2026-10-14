namespace ClankerWorld.Simulation.World;

public enum SmithDeliveryKind { Input, Ore }

public sealed record SmithDeliveryActionPlan(string ActorId, SmithDeliveryKind Kind, string HouseholdId,
    string SmithId, GridPoint Destination, string StockId, string OwnerId, string ItemKind, int Quantity,
    int PhysicalQuantity, bool DirectDelivery, GridPoint Target, int InteractionRange,
    IReadOnlyList<PlannedStockMember>? Family, IReadOnlyList<GridPoint> Route) : ActionPlan(ActorId,
        Kind == SmithDeliveryKind.Ore ? "deliver_smith_ore" : "haul_smith_input")
{
    public override string Activity => "take supplies to the household Blacksmith";
}
