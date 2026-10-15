using System.Text.Json;
using ClankerWorld.Simulation.Content;

namespace ClankerWorld.Simulation.World;

public enum ToolRequestConsentKind { Place, Accept, Refuse, Withdraw }

public sealed record ToolRequestConsentChoice(ToolRequestConsentKind Kind, string Token)
{
    public static string Encode(ToolRequestConsentKind kind, params string[] identity) =>
        DerivedId.Hash(JsonSerializer.Serialize(identity), "tool_request_" + kind.ToString().ToLowerInvariant() + ":");
    public string Encode() => "tool_request_" + Kind.ToString().ToLowerInvariant() + ":" + Token;
    public static ToolRequestConsentChoice? Decode(string value)
    {
        foreach (var kind in Enum.GetValues<ToolRequestConsentKind>())
        {
            var prefix = "tool_request_" + kind.ToString().ToLowerInvariant() + ":";
            if (value.StartsWith(prefix, StringComparison.Ordinal) && value.Length > prefix.Length) return new(kind, value[prefix.Length..]);
        }
        return null;
    }
}

public sealed record ToolRequestConsentActionPlan(string ActorId, ToolRequestConsentChoice Choice, PlacedBuilding Shop,
    RecipeDefinition Recipe, ToolMakingRequestState? Original) : ActionPlan(ActorId, Choice.Encode())
{
    public override string Activity => Choice.Kind switch
    {
        ToolRequestConsentKind.Place => "ask that Blacksmith for a tool",
        ToolRequestConsentKind.Accept => "accept that tool request",
        ToolRequestConsentKind.Refuse => "refuse that tool request",
        _ => "withdraw that tool request",
    };
}
