using ClankerWorld.Simulation.Content;

namespace ClankerWorld.Simulation.World;

public abstract record ProjectInputStep;
public sealed record ProjectInputDelivery(string LotId, int Quantity) : ProjectInputStep;
public sealed record ProjectInputPickup(PlacedBuilding Warehouse, string LotId, int Quantity) : ProjectInputStep;
public sealed record ProjectInputHarvest(MapResource Source, ToolGatheringPlan? Harvest,
    string? ToolOwnerId, string? ToolKind, PlannedEquipmentPickup? ToolPickup) : ProjectInputStep;
public enum ProjectInputWaitReason { WorkSite, Source, CarrySpace }
public sealed record ProjectInputWait(ProjectInputWaitReason Reason, MapResource? Source = null) : ProjectInputStep;

public sealed record ProjectInputActionPlan(string ActorId, string ProjectCandidateId, long ProjectStartedTick,
    ContentQuantity Input, string ActorHouseholdId, string OwnerId, string? HouseId, GridPoint? Destination, int DestinationRange,
    ProjectInputStep Step, IReadOnlyList<GridPoint> Route, IReadOnlyList<GridPoint> ReturnRoute)
    : ActionPlan(ActorId, ProjectCandidateId)
{
    public override string Activity => $"acquire {Input.ResourceId.Replace('_', ' ')} for that project";
}
