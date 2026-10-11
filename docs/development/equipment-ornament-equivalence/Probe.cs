// Portable native feature probe: compile this unchanged against both checkouts.
using System.Collections.Concurrent;
using System.IO.Compression;
using System.Text.Json;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Harness;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Playtest;
using ClankerWorld.Simulation.Society;
using ClankerWorld.Simulation.World;

var output = args[0];
Directory.CreateDirectory(output);
using var generated = CreateGenerated("equipment-ornament-query-equivalence", new Choices());
if (!(await generated.AdvanceOneTickAsync()).Advanced) throw new InvalidOperationException("Activation failed.");
var initial = generated.ExportState();
var actor = initial.Society.Society.Inhabitants.First(person => person.HouseholdId == "household:camp-alpha").Id;
var household = initial.Society.Society.GetInhabitant(actor).HouseholdId!;
var other = initial.Inhabitants.First(person => person.InhabitantId != actor).InhabitantId;
var house = initial.WorldSimulation!.Buildings.Single(building => building.InstanceId == "first-town-house-a");
foreach (var scenario in new[] { "gear-household", "gear-full-upgrade", "gear-personal-stack", "gear-reserved", "gear-borrowed", "gear-remote",
    "ornament-household", "ornament-personal-full", "ornament-partly-reserved", "ornament-full-household", "ornament-borrowed", "ornament-remote" })
{
    var gear = scenario.StartsWith("gear", StringComparison.Ordinal);
    var personal = scenario is "gear-personal-stack" or "ornament-personal-full";
    var owner = personal ? actor : household;
    var inventory = initial.Society.Society.Inventory with
    { Lots = initial.Society.Society.Inventory.Lots.Where(lot => lot.OwnerId != actor).ToArray() };
    var quantity = scenario == "gear-personal-stack" ? 3 : scenario == "ornament-partly-reserved" ? 2 : 1;
    inventory = InventoryFixture.AddLot(inventory, "proof-choice", gear ? "sack" : "gold_ornament", owner, quantity,
        storageBuildingId: personal ? null : house.InstanceId);
    if (scenario.EndsWith("borrowed", StringComparison.Ordinal))
        inventory = inventory with { Lots = inventory.Lots.Select(lot => lot.Id == "proof-choice"
            ? lot with { CarrierId = other, StorageBuildingId = null } : lot).ToArray() };
    if (scenario is "gear-reserved" or "ornament-partly-reserved")
        inventory = InventoryFixture.Reserve(inventory, "proof-reservation", owner, "proof-choice", 1, "other_work", long.MaxValue);
    PersonalEquipment? equipment = null;
    if (scenario == "gear-full-upgrade")
    {
        inventory = InventoryFixture.AddLot(inventory, "proof-old-basket", "basket", actor, 1, conditionBasisPoints: 0);
        inventory = InventoryFixture.AddLot(inventory, "proof-ballast", "wood", actor, 12);
        equipment = new(CarryAidLotId: "proof-old-basket");
    }
    if (scenario is "ornament-personal-full" or "ornament-full-household")
        inventory = InventoryFixture.AddLot(inventory, "proof-ballast", "stone", actor, personal ? 7 : 8);
    var remote = scenario.EndsWith("remote", StringComparison.Ordinal);
    var position = remote ? initial.Map.Tiles.First(tile => initial.Map.IsPassable(tile.Position) &&
        initial.Map.FootDistance(tile.Position, house.Position) == 4 && initial.Inhabitants.All(person => person.Position != tile.Position)).Position : house.Position;
    var state = WithInventory(initial, inventory) with
    {
        JevEnabled = false, RoutineHelper = RoutineHelperSettings.Off,
        Inhabitants = initial.Inhabitants.Select(person => person with
        {
            Position = person.InhabitantId == actor ? position : person.Position,
            Equipment = person.InhabitantId == actor ? equipment : person.Equipment,
            HungerBasisPoints = 10_000, LastDecisionContext = null,
            Survival = new SurvivalCondition { WarmthBasisPoints = 10_000, NutritionBasisPoints = 10_000 }
        }).ToArray()
    };
    var recorder = new ConcurrentQueue<object>();
    using var world = PrivateWorldRuntime.Restore(PrivateWorldRuntimeCodec.Decode(PrivateWorldRuntimeCodec.Encode(state)),
        id => new Choices(id == actor ? gear ? "equip_carry_aid" : "wear_ornament:" : null, recorder));
    await CaptureRun(world, recorder, output, scenario, 2);
    var person = world.Inhabitants.Single(person => person.InhabitantId == actor);
    var selected = gear ? person.Equipment?.CarryAidLotId : person.Equipment?.OrnamentLotId;
    var blocked = scenario is "gear-reserved" or "gear-borrowed" or "ornament-full-household" or "ornament-borrowed";
    if (!remote && (selected is not null) == blocked) throw new InvalidOperationException("Equipment outcome differed: " + scenario);
    if (selected is not null && (world.Society.Inventory.GetLot(selected).OwnerId != actor || world.Society.Inventory.GetLot(selected).Quantity != 1))
        throw new InvalidOperationException("Selected unit lost ownership or quantity.");
    if (blocked && world.Society.Inventory.GetLot("proof-choice") with { LastProcessedTick = inventory.GetLot("proof-choice").LastProcessedTick } != inventory.GetLot("proof-choice"))
        throw new InvalidOperationException("Refused stock changed.");
    if (scenario == "gear-full-upgrade" && (world.Society.Inventory.GetLot("proof-ballast").Quantity != 12 ||
        world.Society.Inventory.GetLot("proof-old-basket").OwnerId != actor)) throw new InvalidOperationException("Replacement lost real cargo.");
    if (remote && person.Position == position) throw new InvalidOperationException("Remote choice did not walk.");
}
Console.WriteLine("Twelve native equipment/ornament scenarios, 36 complete checkpoint/event/candidate frames, refused ticks and strict reloads.");

