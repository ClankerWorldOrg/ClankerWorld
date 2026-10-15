using ClankerWorld.Simulation.Content;

namespace ClankerWorld.Simulation.World;

internal interface IConstructionPlacementWorld : IConstructionPlanningWorld
{
    long WorldTick { get; }
    IReadOnlyList<TownRuntimeState> Towns { get; }
    string NormalizeRequiredText(string? value, string name);
    bool CanPlaceBuilding(BuildingDefinition definition, GridPoint position, out string failure);
    void ConsumeConstructionInputs(IReadOnlyList<ConstructionMaterialInput> inputs, string purpose, string owner);
    void AssignBuildingToTown(PlacedBuilding building, BuildingDefinition definition);
    void AppendEvent(string kind, string detail, GridPoint? position = null);
}
