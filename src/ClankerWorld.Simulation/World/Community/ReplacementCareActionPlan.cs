using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

public sealed record ReplacementCareChoice(string ChildId)
{
    public const string Prefix = "household_accept_care:";
    public string Encode() => Prefix + ChildId;
    public static ReplacementCareChoice? Decode(string value) => value.StartsWith(Prefix, StringComparison.Ordinal) && value.Length > Prefix.Length
        ? new(value[Prefix.Length..]) : null;
}

public sealed record ReplacementCareActionPlan(string ActorId, ReplacementCareChoice Choice, string HouseholdId,
    string OriginalCaregiverId, SocietyRelationship OriginalCare) : ActionPlan(ActorId, Choice.Encode())
{
    public override string Activity => "take primary care of that child";
}
