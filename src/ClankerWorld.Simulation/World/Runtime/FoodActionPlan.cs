namespace ClankerWorld.Simulation.World;

public enum FoodActionKind { Consume, CollectShared, Seek, Harvest }

public sealed record FoodChoice(FoodActionKind Kind)
{
    public string Encode() => Kind switch
    {
        FoodActionKind.Consume => "consume_food",
        FoodActionKind.CollectShared => "collect_shared_food",
        FoodActionKind.Seek => "seek_food",
        FoodActionKind.Harvest => "harvest_food",
        _ => throw new ArgumentOutOfRangeException(nameof(Kind)),
    };
}

public abstract record FoodActionPlan(string ActorId, FoodChoice Choice) : ActionPlan(ActorId, Choice.Encode());

public sealed record ConsumeFoodActionPlan(string ActorId, string StockId, string ItemKind, string RootId)
    : FoodActionPlan(ActorId, new(FoodActionKind.Consume))
{
    public override string Activity => "eat the selected serving";
}

public sealed record SharedFoodActionPlan(string ActorId, string HouseholdId, string StockId, string ItemKind,
    string RootId, GridPoint Position, int InteractionRange, IReadOnlyList<GridPoint> Route)
    : FoodActionPlan(ActorId, new(FoodActionKind.CollectShared))
{
    public override string Activity => "collect the selected household serving";
}

public sealed record FoodResourceActionPlan(string ActorId, FoodChoice Selected, MapResource Source,
    string ItemKind, int Quantity, int SeedQuantity, IReadOnlyList<GridPoint> Route) : FoodActionPlan(ActorId, Selected)
{
    public override string Activity => Selected.Kind == FoodActionKind.Seek
        ? "reach the selected food source" : "harvest the selected food source";
}
