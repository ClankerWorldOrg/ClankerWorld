namespace ClankerWorld.Simulation.World;

public enum LandHearingPropertyKind { Request, Accept, Refuse }

public sealed record LandHearingPropertyChoice(string TownId, LandHearingPropertyKind Kind, string SubjectToken, string TargetToken)
{
    public static string Verb(LandHearingPropertyKind kind) => kind switch
    {
        LandHearingPropertyKind.Request => "hearing_property_request",
        LandHearingPropertyKind.Accept => "hearing_property_accept",
        _ => "hearing_property_refuse"
    };
    public string Encode() => $"civic|{TownId}|{Verb(Kind)}|{SubjectToken}|{TargetToken}";
    public static LandHearingPropertyChoice? Decode(string value)
    {
        if (value.Split('|') is not ["civic", var town, var verb, var subject, var target] || town.Length == 0 || subject.Length == 0) return null;
        LandHearingPropertyKind? kind = verb switch
        {
            "hearing_property_request" => LandHearingPropertyKind.Request,
            "hearing_property_accept" => LandHearingPropertyKind.Accept,
            "hearing_property_refuse" => LandHearingPropertyKind.Refuse,
            _ => null
        };
        return kind is { } selected ? new(town, selected, subject, target) : null;
    }
}

public sealed record LandHearingPropertyActionPlan(string ActorId, LandHearingPropertyChoice Choice,
    TownPropertyRequest Request, TownPropertySnapshot Snapshot, IReadOnlyList<GridPoint> Tiles, GridPoint? NoticePlace,
    bool Mayor, TownOffice? Office, long CouncilRevision, string? CaseId = null, TownLandCaseRevision? Revision = null,
    TownPropertyConsent? PreviousConsent = null, string? Statement = null, bool Bound = false, bool InvalidStatement = false)
    : ActionPlan(ActorId, Choice.Encode())
{
    public override string Activity => Choice.Kind == LandHearingPropertyKind.Request
        ? "file that selected property hearing" : "record that selected personal property consent";
}