static PrivateWorldRuntimeState WithInventory(PrivateWorldRuntimeState state, InventoryCheckpoint inventory) =>
    state with { Society = state.Society with { Society = state.Society.Society with { Inventory = inventory } } };

static async Task CaptureRun(PrivateWorldRuntime world, ConcurrentQueue<object> observations, string output, string name, int ticks)
{
    var directory = Path.Combine(output, name);
    Directory.CreateDirectory(directory);
    world.Validate();
    var before = PrivateWorldRuntimeCodec.Encode(world.ExportState());
    if ((await world.AdvanceOneTickAsync(() => false)).Advanced || !before.SequenceEqual(PrivateWorldRuntimeCodec.Encode(world.ExportState())))
        throw new InvalidOperationException("Refused tick changed the checkpoint.");
    for (var tick = 0; tick <= ticks; tick++)
    {
        var state = world.ExportState();
        var prefix = Path.Combine(directory, $"tick-{tick:D6}");
        var bytes = PrivateWorldRuntimeCodec.Encode(state);
        using (var file = File.Create(prefix + ".checkpoint.gz"))
        using (var compressed = new GZipStream(file, CompressionLevel.Fastest)) compressed.Write(bytes);
        File.WriteAllText(prefix + ".events.json", JsonSerializer.Serialize(state.Events));
        File.WriteAllText(prefix + ".digests.json", JsonSerializer.Serialize(observations.ToArray()));
        using var loaded = PrivateWorldRuntime.Restore(PrivateWorldRuntimeCodec.Decode(bytes));
        if (!bytes.SequenceEqual(PrivateWorldRuntimeCodec.Encode(loaded.ExportState()))) throw new InvalidOperationException("Strict reload differed.");
        if (tick != ticks && !(await world.AdvanceOneTickAsync()).Advanced) throw new InvalidOperationException("Native tick refused.");
    }
    world.Validate();
    Console.WriteLine($"{name}: {ticks + 1} exact frames, {observations.Count} observations.");
}

