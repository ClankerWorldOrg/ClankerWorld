namespace ClankerWorld.Simulation.World;

public enum NonLandJudgeKind { Register, Withdraw, Resign, Vote }

public sealed record NonLandJudgeChoice(string TownId, NonLandJudgeKind Kind, string RevisionOrRoundToken, string CandidateToken)
{
    public static string Verb(NonLandJudgeKind kind) => kind switch
    {
        NonLandJudgeKind.Register => "law_case_judge_register",
        NonLandJudgeKind.Withdraw => "law_case_judge_withdraw",
        NonLandJudgeKind.Resign => "law_case_judge_resign",
        _ => "law_case_judge_vote"
    };
    public string Encode() => $"civic|{TownId}|{Verb(Kind)}|{RevisionOrRoundToken}|{CandidateToken}";
    public static NonLandJudgeChoice? Decode(string value)
    {
        if (value.Split('|') is not ["civic", var town, var verb, var terms, var candidate] || town.Length == 0 || terms.Length == 0) return null;
        NonLandJudgeKind? kind = verb switch
        {
            "law_case_judge_register" => NonLandJudgeKind.Register,
            "law_case_judge_withdraw" => NonLandJudgeKind.Withdraw,
            "law_case_judge_resign" => NonLandJudgeKind.Resign,
            "law_case_judge_vote" => NonLandJudgeKind.Vote,
            _ => null
        };
        return kind is { } selected ? new(town, selected, terms, candidate) : null;
    }
}

public sealed record NonLandJudgeActionPlan(string ActorId, NonLandJudgeChoice Choice, string CaseId,
    TownCaseRevision Revision, TownCaseJudgeConsent? Consent, TownCaseJudge? Judge,
    TownCaseJudgeContest? Contest, string? Candidate) : ActionPlan(ActorId, Choice.Encode())
{
    public override string Activity => Choice.Kind switch
    {
        NonLandJudgeKind.Register => "register for that selected case mandate",
        NonLandJudgeKind.Withdraw => "withdraw that selected case consent",
        NonLandJudgeKind.Resign => "resign that selected case mandate",
        _ => "vote for that selected case judge"
    };
}
