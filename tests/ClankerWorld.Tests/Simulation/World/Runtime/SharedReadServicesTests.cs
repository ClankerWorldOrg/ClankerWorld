using System.Reflection;
using ClankerWorld.Simulation;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.World;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Tests;

public sealed class SharedReadServicesTests
{
    [Fact]
    public void EventLogUsesCompletePayloadActorsExplicitPositionsAndHistoryIds()
    {
        const string actor = "world:agent:birth:agent:parent:agent:other:00000000000000000000000000000001";
        var world = new ReadWorld();
        world.Dead.Add("world:agent", new(3, 4));
        world.People.Add(actor, new(actor, new(8, 2), 10_000, 0, "kind", "learn"));
        world.Dead.Add("agent", new(3, 4));
        var source = new List<WorldEventEntry> { new(42, 9, new TickAdvancedEventPayload(null, 9)) };
        var log = new WorldEventLog(source, 41, "archive-head");
        source.Clear();
        var version = log.Version;
        log.AppendEvent(world, new RecipeCancelledEventPayload(actor, "world:agent:job:effect", "recipe:extra", null));
        Assert.NotSame(version, log.Version);
        log.AppendEvent(world, new InstructionNotUnderstoodEventPayload("agent", "effect"));
        log.AppendEvent(world, new AgentIdleEventPayload(actor), new(7, 7));
        log.AppendEvent(world, new InstructionNotUnderstoodEventPayload("unknown", "effect"));
        log.AppendEvent(world, new PausedEventPayload(null));
        Assert.Equal(Enumerable.Range(42, 6).Select(id => (long)id), log.Events.Select(item => item.EventId));
        Assert.Equal(new GridPoint?[] { null, new(8, 2), new(3, 4), new(7, 7), null, null },
            log.Events.Select(item => item.Position));
        Assert.All(log.Events.Skip(1), item => Assert.Equal(10, item.WorldTick));
        Assert.Equal(48, log.NextEventId);
        Assert.Equal(41, log.HistoryFloor);
        Assert.Equal("archive-head", log.ArchiveHead);
        Assert.Throws<NotSupportedException>(() => ((IList<WorldEventEntry>)log.Events).Clear());
        var prepared = new WorldEventLog(log.Events, log.HistoryFloor, log.ArchiveHead);
        prepared.AppendEvent(world, new AgentIdleEventPayload(actor));
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
        var actor = world.Society.Agents[0].Id;
        world.People.Add(actor, new(actor, new(1, 1), 1_999, 0, "kind", "learn")
        { Survival = new(WarmthBasisPoints: 3_499) });
        Assert.True(AgentQueries.IsAdult(world, actor));
        Assert.False(AgentQueries.IsChild(world, actor));
        Assert.False(AgentQueries.IsAdult(world, "absent"));
        Assert.True(AgentQueries.NeedsUrgentFood(world.People[actor], 2_000));
        Assert.False(AgentQueries.NeedsUrgentFood(world.People[actor] with { FullnessBasisPoints = 2_000 }, 2_000));
        Assert.True(AgentQueries.NeedsUrgentWarmth(world, world.People[actor], 3_500));
        world.Warmth = 0;
        Assert.False(AgentQueries.NeedsUrgentWarmth(world, world.People[actor], 3_500));
        world.Society = world.Society with
        {
            Agents = world.Society.Agents.Select(person =>
            person with { AgeBand = SocietyAgeBand.Child, HouseholdId = null }).ToArray()
        };
        Assert.False(AgentQueries.IsAdult(world, actor));
        Assert.True(AgentQueries.IsChild(world, actor));
        Assert.Equal(actor, AgentQueries.HouseholdFor(world, actor));
        var owner = world.Society.Agents[1].Id;
        var inventory = InventoryAuthority.AddLot(world.Society.Inventory, "carried", "wood", owner, 1);
        inventory = InventoryAuthority.Relocate(inventory, "carry", "carried", owner, 1, carrierId: actor);
        inventory = InventoryAuthority.AddLot(inventory, "ground", "wood", actor, 1, groundPosition: new(4, 5));
        inventory = InventoryAuthority.AddLot(inventory, "building", "wood", actor, 1, storageBuildingId: "store");
        inventory = InventoryAuthority.AddLot(inventory, "fallback", "wood", actor, 1);
        Assert.Equal(new(1, 1), AgentQueries.HouseholdStockPosition(world, inventory.GetLot("carried")));
        world.People[actor] = world.People[actor] with { Position = new(2, 1) };
        Assert.Equal(new(2, 1), AgentQueries.HouseholdStockPosition(world, inventory.GetLot("carried")));
        Assert.Equal(new(4, 5), AgentQueries.HouseholdStockPosition(world, inventory.GetLot("ground")));
        Assert.Equal(new(6, 7), AgentQueries.HouseholdStockPosition(world, inventory.GetLot("building")));
        Assert.Equal(world.SharedStoragePosition, AgentQueries.HouseholdStockPosition(world, inventory.GetLot("fallback")));
        Assert.True(AgentQueries.IsWithinInteractionRange(world, new(0, 0), new(2, 0), 2));
        Assert.False(AgentQueries.IsWithinInteractionRange(world, new(0, 0), new(2, 0), 1));
    }