static PrivateWorldRuntime CreateGenerated(string seed, IDecisionProvider provider)
{
    var options = new GeographyOptions(seed, WorldSizePreset.Small);
    var world = new PrivateWorldRuntime(seed, _ => provider, startPace: WorldStartPace.FounderSetup, geographyOptions: options);
    var map = world.ExportState().Map;
    var camp = map.Resources.Single(item => item.Id == "berry-patch").Position;
    var occupied = map.Resources.Select(item => item.Position).Concat(map.CampObjects.Select(item => item.Position)).ToHashSet();
    var anchor = map.Tiles.Select(tile => tile.Position)
        .Where(point => map.IsBuildable(point) && !occupied.Contains(point))
        .OrderBy(point => Math.Abs(point.X - camp.X) + Math.Abs(point.Y - camp.Y)).ThenBy(point => point.Y).ThenBy(point => point.X)
        .Prepend(camp).First(point =>
        {
            if (FirstTownLayoutPlanner.Plan(map, point) is not { } layout) return false;
            var taken = occupied.Concat(layout.RoadTiles).Concat(layout.Buildings.SelectMany(building =>
                Enumerable.Range(0, building.Height).SelectMany(y => Enumerable.Range(0, building.Width)
                    .Select(x => new GridPoint(building.Position.X + x, building.Position.Y + y))))).ToHashSet();
            return map.Tiles.Count(tile => Math.Abs(tile.Position.X - point.X) <= 5 && Math.Abs(tile.Position.Y - point.Y) <= 5 &&
                map.IsBuildable(tile.Position) && !taken.Contains(tile.Position)) >= PrivateWorldRuntime.RequiredFounders;
        });
    world.InitializeFirstTownContent();
    world.AcceptFirstTownLayout(anchor);
    var buildings = world.WorldSimulation.Buildings.SelectMany(building => WorldContentSimulationRules.Footprint(
        world.WorldContent.Buildings.Single(item => item.CanonicalId == building.DefinitionId), building.Position)).ToHashSet();
    var roads = world.RoadTiles.ToHashSet();
    var sites = map.Tiles.Where(tile => Math.Abs(tile.Position.X - anchor.X) <= 5 && Math.Abs(tile.Position.Y - anchor.Y) <= 5 &&
        map.IsBuildable(tile.Position) && !occupied.Contains(tile.Position) && !buildings.Contains(tile.Position) && !roads.Contains(tile.Position))
        .Take(PrivateWorldRuntime.RequiredFounders).Select(tile => tile.Position).ToArray();
    for (var index = 0; index < sites.Length; index++) world.PlaceFounder($"founder:{index + 1:D32}", sites[index]);
    world.StartWorld();
    return world;
}

sealed class Choices(string? wanted = null, ConcurrentQueue<object>? observations = null) : IDecisionProvider
{
    public DecisionProviderKind Kind => DecisionProviderKind.LargeLanguageModel;
    public long ProviderEpoch => 0;
    public ValueTask<CognitionDecisionResponse> DecideAsync(CognitionDecisionRequest request, CancellationToken cancellationToken = default)
    {
        observations?.Enqueue(new { request.Observation.InhabitantId, request.Observation.WorldTick, request.Observation.ObservationDigest,
            request.Observation.Candidates });
        var selected = wanted is null ? "safe_idle" : request.Observation.Candidates.FirstOrDefault(candidate => candidate.Id.StartsWith(wanted, StringComparison.Ordinal))?.Id ?? "safe_idle";
        return ValueTask.FromResult(new CognitionDecisionResponse(request.RequestId, request.Observation.InhabitantId, Kind, request.ProviderEpoch,
            request.Observation.RunEpoch, request.Observation.DecisionGeneration, request.Observation.ObservationDigest, selected, 1, new Dictionary<string, double> { [selected] = 1 }));
    }
}
