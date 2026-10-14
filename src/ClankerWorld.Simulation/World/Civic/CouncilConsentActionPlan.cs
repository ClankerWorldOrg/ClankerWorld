namespace ClankerWorld.Simulation.World;

public enum CouncilConsentKind { Register, Remainder, WithdrawCandidate, Nominate }

public sealed record CouncilConsentChoice(string TownId, CouncilConsentKind Kind, string SubjectToken)
{
    public static string Verb(CouncilConsentKind kind) => kind switch
    { CouncilConsentKind.Register => "register", CouncilConsentKind.Remainder => "remainder", CouncilConsentKind.Nominate => "nominate", _ => "withdraw_candidate" };
    public string Encode() => $"civic|{TownId}|{Verb(Kind)}|{SubjectToken}|";
    public static CouncilConsentChoice? Decode(string value)
    {
        if (value.Split('|') is not ["civic", var town, var verb, var subject, ""] || town.Length == 0) return null;
        CouncilConsentKind? kind = verb switch
        { "register" => CouncilConsentKind.Register, "remainder" => CouncilConsentKind.Remainder, "withdraw_candidate" => CouncilConsentKind.WithdrawCandidate, "nominate" => CouncilConsentKind.Nominate, _ => null };
        return kind is { } selected ? new(town, selected, subject) : null;
    }
}

public sealed record CouncilConsentActionPlan(string ActorId, CouncilConsentChoice Choice, TownCandidateAgreement? Agreement,
    long? RemainderDeadline, string? Nominee) : ActionPlan(ActorId, Choice.Encode())
{
    public override string Activity => "give that council candidacy response";
}
