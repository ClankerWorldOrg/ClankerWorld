using ClankerWorld.Simulation.Kernel;

namespace ClankerWorld.Simulation.World;

public enum PersonalGoodsKind { Collect, Store, Return }
public enum PersonalGoodsMode { Choice, Native, BoundOrder }

public sealed record PersonalGoodsChoice(PersonalGoodsKind Kind, string LotId)
{
    public string Encode() => (Kind switch { PersonalGoodsKind.Collect => "household_collect:", PersonalGoodsKind.Store => "household_store_personal:", _ => "household_return:" }) + LotId;
    public static PersonalGoodsChoice? Decode(string candidate) => candidate.StartsWith("household_collect:", StringComparison.Ordinal)
        ? new(PersonalGoodsKind.Collect, candidate["household_collect:".Length..]) : candidate.StartsWith("household_store_personal:", StringComparison.Ordinal)
            ? new(PersonalGoodsKind.Store, candidate["household_store_personal:".Length..]) : candidate.StartsWith("household_return:", StringComparison.Ordinal)
                ? new(PersonalGoodsKind.Return, candidate["household_return:".Length..]) : null;
}

public sealed record PersonalGoodsActionPlan(string ActorId, PersonalGoodsChoice Choice, PersonalGoodsMode Mode,
    string OwnerId, string ItemKind, int Quantity, int PhysicalQuantity, string? SourceCarrierId, string? SourceStorageId,
    InventoryGroundPosition? SourceGround, string? HouseId, string? HouseOwnerId, GridPoint Target, int Range,
    IReadOnlyList<GridPoint> Route, IReadOnlyList<PlannedStockMember>? Family) : ActionPlan(ActorId, Choice.Encode())
{
    public override string Activity => (Choice.Kind switch { PersonalGoodsKind.Collect => "collect", PersonalGoodsKind.Store => "store", _ => "return" }) +
        $" the selected {ItemKind.Replace('_', ' ')}";
}
