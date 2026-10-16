namespace ClankerWorld.Simulation.World;

public enum GovernmentProposalKind { Propose, ReplaceMayor, Yes, No, Withdraw }

public sealed record GovernmentProposalChoice(string TownId, GovernmentProposalKind Kind, string Subject)
{
    public static string Verb(GovernmentProposalKind kind) => kind switch
    {
        GovernmentProposalKind.Propose => "government_propose",
        GovernmentProposalKind.ReplaceMayor => "government_replace",
        GovernmentProposalKind.Yes => "government_yes",
        GovernmentProposalKind.No => "government_no",
        _ => "government_withdraw"
    };
    public string Encode() => $"civic|{TownId}|{Verb(Kind)}|{Subject}|";
    public static GovernmentProposalChoice? Decode(string value)
    {
        if (value.Split('|') is not ["civic", var town, var verb, var subject, ""] ||
            !IdentityRules.Reference(town, DerivedId.MaximumLength, requireTrimmed: true) ||
            !IdentityRules.Reference(subject, DerivedId.MaximumLength, requireTrimmed: true)) return null;
        GovernmentProposalKind? kind = verb switch
        {
            "government_propose" => GovernmentProposalKind.Propose,
            "government_replace" => GovernmentProposalKind.ReplaceMayor,
            "government_yes" => GovernmentProposalKind.Yes,
            "government_no" => GovernmentProposalKind.No,
            "government_withdraw" => GovernmentProposalKind.Withdraw,
            _ => null
        };
        return kind is { } selected ? new(town, selected, subject) : null;
    }
}

public sealed record GovernmentProposalActionPlan(string ActorId, GovernmentProposalChoice Choice,
    TownArrangement OriginalArrangement, TownArrangement? Target, GridPoint? NoticePlace,
    TownGovernmentChange? Change) : ActionPlan(ActorId, Choice.Encode())
{
    public override string Activity => "respond to that protected government proposal";
}
