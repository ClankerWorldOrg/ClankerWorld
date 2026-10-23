
namespace ClankerWorld.Simulation.World;

public sealed record HouseholdResumeChoice(string JobId)
{
    public const string Prefix = "household_resume_work:";
    public string Encode() => Prefix + JobId;
    public static HouseholdResumeChoice? Decode(string value) => value.StartsWith(Prefix, StringComparison.Ordinal) && value.Length > Prefix.Length
        ? new(value[Prefix.Length..]) : null;
}

public sealed record HouseholdResumeActionPlan(string ActorId, HouseholdResumeChoice Choice, string HouseholdId,
    PlacedBuilding Building, string WorkerId, long CompletionTick, long PausedAtTick, IReadOnlyList<Identity> ReservationIds,
    string? KnifeId, WorldProductionJob? Production, BuildingExpansionJob? Expansion, IReadOnlyList<GridPoint> Route)
    : ActionPlan(ActorId, Choice.Encode())
{
    public override string Activity => "resume that household work";
}
