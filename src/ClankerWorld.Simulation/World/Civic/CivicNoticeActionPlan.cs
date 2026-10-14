namespace ClankerWorld.Simulation.World;

public sealed record CivicNoticeChoice(string TownId, string? RecipientToken)
{
    public string Encode() => $"civic|{TownId}|{(RecipientToken is null ? "read" : "relay")}|{RecipientToken ?? ""}|";
    public static CivicNoticeChoice? Decode(string value) => value.Split('|') is ["civic", var town, var verb, var recipient, ""] &&
        town.Length > 0 && (verb == "read" && recipient.Length == 0 || verb == "relay" && recipient.Length > 0)
            ? new(town, verb == "relay" ? recipient : null) : null;
}

public sealed record CivicNoticeActionPlan(string ActorId, CivicNoticeChoice Choice, string Recipient,
    GridPoint? NoticePlace, IReadOnlyList<TownCivicNotice> Notices) : ActionPlan(ActorId, Choice.Encode())
{
    public bool ReadsCurrentBoard => Choice.RecipientToken is null;
    public override string Activity => Choice.RecipientToken is null ? "read those public notices" : "relay those learned public notices";
}