    [Fact]
    public void AgentIndexKeepsSingleSemanticsAndDetectsMutationBehindSnapshotKey()
    {
        var checkpoint = new ReadWorld().Society;
        var person = checkpoint.Agents[0];
        Assert.Same(person, checkpoint.GetAgent(person.Id));
        Assert.Throws<InvalidOperationException>(() => checkpoint.GetAgent(person.Id.ToUpperInvariant()));
        Assert.Throws<InvalidOperationException>(() => checkpoint.GetAgent(null!));
        var duplicates = checkpoint with { Agents = [person, person, checkpoint.Agents[1]] };
        Assert.Same(checkpoint.Agents[1], duplicates.GetAgent(checkpoint.Agents[1].Id));
        Assert.Throws<InvalidOperationException>(() => duplicates.GetAgent(person.Id));
        var list = checkpoint.Agents.ToList();
        var mutable = checkpoint with { Agents = list };
        Assert.Same(person, mutable.GetAgent(person.Id));
        list[0] = person with { Name = "Changed Name" };
        Assert.True(DerivedVerification.RecomputeOnHit);
        Assert.Throws<InvalidOperationException>(() => mutable.GetAgent(person.Id));
        var replaced = mutable with { Agents = list.ToArray() };
        Assert.Equal("Changed Name", replaced.GetAgent(person.Id).Name);
        Assert.Same(person, checkpoint.GetAgent(person.Id));
    }

    [Fact]
    public async Task NativeCommitAndFailedPersistenceKeepEventLogAndResidentAdapterIsolated()
    {
        using var world = NormalPathWorld.CreateGenerated("shared-read-native", _ => new DeterministicDecisionProvider());
        Assert.True((await world.AdvanceOneTickAsync()).Advanced);
        world.Pause();
        var before = WorldRuntimeCodec.Encode(world.ExportState());
        var log = Log(world);
        var version = log.Version;
        var command = DeveloperEditTests.Edit(world, "give_goods", "wood", 1);
        Assert.Throws<IOException>(() => world.ApplyDeveloperEdit(command, _ => throw new IOException("refused persistence")));
        Assert.Equal(before, WorldRuntimeCodec.Encode(world.ExportState()));
        Assert.Same(log, Log(world));
        Assert.Same(version, log.Version);
        Assert.True(world.ApplyDeveloperEdit(command).Applied);
        Assert.NotSame(log, Log(world));
        var actor = command.AgentId;
        Assert.True(world.RenameAgent(actor, "Aster Readservice"));
        Assert.Equal("Aster Readservice", world.Society.GetAgent(actor).Name);
        var renamed = world.ExportState().Events.Last(item => item.Kind == "agent_renamed");
        Assert.Equal(world.Agents.Single(person => person.AgentId == actor).Position, renamed.Position);
        var saved = WorldRuntimeCodec.Encode(world.ExportState());
        using var reload = WorldRuntime.Restore(WorldRuntimeCodec.Decode(saved), _ => new DeterministicDecisionProvider());
        Assert.Equal(saved, WorldRuntimeCodec.Encode(reload.ExportState()));
        world.Resume(); reload.Resume();
        var running = WorldRuntimeCodec.Encode(world.ExportState());
        Assert.False((await world.AdvanceOneTickAsync(() => false)).Advanced);
        Assert.Equal(running, WorldRuntimeCodec.Encode(world.ExportState()));
        for (var tick = 0; tick < 3; tick++)
        {
            Assert.True((await world.AdvanceOneTickAsync()).Advanced);
            Assert.True((await reload.AdvanceOneTickAsync()).Advanced);
            Assert.Equal(WorldRuntimeCodec.Encode(world.ExportState()), WorldRuntimeCodec.Encode(reload.ExportState()));
        }
    }

    private static WorldEventLog Log(WorldRuntime world) => (WorldEventLog)typeof(WorldRuntime)
        .GetField("worldEventLog", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(world)!;

    private sealed class ReadWorld : IAgentWorld
    {
        public SocietyCheckpoint Society { get; set; } = SocietyRules.CreateGenesis("read-services",
            [SocietyRules.CreateFounder("agent-a", "Aster Ash"), SocietyRules.CreateFounder("agent-b", "Rowan Reed")]);
        public Dictionary<string, AgentState> People { get; } = new(StringComparer.Ordinal);
        public Dictionary<string, GridPoint> Dead { get; } = new(StringComparer.Ordinal);
        public IReadOnlyDictionary<string, AgentState> Agents => People;
        public long WorldTick => 10;
        public GridPoint SharedStoragePosition => new(9, 9);
        public int Warmth { get; set; } = -1;
        public GridPoint BuildingPosition(string id) => id == "store" ? new(6, 7) : throw new InvalidOperationException();
        public int FootDistance(GridPoint origin, GridPoint destination) => Math.Abs(origin.X - destination.X) + Math.Abs(origin.Y - destination.Y);
        public int WarmthChange(AgentState person) => Warmth;
        public GridPoint? LivingPosition(string id) => People.TryGetValue(id, out var person) ? person.Position : null;
        public GridPoint? DeceasedPosition(string id) => Dead.TryGetValue(id, out var point) ? point : null;
    }
}
