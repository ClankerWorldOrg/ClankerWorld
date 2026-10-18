namespace ClankerWorld.Simulation.World;

public sealed record RecipeChoice(string DefinitionId)
{
    public string Encode() => TownConstructionCandidateIds.Recipe(DefinitionId);
    public static RecipeChoice? Decode(string candidate) =>
        TownConstructionCandidateIds.TryParse(candidate, out var selection) && !selection.IsBuilding
            ? new(selection.DefinitionId) : null;
}
