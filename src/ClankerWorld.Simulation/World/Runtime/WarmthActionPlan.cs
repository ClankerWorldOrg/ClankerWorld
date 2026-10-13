namespace ClankerWorld.Simulation.World;

public abstract record PlannedFireFuel;
public sealed record CarriedFireFuel(string StockId) : PlannedFireFuel;
public sealed record SharedFireFuel(PlannedEquipmentPickup Pickup) : PlannedFireFuel;
public sealed record GatheredFireFuel(MapResource Source, ToolGatheringPlan Harvest,
    string? ToolOwnerId, string? ToolKind) : PlannedFireFuel;

public sealed record FireActionPlan(string ActorId, string BuildingId, GridPoint Destination,
    int InteractionRange, PlannedFireFuel Fuel, IReadOnlyList<GridPoint> Route) : ActionPlan(ActorId, "tend_fire")
{
    public override string Activity => "fuel the selected hearth";
}

public enum WarmthDestinationKind { Remain, Building, NaturalCover }

public sealed record WarmthActionPlan(string ActorId, WarmthDestinationKind Kind, GridPoint Destination,
    int InteractionRange, string? BuildingId, bool Heating, IReadOnlyList<GridPoint> Route) : ActionPlan(ActorId, "seek_warmth")
{
    public override string Activity => "seek the selected warmth protection";
}
