using System.Text.Json.Serialization;

namespace ClankerWorld.Simulation.World;

/// <summary>Events with no subject identities; field validation remains required.</summary>
public abstract record SubjectlessWorldEventPayload(string? ActorId) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public sealed override IReadOnlyList<string> SubjectIds => [];
}

/// <summary>Events with only scalar values that require no additional field checks.</summary>
public abstract record ScalarWorldEventPayload(string? ActorId) : SubjectlessWorldEventPayload(ActorId)
{
    [JsonIgnore] public sealed override bool FieldsAreValid => true;
}
