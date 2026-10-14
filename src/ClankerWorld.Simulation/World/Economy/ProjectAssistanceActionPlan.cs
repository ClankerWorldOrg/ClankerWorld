using ClankerWorld.Simulation.Content;

namespace ClankerWorld.Simulation.World;

public sealed record ProjectAssistanceChoice(string ItemKind)
{
    public const string Prefix = "assist:";
    public string Encode() => Prefix + ItemKind;
}

public sealed record ProjectAssistanceActionPlan(string ActorId, ProjectAssistanceChoice Choice, string RequesterId,
    string ProjectCandidateId, long ProjectStartedTick, ContentQuantity RequestedInput, string OwnerId,
    string RecipientId, string? HouseId, GridPoint Destination, int Range, string? LotId, int Quantity,
    MapResource? Source, ToolGatheringPlan? Harvest, string? ToolOwnerId, string? ToolKind,
    IReadOnlyList<GridPoint> Route, IReadOnlyList<GridPoint> ReturnRoute)
    : ActionPlan(ActorId, Choice.Encode())
{
    public override string Activity => "help that selected project with its materials";
}
