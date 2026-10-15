using System.Text.Json;
using ClankerWorld.Simulation.Content;

namespace ClankerWorld.Simulation.World;

public sealed record ToolRequestVisitChoice(string Token)
{
    public const string Prefix = "tool_request_visit:";
    public static string Encode(string smith, string recipe) => DerivedId.Hash(JsonSerializer.Serialize(new[] { smith, recipe }), Prefix);
    public string Encode() => Prefix + Token;
    public static ToolRequestVisitChoice? Decode(string value) => value.StartsWith(Prefix, StringComparison.Ordinal) && value.Length > Prefix.Length
        ? new(value[Prefix.Length..]) : null;
}

public sealed record ToolRequestVisitActionPlan(string ActorId, ToolRequestVisitChoice Choice, PlacedBuilding Smith,
    RecipeDefinition Recipe, IReadOnlyList<GridPoint> Route) : ActionPlan(ActorId, Choice.Encode())
{
    public override string Activity => "visit that Blacksmith for the selected tool";
}
