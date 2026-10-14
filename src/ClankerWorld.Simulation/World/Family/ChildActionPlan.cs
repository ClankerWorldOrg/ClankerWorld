namespace ClankerWorld.Simulation.World;

public enum ChildActionKind { Converse, Play, Learn, HelpFood, Carry, GatherFood, GatherWood }

public readonly record struct ChildChoice(ChildActionKind Kind, string? Target = null)
{
    public string Id => Kind switch
    {
        ChildActionKind.Converse => IdentityRules.PrefixedReference("child_converse:", Target),
        ChildActionKind.Play => IdentityRules.PrefixedReference("child_play:", Target),
        ChildActionKind.Learn => IdentityRules.PrefixedReference("child_learn:", Target),
        ChildActionKind.HelpFood => "child_help_food",
        ChildActionKind.Carry => IdentityRules.PrefixedReference("child_carry:", Target),
        ChildActionKind.GatherFood => IdentityRules.PrefixedReference("child_gather:food:", Target),
        ChildActionKind.GatherWood => IdentityRules.PrefixedReference("child_gather:wood:", Target),
        _ => throw new InvalidOperationException("Unknown child choice."),
    };

    public static ChildChoice? Parse(string id)
    {
        if (id == "child_help_food") return new(ChildActionKind.HelpFood);
        foreach (var kind in Enum.GetValues<ChildActionKind>())
        {
            if (kind == ChildActionKind.HelpFood) continue;
            var prefix = new ChildChoice(kind).Id;
            if (id.StartsWith(prefix, StringComparison.Ordinal) && id.Length > prefix.Length)
                return new(kind, id[prefix.Length..]);
        }
        return null;
    }
}

public sealed record ChildActionPlan(string ActorId, ChildChoice Choice, GridPoint Destination,
    int InteractionRange, IReadOnlyList<GridPoint> Route, string? HouseholdId = null,
    string? HouseId = null, string? StockId = null, string? ItemKind = null, int Quantity = 0,
    bool IsDelivery = false) : ActionPlan(ActorId, Choice.Id)
{
    public override string Activity => Choice.Kind switch
    {
        ChildActionKind.Converse => "talk with someone",
        ChildActionKind.Play => "play together",
        ChildActionKind.Learn => "learn from an adult",
        ChildActionKind.HelpFood => "take spare food home",
        ChildActionKind.Carry => "carry goods home",
        ChildActionKind.GatherFood => "gather food",
        ChildActionKind.GatherWood => "gather wood",
        _ => throw new InvalidOperationException("Unknown child activity."),
    };
}
