namespace ClankerWorld.Simulation.World;

public sealed record TownFoundingChoice(string TownId, bool Resettle)
{
    public string Encode() => $"civic|{TownId}|{(Resettle ? "resettle" : "found")}||";
    public static TownFoundingChoice? Decode(string value) => value.Split('|') is ["civic", var town, var verb, "", ""] &&
        IdentityRules.Reference(town, DerivedId.MaximumLength, requireTrimmed: true) && verb is "found" or "resettle" ? new(town, verb == "resettle") : null;
}

public sealed record TownFoundingActionPlan(string ActorId, TownFoundingChoice Choice, TownRuntimeState Town,
    GridPoint Site, string? PreviousTown, IReadOnlyList<string> CareGroup) : ActionPlan(ActorId, Choice.Encode())
{
    public override string Activity => Choice.Resettle ? "resettle that abandoned Town" : "found that Town at the chosen site";
}
