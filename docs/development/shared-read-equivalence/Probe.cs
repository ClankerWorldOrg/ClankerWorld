// Compile unchanged against base and candidate; uses only existing public runtime APIs.
using System.Collections.Concurrent;
using System.IO.Compression;
using System.Text.Json;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Harness;
using ClankerWorld.Simulation.Playtest;
using ClankerWorld.Simulation.World;
using ClankerWorld.Simulation.Society;

var output = args[0];
string[] seeds = ["shared-read-native", "town-project-real-donation"];
const int ticks = 10;
Directory.CreateDirectory(output);
File.WriteAllText(Path.Combine(output, "run.json"), JsonSerializer.Serialize(new
{ Commit = args[1], WorkingTree = args[2], Seeds = seeds, Ticks = ticks, Modes = new[] { "generated" } }));
for (var ordinal = 0; ordinal < seeds.Length; ordinal++)
{
    var recorder = new RecordingChoices();
    using var world = CreateGenerated(seeds[ordinal], recorder);
    var directory = Path.Combine(output, $"generated-{ordinal:D2}");
    Directory.CreateDirectory(directory);
    var counted = new CountedResidents(world.Society.Inhabitants);
    var countedCheckpoint = world.Society with { Inhabitants = counted };
    _ = countedCheckpoint.GetInhabitant(counted[0].Id);
    counted.Reset();
    for (var repeat = 0; repeat < 100; repeat++)
        foreach (var person in world.Society.Inhabitants)
            if (!ReferenceEquals(person, countedCheckpoint.GetInhabitant(person.Id)))
                throw new InvalidOperationException("Counted lookup changed identity.");
    File.WriteAllText(Path.Combine(directory, "index-work.json"), JsonSerializer.Serialize(new
    { Residents = counted.Count, Lookups = counted.Count * 100, ListVisits = counted.Visits }));
    for (var frame = 0; frame <= ticks; frame++)
    {
        world.Validate();
        var state = world.ExportState();
        var bytes = PrivateWorldRuntimeCodec.Encode(state);
        using (var reload = PrivateWorldRuntime.Restore(PrivateWorldRuntimeCodec.Decode(bytes)))
            if (!bytes.SequenceEqual(PrivateWorldRuntimeCodec.Encode(reload.ExportState())))
                throw new InvalidOperationException("Strict reload changed captured bytes.");
        foreach (var person in world.Society.Inhabitants)
            if (!ReferenceEquals(person, world.Society.GetInhabitant(person.Id)))
                throw new InvalidOperationException("Resident lookup changed identity.");
        var prefix = Path.Combine(directory, $"tick-{frame:D6}");
        using (var file = File.Create(prefix + ".checkpoint.gz"))
        using (var compressed = new GZipStream(file, CompressionLevel.Fastest)) compressed.Write(bytes);
        File.WriteAllText(prefix + ".events.json", JsonSerializer.Serialize(state.Events));
        File.WriteAllText(prefix + ".digests.json", JsonSerializer.Serialize(recorder.Observations.ToArray()));
        if (frame == ticks) break;
        if (frame == 1)
        {
            world.Pause();
            var paused = PrivateWorldRuntimeCodec.Encode(world.ExportState());
            var actor = world.Inhabitants[0].InhabitantId;
            var edit = new PrivateWorldDeveloperEdit(world.Society.WorldId, world.ExportState().Events[^1].EventId,
                actor, "give_goods", "wood", 1);
            try
            {
                world.ApplyDeveloperEdit(edit, _ => throw new IOException("fixture refused persistence"));
                throw new InvalidOperationException("Failed persistence was accepted.");
            }
            catch (IOException) { }
            if (!paused.SequenceEqual(PrivateWorldRuntimeCodec.Encode(world.ExportState())))
                throw new InvalidOperationException("Failed persistence changed committed state.");
            if (!world.ApplyDeveloperEdit(edit).Applied || !world.RenameAgent(actor, "Aster Readservice"))
                throw new InvalidOperationException("Native edit or rename did not execute.");
            world.Resume();
        }
        var before = PrivateWorldRuntimeCodec.Encode(world.ExportState());
        if ((await world.AdvanceOneTickAsync(() => false)).Advanced ||
            !before.SequenceEqual(PrivateWorldRuntimeCodec.Encode(world.ExportState())))
            throw new InvalidOperationException("Refused tick changed committed state.");
        if (!(await world.AdvanceOneTickAsync()).Advanced) throw new InvalidOperationException("Native tick refused.");
    }
    if (recorder.Observations.IsEmpty) throw new InvalidOperationException("No native candidate lists recorded.");
    Console.WriteLine($"{seeds[ordinal]}: 11 frames, {recorder.Observations.Count} complete candidate lists/digests, strict reload and rollback passed.");
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

sealed class RecordingChoices : IDecisionProvider
{
    private readonly DeterministicDecisionProvider native = new();
    public ConcurrentQueue<ObservationDigest> Observations { get; } = new();
    public DecisionProviderKind Kind => native.Kind;
    public long ProviderEpoch => native.ProviderEpoch;
    public ValueTask<CognitionDecisionResponse> DecideAsync(CognitionDecisionRequest request, CancellationToken cancellationToken = default)
    {
        var observation = request.Observation;
        Observations.Enqueue(new(observation.InhabitantId, observation.WorldTick, observation.ObservationDigest, observation.Candidates));
        return native.DecideAsync(request, cancellationToken);
    }
}

sealed record ObservationDigest(string InhabitantId, long WorldTick, string Digest, IReadOnlyList<CognitionCandidate> Candidates);

sealed class CountedResidents(IReadOnlyList<SocietyInhabitant> people) : IReadOnlyList<SocietyInhabitant>
{
    public long Visits { get; private set; }
    public int Count => people.Count;
    public SocietyInhabitant this[int index] { get { Visits++; return people[index]; } }
    public void Reset() => Visits = 0;
    public IEnumerator<SocietyInhabitant> GetEnumerator()
    {
        foreach (var person in people) { Visits++; yield return person; }
    }
    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
}
