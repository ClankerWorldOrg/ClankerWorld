namespace ClankerWorld.Simulation.World;

public sealed record NonLandEvidenceChoice(string TownId, string Verb, string CaseToken, string PartyOrRecipientToken)
{
    public string Encode() => $"civic|{TownId}|{Verb}|{CaseToken}|{PartyOrRecipientToken}";
    public static NonLandEvidenceChoice? Decode(string value) =>
        value.Split('|') is ["civic", var town, var verb, var item, var target] && town.Length > 0 && item.Length > 0 &&
        verb is "law_case_inspect" or "law_case_evidence" or "law_case_statement" or "law_case_advise" or
            "law_case_answer" or "law_case_waive" or "law_case_relay" ? new(town, verb, item, target) : null;
}

public sealed record NonLandEvidenceActionPlan(string ActorId, NonLandEvidenceChoice Choice, string CaseId,
    TownCaseRevision Revision, GridPoint? NoticePlace, IReadOnlyList<TownCaseEvidence> Evidence, TownCaseEvidence? LawEvidence,
    TownCaseParty? Party, string? Recipient, TownCaseRead? SourceRead, string? Statement = null, bool Bound = false)
    : ActionPlan(ActorId, Choice.Encode())
{
    public override string Activity => Choice.Verb switch
    {
        "law_case_inspect" => "inspect that selected public case",
        "law_case_evidence" => "submit those selected known sources",
        "law_case_answer" or "law_case_waive" => "respond for that selected person",
        "law_case_relay" => "relay that selected case material",
        _ => "submit that personal case statement"
    };
}
