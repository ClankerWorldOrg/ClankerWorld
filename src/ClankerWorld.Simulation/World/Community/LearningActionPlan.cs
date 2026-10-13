namespace ClankerWorld.Simulation.World;

public enum LearningChoiceKind { Request, Accept, Decline, Cancel, Attend, Teach }

public sealed record LearningChoice(LearningChoiceKind Kind, string? TargetId = null, SkillKind? Skill = null)
{
    public string Encode() => Kind switch
    {
        LearningChoiceKind.Request => "learn:" + Skill!.Value.ToString().ToLowerInvariant() + ":" + TargetId,
        LearningChoiceKind.Accept => "lesson_accept:" + TargetId,
        LearningChoiceKind.Decline => "lesson_decline:" + TargetId,
        LearningChoiceKind.Cancel => "lesson_cancel",
        LearningChoiceKind.Attend => "lesson_attend",
        LearningChoiceKind.Teach => "lesson_teach:" + TargetId,
        _ => throw new ArgumentOutOfRangeException(nameof(Kind)),
    };
    public static LearningChoice? Decode(string value)
    {
        if (value == "lesson_cancel") return new(LearningChoiceKind.Cancel);
        if (value == "lesson_attend") return new(LearningChoiceKind.Attend);
        if (value.StartsWith("learn:", StringComparison.Ordinal))
        {
            var parts = value.Split(':', 3);
            return parts.Length == 3 && !string.IsNullOrEmpty(parts[2]) && Enum.TryParse<SkillKind>(parts[1], true, out var skill) && Enum.IsDefined(skill)
                ? new(LearningChoiceKind.Request, parts[2], skill) : null;
        }
        foreach (var (prefix, kind) in new[] { ("lesson_accept:", LearningChoiceKind.Accept),
                     ("lesson_decline:", LearningChoiceKind.Decline), ("lesson_teach:", LearningChoiceKind.Teach) })
            if (value.StartsWith(prefix, StringComparison.Ordinal) && value.Length > prefix.Length)
                return new(kind, value[prefix.Length..]);
        return null;
    }
}

public sealed record LearningActionPlan(string ActorId, LearningChoice Choice, string StudentId, string TeacherId,
    SkillKind Skill, SkillLesson? Original, GridPoint Site, IReadOnlyList<GridPoint> ActorRoute,
    IReadOnlyList<GridPoint> OtherRoute, bool WaitingForModel = false) : ActionPlan(ActorId, Choice.Encode())
{
    public override string Activity => Choice.Kind switch
    {
        LearningChoiceKind.Request => "ask that teacher for a lesson",
        LearningChoiceKind.Accept => "accept that teaching request",
        LearningChoiceKind.Decline => "decline that lesson",
        LearningChoiceKind.Cancel => "withdraw from that lesson",
        LearningChoiceKind.Attend => "attend that lesson",
        _ => "teach that lesson",
    };
}
