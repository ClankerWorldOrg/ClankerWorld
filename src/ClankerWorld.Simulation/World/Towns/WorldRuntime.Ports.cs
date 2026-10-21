using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Content;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private BoatTransportState boatTransport => transportSystem.Boats;
    public IReadOnlyList<BoatState> Boats => boatTransport.Boats;
    public IReadOnlyList<BoatTripRequest> BoatRequests => boatTransport.Requests;

    private PlacedBuilding? Port(string id) =>
        TransportSystem.Port(BoatWorld, id);

    private PortGeometry PortGeometryFor(PlacedBuilding port) =>
        TransportSystem.PortGeometryFor(BoatWorld, port);

    private HashSet<GridPoint> PortObstacles(string? exceptProjectId = null, string? exceptPortId = null, bool protectDocks = false) =>
        TransportSystem.PortObstacles(BoatWorld, exceptProjectId, exceptPortId, protectDocks);

    private sealed record PortProjectSiteWorld(HashSet<GridPoint> Common,
        IReadOnlyDictionary<string, HashSet<GridPoint>> BuildingTiles, HashSet<GridPoint> Occupied)
    {
        internal Dictionary<string, HashSet<GridPoint>> WithoutPort { get; } = new(StringComparer.Ordinal);
    }

    private PortProjectSiteWorld CreatePortProjectSiteWorld(string? projectId)
    {
        var ground = Ground;
        var common = GroundOccupancy.Combine(ground.Resources, ground.MapObjects, ground.Fields,
            ground.RoadsAndBridgeEntrances, ground.ExpansionSites, ground.ProtectedProjectSites(projectId));
        var buildings = worldSimulation.Buildings.ToDictionary(building => building.InstanceId,
            building => PortNavigationRules.ProtectedBuildingTiles(map,
                worldContent.BuildingDefinitions[building.DefinitionId], building).ToHashSet(), StringComparer.Ordinal);
        return new(common, buildings, common.Concat(buildings.Values.SelectMany(tiles => tiles)).ToHashSet());
    }

    private static HashSet<GridPoint> PortConstructionObstacles(TownProjectSiteWorld world, string? exceptPortId = null)
    {
        var ports = world.Ports.Value;
        if (exceptPortId is null) return ports.Occupied;
        if (!ports.WithoutPort.TryGetValue(exceptPortId, out var occupied))
        {
            occupied = ports.Common.Concat(ports.BuildingTiles.Where(item => item.Key != exceptPortId)
                .SelectMany(item => item.Value)).ToHashSet();
            ports.WithoutPort.Add(exceptPortId, occupied);
        }
        return occupied;
    }

    private bool PortIsLegal(PlacedBuilding port) => PortIsLegal(port, null);

    private bool PortIsLegal(PlacedBuilding port, TownProjectSiteWorld? world) => port.TownId is { } townId &&
        towns.SingleOrDefault(town => town.Id == townId) is { } town && port.Entrance is { } approach &&
        PortGeometryFor(port).LandTiles.Append(approach).All((world?.Legal.Value ?? TownProjectLandTiles(town)).Contains) &&
        (!(world is null ? PortObstacles(exceptPortId: port.InstanceId, protectDocks: true) : PortConstructionObstacles(world, port.InstanceId)).Contains(approach) || roadTiles.Contains(approach)) &&
        PortNavigationRules.Fits(map, worldContent.Buildings.Single(item => item.CanonicalId == port.DefinitionId),
            port.Position, world is null ? PortObstacles(exceptPortId: port.InstanceId, protectDocks: true) : PortConstructionObstacles(world, port.InstanceId), out _, roadTiles);

    private string? PortProjectSiteFailure(TownRuntimeState town, TownProjectPayload plan,
        BuildingDefinition definition, string? projectId, string? actor, TownProjectSiteWorld world)
    {
        var geometry = PortNavigationRules.Geometry(map, definition, plan.Site);
        var legal = world.Legal.Value;
        if (geometry.LandTiles.Any(point => !legal.Contains(point)) || !legal.Contains(plan.Entrance))
            return "The Port's land end and approach must be undisputed Town land.";
        if (plan.BoatPortId is { } portId)
        {
            var port = Port(portId);
            if (port is null || port.TownId != town.Id || port.DefinitionId != plan.DefinitionId ||
                port.Position != plan.Site || port.Entrance != plan.Entrance || !PortIsLegal(port, world))
                return "The boat needs its approved, usable Town Port.";
            if (FreePortDock(port) is null) return "The Port has no free docking space for the finished boat.";
        }
        else
        {
            if (projectId is null && geometry.LandTiles.Concat(geometry.WaterTiles).Concat(geometry.DockingTiles)
                    .Append(plan.Entrance).Any(world.Pending.Value.Contains))
                return "Another proposed Town project already uses the Port site or docking space.";
            var occupied = PortConstructionObstacles(world);
            if (!PortNavigationRules.Fits(map, definition, plan.Site, occupied, out var failure, roadTiles)) return failure;
            if (occupied.Contains(plan.Entrance) && !roadTiles.Contains(plan.Entrance))
                return "The approved Port's land approach is blocked.";
        }
        var work = TownProjectRules.WorkSite(plan);
        if (actor is not null && agents.TryGetValue(actor, out var person) &&
            person.Position != work && Reach(actor, work, ReachPurpose.TownProjectProposal, 0, 0, SwimmingPolicy.WhenCapable).Count == 0)
            return "No walking route reaches the Port's land end.";
        return null;
    }

    private IEnumerable<GridPoint> FreePortDocks(PlacedBuilding port, string? exceptBoatId = null) =>
        TransportSystem.FreePortDocks(BoatWorld, port, exceptBoatId);

    private GridPoint? FreePortDock(PlacedBuilding port, string? exceptBoatId = null) =>
        TransportSystem.FreePortDock(BoatWorld, port, exceptBoatId);

    private bool HasCommunalBoatPort(string townId) =>
        TransportSystem.HasCommunalBoatPort(BoatWorld, townId);

    private void AddBoatAccessCandidates(List<CognitionCandidate> candidates, string actor, TownRuntimeState town)
    {
        TownSystem.OfferTownPolicy(civicWorld, candidates, actor, new(town.Id, TownPolicyKind.BoatAccess, "all", ""),
            "Ask the Council to adopt standing permission for all visitors to use the Town's communal boats; this grants no ownership or membership.", 193);
        var eligible = new List<CognitionCandidate>();
        foreach (var visitor in agents.Keys.Order(StringComparer.Ordinal))
            TownSystem.OfferTownPolicy(civicWorld, eligible, actor,
                new(town.Id, TownPolicyKind.BoatAccess, CivicAgentToken(visitor), ""),
                $"Ask the Council to allow {society.Checkpoint.GetAgent(visitor).Name} to use the Town's communal boats; this grants no ownership or membership.", 194);
        candidates.AddRange(eligible.Take(8));
    }

    private void AddPortProjectProposalCandidates(List<CognitionCandidate> candidates, string actor, TownRuntimeState town, TownProjectSiteWorld world)
    {
        var ports = worldSimulation.Buildings.Where(building => building.TownId == town.Id && Port(building.InstanceId) is not null).ToArray();
        var legal = world.Legal.Value;
        var occupied = PortConstructionObstacles(world);
        foreach (var definition in PortContent.Definitions.Where(definition => worldContent.Buildings.Any(d => d.CanonicalId == definition.CanonicalId)))
        {
            var offered = 0;
            foreach (var land in legal.OrderBy(point => point.Y).ThenBy(point => point.X))
            {
                var site = PortNavigationRules.Facing(definition) switch
                {
                    PortFacing.North => new GridPoint(land.X, land.Y - 3),
                    PortFacing.West => new GridPoint(land.X - 3, land.Y),
                    _ => land,
                };
                if (!PortNavigationRules.Fits(map, definition, site, occupied, out _, roadTiles)) continue;
                var geometry = PortNavigationRules.Geometry(map, definition, site);
                if (geometry.LandTiles.Any(point => !legal.Contains(point)) || !geometry.ApproachTiles.Any(legal.Contains)) continue;
                var plan = PlanFor(town, definition, site, definition.DisplayName, world);
                if (plan is null || TownProjectSiteFailure(town, plan, actor: actor, world: world) is not null) continue;
                if (!AddTownProjectProposalCandidate(candidates, actor, town, definition, site, plan)) continue;
                if (++offered == 2) break;
            }
        }
        foreach (var port in ports.Where(port => PortIsLegal(port, world)).OrderBy(port => port.InstanceId, StringComparer.Ordinal))
        {
            var plan = BoatProjectPlan(port, "Communal boat");
            if (FreePortDock(port) is null || !TownProjectDemandIsCurrent(town, plan)) continue;
            if (TownProjectSiteFailure(town, plan, actor: actor, world: world) is not null) continue;
            var choice = new TownProjectProposalChoice(town.Id, true, port.InstanceId, "");
            var proposal = TownSystem.PlanTownProjectProposal(civicWorld, actor, choice, plan);
            if (proposal is null || !TownSystem.CheckTownProjectProposal(civicWorld, proposal).Allowed) continue;
            candidates.Add(new(choice.Encode(),
                $"Propose building a communal boat at this Town's Port, with 8 wood, 2 rope and 2 refined iron; put its name in civic_proposal. Council approval creates no goods.", 192)
            { ActionPlan = proposal });
        }
    }

    private static TownProjectPayload BoatProjectPlan(PlacedBuilding port, string name) =>
        new(name, port.DefinitionId, port.Position, port.Entrance!.Value, PortContent.BoatCosts) { BoatPortId = port.InstanceId };

    private void CompletePaidBoatProject(string townId, TownProject project) =>
        TransportSystem.CompletePaidBoatProject(BoatWorld, townId, project);
}
