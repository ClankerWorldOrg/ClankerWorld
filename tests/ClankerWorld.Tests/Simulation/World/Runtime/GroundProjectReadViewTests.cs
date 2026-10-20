using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.World;

namespace ClankerWorld.Tests;

public sealed class GroundProjectReadViewTests
{
    [Fact]
    public void DeferredProjectLayersReadCurrentTownsAndKeepBoatPortAndTerminalExclusions()
    {
        using var world = NormalPathWorld.CreateGenerated("ground-project-read-view", _ => new DeterministicDecisionProvider());
        var state = world.ExportState();
        var towns = new List<TownRuntimeState>();
        var ground = new GroundOccupancy(state.Map, state.WorldContent!, state.WorldSimulation!, towns: towns);
        var footprints = ground.ProjectFootprints();
        var protectedSites = ground.ProtectedProjectSites();
        var exceptHall = ground.ProtectedProjectSites("hall");
        Assert.Empty(footprints);
        Assert.Empty(protectedSites);
        var hallDefinition = TownProjectRules.Definitions.First(definition => !PortNavigationRules.IsPort(definition));
        var portDefinition = TownProjectRules.Definitions.First(PortNavigationRules.IsPort);
        var hall = new TownProjectPayload("Hall", hallDefinition.CanonicalId, new(10, 10), new(10, 9), []);
        var port = new TownProjectPayload("Port", portDefinition.CanonicalId, new(20, 20), new(20, 19), []);
        var ignored = hall with { DefinitionId = "unavailable-definition" };
        TownProject Project(string id, TownProjectPayload plan, string stage) =>
            new(id, "proposal-" + id, plan, 0, stage, 0, 0, []);
        // Reader-only controls; these projects are never installed into the running world or claimed as paid gameplay.
        var projects = new List<TownProject>
        {
            Project("hall", hall, "approved"),
            Project("completed", ignored, "completed"),
            Project("boat", ignored with { BoatPortId = "port" }, "approved"),
            Project("port", port, "approved"),
            Project("cancelled", ignored, "cancelled"),
        };
        towns.Add(state.Towns![0] with { Projects = projects });
        var hallTiles = TownProjectRules.Footprint(hall).ToArray();
        var portTiles = TownProjectRules.Footprint(port).ToArray();
        var docking = PortNavigationRules.Geometry(state.Map, portDefinition, port.Site).DockingTiles;
        Assert.Equal(hallTiles.Concat(portTiles), footprints);
        Assert.Equal(hallTiles.Concat(portTiles).Concat([hall.Entrance, port.Entrance]).Concat(docking), protectedSites);
        Assert.Equal(portTiles.Append(port.Entrance).Concat(docking), exceptHall);
        projects[0] = projects[0] with { Stage = "completed" };
        Assert.Equal(portTiles, footprints);
        Assert.Equal(portTiles.Append(port.Entrance).Concat(docking), protectedSites);
        projects.RemoveAt(3);
        Assert.Empty(footprints);
        Assert.Empty(protectedSites);
        Assert.Equal(WorldRuntimeCodec.Encode(state), WorldRuntimeCodec.Encode(world.ExportState()));
    }

    [Fact]
    public void CheckedBuildingLayersKeepDeferredLookupAndTheirExactMissingDefinitionError()
    {
        using var world = NormalPathWorld.CreateGenerated("ground-definition-read-view", _ => new DeterministicDecisionProvider());
        var state = world.ExportState();
        var buildings = state.WorldSimulation!.Buildings.ToList();
        var ground = new GroundOccupancy(state.Map, state.WorldContent!, state.WorldSimulation with { Buildings = buildings });
        var checkedTiles = ground.CheckedBuildingFootprints;
        var originalTiles = ground.OriginalBuildingFootprints;
        Assert.NotEmpty(checkedTiles);
        Assert.NotEmpty(originalTiles);
        buildings[0] = buildings[0] with { DefinitionId = "unavailable-definition" };
        const string message = "A placed building has no active definition.";
        Assert.Equal(message, Assert.Throws<InvalidDataException>(() => checkedTiles.ToArray()).Message);
        Assert.Equal(message, Assert.Throws<InvalidDataException>(() => originalTiles.ToArray()).Message);
        Assert.Equal(WorldRuntimeCodec.Encode(state), WorldRuntimeCodec.Encode(world.ExportState()));
    }
}
