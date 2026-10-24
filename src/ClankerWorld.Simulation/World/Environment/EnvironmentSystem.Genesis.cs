using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

internal sealed partial class EnvironmentSystem
{
    internal static WorldSystemsState CreateWorldSystems(string worldSeed, WorldMap map,
        WorldStartPace startPace = WorldStartPace.Legacy, bool regionalWeather = true)
    {
        var config = WorldStartPaceRules.WorldSystems(startPace);
        var resources = map.Resources
            .Select(resource => resource.TreeKind is not null
                ? TreeGrowthRules.GeneratedTree(resource)
                : resource.IsRenewable
                ? new EcologyResource(
                    resource.Id,
                    resource.Kind,
                    resource.Position,
                    true,
                    8,
                    12,
                    2,
                    1,
                    SeasonKind.Spring,
                    1,
                    EcologyResourceState.Available)
                : new EcologyResource(
                    resource.Id,
                    resource.Kind,
                    resource.Position,
                    false,
                    3,
                    3,
                    0,
                    0,
                    SeasonKind.Spring,
                    0,
                    EcologyResourceState.Available))
            .ToArray();
        var chunkSize = map.Width > ChunkRules.DefaultChunkSize || map.Height > ChunkRules.DefaultChunkSize
            ? GeographyGenerator.ChunkSize : ChunkRules.DefaultChunkSize;
        var chunks = new List<ChunkManifest>();
        for (var top = 0; top < map.Height; top += chunkSize)
            for (var left = 0; left < map.Width; left += chunkSize)
            {
                var coordinate = new ChunkCoordinate(left / chunkSize, top / chunkSize);
                var chunkWidth = Math.Min(chunkSize, map.Width - left);
                var chunkHeight = Math.Min(chunkSize, map.Height - top);
                chunks.Add(ChunkManifestCodec.WithDigest(new ChunkManifest(
                    coordinate,
                    chunkSize,
                    chunkWidth,
                    chunkHeight,
                    map.Width > ChunkRules.DefaultChunkSize || map.Height > ChunkRules.DefaultChunkSize
                        ? "noise-drainage" : SmallTemperateMapGenerator.GeneratorId,
                    map.Width > ChunkRules.DefaultChunkSize || map.Height > ChunkRules.DefaultChunkSize
                        ? "v2" : SmallTemperateMapGenerator.GeneratorVersion,
                    map.Resources.Where(resource => resource.Position.X >= left &&
                            resource.Position.X < left + chunkWidth && resource.Position.Y >= top &&
                            resource.Position.Y < top + chunkHeight)
                        .Select(resource => new ChunkResourceMetadata(
                            resource.Id, resource.Kind,
                            new GridPoint(resource.Position.X - left, resource.Position.Y - top),
                            resource.IsRenewable)).ToArray())));
            }
        var state = WorldSystemsRules.CreateGenesis(
            worldSeed,
            config,
            resources,
            chunks);
        return regionalWeather ? RegionalWeatherRules.Initialize(state, map) : state;
    }

