using System.Reflection;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Harness;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Playtest;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.Tests;

public sealed class SharedReadServicesTests
{
    [Fact]
    public void EventLogPreservesLongestPrefixExplicitPositionsAndHistoryIds()
    {
        var world = new ReadWorld();
        world.People.Add("agent:child", new("agent:child", new(8, 2), 10_000, 0, "kind", "learn"));
        world.Dead.Add("agent", new(3, 4));
        var source = new List<PlaytestWorldEvent> { new(42, 9, "prior", "prior", null) };
        var log = new WorldEventLog(source, 41, "archive-head");
        source.Clear();
        var version = log.Version;
        log.AppendEvent(world, "living", "agent:child:effect:extra");
        Assert.NotSame(version, log.Version);
        log.AppendEvent(world, "deceased", "agent:effect");
        log.AppendEvent(world, "explicit", "agent:child", new(7, 7));
        log.AppendEvent(world, "unlocated", "unknown:effect");
        log.AppendEvent(world, "empty", "");
        Assert.Equal(Enumerable.Range(42, 6).Select(id => (long)id), log.Events.Select(item => item.EventId));
        Assert.Equal(new GridPoint?[] { null, new(8, 2), new(3, 4), new(7, 7), null, null },
            log.Events.Select(item => item.Position));
        Assert.All(log.Events.Skip(1), item => Assert.Equal(10, item.WorldTick));
        Assert.Equal(48, log.NextEventId);
        Assert.Equal(41, log.HistoryFloor);
        Assert.Equal("archive-head", log.ArchiveHead);
        Assert.Throws<NotSupportedException>(() => ((IList<PlaytestWorldEvent>)log.Events).Clear());
        var prepared = new WorldEventLog(log.Events, log.HistoryFloor, log.ArchiveHead);
        prepared.AppendEvent(world, "discarded", "agent:child");
        Assert.Equal(6, log.Events.Count);
        Assert.Equal(48, log.NextEventId);
        Assert.Equal(49, prepared.NextEventId);
        Assert.Equal(42, new WorldEventLog([], 41, "archive-head").NextEventId);
        Assert.Throws<OverflowException>(() => new WorldEventLog([], long.MaxValue));
    }

    [Fact]
    public void ResidentQueriesUseLivePositionWarmthAndReplacedSocietySnapshots()
    {
        var world = new ReadWorld();
        var actor = world.Society.Inhabitants[0].Id;
        world.People.Add(actor, new(actor, new(1, 1), 1_999, 0, "kind", "learn")
        { Survival = new(WarmthBasisPoints: 3_499) });
        Assert.True(ResidentQueries.AdultResident(world, actor));
        Assert.False(ResidentQueries.ChildResident(world, actor));
        Assert.False(ResidentQueries.AdultResident(world, "absent"));
        Assert.True(ResidentQueries.NeedsUrgentFood(world.People[actor], 2_000));
        Assert.False(ResidentQueries.NeedsUrgentFood(world.People[actor] with { HungerBasisPoints = 2_000 }, 2_000));
        Assert.True(ResidentQueries.NeedsUrgentWarmth(world, world.People[actor], 3_500));
        world.Warmth = 0;
        Assert.False(ResidentQueries.NeedsUrgentWarmth(world, world.People[actor], 3_500));
        world.Society = world.Society with
        {
            Inhabitants = world.Society.Inhabitants.Select(person =>
            person with { AgeBand = SocietyAgeBand.Child, HouseholdId = null }).ToArray()
        };
        Assert.False(ResidentQueries.AdultResident(world, actor));
        Assert.True(ResidentQueries.ChildResident(world, actor));
        Assert.Equal(actor, ResidentQueries.HouseholdFor(world, actor));
        var owner = world.Society.Inhabitants[1].Id;
        var inventory = InventoryFixture.AddLot(world.Society.Inventory, "carried", "wood", owner, 1);
        inventory = InventoryFixture.Relocate(inventory, "carry", "carried", owner, 1, carrierId: actor);
        inventory = InventoryFixture.AddLot(inventory, "ground", "wood", actor, 1, groundPosition: new(4, 5));
        inventory = InventoryFixture.AddLot(inventory, "building", "wood", actor, 1, storageBuildingId: "store");
        inventory = InventoryFixture.AddLot(inventory, "fallback", "wood", actor, 1);
        Assert.Equal(new(1, 1), ResidentQueries.HouseholdStockPosition(world, inventory.GetLot("carried")));
        world.People[actor] = world.People[actor] with { Position = new(2, 1) };
        Assert.Equal(new(2, 1), ResidentQueries.HouseholdStockPosition(world, inventory.GetLot("carried")));
        Assert.Equal(new(4, 5), ResidentQueries.HouseholdStockPosition(world, inventory.GetLot("ground")));
        Assert.Equal(new(6, 7), ResidentQueries.HouseholdStockPosition(world, inventory.GetLot("building")));
        Assert.Equal(world.SettlementStoragePosition, ResidentQueries.HouseholdStockPosition(world, inventory.GetLot("fallback")));
        Assert.True(ResidentQueries.IsWithinInteractionRange(world, new(0, 0), new(2, 0), 2));
        Assert.False(ResidentQueries.IsWithinInteractionRange(world, new(0, 0), new(2, 0), 1));
    }

