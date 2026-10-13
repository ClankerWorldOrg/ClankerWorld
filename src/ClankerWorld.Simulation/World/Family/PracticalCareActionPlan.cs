using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

public enum PracticalCareKind { Child, IllDependent }

public sealed record PracticalCareChoice(PracticalCareKind Kind, string DependentId)
{
    public string Encode() => (Kind == PracticalCareKind.Child ? "care:" : "guardian_tend:") + DependentId;
    public static PracticalCareChoice? Decode(string value) => value.StartsWith("care:", StringComparison.Ordinal) && value.Length > 5
        ? new(PracticalCareKind.Child, value[5..]) : value.StartsWith("guardian_tend:", StringComparison.Ordinal) && value.Length > 14
            ? new(PracticalCareKind.IllDependent, value[14..]) : null;
}

public sealed record PracticalCareActionPlan(string ActorId, PracticalCareChoice Choice, GridPoint Position,
    SocietyRelationship? Relationship, bool Orphan, bool NeedsFeeding, bool PreparingFood, FoodActionPlan? Food,
    IReadOnlyList<GridPoint> Route) : ActionPlan(ActorId, Choice.Encode())
{
    public override string Activity => Choice.Kind == PracticalCareKind.Child ? "care for that child" : "tend to that ill dependent";
}
