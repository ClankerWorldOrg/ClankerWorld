namespace ClankerWorld.Simulation.World;

public sealed record TownProjectProposalChoice(string TownId, bool Boat, string DefinitionOrPort, string Coordinates)
{
    public string Encode() => $"civic|{TownId}|{(Boat ? "boat_project" : "project")}|{DefinitionOrPort}|{Coordinates}";
    public static TownProjectProposalChoice? Decode(string value) => value.Split('|') is ["civic", var town, var verb, var definition, var coordinates] &&
        town.Length > 0 && definition.Length > 0 && verb is "project" or "boat_project" ? new(town, verb == "boat_project", definition, coordinates) : null;
}

public sealed record TownProjectProposalActionPlan(string ActorId, TownProjectProposalChoice Choice, TownProjectPayload Project,
    long CouncilRevision, bool Bound = false) : ActionPlan(ActorId, Choice.Encode())
{
    public override string Activity => "propose that selected shared project";
}
