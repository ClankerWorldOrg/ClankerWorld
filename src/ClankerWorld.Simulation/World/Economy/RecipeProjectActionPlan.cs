using ClankerWorld.Simulation.Content;

namespace ClankerWorld.Simulation.World;

public sealed record RecipeProjectInput(int QuantityIndex, string LotId, string ItemKind, int Quantity);

public sealed record RecipeProjectActionPlan(string ActorId, RecipeDefinition Recipe, PlacedBuilding Workstation,
    string OwnerId, string? RequiredStorageBuildingId, bool RequireCarried,
    IReadOnlyList<RecipeProjectInput>? Inputs, ToolWorkPlan? Knife)
    : ActionPlan(ActorId, TownConstructionCandidateIds.Recipe(Recipe.CanonicalId))
{
    public override string Activity => "make the selected recipe at that workstation";
}
