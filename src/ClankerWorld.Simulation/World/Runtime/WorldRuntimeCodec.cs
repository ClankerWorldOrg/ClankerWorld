using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

public static class WorldRuntimeCodec
{
    private const string ChunkedHeader = "clankerworld.world-runtime/v2";
    private static readonly JsonSerializerOptions CurrentOptions = CreateCurrentOptions();
    private static readonly JsonSerializerOptions CurrentReadOptions = CreateReadOptions(CurrentOptions);

    private static JsonSerializerOptions CreateReadOptions(JsonSerializerOptions source) => new(source)
    {
        // Required checkpoint members must not become null/default objects that
        // escape per-world compatibility checks as unexpected runtime faults.
        RespectNullableAnnotations = true,
        RespectRequiredConstructorParameters = true,
    };

    private static JsonSerializerOptions CreateCurrentOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false,
        };
        options.Converters.Add(new StoredIdentityJsonConverter());
        options.Converters.Add(new WorldTerrainChunkCodec());
        return options;
    }

    public static byte[] Encode(WorldRuntimeState state)
    {
        WorldRuntime.ValidateStateForCodec(state);
        return JsonSerializer.SerializeToUtf8Bytes(
            new RuntimeDocument(ChunkedHeader, state), CurrentOptions);
    }

    public static WorldRuntimeState Decode(ReadOnlyMemory<byte> bytes)
    {
        try
        {
            using var header = JsonDocument.Parse(bytes);
            if (header.RootElement.ValueKind != JsonValueKind.Object)
                throw new InvalidDataException("The world runtime checkpoint envelope is invalid.");
            if (header.RootElement.TryGetProperty("state", out var state) && state.ValueKind == JsonValueKind.Object &&
                state.TryGetProperty("schemaVersion", out var schemaVersion) &&
                schemaVersion.ValueKind == JsonValueKind.Number &&
                schemaVersion.TryGetInt32(out var schema))
            {
                WorldRuntime.ValidateMinimumSupportedSchemaVersion(schema);
            }
            if (!header.RootElement.TryGetProperty("format", out var format) ||
                format.ValueKind != JsonValueKind.String)
                throw new InvalidDataException("The world runtime checkpoint format is missing.");
            var version = format.GetString();
            if (version != ChunkedHeader)
                throw new InvalidDataException("The world runtime checkpoint format is unsupported.");
            if (header.RootElement.TryGetProperty("state", out var savedState))
                RejectEmptyListEntries(savedState);
            var document = JsonSerializer.Deserialize<RuntimeDocument>(bytes.Span, CurrentReadOptions)
                ?? throw new InvalidDataException("The world runtime checkpoint is empty.");
            if (document.State is null)
                throw new InvalidDataException("The world runtime checkpoint is empty.");
            WorldRuntime.ValidateStateForCodec(document.State);
            return document.State;
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("The world runtime checkpoint JSON is damaged.", exception);
        }
    }

    /// <summary>
    /// No saved list holds empty entries. <see cref="JsonSerializerOptions.RespectNullableAnnotations"/>
    /// rejects null members but not null list entries, which would otherwise
    /// reach validators as unexpected faults instead of a refused checkpoint.
    /// </summary>
    private static void RejectEmptyListEntries(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var member in element.EnumerateObject())
                    RejectEmptyListEntries(member.Value);
                break;
            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                {
                    if (item.ValueKind == JsonValueKind.Null)
                        throw new InvalidDataException("The world runtime checkpoint has an empty entry in a saved list.");
                    RejectEmptyListEntries(item);
                }
                break;
        }
    }

    private sealed record RuntimeDocument(string Format, WorldRuntimeState State);
}
