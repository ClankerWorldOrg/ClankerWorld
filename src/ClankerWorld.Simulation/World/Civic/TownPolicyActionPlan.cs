namespace ClankerWorld.Simulation.World;

public enum TownPolicyKind { EstateDefault, BoatAccess }

public sealed record TownPolicyChoice(string TownId, TownPolicyKind Kind, string Target, string Value)
{
    public string Encode() => $"civic|{TownId}|{(Kind == TownPolicyKind.EstateDefault ? "estate_default" : "boat_access")}|{Target}|{Value}";
    public static TownPolicyChoice? Decode(string value) => value.Split('|') is ["civic", var town, var verb, var target, var choice] &&
        IdentityRules.Reference(town, DerivedId.MaximumLength, requireTrimmed: true) &&
        IdentityRules.Reference(target) && verb is "estate_default" or "boat_access"
        ? new(town, verb == "estate_default" ? TownPolicyKind.EstateDefault : TownPolicyKind.BoatAccess, target, choice) : null;
}

public sealed record TownPolicyActionPlan(string ActorId, TownPolicyChoice Choice, int? TownShare,
    string? Visitor, string? LawId, TownLawVersion? Version) : ActionPlan(ActorId, Choice.Encode())
{
    public override string Activity => Choice.Kind == TownPolicyKind.EstateDefault
        ? "propose that default estate share" : "propose that communal boat access";
}
