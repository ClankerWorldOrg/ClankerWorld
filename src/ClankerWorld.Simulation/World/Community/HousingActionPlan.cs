namespace ClankerWorld.Simulation.World;

public enum HousingChoiceKind { Ask, Admit, Refuse }

public sealed record HousingChoice(HousingChoiceKind Kind, string TargetId)
{
    private static string Prefix(HousingChoiceKind kind) => kind switch
    {
        HousingChoiceKind.Ask => "household_ask:",
        HousingChoiceKind.Admit => "household_admit:",
        HousingChoiceKind.Refuse => "household_refuse:",
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };
    public string Encode() => Prefix(Kind) + TargetId;
    public static HousingChoice? Decode(string value)
    {
        foreach (var kind in Enum.GetValues<HousingChoiceKind>())
        {
            var prefix = Prefix(kind);
            if (value.StartsWith(prefix, StringComparison.Ordinal) && value.Length > prefix.Length)
                return new(kind, value[prefix.Length..]);
        }
        return null;
    }
}

public sealed record HousingActionPlan(string ActorId, HousingChoice Choice, string ApplicantId,
    string HouseholdId, PlacedBuilding House, HousingRequest? OriginalRequest,
    IReadOnlyList<string> AdultMembers) : ActionPlan(ActorId, Choice.Encode())
{
    public override string Activity => Choice.Kind switch
    {
        HousingChoiceKind.Ask => "ask that household for a home",
        HousingChoiceKind.Admit => "agree to that housing request",
        _ => "refuse that housing request",
    };
}
