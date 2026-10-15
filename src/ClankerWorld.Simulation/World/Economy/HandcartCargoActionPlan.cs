namespace ClankerWorld.Simulation.World;

public enum HandcartCargoKind { Load, Unload, UnloadGround }

public sealed record HandcartCargoChoice(HandcartCargoKind Kind, string StockId)
{
    public const string LoadPrefix = "load_handcart:";
    public const string UnloadPrefix = "unload_handcart:";
    public const string GroundPrefix = "unload_handcart_ground:";
    public string Encode() => (Kind == HandcartCargoKind.Load ? LoadPrefix : Kind == HandcartCargoKind.Unload ? UnloadPrefix : GroundPrefix) + StockId;
    public static HandcartCargoChoice? Decode(string value) => value.StartsWith(LoadPrefix, StringComparison.Ordinal) ? new(HandcartCargoKind.Load, value[LoadPrefix.Length..]) :
        value.StartsWith(UnloadPrefix, StringComparison.Ordinal) ? new(HandcartCargoKind.Unload, value[UnloadPrefix.Length..]) :
        value.StartsWith(GroundPrefix, StringComparison.Ordinal) ? new(HandcartCargoKind.UnloadGround, value[GroundPrefix.Length..]) : null;
}

public sealed record HandcartCargoActionPlan(string ActorId, HandcartCargoChoice Choice, string CartId, GridPoint Position,
    IReadOnlyList<PlannedStockMember> CartFamily, HandcartHitch? Hitch, string StockOwner, string ItemKind,
    IReadOnlyList<PlannedStockMember> StockFamily, GoodsPlace SourcePlace, int Quantity, int DisplayQuantity)
    : ActionPlan(ActorId, Choice.Encode())
{
    public override string Activity => Choice.Kind == HandcartCargoKind.Load ? "load the handcart" : "unload the handcart";
}
