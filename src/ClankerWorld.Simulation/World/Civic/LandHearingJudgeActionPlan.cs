namespace ClankerWorld.Simulation.World;

public enum LandHearingJudgeKind { Register, Withdraw, Resign, Vote }

public sealed record LandHearingJudgeChoice(string TownId, LandHearingJudgeKind Kind, string RevisionOrRoundToken, string CandidateToken)
{
    public static string Verb(LandHearingJudgeKind kind) => kind switch
    {
        LandHearingJudgeKind.Register => "hearing_judge_register",
        LandHearingJudgeKind.Withdraw => "hearing_judge_withdraw",
        LandHearingJudgeKind.Resign => "hearing_judge_resign",
        _ => "hearing_judge_vote"
    };
    public string Encode() => $"civic|{TownId}|{Verb(Kind)}|{RevisionOrRoundToken}|{CandidateToken}";
    public static LandHearingJudgeChoice? Decode(string value)
    {
        if (value.Split('|') is not ["civic", var town, var verb, var terms, var candidate] || town.Length == 0 || terms.Length == 0) return null;
        LandHearingJudgeKind? kind = verb switch
        {
            "hearing_judge_register" => LandHearingJudgeKind.Register,
            "hearing_judge_withdraw" => LandHearingJudgeKind.Withdraw,
            "hearing_judge_resign" => LandHearingJudgeKind.Resign,
            "hearing_judge_vote" => LandHearingJudgeKind.Vote,
            _ => null
        };
        return kind is { } selected ? new(town, selected, terms, candidate) : null;
    }
}

public sealed record LandHearingJudgeActionPlan(string ActorId, LandHearingJudgeChoice Choice, string CaseId,
    TownLandCaseRevision Revision, TownLandCaseJudgeConsent? Consent, TownLandCaseJudge? Judge,
    TownLandCaseJudgeContest? Contest, string? Candidate) : ActionPlan(ActorId, Choice.Encode())
{
    public override string Activity => Choice.Kind switch
    {
        LandHearingJudgeKind.Register => "register for that selected case mandate",
        LandHearingJudgeKind.Withdraw => "withdraw that selected case consent",
        LandHearingJudgeKind.Resign => "resign that selected case mandate",
        _ => "vote for that selected case judge"
    };
}
