using ClankerWorld.Simulation.Content;

namespace ClankerWorld.Simulation.World;

internal interface IProductionStartWorld : IRecipeWorld
{
    long WorldTick { get; }
    string NormalizeRequiredText(string? value, string name);
    bool HasCarriedUnreservedQuantities(string actor, IReadOnlyList<ContentQuantity> quantities);
    int DestinationRoom(string building);
    int SkilledRecipeWorkTicks(string actor, RecipeDefinition recipe, int ticks);
    string ProductionJobId(long sequence);
    IReadOnlyList<string> ReserveProductionInputs(RecipeProjectActionPlan plan, string purpose, long expiry);
    string? ToolMakingRequestJobFor(string worker, RecipeDefinition recipe, string building);
    void BindToolMakingJob(WorldProductionJob job);
    void MarkCurrentSchema();
    void AppendEvent(WorldEventPayload payload, GridPoint? position = null);
}
