using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

public enum PracticalCareKind { Child, IllDependent }

public sealed record PracticalCareChoice(PracticalCareKind Kind, string DependentId)
{
    private const string ChildPrefix = "care:";
    private const string IllDependentPrefix = "guardian_tend:";
    public string Encode() => (Kind == PracticalCareKind.Child ? ChildPrefix : IllDependentPrefix) + DependentId;
    public static PracticalCareChoice? Decode(string value) => value.StartsWith(ChildPrefix, StringComparison.Ordinal) && value.Length > ChildPrefix.Length
        ? new(PracticalCareKind.Child, value[ChildPrefix.Length..]) : value.StartsWith(IllDependentPrefix, StringComparison.Ordinal) && value.Length > IllDependentPrefix.Length
            ? new(PracticalCareKind.IllDependent, value[IllDependentPrefix.Length..]) : null;
}

public sealed record PracticalCareActionPlan(string ActorId, PracticalCareChoice Choice, GridPoint Position,
    SocietyRelationship? Relationship, bool Orphan, bool NeedsFeeding, bool PreparingFood, FoodActionPlan? Food,
    IReadOnlyList<GridPoint> Route) : ActionPlan(ActorId, Choice.Encode())
{
    public override string Activity => Choice.Kind == PracticalCareKind.Child ? "care for that child" : "tend to that ill dependent";
}
