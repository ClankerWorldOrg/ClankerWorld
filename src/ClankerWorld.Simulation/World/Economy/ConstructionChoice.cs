namespace ClankerWorld.Simulation.World;

public sealed record ConstructionChoice(string DefinitionId, GridPoint Site)
{
    public string Encode() => TownConstructionCandidateIds.Building(DefinitionId, Site);
    public static ConstructionChoice? Decode(string candidate) =>
        TownConstructionCandidateIds.TryParse(candidate, out var selection) && selection.IsBuilding && selection.SitePosition is { } site
            ? new(selection.DefinitionId, site) : null;
}
