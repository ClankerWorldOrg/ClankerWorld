using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.World;

namespace ClankerWorld.Tests;

public sealed class WorldEventPayloadTests
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    public static IEnumerable<object[]> PayloadTypes => typeof(WorldEventPayload)
        .GetCustomAttributes(typeof(JsonDerivedTypeAttribute), false).Cast<JsonDerivedTypeAttribute>()
        .Select(attribute => new object[] { attribute.DerivedType });

    [Theory]
    [MemberData(nameof(PayloadTypes))]
    public void EveryDeclaredKindRoundTripsItsOwnFieldsAndRefusesForeignFields(Type type)
    {
        var constructor = type.GetConstructors().Single();
        var values = constructor.GetParameters().Select(parameter => parameter.ParameterType == typeof(string)
            ? (object)"person:Zoë:🌿:complete-reference"
            : parameter.ParameterType == typeof(bool) ? (object)true
            : parameter.ParameterType == typeof(int) || parameter.ParameterType == typeof(int?) ? (object)7
            : parameter.ParameterType == typeof(long) || parameter.ParameterType == typeof(long?) ? (object)11L
            : throw new InvalidOperationException("Unhandled payload field type: " + parameter.ParameterType)).ToArray();
        var payload = (WorldEventPayload)constructor.Invoke(values);
        payload = payload with { Position = new(7, 9) };
        var encoded = JsonSerializer.SerializeToUtf8Bytes(payload, Options);
        var decoded = JsonSerializer.Deserialize<WorldEventPayload>(encoded, Options)!;
        Assert.Equal(type, decoded.GetType());
        Assert.Equal(payload, decoded);
        Assert.Equal(payload.DiagnosticDetail, decoded.DiagnosticDetail);
        Assert.Equal(payload.SubjectIds, decoded.SubjectIds);
        var document = JsonNode.Parse(encoded)!.AsObject();
        Assert.False(document.ContainsKey("diagnosticDetail"));
        Assert.Equal(payload.Kind, document["kind"]!.GetValue<string>());
        document["foreignPayloadField"] = "not this kind";
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<WorldEventPayload>(document.ToJsonString(), Options));
    }

    [Fact]
    public async Task NativeBornActorAndTypedEventsSurviveExactSaveAndMatchedContinuation()
    {
        var state = await GrownAgentHelperMemoryTests.CancellationAdultStateAsync();
        var actor = Assert.Single(state.Society.Society.Births).ChildId;
        Assert.Contains(':', actor);
        using var world = WorldRuntime.Restore(state, _ => new DeterministicDecisionProvider());
        world.RenameAgent(actor, "Zoë 🌿 Vale");
        world.Pause();
        world.SubmitInstruction(new("typed-event-roundtrip", "owner", actor, OwnerInstructionKind.Order, "dance"));
        var accepted = world.ExportState();
        var entry = accepted.Events.Last(item => item.Kind == "instruction_not_understood");
        Assert.IsType<InstructionNotUnderstoodEventPayload>(entry.Payload);
        Assert.Equal(actor, entry.Payload.ActorId);
        Assert.Equal(accepted.Agents.Single(person => person.AgentId == actor).Position, entry.Position);
        var bytes = WorldRuntimeCodec.Encode(accepted);
        using var loaded = WorldRuntime.Restore(WorldRuntimeCodec.Decode(bytes), _ => new DeterministicDecisionProvider());
        Assert.Equal(bytes, WorldRuntimeCodec.Encode(loaded.ExportState()));
        world.Resume();
        loaded.Resume();
        await world.AdvanceOneTickAsync();
        await loaded.AdvanceOneTickAsync();
        Assert.Equal(WorldRuntimeCodec.Encode(world.ExportState()), WorldRuntimeCodec.Encode(loaded.ExportState()));
        var document = JsonNode.Parse(bytes)!;
        var payload = document["state"]!["events"]!.AsArray().Last()!["payload"]!.AsObject();
        payload["foreignField"] = "wrong kind";
        Assert.Throws<InvalidDataException>(() => WorldRuntimeCodec.Decode(JsonSerializer.SerializeToUtf8Bytes(document)));
    }
}
