using System.Text.Json;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private WorldMapAuthoring? mapAuthoring;

    public WorldDeveloperEditResult ApplyMapEdit(WorldMapEdit edit, Action<WorldRuntimeState>? persist = null)
    {
        ArgumentNullException.ThrowIfNull(edit);
        using var gateScope = EnterGate();
        if (edit.WorldId != society.Checkpoint.WorldId)
            return new(false, false, "The active world changed. Refresh before editing.");
        var command = JsonSerializer.Serialize(edit);
        if (events.Any(item => item.Payload is DeveloperEditEventPayload { Operation: "edit_world" } saved && saved.Value == command))
            return new(false, true, null);
        if (!society.Checkpoint.IsPaused)
            return new(false, false, "Pause the world before editing the map.");
        if (edit.ExpectedEventId != nextEventId - 1 || edit.RunEpoch != society.Checkpoint.RunEpoch)
            return new(false, false, "The world changed. Refresh and select the tiles again.");
        if (!IdentityRules.Reference(edit.BatchId, DerivedId.MaximumLength) || edit.Operations is not { Count: > 0 and <= WorldMapEditRules.MaximumOperations })
            return new(false, false, "Choose between 1 and 64 tiles and a valid edit batch.");
        using var proposed = RestoreCore(CaptureState(), providerFactory,
            maxCognitionDispatchPerCycle, trustedPreparedState: true, preparedCivic: townSystem.CheckpointState);
        try
        {
            if (proposed.PrepareMapEdit(edit) is { } failure)
                return new(false, false, failure);
            proposed.AppendEvent(new DeveloperEditEventPayload(null, edit.WorldId, edit.ExpectedEventId, "edit_world", command, edit.Operations.Count, null));
            proposed.Validate();
            using var validated = RestoreCore(proposed.CaptureState(), providerFactory,
                maxCognitionDispatchPerCycle, trustedPreparedState: false, applyLoadTransitions: false);
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or InvalidDataException)
        {
            return new(false, false, "That change would leave the world in an invalid state. " + exception.Message);
        }
        persist?.Invoke(proposed.CaptureState());
        CommitPreparedTick(proposed);
        plannedRoutes = [];
        return new(true, false, null);
    }

    private string? PrepareMapEdit(WorldMapEdit edit)
    {
        var ground = Ground;
        var occupied = GroundOccupancy.Combine(ground.MapObjects, ground.Resources, ground.Fields,
            ground.RoadsAndBridgeEntrances, ground.ProtectedBuildingTiles(), ground.ExpansionSites,
            ground.MarketSites, ground.MarketStallSlots, ground.ProtectedProjectSites(), ground.AnimalPositions(),
            agents.Values.Select(agent => agent.Position), bridges.SelectMany(bridge => bridge.Span),
            boatTransport.Boats.Select(boat => boat.Position));
        var selected = new HashSet<GridPoint>();
        var terrainEdits = new List<WorldTerrainEdit>();
        var additions = new List<MapResource>();
        var objects = map.MapObjects.ToList();
        foreach (var operation in edit.Operations)
        {
            if (operation is null || operation.X is not { } x || operation.Y is not { } y || !map.Contains(new(x, y)))
                return "Choose tiles inside the map.";
            var point = new GridPoint(x, y);
            if (!selected.Add(point)) return "Each tile can appear only once in an edit.";
            var choice = WorldMapEditRules.Choices.FirstOrDefault(choice => choice.Operation == operation.Kind && choice.Value == operation.Value);
            if (choice is null || operation.SecondaryValue is not null ||
                operation.IsRenewable is { } renewable && renewable != choice.IsRenewable && choice.ResourceKind is not null)
                return "Choose one of the terrain, resource or object options offered by this server.";
            if (occupied.Contains(point)) return $"Tile ({x}, {y}) is occupied. Choose an empty tile.";
            if (choice.Terrain is { } terrain)
            {
                if (operation.Id is not null) return "Terrain edits do not use an object ID.";
                terrainEdits.Add(new(point, terrain));
                continue;
            }
            var id = operation.Id ?? DerivedId.Create(FormattableString.Invariant($"developer-map:{edit.BatchId}:{x}:{y}"));
            if (!IdentityRules.Reference(id, DerivedId.MaximumLength) || map.Resources.Any(resource => resource.Id == id) || objects.Any(item => item.Id == id) || additions.Any(resource => resource.Id == id))
                return "Choose a new, valid resource or object ID, or leave the ID empty.";
            if (choice.ResourceKind is { } kind)
            {
                var resource = new MapResource(id, kind, point, choice.IsRenewable == true, NaturalObjectKind: choice.NaturalObjectKind);
                if (!MapAcceptance.IsValidResourcePosition(map, resource))
                    return $"{choice.DisplayName} cannot stand on tile ({x}, {y}). Choose suitable ground.";
                if (TerrainPlacementRules.IsOrdinaryVegetation(resource) && map.SurfaceAt(point) is { } currentSurface &&
                    !TerrainPlacementRules.CanHoldOrdinaryVegetation(currentSurface))
                    return $"Tile ({x}, {y}) cannot hold that plant. Choose grass or forest ground.";
                additions.Add(resource);
            }
            else
            {
                if (WorldMapEditRules.IsReservedObjectId(id))
                    return "That ID belongs to the starting site. Choose another object ID or leave it empty.";
                if (!map.IsBuildable(point)) return $"Tile ({x}, {y}) needs open, buildable ground.";
                if (founderSetup is { Started: false }) return "Start the settlement before placing camp objects.";
                objects.Add(new(id, choice.ObjectKind!, point));
            }
        }
        var addedObjects = objects.Skip(map.MapObjects.Count).ToArray();
        map = WorldMapAuthoring.PaintTerrain(map, terrainEdits) with
        {
            MapObjects = objects.ToArray(),
            Resources = map.Resources.Concat(additions).OrderBy(resource => resource.Id, StringComparer.Ordinal).ToArray(),
        };
        var previousTerrain = (mapAuthoring?.Terrain ?? []).ToDictionary(edit => edit.Position);
        foreach (var editTerrain in terrainEdits) previousTerrain[editTerrain.Position] = editTerrain;
        mapAuthoring = new(previousTerrain.Values.OrderBy(edit => edit.Position.Y).ThenBy(edit => edit.Position.X).ToArray(),
            (mapAuthoring?.Resources ?? []).Concat(additions).OrderBy(resource => resource.Id, StringComparer.Ordinal).ToArray(),
            (mapAuthoring?.Objects ?? []).Concat(addedObjects).OrderBy(item => item.Id, StringComparer.Ordinal).ToArray());
        map = map with { ManifestDigest = MapManifestCodec.Digest(map) };
        if (MapAcceptance.Validate(map, allowEmptyStartingSite: founderSetup is not null) is { IsValid: false } rejected)
            return rejected.Failure;
        if (additions.Count > 0)
        {
            var changedChunks = additions.Select(resource => ChunkRules.ToChunkCoordinate(resource.Position, worldSystems.Chunks[0].ChunkSize)).ToHashSet();
            if (worldSystems.Chunks.Any(chunk => changedChunks.Contains(chunk.Coordinate) &&
                map.Resources.Count(resource => ChunkRules.ToChunkCoordinate(resource.Position, chunk.ChunkSize) == chunk.Coordinate) > worldSystems.Config.MaxResourcesPerChunk))
                return "That part of the map has too many resources. Choose another area.";
            environmentSystem.AddStartingResources(map, additions, changedChunks, WorldTick);
            SyncEcologyResourceStates();
        }
        checkpointSchemaVersion = StateSchemaVersion;
        return null;
    }
}
