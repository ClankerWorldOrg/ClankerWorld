namespace ClankerWorld.Simulation.World;

public enum LandHearingFilingKind { Household, TownOffice, CouncilProposal }

public sealed record LandHearingFilingChoice(string TownId, LandHearingFilingKind Kind, string AuthorityToken)
{
    public static string Verb(LandHearingFilingKind kind) => kind switch
    {
        LandHearingFilingKind.Household => "hearing_file",
        LandHearingFilingKind.TownOffice => "hearing_file_town",
        _ => "hearing_propose_town"
    };
    public string Encode() => $"civic|{TownId}|{Verb(Kind)}||{AuthorityToken}";
    public static LandHearingFilingChoice? Decode(string value)
    {
        if (value.Split('|') is not ["civic", var town, var verb, "", var authority] ||
            !IdentityRules.Reference(town, DerivedId.MaximumLength, requireTrimmed: true)) return null;
        LandHearingFilingKind? kind = verb switch
        {
            "hearing_file" => LandHearingFilingKind.Household,
            "hearing_file_town" => LandHearingFilingKind.TownOffice,
            "hearing_propose_town" => LandHearingFilingKind.CouncilProposal,
            _ => null
        };
        return kind is { } selected && (selected == LandHearingFilingKind.TownOffice && authority == "" ||
            IdentityRules.Reference(authority, DerivedId.MaximumLength)) ? new(town, selected, authority) : null;
    }
}

public sealed record LandHearingFilingActionPlan(string ActorId, LandHearingFilingChoice Choice, GridPoint NoticePlace,
    string? Household, TownOffice? Office, long CouncilRevision, IReadOnlyList<GridPoint> NearbyAffectedTiles,
    bool NearbyPending, TownLandFilingRequest? Filing = null, bool Bound = false, bool InvalidTerms = false)
    : ActionPlan(ActorId, Choice.Encode())
{
    public override string Activity => "file that selected land hearing request";
}
