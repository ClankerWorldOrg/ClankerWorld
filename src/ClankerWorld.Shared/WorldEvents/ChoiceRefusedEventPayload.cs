using System.Text.Json.Serialization;

namespace ClankerWorld.Simulation.World;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ChoiceRefusedEventPayload(string? ActorId,
    [property: JsonRequired] string ActorName,
    [property: JsonRequired] string Activity,
    [property: JsonRequired] string Explanation) : SubjectlessWorldEventPayload(ActorId)
{
    [JsonIgnore] public override string DiagnosticDetail => $"{ActorName} didn't {Activity}: {Explanation}.";
    [JsonIgnore] public override bool FieldsAreValid => ActorName is not null && Activity is not null && Explanation is not null;
}
