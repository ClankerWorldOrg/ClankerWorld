using System.Text.Json;
using System.Text.Json.Nodes;
using ClankerWorld.Simulation.World;

namespace ClankerWorld.Tests;

internal sealed record OrderSaveChange(OrderTask Original, IReadOnlyDictionary<string, object?> Fields);

/// <summary>Mutates serialized input so tests can exercise shapes the typed API cannot construct.</summary>
internal static class OrderSaveMutation
{
    private static readonly JsonSerializerOptions Options = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    private static readonly HashSet<string> ProgressFields = new(StringComparer.OrdinalIgnoreCase)
    { "Status", "RequestedUnits", "CompletedUnits", "ProgressUnit", "RepeatUntilCancelled", "QuantityIsExplicit",
        "BlockedReason", "LastEffectId", "WaitForDecisionAfterFailure" };

    public static void SetField(JsonObject task, string field, JsonNode? value)
    {
        var target = ProgressFields.Contains(field) ? task : task["definition"]!.AsObject();
        var name = field.Equals("action", StringComparison.OrdinalIgnoreCase) ? "kind" : JsonNamingPolicy.CamelCase.ConvertName(field);
        target[name] = value;
    }

    public static WorldRuntimeState Decode(WorldRuntimeState state, OrderSaveChange change) =>
        WorldRuntimeCodec.Decode(Encode(state, change));

    public static byte[] Encode(WorldRuntimeState state, OrderSaveChange change)
    {
        var instruction = state.Instructions!.Single(item => item.Task == change.Original);
        var document = JsonNode.Parse(WorldRuntimeCodec.Encode(state))!.AsObject();
        var savedInstruction = document["state"]!["instructions"]!.AsArray()
            .Single(item => item!["instructionId"]!.GetValue<string>() == instruction.InstructionId)!;
        var task = savedInstruction["task"]!.AsObject();
        foreach (var (field, value) in change.Fields)
        {
            SetField(task, field, JsonSerializer.SerializeToNode(value, Options));
        }
        return JsonSerializer.SerializeToUtf8Bytes(document, Options);
    }
}