    internal static SocietyRuntime CreateSociety(
        string worldSeed,
        string worldId,
        Func<string, IDecisionProvider>? providerFactory,
        int maxCognitionQueueLength,
        int maxCognitionDispatchPerCycle,
        WorldStartPace startPace, string foodLotId, string householdId, string secondHouseholdId)
    {
        var config = WorldStartPaceRules.Society(startPace);
        int Age(int index) => SocietyRules.FounderArrivalAge(config, worldSeed, index);
        var founders = new[]
        {
            SocietyRules.CreateFounder("founder-scout", "Scout", "model:scout", config: config, startingAge: Age(0)),
            SocietyRules.CreateFounder("founder-mira", "Mira", "model:mira", config: config, startingAge: Age(1)),
            SocietyRules.CreateFounder("founder-rowan", "Rowan", "model:rowan", config: config, startingAge: Age(2)),
            SocietyRules.CreateFounder("founder-ilya", "Ilya", "model:ilya", config: config, startingAge: Age(3)),
        };
        var initialFounders = startPace == WorldStartPace.FounderSetup ? [] : founders;
        var checkpoint = SocietyRules.CreateGenesis(
            worldId,
            initialFounders,
            [
                InventoryStore.CreateInventoryLot(foodLotId, "food", householdId, 32, 10_000, 10_000, 0),
                InventoryStore.CreateInventoryLot("wood:first", "wood", householdId, 48, 10_000, 10_000, 0),
                InventoryStore.CreateInventoryLot("tools:first", "tool", householdId, 4, 10_000, 10_000, 0),
                ..(startPace == WorldStartPace.Legacy ? new InventoryLot[]
                {
                    InventoryStore.CreateInventoryLot("legacy-wooden-axe:first", "wooden_axe", householdId, 1, 10_000, 10_000, 0),
                    InventoryStore.CreateInventoryLot("legacy-wooden-pickaxe:first", "wooden_pickaxe", householdId, 1, 10_000, 10_000, 0),
                } : []),
                ..(startPace == WorldStartPace.FounderSetup ? new InventoryLot[]
                {
                    InventoryStore.CreateInventoryLot("food:second", "food", secondHouseholdId, 16, 10_000, 10_000, 0),
                    InventoryStore.CreateInventoryLot("wood:second", "wood", secondHouseholdId, 24, 10_000, 10_000, 0),
                    InventoryStore.CreateInventoryLot("tools:second", "tool", secondHouseholdId, 2, 10_000, 10_000, 0),
                    InventoryStore.CreateInventoryLot("seeds:first", "grain_seed", householdId, 8, 10_000, 10_000, 0),
                    InventoryStore.CreateInventoryLot("green-seeds:first", "cultivated_green_seed", householdId, 4, 10_000, 10_000, 0),
                    InventoryStore.CreateInventoryLot("potatoes:first", "potatoes", householdId, 4, 10_000, 10_000, 0),
                    InventoryStore.CreateInventoryLot("clothing:first", "clothing", householdId, 2, 10_000, 10_000, 0),
                    InventoryStore.CreateInventoryLot("seeds:second", "grain_seed", secondHouseholdId, 8, 10_000, 10_000, 0),
                    InventoryStore.CreateInventoryLot("green-seeds:second", "cultivated_green_seed", secondHouseholdId, 4, 10_000, 10_000, 0),
                    InventoryStore.CreateInventoryLot("potatoes:second", "potatoes", secondHouseholdId, 4, 10_000, 10_000, 0),
                    InventoryStore.CreateInventoryLot("clothing:second", "clothing", secondHouseholdId, 2, 10_000, 10_000, 0),
                } : []),
            ],
            config,
            "model:world-default");
        checkpoint = SocietyRules.CreateHousehold(
            checkpoint,
            householdId,
            "First household",
            initialFounders.Select(item => item.Id)).Checkpoint;
        if (startPace == WorldStartPace.FounderSetup)
            checkpoint = SocietyRules.CreateHousehold(checkpoint, secondHouseholdId, "Second household", []).Checkpoint;
        if (startPace != WorldStartPace.FounderSetup)
        {
            checkpoint = SocietyRules.AssignRole(checkpoint, "founder-scout", SocietyWorkRole.Trader).Checkpoint;
            checkpoint = SocietyRules.AssignRole(checkpoint, "founder-mira", SocietyWorkRole.Farmer).Checkpoint;
            checkpoint = SocietyRules.AssignRole(checkpoint, "founder-rowan", SocietyWorkRole.Builder).Checkpoint;
            checkpoint = SocietyRules.AssignRole(checkpoint, "founder-ilya", SocietyWorkRole.Teacher).Checkpoint;
        }
        return new SocietyRuntime(
            checkpoint,
            providerFactory,
            maxCognitionQueueLength,
            maxCognitionDispatchPerCycle);
    }
}
