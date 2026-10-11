using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Harness;

namespace ClankerWorld.Simulation.Playtest;

/// <summary>Current physical layers, enumerated in source order; never saved or cached.</summary>
internal sealed class GroundOccupancy(SeededMap map, DeclarativeWorldContentState content,
    WorldContentSimulationState simulation, IEnumerable<FarmFieldState>? fields = null,
    IEnumerable<GridPoint>? roads = null, IEnumerable<BridgeState>? bridges = null,
    IEnumerable<TownRuntimeState>? towns = null, IEnumerable<AnimalState>? animals = null)
{
    internal IEnumerable<GridPoint> Resources => map.Resources.Select(item => item.Position);
    internal IEnumerable<GridPoint> CampObjects => map.CampObjects.Select(item => item.Position);
    internal IEnumerable<GridPoint> Fields => (fields ?? []).Select(item => item.Position);
    internal IEnumerable<GridPoint> Roads => roads ?? [];
    internal IEnumerable<GridPoint> BridgeEntrances => (bridges ?? []).SelectMany(item => item.Entrances);
    internal IEnumerable<GridPoint> RoadsAndBridgeEntrances => Roads.Concat(BridgeEntrances);
    internal IEnumerable<GridPoint> BuildingFootprints => simulation.Buildings.SelectMany(building =>
        WorldContentSimulationRules.Footprint(content.BuildingDefinitions[building.DefinitionId], building));
    internal IEnumerable<GridPoint> CheckedBuildingFootprints => simulation.Buildings.SelectMany(building =>
        content.BuildingDefinitions.TryGetValue(building.DefinitionId, out var definition)
            ? WorldContentSimulationRules.Footprint(definition, building)
            : throw new InvalidDataException("A placed building has no active definition."));
    internal IEnumerable<GridPoint> OriginalBuildingFootprints => simulation.Buildings.SelectMany(building =>
        content.BuildingDefinitions.TryGetValue(building.DefinitionId, out var definition)
            ? WorldContentSimulationRules.Footprint(definition, building.Position)
            : throw new InvalidDataException("A placed building has no active definition."));
    internal IEnumerable<GridPoint> PlantingBuildingFootprints => simulation.Buildings.SelectMany(building =>
        content.BuildingDefinitions.TryGetValue(building.DefinitionId, out var definition)
            ? WorldContentSimulationRules.Footprint(definition, building) : [building.Position]);
    internal IEnumerable<GridPoint> ExpansionSites => (simulation.BuildingExpansions ?? [])
        .Where(job => job.State is WorldProductionJobState.Running or WorldProductionJobState.Paused)
        .SelectMany(ExpansionTiles);
    internal IEnumerable<GridPoint> MarketSites => (towns ?? []).SelectMany(town => town.Markets)
        .Where(market => market.RemovedTick is null).SelectMany(market => MarketContent.SiteTiles(market.Site));
    internal IEnumerable<GridPoint> MarketStallSlots => (towns ?? []).SelectMany(town => town.Markets)
        .Where(market => market.RemovedTick is null).SelectMany(market =>
            Enumerable.Range(0, MarketContent.MaximumStalls).Select(slot => MarketContent.StallSite(market.Site, slot)));

    internal IEnumerable<GridPoint> AnimalPositions(string? exceptAnimalId = null) => (animals ?? [])
        .Where(animal => animal.DiedTick is null && animal.Id != exceptAnimalId).Select(animal => animal.Position);

    internal IEnumerable<GridPoint> ProtectedBuildingTiles(string? exceptBuildingId = null) => simulation.Buildings
        .Where(building => building.InstanceId != exceptBuildingId).SelectMany(building =>
            PortNavigationRules.ProtectedBuildingTiles(map, content.BuildingDefinitions[building.DefinitionId], building));

    internal IEnumerable<GridPoint> BuildingFootprintsExcept(string? exceptBuildingId) => simulation.Buildings
        .Where(building => building.InstanceId != exceptBuildingId).SelectMany(building =>
            WorldContentSimulationRules.Footprint(content.BuildingDefinitions[building.DefinitionId], building));

    internal IEnumerable<GridPoint> KnownBuildingFootprints(Func<PlacedBuilding, bool> include) => simulation.Buildings
        .Where(building => include(building) && content.BuildingDefinitions.ContainsKey(building.DefinitionId))
        .SelectMany(building => WorldContentSimulationRules.Footprint(content.BuildingDefinitions[building.DefinitionId], building));

    internal IEnumerable<GridPoint> ProjectFootprints(string? exceptProjectId = null) => (towns ?? [])
        .SelectMany(town => town.Projects).Where(project => project.Id != exceptProjectId &&
            project.Stage is not ("completed" or "cancelled"))
        .SelectMany(project => TownProjectRules.Footprint(project.Plan));

    internal IEnumerable<GridPoint> ProtectedProjectSites(string? exceptProjectId = null) =>
        ProjectFootprints(exceptProjectId).Concat((towns ?? []).SelectMany(town => town.Projects)
            .Where(project => project.Id != exceptProjectId && project.Plan.BoatPortId is null &&
                project.Stage is not ("completed" or "cancelled"))
            .Select(project => project.Plan.Entrance))
        .Concat((towns ?? []).SelectMany(town => town.Projects)
            .Where(project => project.Id != exceptProjectId && project.Stage is not ("completed" or "cancelled") &&
                project.Plan.BoatPortId is null && TownProjectRules.DefinitionFor(project.Plan.DefinitionId) is { } definition &&
                PortNavigationRules.IsPort(definition))
            .SelectMany(project => PortNavigationRules.Geometry(map,
                TownProjectRules.DefinitionFor(project.Plan.DefinitionId)!, project.Plan.Site).DockingTiles));

    internal static IEnumerable<GridPoint> ExpansionTiles(BuildingExpansionJob job) =>
        Enumerable.Range(0, job.TargetFootprint.Height).SelectMany(y =>
            Enumerable.Range(0, job.TargetFootprint.Width).Select(x =>
                new GridPoint(job.TargetPosition.X + x, job.TargetPosition.Y + y)));

    // Callers select layer order deliberately: HashSet enumeration also follows insertion order.
    internal static HashSet<GridPoint> Combine(params IEnumerable<GridPoint>[] layers) =>
        layers.SelectMany(layer => layer).ToHashSet();

    internal static HashSet<GridPoint> InitialObstacles(SeededMap map) =>
        Combine(map.CampObjects.Select(item => item.Position), map.Resources.Select(item => item.Position));
}
