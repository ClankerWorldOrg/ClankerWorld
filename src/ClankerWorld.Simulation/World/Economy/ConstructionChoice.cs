namespace ClankerWorld.Simulation.World;

public sealed record ConstructionChoice(string DefinitionId, GridPoint? Site)
{
    public string Encode() => Site is { } site ? TownConstructionCandidateIds.Building(DefinitionId, site) : "build:building:" + DefinitionId;
    public static ConstructionChoice? Decode(string candidate) =>
        TownConstructionCandidateIds.TryParse(candidate, out var selection) && selection.IsBuilding
            ? new(selection.DefinitionId, selection.SitePosition) : null;
}
