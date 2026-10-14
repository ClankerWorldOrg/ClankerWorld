namespace ClankerWorld.Simulation.World;

public enum LandHearingEvidenceKind { Inspect, Observe, Statement, Answer, Waive, Relay }

public sealed record LandHearingEvidenceChoice(string TownId, LandHearingEvidenceKind Kind, string RevisionToken, string PartyOrRecipientToken)
{
    public static string Verb(LandHearingEvidenceKind kind) => kind switch
    {
        LandHearingEvidenceKind.Inspect => "hearing_inspect",
        LandHearingEvidenceKind.Observe => "hearing_observe",
        LandHearingEvidenceKind.Statement => "hearing_statement",
        LandHearingEvidenceKind.Answer => "hearing_answer",
        LandHearingEvidenceKind.Waive => "hearing_waive",
        _ => "hearing_relay"
    };
    public string Encode() => $"civic|{TownId}|{Verb(Kind)}|{RevisionToken}|{PartyOrRecipientToken}";
    public static LandHearingEvidenceChoice? Decode(string value)
    {
        if (value.Split('|') is not ["civic", var town, var verb, var revision, var target] || town.Length == 0 || revision.Length == 0) return null;
        LandHearingEvidenceKind? kind = verb switch
        {
            "hearing_inspect" => LandHearingEvidenceKind.Inspect,
            "hearing_observe" => LandHearingEvidenceKind.Observe,
            "hearing_statement" => LandHearingEvidenceKind.Statement,
            "hearing_answer" => LandHearingEvidenceKind.Answer,
            "hearing_waive" => LandHearingEvidenceKind.Waive,
            "hearing_relay" => LandHearingEvidenceKind.Relay,
            _ => null
        };
        return kind is { } selected ? new(town, selected, revision, target) : null;
    }
}

public sealed record LandPlotObservation(IReadOnlyList<GridPoint> VisibleTiles, string Description);

public sealed record LandHearingEvidenceActionPlan(string ActorId, LandHearingEvidenceChoice Choice,
    string CaseId, TownLandCaseRevision Revision, GridPoint? NoticePlace, TownLandCaseParty? Party,
    string? Recipient, IReadOnlyList<string> RelayedEvidence, IReadOnlyList<string> RelayedRequests,
    LandPlotObservation? Observation, string? Statement = null, bool StatementBound = false, bool InvalidStatement = false)
    : ActionPlan(ActorId, Choice.Encode())
{
    public override string Activity => Choice.Kind switch
    {
        LandHearingEvidenceKind.Inspect => "inspect that selected land case file",
        LandHearingEvidenceKind.Observe => "observe that selected nearby land case plot",
        LandHearingEvidenceKind.Statement => "submit that selected land case statement",
        LandHearingEvidenceKind.Answer => "answer for that selected land case party",
        LandHearingEvidenceKind.Waive => "waive that selected land case response",
        _ => "relay that selected land case material"
    };
}
