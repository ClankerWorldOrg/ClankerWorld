using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

internal sealed partial class EnvironmentSystem
{
    internal static void CreatePhysicalState(IEnvironmentAgentWorld world)
    {
        var startingPositions = new[]
        {
            new GridPoint(0, 0),
            new GridPoint(1, 1),
            new GridPoint(2, 1),
            new GridPoint(3, 1),
        };
        var profiles = new (string Id, string Personality, string Aspiration)[]
        {
            ("founder-scout", "curious", "map the nearby world"),
            ("founder-mira", "practical", "make the household self-sufficient"),
            ("founder-rowan", "patient", "build something lasting"),
            ("founder-ilya", "observant", "teach and preserve memory"),
        };
        for (var index = 0; index < profiles.Length; index++)
        {
            var profile = profiles[index];
            world.PhysicalAgents.PlaceStartingAgent(profile.Id, startingPositions[index], profile.Personality, profile.Aspiration);
        }
    }

    internal static void DrainNeeds(IEnvironmentAgentWorld world)
    {
        foreach (var state in world.Agents.Values.ToArray())
        {
            world.PhysicalAgents.RecordFullness(state.AgentId, state, Math.Max(0, state.FullnessBasisPoints - 4));
        }
    }

    internal static void RemoveDeadPhysicalState(IEnvironmentAgentWorld world)
    {
        var activeIds = world.Society.Agents
            .Where(item => item.Status == SocietyAgentStatus.Active)
            .Select(item => item.Id)
            .ToHashSet(StringComparer.Ordinal);
        foreach (var id in world.Agents.Keys.Where(id => !activeIds.Contains(id)).ToArray())
        {
            if (world.PendingInstructionFor(id) is { Task.Action: "construct_building" or "expand_building" } buildingOrder)
            {
                world.CancelConstructionForOrder(buildingOrder);
                world.CancelExpansionForOrder(buildingOrder);
                world.SetOrderStatus(buildingOrder, OrderStatus.Blocked, "The ordered building worker is no longer alive.");
            }
            if (world.PendingInstructionFor(id) is { Task.Action: "produce_item" } productionOrder)
            {
                world.CancelProductionForOrder(productionOrder);
                world.SetOrderStatus(productionOrder, OrderStatus.Blocked, "The ordered production worker is no longer alive.");
            }
            if (world.Society.Inventory.Lots.Any(lot => lot.CarrierId == id))
            {
                var position = world.Agents[id].Position;
                world.RunInventoryCommand(InventorySystem.DropDeceasedCarrierGoods(id, position));
            }
            var deceased = world.Society.GetAgent(id);
            var deathTick = deceased.DeathTick ?? throw new InvalidDataException("A removed agent has no committed death.");
            // Society escrows ownership at death. Goods that had no separate
            // custodian or storage were carried by their owner; keep their
            // physical position at the deceased's tile after that owner is gone.
            var estate = world.Society.Estates.SingleOrDefault(item => item.DeceasedId == id);
            if (estate is not null)
            {
                var position = world.Agents[id].Position;
                foreach (var lot in world.Society.Inventory.Lots.Where(item => item.OwnerId == estate.Id &&
                             item.ContainerLotId is null && item.CarrierId is null &&
                             item.StorageBuildingId is null && item.GroundPosition is null).ToArray())
                    world.RunInventoryCommand(InventorySystem.PlaceEstateCustodyGoods(id, lot, estate, position));
            }
            foreach (var moment in (world.Agents[id].IdentityMoments ?? [])
                         .Where(item => item.Outcome is "waiting" or "requested").ToArray())
                world.FinishIdentityMoment(id, moment.Kind, "interrupted");
            world.PhysicalAgents.RecordDeath(id, new DeceasedAgentState(
                id, deathTick, world.Society.AgeAt(deceased, deathTick),
                AgentStore.PrepareForDeath(world.Agents[id], null, null, null, world.Agents[id].Equipment is { } equipment
                        ? equipment with { OrnamentLotId = null } : null), world.TownForResident(id))
            { BoatIdAtDeath = world.RecordBoatDeath(id) });
            world.PhysicalAgents.RemovePhysicalPresence(id);
            world.RemoveTownResident(id);
            world.AppendEvent(new AgentRemovedEventPayload(id));
        }
    }

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

    internal WeatherKind WeatherAt(WorldMap map, GridPoint position) => WeatherRules.At(State, position, map.Height,
        WeatherRules.RegionClimate(map, position));

    internal int OutdoorExposure(WorldMap map, GridPoint position, int nightChillAtFullDarkness) => WeatherExposure(map, position) +
        nightChillAtFullDarkness * DaylightRules.DarknessBasisPoints(State) / DaylightRules.FullDarkness;

    internal int WeatherExposure(WorldMap map, GridPoint position) => WeatherAt(map, position) switch
    {
        WeatherKind.Snow => 60,
        WeatherKind.Storm => 55,
        WeatherKind.Rain => 25,
        _ => map.ClimateAt(position) switch
        {
            ClimateZone.Tropical => 0,
            ClimateZone.Polar => 55,
            ClimateZone.Cold => State.Climate.Season == SeasonKind.Winter ? 45 : 15,
            _ => State.Climate.Season == SeasonKind.Winter ? 40 : 0,
        },
    };

    internal void SyncEcologyResourceStates(IEnvironmentWorld world)
    {
        foreach (var resource in State.Ecology.Resources)
            world.SetResourceAvailability(resource.Id, resource.State == EcologyResourceState.Available && resource.Quantity > 0);
    }

    internal void AdvanceWeather(IEnvironmentWorld world)
    {
        var previousClimate = State.Climate;
        var anchorPosition = world.WeatherAnchor;
        var previousAnchorWeather = WeatherAt(world.Map, anchorPosition);
        // Old saves retain their exact active daily weather until the first
        // resumed tick imports it into an episode. Loading alone changes nothing.
        State = RegionalWeatherRules.Initialize(State, world.Map);
        State = WorldSystemsRules.AdvanceOneTick(State);
        SyncEcologyResourceStates(world);
        var anchorWeather = WeatherAt(world.Map, anchorPosition);
        if (previousClimate.Season != State.Climate.Season || previousAnchorWeather != anchorWeather)
            world.AppendEvent(new WeatherChangedEventPayload(null, State.Climate.Season.ToString().ToLowerInvariant(), anchorWeather.ToString().ToLowerInvariant()));
    }
}