    [Fact]
    public void InhabitantIndexKeepsSingleSemanticsAndDetectsMutationBehindSnapshotKey()
    {
        var checkpoint = new ReadWorld().Society;
        var person = checkpoint.Inhabitants[0];
        Assert.Same(person, checkpoint.GetInhabitant(person.Id));
        Assert.Throws<InvalidOperationException>(() => checkpoint.GetInhabitant(person.Id.ToUpperInvariant()));
        Assert.Throws<InvalidOperationException>(() => checkpoint.GetInhabitant(null!));
        var duplicates = checkpoint with { Inhabitants = [person, person, checkpoint.Inhabitants[1]] };
        Assert.Same(checkpoint.Inhabitants[1], duplicates.GetInhabitant(checkpoint.Inhabitants[1].Id));
        Assert.Throws<InvalidOperationException>(() => duplicates.GetInhabitant(person.Id));
        var list = checkpoint.Inhabitants.ToList();
        var mutable = checkpoint with { Inhabitants = list };
        Assert.Same(person, mutable.GetInhabitant(person.Id));
        list[0] = person with { Name = "Changed Name" };
        Assert.True(DerivedVerification.RecomputeOnHit);
        Assert.Throws<InvalidOperationException>(() => mutable.GetInhabitant(person.Id));
        var replaced = mutable with { Inhabitants = list.ToArray() };
        Assert.Equal("Changed Name", replaced.GetInhabitant(person.Id).Name);
        Assert.Same(person, checkpoint.GetInhabitant(person.Id));
    }

    [Fact]
    public async Task NativeCommitAndFailedPersistenceKeepEventLogAndResidentAdapterIsolated()
    {
        using var world = NormalPathWorld.CreateGenerated("shared-read-native", _ => new DeterministicDecisionProvider());
        Assert.True((await world.AdvanceOneTickAsync()).Advanced);
        world.Pause();
        var before = PrivateWorldRuntimeCodec.Encode(world.ExportState());
        var log = Log(world);
        var version = log.Version;
        var command = DeveloperEditTests.Edit(world, "give_goods", "wood", 1);
        Assert.Throws<IOException>(() => world.ApplyDeveloperEdit(command, _ => throw new IOException("refused persistence")));
        Assert.Equal(before, PrivateWorldRuntimeCodec.Encode(world.ExportState()));
        Assert.Same(log, Log(world));
        Assert.Same(version, log.Version);
        Assert.True(world.ApplyDeveloperEdit(command).Applied);
        Assert.NotSame(log, Log(world));
        var actor = command.AgentId;
        Assert.True(world.RenameAgent(actor, "Aster Readservice"));
        Assert.Equal("Aster Readservice", world.Society.GetInhabitant(actor).Name);
        var renamed = world.ExportState().Events.Last(item => item.Kind == "agent_renamed");
        Assert.Equal(world.Inhabitants.Single(person => person.InhabitantId == actor).Position, renamed.Position);
        var saved = PrivateWorldRuntimeCodec.Encode(world.ExportState());
        using var reload = PrivateWorldRuntime.Restore(PrivateWorldRuntimeCodec.Decode(saved), _ => new DeterministicDecisionProvider());
        Assert.Equal(saved, PrivateWorldRuntimeCodec.Encode(reload.ExportState()));
        world.Resume(); reload.Resume();
        var running = PrivateWorldRuntimeCodec.Encode(world.ExportState());
        Assert.False((await world.AdvanceOneTickAsync(() => false)).Advanced);
        Assert.Equal(running, PrivateWorldRuntimeCodec.Encode(world.ExportState()));
        for (var tick = 0; tick < 3; tick++)
        {
            Assert.True((await world.AdvanceOneTickAsync()).Advanced);
            Assert.True((await reload.AdvanceOneTickAsync()).Advanced);
            Assert.Equal(PrivateWorldRuntimeCodec.Encode(world.ExportState()), PrivateWorldRuntimeCodec.Encode(reload.ExportState()));
        }
    }

    private static WorldEventLog Log(PrivateWorldRuntime world) => (WorldEventLog)typeof(PrivateWorldRuntime)
        .GetField("worldEventLog", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(world)!;

    private sealed class ReadWorld : IResidentWorld
    {
        public SocietyCheckpoint Society { get; set; } = SocietyFixture.CreateGenesis("read-services",
            [SocietyFixture.CreateFounder("agent-a", "Aster Ash"), SocietyFixture.CreateFounder("agent-b", "Rowan Reed")]);
        public Dictionary<string, PlaytestInhabitantState> People { get; } = new(StringComparer.Ordinal);
        public Dictionary<string, GridPoint> Dead { get; } = new(StringComparer.Ordinal);
        public IReadOnlyDictionary<string, PlaytestInhabitantState> Inhabitants => People;
        public long WorldTick => 10;
        public GridPoint SettlementStoragePosition => new(9, 9);
        public int Warmth { get; set; } = -1;
        public GridPoint BuildingPosition(string id) => id == "store" ? new(6, 7) : throw new InvalidOperationException();
        public int FootDistance(GridPoint origin, GridPoint destination) => Math.Abs(origin.X - destination.X) + Math.Abs(origin.Y - destination.Y);
        public int WarmthChange(PlaytestInhabitantState person) => Warmth;
        public GridPoint? LivingPosition(string id) => People.TryGetValue(id, out var person) ? person.Position : null;
        public GridPoint? DeceasedPosition(string id) => Dead.TryGetValue(id, out var point) ? point : null;
    }
}
