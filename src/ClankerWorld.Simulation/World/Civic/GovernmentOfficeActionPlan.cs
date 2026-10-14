namespace ClankerWorld.Simulation.World;

public enum GovernmentOfficeKind { Register, Withdraw, Resign, ExtendNonLand, AcceptNonLand }

public sealed record GovernmentOfficeChoice(string TownId, GovernmentOfficeKind Kind, string Subject, string Value)
{
    public static string Verb(GovernmentOfficeKind kind) => kind switch
    {
        GovernmentOfficeKind.Register => "mayor_register",
        GovernmentOfficeKind.Withdraw => "mayor_withdraw",
        GovernmentOfficeKind.Resign => "mayor_resign",
        GovernmentOfficeKind.ExtendNonLand => "government_extend_non_land",
        _ => "government_accept_non_land"
    };
    public string Encode() => $"civic|{TownId}|{Verb(Kind)}|{Subject}|{Value}";
    public static GovernmentOfficeChoice? Decode(string value)
    {
        if (value.Split('|') is not ["civic", var town, var verb, var subject, var choice] || town.Length == 0 || subject.Length == 0) return null;
        GovernmentOfficeKind? kind = verb switch
        {
            "mayor_register" => GovernmentOfficeKind.Register,
            "mayor_withdraw" => GovernmentOfficeKind.Withdraw,
            "mayor_resign" => GovernmentOfficeKind.Resign,
            "government_extend_non_land" => GovernmentOfficeKind.ExtendNonLand,
            "government_accept_non_land" => GovernmentOfficeKind.AcceptNonLand,
            _ => null
        };
        return kind is { } selected ? new(town, selected, subject, choice) : null;
    }
}

public sealed record GovernmentOfficeActionPlan(string ActorId, GovernmentOfficeChoice Choice,
    TownMayoralConsent? Consent, TownOffice? Office, TownGovernmentChange? Change,
    TownArrangement? Arrangement, GridPoint? NoticePlace) : ActionPlan(ActorId, Choice.Encode())
{
    public override string Activity => "make that personal office choice";
}
