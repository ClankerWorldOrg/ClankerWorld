using System.Security.Cryptography;
using System.Text.Json;

namespace ClankerWorld.Simulation.Society;

public static class SocietyRuntimeCodec
{
    private const string Header = "clankerworld.society-runtime/v4";
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
        Converters = { new StoredIdentityJsonConverter() },
    };

    public static byte[] Encode(SocietyWorldRuntimeState state)
    {
        Validate(state);
        return JsonSerializer.SerializeToUtf8Bytes(new RuntimeDocument(Header, state), Options);
    }

    public static SocietyWorldRuntimeState Decode(ReadOnlyMemory<byte> bytes)
    {
        var document = JsonSerializer.Deserialize<RuntimeDocument>(bytes.Span, Options)
            ?? throw new InvalidDataException("The Phase 4 runtime checkpoint is empty.");
        if (document.Format != Header || document.State is null)
        {
            throw new InvalidDataException("The Phase 4 runtime checkpoint format is not supported.");
        }

        Validate(document.State);
        return document.State;
    }

    public static string Digest(SocietyWorldRuntimeState state) =>
        Convert.ToHexStringLower(SHA256.HashData(Encode(state)));

    private static void Validate(SocietyWorldRuntimeState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (state.SchemaVersion != SocietyRuntime.StateSchemaVersion)
        {
            throw new InvalidDataException("The Phase 4 runtime state schema is unsupported.");
        }

        SocietyRules.Validate(state.Society);
        var scheduler = SocietyCognitionScheduler.Restore(state.Cognition);
        scheduler.Validate();
        var activeIds = state.Society.Agents
            .Where(item => item.Status == SocietyAgentStatus.Active)
            .Select(item => item.Id)
            .OrderBy(item => item, StringComparer.Ordinal);
        if (!activeIds.SequenceEqual(scheduler.AgentIds.OrderBy(item => item, StringComparer.Ordinal)))
        {
            throw new InvalidDataException("The Phase 4 runtime populations disagree.");
        }
    }

    private sealed record RuntimeDocument(string Format, SocietyWorldRuntimeState State);
}

public static class SocietyCheckpointCodec
{
    private const string Header = "clankerworld.society/v4";
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
        Converters = { new StoredIdentityJsonConverter() },
    };

    public static byte[] Encode(SocietyCheckpoint checkpoint)
    {
        SocietyRules.Validate(checkpoint);
        return JsonSerializer.SerializeToUtf8Bytes(
            new SocietyDocument(Header, checkpoint),
            Options);
    }

    public static SocietyCheckpoint Decode(ReadOnlyMemory<byte> bytes)
    {
        var document = JsonSerializer.Deserialize<SocietyDocument>(bytes.Span, Options)
            ?? throw new InvalidDataException("The society checkpoint is empty.");
        if (document.Format != Header || document.Checkpoint is null)
        {
            throw new InvalidDataException("The society checkpoint format is not supported.");
        }

        SocietyRules.Validate(document.Checkpoint);
        return document.Checkpoint;
    }

    public static string StateDigest(SocietyCheckpoint checkpoint)
    {
        var stateOnly = checkpoint with { Events = [] };
        return Convert.ToHexStringLower(SHA256.HashData(Encode(stateOnly)));
    }

    private sealed record SocietyDocument(string Format, SocietyCheckpoint Checkpoint);
}
