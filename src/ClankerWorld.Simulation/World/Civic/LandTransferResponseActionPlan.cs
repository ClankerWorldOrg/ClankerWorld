namespace ClankerWorld.Simulation.World;

public enum LandTransferResponseKind { Read, Accept, Decline, Withdraw }

public sealed record LandTransferResponseChoice(string TownId, LandTransferResponseKind Kind, string TermsToken, string PartyToken)
{
    public static string Verb(LandTransferResponseKind kind) => kind switch
    {
        LandTransferResponseKind.Read => "land_transfer_read",
        LandTransferResponseKind.Accept => "land_transfer_accept",
        LandTransferResponseKind.Decline => "land_transfer_decline",
        _ => "land_transfer_withdraw"
    };
    public string Encode() => $"civic|{TownId}|{Verb(Kind)}|{TermsToken}|{PartyToken}";
    public static LandTransferResponseChoice? Decode(string value)
    {
        if (value.Split('|') is not ["civic", var town, var verb, var terms, var party] ||
            !IdentityRules.Reference(town, DerivedId.MaximumLength, requireTrimmed: true) ||
            !IdentityRules.Reference(terms, DerivedId.MaximumLength)) return null;
        LandTransferResponseKind? kind = verb switch
        {
            "land_transfer_read" => LandTransferResponseKind.Read,
            "land_transfer_accept" => LandTransferResponseKind.Accept,
            "land_transfer_decline" => LandTransferResponseKind.Decline,
            "land_transfer_withdraw" => LandTransferResponseKind.Withdraw,
            _ => null
        };
        return kind is { } selected && (selected is LandTransferResponseKind.Read or LandTransferResponseKind.Withdraw
            ? party == "" : IdentityRules.Reference(party, DerivedId.MaximumLength)) ? new(town, selected, terms, party) : null;
    }
}

public sealed record LandTransferResponseActionPlan(string ActorId, LandTransferResponseChoice Choice,
    TownLandTransferRequest Request, string PartyHousehold, GridPoint? NoticePlace) : ActionPlan(ActorId, Choice.Encode())
{
    public override string Activity => "respond to those selected household permission terms";
}
