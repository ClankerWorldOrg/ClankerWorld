using System.Text.Json;
using ClankerWorld.Simulation.Kernel;

namespace ClankerWorld.Simulation.World;

public sealed record ToolRequestCollectionChoice(string Token)
{
    public const string Prefix = "tool_request_collect:";
    public static string Encode(string request) => DerivedId.Hash(JsonSerializer.Serialize(new[] { request }), Prefix);
    public string Encode() => Prefix + Token;
    public static ToolRequestCollectionChoice? Decode(string value) => value.StartsWith(Prefix, StringComparison.Ordinal) && value.Length > Prefix.Length
        ? new(value[Prefix.Length..]) : null;
}

public sealed record PlannedShopQuote(InventoryLot Goods, int GoodsQuantity, InventoryLot Payment, int PaymentQuantity);

public sealed record ToolRequestCollectionActionPlan(string ActorId, ToolRequestCollectionChoice Choice,
    ToolMakingRequestState Request, PlacedBuilding Smith, IReadOnlyList<GridPoint> Route, PlannedShopQuote? Quote) : ActionPlan(ActorId, Choice.Encode())
{
    public override string Activity => "collect that finished tool at the agreed Blacksmith";
}
