using ClankerWorld.Simulation;
using System.Text.Json;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.World;
using ClankerWorld.Simulation.Society;
using ClankerWorld.Server.Observation;

namespace ClankerWorld.Tests;

public sealed partial class PhysicalKnowledgePipelineTests
{
    [Theory]
    [InlineData("field_record", "Read a field record", 1, 0, true)]
    [InlineData("field_map", "Read a map", 1, 0, false)]
    [InlineData("book", "Read a book", 2, 1, true)]
    public async Task ReadOrderLearnsOnlyAnActualWrittenArtifactsFactsAndReplays(string kind, string command, int paper, int cloth, bool exact)
    {
        var (state, artifact, reader) = await WrittenReadOrderState(kind, paper, cloth);
        Func<string, IDecisionProvider> factory = _ => new ChoosingProvider(reader);
        using var world = WorldRuntime.Restore(state, factory);
        var receipt = world.SubmitInstruction(new("read-real-artifact", "owner:test", reader,
            OwnerInstructionKind.Order, command + (exact ? " " + artifact.Id : string.Empty)));
        var order = Assert.Single(world.ExportState().Instructions!).Task!;
        Assert.Equal("read_knowledge", order.Action);
        Assert.Equal(0, order.CompletedUnits);
        var initial = WorldRuntimeCodec.Encode(world.ExportState());
        using var replay = WorldRuntime.Restore(WorldRuntimeCodec.Decode(initial), factory);
        for (var tick = 0; tick < 8 && !world.ExportState().CompletedInstructionIds!.Contains(receipt.InstructionId); tick++)
        {
            Assert.True((await world.AdvanceOneTickAsync()).Advanced);
            Assert.True((await replay.AdvanceOneTickAsync()).Advanced);
            Assert.Equal(WorldRuntimeCodec.Encode(world.ExportState()), WorldRuntimeCodec.Encode(replay.ExportState()));
        }
        var final = world.ExportState();
        Assert.Equal(("finished", 1), (Assert.Single(final.Instructions!).Task!.Status, Assert.Single(final.Instructions!).Task!.CompletedUnits));
        var learned = final.Knowledge!.Facts.Where(fact => fact.OwnerId == reader).ToArray();
        Assert.Equal(artifact.Facts.Count, learned.Length);
        Assert.All(learned, fact =>
        {
            var written = Assert.Single(artifact.Facts, item => item.Position == fact.Position);
            Assert.Equal((written.DiscovererId, written.Terrain), (fact.DiscovererId, fact.Terrain));
            Assert.Equal(written.ResourceKinds, fact.ResourceKinds);
            Assert.Equal(("read", artifact.CreatorId, artifact.Id), (fact.Acquisition, fact.SourceAgentId, fact.SourceArtifactId));
        });
        Assert.Equal(JsonSerializer.Serialize(state.Knowledge!.Facts.Where(fact => fact.OwnerId != reader)),
            JsonSerializer.Serialize(final.Knowledge.Facts.Where(fact => fact.OwnerId != reader)));
        Assert.Equal(JsonSerializer.Serialize(state.Knowledge.Artifacts), JsonSerializer.Serialize(final.Knowledge.Artifacts));
        var held = final.Society.Society.Inventory.GetLot(artifact.LotId);
        // Inventory time may advance; reading changes none of the physical lot.
        Assert.Equal(state.Society.Society.Inventory.GetLot(artifact.LotId) with { LastProcessedTick = held.LastProcessedTick }, held);
        Assert.Equal(artifact.Id, Assert.Single(new OwnerWorldObservationStore(world).GetSnapshot().Instructions).Task!.TargetKnowledgeArtifactId);
        Assert.Single(final.Events, item => item.Kind == "agent_knowledge_artifact_read" && item.Detail.Contains(artifact.Id, StringComparison.Ordinal));
        using var reloaded = WorldRuntime.Restore(WorldRuntimeCodec.Decode(WorldRuntimeCodec.Encode(final)), factory);
        Assert.True((await reloaded.AdvanceOneTickAsync()).Advanced);
        Assert.Equal(1, Assert.Single(reloaded.ExportState().Instructions!).Task!.CompletedUnits);
        Assert.Single(reloaded.ExportState().Events, item => item.Kind == "agent_knowledge_artifact_read" && item.Detail.Contains(artifact.Id, StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("foreign")]
    [InlineData("reserved")]
    [InlineData("ground")]
    [InlineData("missing")]
    public async Task ReadOrderWaitsForItsExactItemWithoutPrivateOrRemoteLearning(string obstacle)
    {
        var (state, artifact, reader) = await WrittenReadOrderState();
        using var setup = WorldRuntime.Restore(state, _ => new ChoosingProvider(reader));
        setup.SubmitInstruction(new("blocked-read", "owner:test", reader, OwnerInstructionKind.Order, "Read " + artifact.Id));
        state = setup.ExportState();
        var inventory = state.Society.Society.Inventory;
        if (obstacle == "foreign") inventory = InventoryAuthority.Transfer(inventory, "withhold-read-item", reader, artifact.CreatorId, artifact.LotId, 1, "withhold it");
        else if (obstacle == "reserved") inventory = InventoryAuthority.Reserve(inventory, "read-item-held", reader, artifact.LotId, 1, "other-work", inventory.WorldTick + 200);
        else if (obstacle == "ground") inventory = inventory with
        {
            Lots = inventory.Lots.Select(lot => lot.Id == artifact.LotId ? lot with
            {
                GroundPosition = new(state.Agents.Single(person => person.AgentId == reader).Position.X,
                    state.Agents.Single(person => person.AgentId == reader).Position.Y)
            } : lot).ToArray()
        };
        var blocked = WithInventory(state, inventory);
        if (obstacle == "missing") blocked = blocked with { Knowledge = blocked.Knowledge! with { Artifacts = [], EarlierFacts = [] } };
        using var world = WorldRuntime.Restore(WorldRuntimeCodec.Decode(WorldRuntimeCodec.Encode(blocked)), _ => new ChoosingProvider(reader));
        for (var tick = 0; tick < 4; tick++) Assert.True((await world.AdvanceOneTickAsync()).Advanced);
        var order = Assert.Single(world.ExportState().Instructions!).Task!;
        Assert.Equal("blocked", order.Status);
        Assert.Contains(obstacle == "missing" ? "no longer available" : "personally owned, carried and available", order.BlockedReason);
        Assert.Equal(artifact.Id, order.TargetKnowledgeArtifactId);
        Assert.Equal(0, order.CompletedUnits);
        Assert.DoesNotContain(world.Knowledge.Facts, fact => fact.OwnerId == reader);
        Assert.DoesNotContain(world.ExportState().Events, item => item.Kind == "agent_knowledge_artifact_read");
    }

    [Fact]
    public async Task ReadOrderDoesNotCreditAnotherReadersLearningAndWaitsForOwnershipToReturn()
    {
        var (state, artifact, reader) = await WrittenReadOrderState();
        var other = state.Agents.First(person => person.AgentId != reader && person.AgentId != artifact.CreatorId).AgentId;
        using var setup = WorldRuntime.Restore(state, _ => new ChoosingProvider(reader));
        setup.SubmitInstruction(new("wait-for-my-item", "owner:test", reader, OwnerInstructionKind.Order, "Read " + artifact.Id));
        state = setup.ExportState();
        var loan = InventoryAuthority.Transfer(state.Society.Society.Inventory, "give-to-other-reader", reader, other, artifact.LotId, 1, "give to another reader");
        using var world = WorldRuntime.Restore(WithInventory(state, loan), _ => new ChoosingProvider(other, "knowledge_read:" + artifact.Id));
        await AdvanceUntil(world, () => world.Knowledge.Facts.Any(fact => fact.OwnerId == other), 8);
        Assert.Equal(0, Assert.Single(world.ExportState().Instructions!).Task!.CompletedUnits);
        Assert.DoesNotContain(world.Knowledge.Facts, fact => fact.OwnerId == reader);
        var afterOtherRead = world.ExportState();
        var returned = InventoryAuthority.Transfer(afterOtherRead.Society.Society.Inventory, "give-back-for-reading", other, reader, artifact.LotId, 1, "return the item");
        using var restored = WorldRuntime.Restore(WorldRuntimeCodec.Decode(WorldRuntimeCodec.Encode(WithInventory(afterOtherRead, returned))), _ => new ChoosingProvider(reader));
        await AdvanceUntil(restored, () => Assert.Single(restored.ExportState().Instructions!).Task!.Status == "finished", 8);
        Assert.Equal(artifact.Id, Assert.Single(restored.ExportState().Instructions!).Task!.TargetKnowledgeArtifactId);
        Assert.Equal(1, Assert.Single(restored.ExportState().Instructions!).Task!.CompletedUnits);
        Assert.Equal(JsonSerializer.Serialize(afterOtherRead.Knowledge!.Facts.Where(fact => fact.OwnerId == other)),
            JsonSerializer.Serialize(restored.Knowledge.Facts.Where(fact => fact.OwnerId == other)));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CancellingOrReplacingAReadOrderKeepsTheBookUnread(bool replace)
    {
        var (state, artifact, reader) = await WrittenReadOrderState("book", 2, 1);
        using var world = WorldRuntime.Restore(state, _ => new ChoosingProvider(reader));
        var read = world.SubmitInstruction(new("cancel-reading", "owner:test", reader, OwnerInstructionKind.Order, "Read a book"));
        if (replace) world.SubmitInstruction(new("instead-of-reading", "owner:test", reader, OwnerInstructionKind.Order, "Move to (1000,1000)"));
        else world.CancelOrder(new("cancel-read-receipt", "owner:test", world.Society.WorldId, reader, read.InstructionId));
        for (var tick = 0; tick < 3; tick++) Assert.True((await world.AdvanceOneTickAsync()).Advanced);
        var order = world.ExportState().Instructions!.Single(item => item.InstructionId == read.InstructionId).Task!;
        Assert.Equal(("cancelled", 0), (order.Status, order.CompletedUnits));
        Assert.DoesNotContain(world.Knowledge.Facts, fact => fact.OwnerId == reader);
        Assert.Equal(reader, world.Society.Inventory.GetLot(artifact.LotId).OwnerId);
        using var restored = WorldRuntime.Restore(WorldRuntimeCodec.Decode(WorldRuntimeCodec.Encode(world.ExportState())), _ => new ChoosingProvider(reader));
        Assert.DoesNotContain(restored.Knowledge.Facts, fact => fact.OwnerId == reader);
    }

    [Fact]
    public async Task QueuedReadingSurvivesATypoUrgentFoodAndSavingWithoutFreeKnowledge()
    {
        var (state, artifact, reader) = await WrittenReadOrderState();
        state = WithInventory(state, InventoryAuthority.AddLot(state.Society.Society.Inventory, "reading-food", "berries", reader, 3, state.Society.Society.WorldTick));
        using var world = WorldRuntime.Restore(state, _ => new ChoosingProvider(reader));
        var hold = world.SubmitInstruction(new("hold-reading", "owner:test", reader, OwnerInstructionKind.Order, "Move to (1000,1000)"));
        var read = world.SubmitInstruction(new("queued-reading", "owner:test", reader, OwnerInstructionKind.Order, "Read a map", Queue: true));
        world.SubmitInstruction(new("reading-typo", "owner:test", reader, OwnerInstructionKind.Order, "Read a mapp"));
        var queued = world.ExportState();
        Assert.Equal("queued", queued.Instructions!.Single(item => item.InstructionId == read.InstructionId).Task!.Status);
        queued = queued with
        {
            Agents = queued.Agents.Select(person => person.AgentId == reader
            ? person with { FullnessBasisPoints = 0 } : person).ToArray()
        };
        using var restored = WorldRuntime.Restore(WorldRuntimeCodec.Decode(WorldRuntimeCodec.Encode(queued)), _ => new ChoosingProvider(reader));
        restored.CancelOrder(new("release-reading", "owner:test", restored.Society.WorldId, reader, hold.InstructionId));
        Assert.True((await restored.AdvanceOneTickAsync()).Advanced);
        var interrupted = restored.ExportState();
        Assert.Equal("interrupted", interrupted.Instructions!.Single(item => item.InstructionId == read.InstructionId).Task!.Status);
        Assert.DoesNotContain(interrupted.Knowledge!.Facts, fact => fact.OwnerId == reader);
        Assert.True(interrupted.Society.Society.Inventory.GetLot("reading-food").Quantity < 3);
        using var eatingReload = WorldRuntime.Restore(WorldRuntimeCodec.Decode(WorldRuntimeCodec.Encode(interrupted)), _ => new ChoosingProvider(reader));
        await AdvanceUntil(eatingReload, () => eatingReload.ExportState().Instructions!.Single(item => item.InstructionId == read.InstructionId).Task!.Status == "finished", 12);
        Assert.Equal(artifact.Id, eatingReload.ExportState().Instructions!.Single(item => item.InstructionId == read.InstructionId).Task!.TargetKnowledgeArtifactId);
        Assert.Single(eatingReload.ExportState().Events, item => item.Kind == "agent_knowledge_artifact_read");
    }

    [Fact]
    public async Task ReadCompletionRejectsUnrelatedFactsChangedTargetsAndForgedProgressAndRollsBackAtomically()
    {
        var (state, artifact, reader) = await WrittenReadOrderState();
        using var world = WorldRuntime.Restore(state, _ => new ChoosingProvider(reader));
        world.SubmitInstruction(new("atomic-reading", "owner:test", reader, OwnerInstructionKind.Order, "Read " + artifact.Id));
        var before = WorldRuntimeCodec.Encode(world.ExportState());
        Assert.False((await world.AdvanceOneTickAsync(() => false)).Advanced);
        Assert.Equal(before, WorldRuntimeCodec.Encode(world.ExportState()));
        Assert.True((await world.AdvanceOneTickAsync()).Advanced);
        var completed = world.ExportState();
        var instruction = Assert.Single(completed.Instructions!);
        Assert.Equal(1, instruction.Task!.CompletedUnits);
        Assert.Throws<InvalidDataException>(() => WorldRuntimeCodec.Encode(completed with
        { Instructions = [instruction with { Task = instruction.Task with { TargetKnowledgeArtifactId = "another-artifact" } }] }));
        Assert.Throws<InvalidDataException>(() => WorldRuntimeCodec.Encode(completed with
        { Instructions = [instruction with { Task = instruction.Task with { LastEffectId = Identity.Parse("read-order:forged") } }] }));
        Assert.Throws<InvalidDataException>(() => WorldRuntimeCodec.Encode(completed with
        { Knowledge = completed.Knowledge! with { Facts = completed.Knowledge!.Facts.Where(fact => fact.OwnerId != reader).ToArray() } }));
        Assert.Throws<InvalidDataException>(() => WorldRuntimeCodec.Encode(completed with
        { Instructions = [instruction with { Task = instruction.Task with { KnowledgeReadCompletion = instruction.Task.KnowledgeReadCompletion! with { LearnedSites = [] } } }] }));
        Assert.Single(completed.Events, item => item.Kind == "agent_knowledge_artifact_read");
    }

    [Fact]
    public async Task ReadOrderHonorsKnowledgeCapacityWithoutInventingOrReplacingFacts()
    {
        // The small test map has only thirty tiles. Use a normal generated world
        // for the boundary, with a real explored and written source.
        using var generated = NormalPathWorld.CreateGenerated("read-order-knowledge-cap", _ => new ActionCoverageRecorder(chooseIdle: true));
        var initial = generated.ExportState();
        var author = initial.Agents[0].AgentId;
        var reader = initial.Agents[1].AgentId;
        initial = initial with
        {
            Agents = initial.Agents.Select(person => person with
            { FullnessBasisPoints = 9_500 }).ToArray()
        };
        var scout = new ChoosingProvider(author, "explore");
        using var exploring = WorldRuntime.Restore(initial, _ => scout);
        await AdvanceUntil(exploring, () => exploring.Knowledge.Facts.Count(fact => fact.OwnerId == author) >= 2, 20);
        // This boundary needs two actual discoveries, not a long outing. Ask for
        // the native return before writing so current scouting does not consume
        // the writing budget or add unrelated facts to the capacity fixture.
        scout.Prefixes = ["explore_return"];
        exploring.SubmitInstruction(new("return-before-capacity-map", "owner:test", author,
            OwnerInstructionKind.Suggestion, "Return from scouting before writing the map."));
        await AdvanceUntil(exploring, () => exploring.ExportState().Agents.Single(person =>
            person.AgentId == author).Exploration is { OutingPath.Count: 0 }, 12);
        Assert.Contains(exploring.ExportState().Events, item => item.Kind == "exploration_completed");
        var explored = exploring.ExportState();
        using var writing = WorldRuntime.Restore(WithInventory(explored, AddWritingSupplies(explored, author, 1, 0)), _ =>
            new ChoosingProvider(author, "knowledge_write:field_map", "knowledge_continue"));
        await AdvanceUntil(writing, () => writing.Knowledge.Artifacts.Count == 1, 32);
        var written = writing.ExportState();
        var artifact = Assert.Single(written.Knowledge!.Artifacts);
        var state = WithInventory(written, InventoryAuthority.Transfer(written.Society.Society.Inventory,
            "give-capacity-map", author, reader, artifact.LotId, 1, "give the actual map"));
        foreach (var full in new[] { false, true })
        {
            // A controlled existing ledger isolates its saved capacity boundary.
            var existing = state.Map.Tiles.Where(tile => state.Map.IsPassable(tile.Position) && artifact.Facts.All(fact => fact.Position != tile.Position))
                .Take(full ? 128 : 127).Select(tile => new AgentKnowledgeFact(
                    DerivedId.Create($"knowledge-fact:{reader}:{tile.Position.X}:{tile.Position.Y}"), reader, reader, tile.Position,
                    tile.Terrain.ToString(), [], state.Society.Society.WorldTick, "firsthand")).ToArray();
            Assert.Equal(full ? 128 : 127, existing.Length);
            using var world = WorldRuntime.Restore(state with
            { Knowledge = state.Knowledge! with { Facts = state.Knowledge!.Facts.Concat(existing).ToArray() } }, _ => new ChoosingProvider(reader));
            world.SubmitInstruction(new("capacity-read", "owner:test", reader, OwnerInstructionKind.Order, "Read " + artifact.Id));
            Assert.True((await world.AdvanceOneTickAsync()).Advanced);
            var order = Assert.Single(world.ExportState().Instructions!, item => item.IdempotencyKey == "capacity-read").Task!;
            Assert.Equal(128, world.Knowledge.Facts.Count(fact => fact.OwnerId == reader));
            Assert.Equal(JsonSerializer.Serialize(existing), JsonSerializer.Serialize(world.Knowledge.Facts.Where(fact => fact.OwnerId == reader && fact.Acquisition == "firsthand")));
            if (full)
            {
                Assert.Equal(("blocked", 0), (order.Status, order.CompletedUnits));
                Assert.Contains("ledger is full", order.BlockedReason);
                Assert.DoesNotContain(world.ExportState().Events, item => item.Kind == "agent_knowledge_artifact_read");
            }
            else
            {
                Assert.Equal(("finished", 1), (order.Status, order.CompletedUnits));
                Assert.Single(order.KnowledgeReadCompletion!.LearnedSites);
                var learned = Assert.Single(world.Knowledge.Facts, fact => fact.OwnerId == reader && fact.Acquisition == "read");
                Assert.Contains(artifact.Facts, fact => fact.Position == learned.Position);
                Assert.Equal(artifact.Id, learned.SourceArtifactId);
            }
        }
    }

    [Fact]
    public async Task ReadReceiptRequiresItsOwnReadButSurvivesALaterFirsthandAccount()
    {
        var (state, artifact, reader) = await WrittenReadOrderState();
        using var world = WorldRuntime.Restore(state, _ => new ChoosingProvider(reader));
        world.SubmitInstruction(new("historical-read", "owner:test", reader, OwnerInstructionKind.Order, "Read " + artifact.Id));
        Assert.True((await world.AdvanceOneTickAsync()).Advanced);
        var read = world.ExportState();
        var firsthand = read.Knowledge!.Facts.Select(fact => fact.OwnerId == reader ? fact with
        { DiscovererId = reader, Acquisition = "firsthand", SourceAgentId = null, SourceArtifactId = null } : fact).ToArray();
        Assert.Throws<InvalidDataException>(() => WorldRuntimeCodec.Encode(read with { Knowledge = read.Knowledge with { Facts = firsthand } }));
        Assert.True((await world.AdvanceOneTickAsync()).Advanced);
        var later = world.ExportState();
        // A later account of the same sites must not erase the completed task.
        firsthand = firsthand.Select(fact => fact.OwnerId == reader ? fact with { LearnedTick = later.Society.Society.WorldTick } : fact).ToArray();
        using var restored = WorldRuntime.Restore(WorldRuntimeCodec.Decode(WorldRuntimeCodec.Encode(
            later with { Knowledge = later.Knowledge! with { Facts = firsthand } })), _ => new ChoosingProvider(reader));
        Assert.Equal(JsonSerializer.Serialize(Assert.Single(read.Instructions!).Task), JsonSerializer.Serialize(Assert.Single(restored.ExportState().Instructions!).Task));
    }

    [Fact]
    public async Task NamedReadingDoesNotSwitchToAnEligibleBookWhileTheSelectedMapIsElsewhere()
    {
        var (state, map, reader) = await WrittenReadOrderState();
        var materials = InventoryAuthority.AddLot(state.Society.Society.Inventory, "other-book-paper", "paper", map.CreatorId, 2, state.Society.Society.WorldTick);
        materials = InventoryAuthority.AddLot(materials, "other-book-cover", "cloth", map.CreatorId, 1, state.Society.Society.WorldTick);
        using var writing = WorldRuntime.Restore(WithInventory(state, materials), _ =>
            new ChoosingProvider(map.CreatorId, "knowledge_write:book", "knowledge_continue"));
        await AdvanceUntil(writing, () => writing.Knowledge.Artifacts.Count == 2, 32);
        var written = writing.ExportState();
        var book = Assert.Single(written.Knowledge!.Artifacts, item => item.Kind == "book");
        var inventory = InventoryAuthority.Transfer(written.Society.Society.Inventory, "hold-other-book", book.CreatorId, reader, book.LotId, 1, "give the alternative book");
        inventory = InventoryAuthority.Transfer(inventory, "withhold-named-map", reader, map.CreatorId, map.LotId, 1, "withhold the selected map");
        using var world = WorldRuntime.Restore(WithInventory(written, inventory), _ => new ChoosingProvider(reader));
        world.SubmitInstruction(new("exact-map-title", "owner:test", reader, OwnerInstructionKind.Order, "Read " + map.Title));
        for (var tick = 0; tick < 3; tick++) Assert.True((await world.AdvanceOneTickAsync()).Advanced);
        Assert.Equal(map.Id, Assert.Single(world.ExportState().Instructions!).Task!.TargetKnowledgeArtifactId);
        Assert.Equal(("blocked", 0), (Assert.Single(world.ExportState().Instructions!).Task!.Status, Assert.Single(world.ExportState().Instructions!).Task!.CompletedUnits));
        Assert.DoesNotContain(world.Knowledge.Facts, fact => fact.OwnerId == reader);
        var waiting = world.ExportState();
        inventory = InventoryAuthority.Transfer(waiting.Society.Society.Inventory, "return-selected-map", map.CreatorId, reader, map.LotId, 1, "return the selected map");
        using var restored = WorldRuntime.Restore(WorldRuntimeCodec.Decode(WorldRuntimeCodec.Encode(WithInventory(waiting, inventory))), _ => new ChoosingProvider(reader));
        await AdvanceUntil(restored, () => Assert.Single(restored.ExportState().Instructions!).Task!.Status == "finished", 8);
        Assert.Equal(map.Id, Assert.Single(restored.ExportState().Instructions!).Task!.TargetKnowledgeArtifactId);
        Assert.All(restored.Knowledge.Facts.Where(fact => fact.OwnerId == reader), fact => Assert.Equal(map.Id, fact.SourceArtifactId));
    }

    private static async Task<(WorldRuntimeState State, AgentKnowledgeArtifact Artifact, string Reader)> WrittenReadOrderState(
        string kind = "field_map", int paper = 1, int cloth = 0)
    {
        var (state, author) = await LearnByExploring("read-order-physical-" + kind);
        using var writing = WorldRuntime.Restore(WithInventory(state, AddWritingSupplies(state, author, paper, cloth)), _ =>
            new ChoosingProvider(author, "knowledge_write:" + kind, "knowledge_continue"));
        await AdvanceUntil(writing, () => writing.Knowledge.Artifacts.Count == 1, 64);
        var written = writing.ExportState();
        var artifact = Assert.Single(written.Knowledge!.Artifacts);
        var reader = written.Agents.First(person => person.AgentId != author).AgentId;
        var transferred = InventoryAuthority.Transfer(written.Society.Society.Inventory, "give-read-order-artifact",
            author, reader, artifact.LotId, 1, "give the actual written artifact");
        return (WithInventory(written, transferred), artifact, reader);
    }
}
