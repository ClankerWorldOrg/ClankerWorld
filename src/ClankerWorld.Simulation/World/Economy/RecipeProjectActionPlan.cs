using ClankerWorld.Simulation.Content;

namespace ClankerWorld.Simulation.World;

public sealed record RecipeProjectInput(int QuantityIndex, string LotId, string ItemKind, int Quantity);

public abstract record RecipeActionPlan(string ActorId, string RecipeCandidateId) : ActionPlan(ActorId, RecipeCandidateId);

public sealed record RecipeGoalActionPlan(string ActorId, RecipeDefinition Recipe)
    : RecipeActionPlan(ActorId, new RecipeChoice(Recipe.CanonicalId).Encode())
{
    public override string Activity => "start that recipe goal without a selected workstation";
}

public sealed record RecipeProjectActionPlan(string ActorId, RecipeDefinition Recipe, PlacedBuilding Workstation,
    string OwnerId, string? RequiredStorageBuildingId, bool RequireCarried,
    IReadOnlyList<RecipeProjectInput>? Inputs, ToolWorkPlan? Knife)
    : RecipeActionPlan(ActorId, new RecipeChoice(Recipe.CanonicalId).Encode())
{
    public override string Activity => "make the selected recipe at that workstation";
}
