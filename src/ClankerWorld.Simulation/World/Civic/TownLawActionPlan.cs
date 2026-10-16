namespace ClankerWorld.Simulation.World;

public enum TownLawChoiceKind { Adopt, Amend, Repeal }

public sealed record TownLawChoice(string TownId, TownLawChoiceKind Kind, string Target, string VersionToken)
{
    public static string Verb(TownLawChoiceKind kind) => kind switch { TownLawChoiceKind.Adopt => "propose", TownLawChoiceKind.Amend => "amend", _ => "repeal" };
    public string Encode() => $"civic|{TownId}|{Verb(Kind)}|{Target}|{VersionToken}";
    public static TownLawChoice? Decode(string value)
    {
        if (value.Split('|') is not ["civic", var town, var verb, var target, var version] ||
            !IdentityRules.Reference(town, DerivedId.MaximumLength, requireTrimmed: true) ||
            !IdentityRules.Reference(target, DerivedId.MaximumLength, requireTrimmed: true)) return null;
        TownLawChoiceKind? kind = verb switch { "propose" => TownLawChoiceKind.Adopt, "amend" => TownLawChoiceKind.Amend, "repeal" => TownLawChoiceKind.Repeal, _ => null };
        return kind is { } selected ? new(town, selected, target, version) : null;
    }
}

public sealed record TownLawActionPlan(string ActorId, TownLawChoice Choice, string Scope, IReadOnlyList<GridPoint> SiteTiles,
    TownLawVersion? Version, string? Text = null) : ActionPlan(ActorId, Choice.Encode())
{
    public override string Activity => "propose that selected Town law change";
}
