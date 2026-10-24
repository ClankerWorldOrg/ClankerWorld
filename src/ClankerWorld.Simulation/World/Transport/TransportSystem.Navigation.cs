
namespace ClankerWorld.Simulation.World;

internal sealed partial class TransportSystem
{
    internal static PlacedBuilding? Port(IBoatWorld world, string id) => world.Buildings.FirstOrDefault(building =>
        building.InstanceId == id && world.Definitions.Any(definition =>
            definition.CanonicalId == building.DefinitionId && PortNavigationRules.IsPort(definition)));

    internal static PortGeometry PortGeometryFor(IBoatWorld world, PlacedBuilding port) => PortNavigationRules.Geometry(world.Map,
        world.Definitions.Single(definition => definition.CanonicalId == port.DefinitionId), port.Position);

    internal static HashSet<GridPoint> PortObstacles(IBoatWorld world, string? exceptProjectId = null, string? exceptPortId = null, bool protectDocks = false)
    {
        var ground = world.Ground;
        return GroundOccupancy.Combine(ground.Resources, ground.MapObjects, ground.Fields, ground.RoadsAndBridgeEntrances,
            protectDocks ? ground.ProtectedBuildingTiles(exceptPortId) : ground.BuildingFootprintsExcept(exceptPortId),
            ground.ExpansionSites, ground.ProtectedProjectSites(exceptProjectId));
    }

    internal static IEnumerable<GridPoint> FreePortDocks(IBoatWorld world, PlacedBuilding port, string? exceptBoatId = null) =>
        PortGeometryFor(world, port).DockingTiles.Where(dock => !world.Transport.Boats.Boats.Any(boat => boat.Id != exceptBoatId &&
            (boat.Position == dock || boat.Journey?.ReservedDock == dock)));

    internal static GridPoint? FreePortDock(IBoatWorld world, PlacedBuilding port, string? exceptBoatId = null) =>
        FreePortDocks(world, port, exceptBoatId).Cast<GridPoint?>().FirstOrDefault();

    internal static bool HasCommunalBoatPort(IBoatWorld world, string townId) =>
        world.Buildings.Any(building => building.TownId == townId && Port(world, building.InstanceId) is not null);

    internal static IReadOnlyList<GridPoint> ReachBoatGeography(IBoatWorld world, BoatState boat, PlacedBuilding destination) =>
        PortNavigationRules.WaterRoute(world.Map, boat.Position, PortGeometryFor(world, destination).DockingTiles, PortObstacles(world));
}
