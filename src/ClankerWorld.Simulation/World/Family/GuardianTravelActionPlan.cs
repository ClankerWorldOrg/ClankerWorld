namespace ClankerWorld.Simulation.World;

public enum GuardianTravelKind { Relocate, Follow }

public sealed record GuardianTravelChoice(GuardianTravelKind Kind, string TargetId)
{
    public const string RelocatePrefix = "guardian_relocate:";
    public const string FollowPrefix = "guardian_follow:";
    public string Encode() => (Kind == GuardianTravelKind.Relocate ? RelocatePrefix : FollowPrefix) + TargetId;
    public static GuardianTravelChoice? Decode(string value) => value.StartsWith(RelocatePrefix, StringComparison.Ordinal) && value.Length > RelocatePrefix.Length
        ? new(GuardianTravelKind.Relocate, value[RelocatePrefix.Length..]) : value.StartsWith(FollowPrefix, StringComparison.Ordinal) && value.Length > FollowPrefix.Length
            ? new(GuardianTravelKind.Follow, value[FollowPrefix.Length..]) : null;
}

public sealed record GuardianTravelActionPlan(string ActorId, GuardianTravelChoice Choice, string ChildId,
    GuardianPlacementState Placement, IReadOnlyList<GridPoint> Route) : ActionPlan(ActorId, Choice.Encode())
{
    public override string Activity => Choice.Kind == GuardianTravelKind.Relocate ? "accompany that child home" : "follow the accepted guardian home";
}
