using System.Text.Json;
using ClankerWorld.Simulation.Content;

namespace ClankerWorld.Simulation.World;

public sealed record ToolRequestWorkChoice(string Token)
{
    public const string Prefix = "tool_request_work:";
    public static string Encode(string request) => DerivedId.Hash(JsonSerializer.Serialize(new[] { request }), Prefix);
    public string Encode() => Prefix + Token;
    public static ToolRequestWorkChoice? Decode(string value) => value.StartsWith(Prefix, StringComparison.Ordinal) && value.Length > Prefix.Length
        ? new(value[Prefix.Length..]) : null;
}

public sealed record ToolRequestWorkActionPlan(string ActorId, ToolRequestWorkChoice Choice, ToolMakingRequestState Request,
    AgentProject Project, PlacedBuilding Smith, RecipeDefinition Recipe) : ActionPlan(ActorId, Choice.Encode())
{
    public override string Activity => "continue that household tool commission";
}
