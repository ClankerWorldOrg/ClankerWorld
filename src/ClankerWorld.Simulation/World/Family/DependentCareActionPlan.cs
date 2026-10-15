using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

public enum DependentCareChoiceKind { Accept, Refuse, End, Offer, Primary }

public sealed record DependentCareChoice(DependentCareChoiceKind Kind, string TargetId)
{
    private static string Prefix(DependentCareChoiceKind kind) => kind switch
    {
        DependentCareChoiceKind.Accept => "guardian_accept:",
        DependentCareChoiceKind.Refuse => "guardian_refuse:",
        DependentCareChoiceKind.End => "guardian_end:",
        DependentCareChoiceKind.Offer => "guardian_offer:",
        DependentCareChoiceKind.Primary => "guardian_primary:",
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };
    public string Encode() => Prefix(Kind) + TargetId;
    public static DependentCareChoice? Decode(string value)
    {
        foreach (var kind in Enum.GetValues<DependentCareChoiceKind>())
        {
            var prefix = Prefix(kind);
            if (value.StartsWith(prefix, StringComparison.Ordinal) && value.Length > prefix.Length) return new(kind, value[prefix.Length..]);
        }
        return null;
    }
}

public sealed record DependentCareActionPlan(string ActorId, DependentCareChoice Choice, SocietyRelationship? Relationship,
    string? ActorHousehold, string? ChildHousehold, long? GuardianSearchStartedTick, string? DestinationHousehold,
    string? ActorTown, string? ChildTown, string? HouseId, string? HouseDefinitionId, long? HousePlacedTick,
    string? ActorFamilyUnit, string? ChildFamilyUnit) : ActionPlan(ActorId, Choice.Encode())
{
    public bool AcceptsGuardianSearch => GuardianSearchStartedTick is not null;
    public override string Activity => Choice.Kind switch
    {
        DependentCareChoiceKind.Accept => "accept that caregiving responsibility",
        DependentCareChoiceKind.Refuse => "refuse the caregiving proposal",
        DependentCareChoiceKind.End => "end that caregiving relationship",
        DependentCareChoiceKind.Offer => "offer care to that child",
        _ => "become that child's primary caregiver",
    };
}
