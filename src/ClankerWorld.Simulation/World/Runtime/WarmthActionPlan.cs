namespace ClankerWorld.Simulation.World;

public enum WarmthChoiceKind { Fire, Protection }

public readonly record struct WarmthChoice(WarmthChoiceKind Kind)
{
    public string Encode() => Kind == WarmthChoiceKind.Fire ? "tend_fire" : "seek_warmth";
    public static bool TryDecode(string candidate, out WarmthChoice choice)
    {
        choice = new(candidate == "tend_fire" ? WarmthChoiceKind.Fire : WarmthChoiceKind.Protection);
        return candidate == choice.Encode();
    }
}

public abstract record PlannedFireFuel;
public sealed record CarriedFireFuel(string StockId) : PlannedFireFuel;
public sealed record SharedFireFuel(PlannedEquipmentPickup Pickup) : PlannedFireFuel;
public sealed record GatheredFireFuel(MapResource Source, ToolGatheringPlan Harvest,
    string? ToolOwnerId, string? ToolKind) : PlannedFireFuel;

public sealed record FireActionPlan(string ActorId, string BuildingId, GridPoint Destination,
    int InteractionRange, PlannedFireFuel Fuel, IReadOnlyList<GridPoint> Route) : ActionPlan(ActorId, new WarmthChoice(WarmthChoiceKind.Fire).Encode())
{
    public override string Activity => "fuel the selected hearth";
}

public enum WarmthDestinationKind { Remain, Building, NaturalCover }

public sealed record WarmthActionPlan(string ActorId, WarmthDestinationKind Kind, GridPoint Destination,
    int InteractionRange, string? BuildingId, bool Heating, IReadOnlyList<GridPoint> Route) : ActionPlan(ActorId, new WarmthChoice(WarmthChoiceKind.Protection).Encode())
{
    public override string Activity => "seek the selected warmth protection";
}
