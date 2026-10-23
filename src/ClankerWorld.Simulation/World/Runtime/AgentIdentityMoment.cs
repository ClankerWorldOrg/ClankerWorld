using System.Text.Json.Serialization;

namespace ClankerWorld.Simulation.World;

public enum AgentIdentityMomentKind
{
    Midlife,
    Parenthood,
    PartnerLoss,
    ParentLoss,
    Elder,
}

/// <summary>One saved opportunity per named moment; requested opportunities are never retried.</summary>
public sealed record AgentIdentityMoment(
    AgentIdentityMomentKind Kind,
    long TriggeredTick,
    string Outcome = "waiting",
    long? RequestedTick = null,
    long? CompletedTick = null,
    string? Personality = null,
    string? Aspiration = null)
{
    [JsonIgnore]
    public string Reason => ReasonFor(Kind);

    public static string ReasonFor(AgentIdentityMomentKind kind) => kind switch
    {
        AgentIdentityMomentKind.Midlife => "Reaching the middle of life",
        AgentIdentityMomentKind.Parenthood => "Becoming a parent",
        AgentIdentityMomentKind.PartnerLoss => "Losing a partner",
        AgentIdentityMomentKind.ParentLoss => "Losing a parent",
        AgentIdentityMomentKind.Elder => "Becoming an elder",
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    internal static void Validate(IReadOnlyList<AgentIdentityMoment>? moments, long worldTick, int schemaVersion)
    {
        if (moments is null) return;
        if (schemaVersion < WorldRuntime.LifeMomentIdentitySchemaVersion)
            throw new InvalidDataException(
                $"Saved life moments require world schema {WorldRuntime.LifeMomentIdentitySchemaVersion}.");
        CognitionSystem.ValidateLocalIdentityMoments(moments);
        foreach (var item in moments)
            if (item.TriggeredTick > worldTick || item.RequestedTick > worldTick || item.CompletedTick > worldTick)
                throw new InvalidDataException("The saved life moment is invalid.");
    }
}
