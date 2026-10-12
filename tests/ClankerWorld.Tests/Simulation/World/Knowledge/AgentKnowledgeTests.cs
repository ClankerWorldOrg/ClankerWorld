using System.Collections.Concurrent;
using ClankerWorld.Simulation;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.World;
using ClankerWorld.Simulation.Society;
using ClankerWorld.Server.Observation;

namespace ClankerWorld.Tests;

public sealed class AgentKnowledgeTests
{
    [Theory]
    [InlineData(8, false)]
    [InlineData(7, true)]
    public async Task ExplorationKeepsLearnedFactsWhenThereIsNoPaperForAFieldRecord(int cargo, bool recordFits)
    {
        using var seed = new WorldRuntime("personal-map-records");
        var initial = seed.ExportState();
        var actor = initial.Agents[0].AgentId;
        var inventory = initial.Society.Society.Inventory with
        {
            Lots = initial.Society.Society.Inventory.Lots.Where(lot => lot.OwnerId != actor).ToArray(),
        };
        inventory = InventoryAuthority.AddLot(inventory, "scout-cargo", "wood", actor, cargo);
        if (recordFits)
            inventory = InventoryAuthority.AddLot(inventory, "writing-paper", "paper", actor, 1);
        var provider = new CandidateProvider(actor, "explore");
        using var world = WorldRuntime.Restore(initial with
        {
            Agents = initial.Agents.Select(person => person with { FullnessBasisPoints = 9_500 }).ToArray(),
            Society = initial.Society with { Society = initial.Society.Society with { Inventory = inventory } },
        }, _ => provider);
        for (var tick = 0; tick < 75; tick++)
            _ = await world.AdvanceOneTickAsync();
        await WriteFieldRecord(world, provider, actor);

        var saved = world.ExportState();
        Assert.Contains(saved.Events, item => item.Kind == "exploration_completed");
        Assert.Contains(saved.Knowledge!.Facts, fact => fact.OwnerId == actor);
        Assert.Equal(recordFits, saved.Knowledge.Artifacts.Any(artifact => artifact.CreatorId == actor));
        Assert.Equal(cargo, saved.Society.Society.Inventory.GetLot("scout-cargo").Quantity);
        Assert.Equal(8, saved.Society.Society.Inventory.Lots.Where(lot => lot.OwnerId == actor).Sum(lot => lot.Quantity));
        Assert.DoesNotContain(saved.Society.Society.Inventory.Lots, lot => lot.ItemKind == "paper");
        using var restored = WorldRuntime.Restore(
            WorldRuntimeCodec.Decode(WorldRuntimeCodec.Encode(saved)));
        Assert.Equal(saved.Knowledge.Facts.Select(FactKey), restored.ExportState().Knowledge!.Facts.Select(FactKey));
        Assert.Equal(saved.Knowledge.Artifacts.Select(ArtifactKey), restored.ExportState().Knowledge!.Artifacts.Select(ArtifactKey));
        restored.Validate();
    }

    [Theory]
    [InlineData(100, 1)]
    [InlineData(50, 2)]
    public async Task DescendantsKeepExploringAndSavingWithLongInheritedIdentities(int seedLength, int generations)
    {
        using var seed = new WorldRuntime(new string('s', seedLength));
        var state = seed.ExportState();
        var parent = "founder-scout";
        for (var generation = 0; generation < generations; generation++)
        {
            var partner = generation == 0 ? "founder-mira" : "founder-rowan";
            var society = ChosenBirthNameTestFixture.NameParent(state.Society.Society, parent);
            var relationshipId = $"descendant-partnership-{generation}";
            society = SocietyRules.ProposeRelationship(society, new(relationshipId, 1,
                SocietyRelationshipType.Partnership, parent, partner, society.WorldTick)).Checkpoint;
            society = SocietyRules.AcceptRelationship(society, relationshipId, 1, partner).Checkpoint;
            var caregiverHousehold = society.GetAgent(parent).HouseholdId!;
            var caregivers = new[] { parent, partner }
                .Where(id => society.GetAgent(id).HouseholdId == caregiverHousehold)
                .Append(parent).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
            var birth = SocietyRules.CommitBirth(society, new($"family:{parent}:{society.WorldTick}", 1,
                parent, partner, caregiverHousehold, caregivers, [parent, partner],
                "food:camp-alpha", 2, society.WorldTick, ChildName: ChosenBirthNameTestFixture.ChildName(society, parent, $"Explorer{generation}"),
                PrimaryCaregiverId: parent));
            parent = Assert.IsType<string>(birth.CreatedId);
            society = birth.Checkpoint;
            // Accelerate age only, retaining the actual birth identity and family records.
            var adultBirth = society.LifeTickAt(society.WorldTick) - 20 * society.Config.TicksPerLifecycleAge;
            society = society with
            {
                Agents = society.Agents.Select(person => person.Id == parent ? person with
                {
                    BirthTick = adultBirth,
                    BirthLifeTick = society.LifeClock is null ? null : adultBirth,
                    AgeBand = SocietyAgeBand.Adult,
                    LastLifecycleYearChecked = 20,
                } : person).ToArray(),
            };
            var position = state.Map.Tiles.Select(tile => tile.Position).First(point =>
                state.Map.IsPassable(point) && state.Agents.All(person => person.Position != point) &&
                state.Map.FootNeighbors(point).Any(state.Map.IsPassable));
            state = state with
            {
                Society = state.Society with { Society = society },
                Agents = state.Agents.Append(new AgentState(parent, position,
                    9_500, 0, "curious", "explore")).ToArray(),
            };
        }
        Assert.InRange(parent.Length, 1, DerivedId.MaximumLength);
        state = WithWritingPaper(state, parent);
        var provider = new CandidateProvider(parent, "explore");
        using var world = WorldRuntime.Restore(
            WorldRuntimeCodec.Decode(WorldRuntimeCodec.Encode(state)), _ => provider);
        for (var tick = 0; tick < 80; tick++)
        {
            Assert.True((await world.AdvanceOneTickAsync()).Advanced);
            world.Validate();
            _ = WorldRuntimeCodec.Encode(world.ExportState());
        }
        await WriteFieldRecord(world, provider, parent);
        var saved = world.ExportState();
        Assert.Contains(saved.Knowledge!.Facts, fact => fact.OwnerId == parent);
        Assert.All(saved.Knowledge.Facts.Where(fact => fact.OwnerId == parent),
            fact => Assert.Equal(parent, fact.DiscovererId));
        Assert.Contains(saved.Knowledge.Artifacts, artifact => artifact.CreatorId == parent);
        Assert.NotEmpty(provider.KnownMapFactsByAgent[parent]);
        var discovererReference = Assert.Single(provider.KnownMapFactsByAgent[parent]
            .Select(fact => fact.DiscovererId).Distinct());
        Assert.InRange(discovererReference.Length, 1, 128);
        using var reloaded = WorldRuntime.Restore(
            WorldRuntimeCodec.Decode(WorldRuntimeCodec.Encode(saved)), _ => provider);
        for (var tick = 0; tick < 25; tick++)
        {
            Assert.True((await reloaded.AdvanceOneTickAsync()).Advanced);
            Assert.True((await world.AdvanceOneTickAsync()).Advanced);
        }
        reloaded.Validate();
        Assert.Equal(WorldRuntimeCodec.Encode(world.ExportState()),
            WorldRuntimeCodec.Encode(reloaded.ExportState()));
        Assert.All(provider.KnownMapFactsByAgent[parent],
            fact => Assert.Equal(discovererReference, fact.DiscovererId));
        Assert.Equal(saved.Knowledge.Facts.Select(fact => fact.Id),
            reloaded.ExportState().Knowledge!.Facts.Select(fact => fact.Id));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task InheritedNaturalFieldRecordKeepsItsPhysicalIdentityAndPrivateProvenance(bool selectedHeir)
    {
        using var seed = new WorldRuntime("inherited-natural-record");
        var initial = seed.ExportState();
        var creator = initial.Agents[0].AgentId;
        initial = WithWritingPaper(initial, creator);
        var provider = new CandidateProvider(creator, "explore");
        using var scout = WorldRuntime.Restore(initial with
        {
            Agents = initial.Agents.Select(person => person with { FullnessBasisPoints = 9_500 }).ToArray(),
        }, _ => provider);
        for (var tick = 0; tick < 75 && scout.ExportState().Knowledge!.Artifacts.Count == 0; tick++)
            _ = await scout.AdvanceOneTickAsync();
        await WriteFieldRecord(scout, provider, creator);
        var state = scout.ExportState();
        var artifact = Assert.Single(state.Knowledge!.Artifacts);
        var physical = state.Agents.Single(person => person.AgentId == creator);
        var dead = SocietyRules.Kill(state.Society.Society, creator, SocietyDeathCause.Accident).Checkpoint;
        var estate = Assert.Single(dead.Estates);
        // A raw society death does not know the physical tile. Preserve the
        // natural artifact there, as the runtime's death transition does.
        var inventory = dead.Inventory;
        if (inventory.Lots.Any(lot => lot.CarrierId == creator))
            inventory = InventoryAuthority.DropCarrierGoods(inventory, creator,
                new InventoryGroundPosition(physical.Position.X, physical.Position.Y));
        foreach (var lot in inventory.Lots.Where(lot => lot.OwnerId == estate.Id && lot.ContainerLotId is null &&
                     lot.CarrierId is null && lot.StorageBuildingId is null && lot.GroundPosition is null).ToArray())
            inventory = InventoryAuthority.Relocate(inventory, $"death:{creator}:{lot.Id}", lot.Id, estate.Id, lot.Quantity,
                groundPosition: new InventoryGroundPosition(physical.Position.X, physical.Position.Y));
        dead = dead with { Inventory = inventory };
        // Bounded test clock: accelerate only this valid fixture's escrow deadline.
        dead = dead with { Estates = [estate with { ExpiryTick = state.Society.Society.WorldTick + 1 }] };
        var heir = dead.Agents.Where(person => person.Status == SocietyAgentStatus.Active).Last().Id;
        if (selectedHeir)
        {
            dead = SocietyRules.MarkWillStarted(dead, estate.Id).Checkpoint;
            dead = SocietyRules.ResolveWill(dead, estate.Id, new SocietyWillDirective([heir], "equal"), "accepted").Checkpoint;
        }
        state = state with
        {
            Society = state.Society with { Society = dead },
            Agents = state.Agents.Where(person => person.AgentId != creator).ToArray(),
            DeceasedAgents = [new DeceasedAgentState(creator, state.Society.Society.WorldTick,
                dead.AgeAt(dead.GetAgent(creator), state.Society.Society.WorldTick), physical)],
        };
        using var world = WorldRuntime.Restore(WorldRuntimeCodec.Decode(WorldRuntimeCodec.Encode(state)),
            _ => new CandidateProvider("nobody", "never"));
        _ = await world.AdvanceOneTickAsync();
        var settled = world.ExportState();
        Assert.True(settled.Society.Society.GetEstate(estate.Id).Settled);
        var inherited = settled.Society.Society.Inventory.GetLot(artifact.LotId);
        Assert.Equal(1, inherited.Quantity);
        Assert.Equal(selectedHeir ? heir : estate.BeneficiaryIds.Order(StringComparer.Ordinal).First(), inherited.OwnerId);
        Assert.Equal(((string?)null, (string?)null,
                (InventoryGroundPosition?)new InventoryGroundPosition(physical.Position.X, physical.Position.Y)),
            (inherited.CarrierId, inherited.StorageBuildingId, inherited.GroundPosition));
        Assert.Equal(ArtifactKey(artifact), ArtifactKey(Assert.Single(settled.Knowledge!.Artifacts)));
        Assert.DoesNotContain(settled.Knowledge.Facts, fact => fact.OwnerId != creator);
        var directory = Directory.CreateTempSubdirectory("inherited-record-save-");
        try
        {
            var file = new WorldStateFile(Path.Combine(directory.FullName, "world.json"));
            file.Save(world);
            using var restored = file.LoadOrCreate(state.WorldSeed);
            var snapshot = new OwnerWorldObservationStore(restored).GetSnapshot();
            Assert.Single(snapshot.Agents.Single(person => person.Id == inherited.OwnerId).KnowledgeArtifacts);
            Assert.Equal(artifact.CreatorId, restored.ExportState().Knowledge!.Artifacts.Single().CreatorId);
        }
        finally { directory.Delete(recursive: true); }
    }

    [Fact]
    public async Task ExplorationCreatesBoundedPersonalKnowledgeAndMigratesThroughSaveReload()
    {
        using var seed = new WorldRuntime("personal-map-records");
        var initial = seed.ExportState();
        var explorerId = initial.Agents[0].AgentId;
        initial = WithWritingPaper(initial, explorerId);
        var provider = new CandidateProvider(explorerId, "explore");
        using var world = WorldRuntime.Restore(initial with
        {
            Agents = initial.Agents.Select(person => person with { FullnessBasisPoints = 9_500 }).ToArray(),
        }, _ => provider);

        for (var tick = 0; tick < 75; tick++)
            _ = await world.AdvanceOneTickAsync();
        await WriteFieldRecord(world, provider, explorerId);

        var saved = world.ExportState();
        var facts = saved.Knowledge!.Facts.Where(fact => fact.OwnerId == explorerId).ToArray();
        var artifacts = saved.Knowledge.Artifacts.Where(artifact => artifact.CreatorId == explorerId).ToArray();
        Assert.NotEmpty(facts);
        Assert.NotEmpty(artifacts);
        Assert.All(facts, fact => Assert.Equal(explorerId, fact.DiscovererId));
        Assert.All(artifacts, artifact => Assert.InRange(artifact.Facts.Count, 1, 9));
        Assert.DoesNotContain(saved.Knowledge.Facts, fact => fact.OwnerId != explorerId);
        Assert.NotEmpty(provider.KnownMapFactsByAgent[explorerId]);
        Assert.All(saved.Agents.Where(person => person.AgentId != explorerId), person =>
            Assert.Empty(provider.KnownMapFactsByAgent.GetValueOrDefault(person.AgentId) ?? []));
        Assert.All(artifacts, artifact => Assert.Equal(artifact.Kind,
            saved.Society.Society.Inventory.GetLot(artifact.LotId).ItemKind));

        var bytes = WorldRuntimeCodec.Encode(saved);
        using var restored = WorldRuntime.Restore(WorldRuntimeCodec.Decode(bytes));
        var restoredKnowledge = restored.ExportState().Knowledge!;
        Assert.Equal(facts.Select(FactKey), restoredKnowledge.Facts.Where(fact => fact.OwnerId == explorerId).Select(FactKey));
        Assert.Equal(artifacts.Select(ArtifactKey), restoredKnowledge.Artifacts.Where(artifact => artifact.CreatorId == explorerId).Select(ArtifactKey));
    }

    [Fact]
    public async Task SharingTeachesOnlyTheRecipientAndProjectsSafeTelemetryAndAgentRecords()
    {
        using var seed = new WorldRuntime("shared-field-record");
        var state = seed.ExportState();
        var sourceId = state.Agents[0].AgentId;
        var recipientId = state.Agents[1].AgentId;
        var otherId = state.Agents[2].AgentId;
        state = WithArtifact(state, sourceId, state.Agents[0].Position);
        var sourcePosition = state.Agents.Single(person => person.AgentId == sourceId).Position;
        var recipientPosition = state.Map.FootNeighbors(sourcePosition)
            .Where(state.Map.IsPassable)
            .First(point => !state.Agents.Any(person => person.AgentId != recipientId && person.Position == point));
        state = state with
        {
            Agents = state.Agents.Select(person => person.AgentId == recipientId
                ? person with { Position = recipientPosition } : person).ToArray(),
        };
        using var world = WorldRuntime.Restore(state, id => new CandidateProvider(
            sourceId, $"knowledge_share:knowledge-artifact-000001|{recipientId}", id == sourceId));
        var directory = Directory.CreateTempSubdirectory("clankerworld-knowledge-log-");
        try
        {
            var presence = new OwnerClientPresenceLease(TimeSpan.FromSeconds(30));
            presence.RecordAuthenticatedReconnect("test-owner");
            var logger = new RecordingLogger<WorldRuntimeService>();
            using var service = new WorldRuntimeService(world,
                new WorldStateFile(Path.Combine(directory.FullName, "world.json")), presence, logger);
            Assert.True(await service.TryAdvanceOnceAsync());

            var learned = world.ExportState().Knowledge!.Facts.Single(fact => fact.OwnerId == recipientId);
            Assert.Equal(sourceId, learned.DiscovererId);
            Assert.Equal(sourceId, learned.SourceAgentId);
            Assert.Equal("shared", learned.Acquisition);
            Assert.DoesNotContain(world.ExportState().Knowledge!.Facts, fact => fact.OwnerId == otherId);
            Assert.Contains(world.ExportState().Events, item => item.Kind == "agent_knowledge_shared");

            var snapshot = new OwnerWorldObservationStore(world).GetSnapshot();
            var source = snapshot.Agents.Single(item => item.Id == sourceId);
            var recipient = snapshot.Agents.Single(item => item.Id == recipientId);
            Assert.Single(source.KnowledgeArtifacts);
            Assert.Contains(recipient.RecentKnowledgeFacts, fact => fact.Acquisition == "shared" && fact.SourceAgentName == source.DisplayName);
            Assert.Empty(recipient.KnowledgeArtifacts);

            Assert.Contains(logger.Messages, message => message.Contains("agent_knowledge", StringComparison.Ordinal) &&
                message.Contains("agent_knowledge_shared", StringComparison.Ordinal) &&
                message.Contains("recipient=" + recipientId, StringComparison.Ordinal));
            Assert.DoesNotContain(logger.Messages, message => message.Contains("private-secret-resource", StringComparison.Ordinal));
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [Theory]
    [InlineData("field_map")]
    [InlineData("book")]
    public async Task TradingAPhysicalArtifactTransfersItsKnowledgeOnlyToTheBuyer(string kind)
    {
        using var seed = new WorldRuntime("traded-field-map");
        var state = seed.ExportState();
        var sellerId = state.Agents[0].AgentId;
        var buyerId = state.Agents[1].AgentId;
        var otherId = state.Agents[2].AgentId;
        state = WithArtifact(state, sellerId, state.Agents[0].Position, kind);
        var artifact = Assert.Single(state.Knowledge!.Artifacts);
        state = DirectBarterTests.AtTradeMeeting(state, sellerId, buyerId);
        var inventory = InventoryAuthority.AddLot(state.Society.Society.Inventory,
            "trade-clothing-for-map", "clothing", buyerId, 2);
        state = state with
        {
            Agents = state.Agents.Select(person => person with { FullnessBasisPoints = 9_500 }).ToArray(),
            Society = state.Society with
            {
                Society = state.Society.Society with { Inventory = inventory },
            },
        };
        using var world = WorldRuntime.Restore(state, id => id == sellerId
            ? new CandidateProvider(sellerId, "trade_propose:")
            : id == buyerId ? new CandidateProvider(buyerId, "trade_accept:")
            : new CandidateProvider("nobody", "never"));

        for (var tick = 0; tick < 120; tick++)
        {
            var currentOffers = world.ExportState().Society.Society.Inventory.Offers;
            if (currentOffers.Count > 0 && currentOffers.All(offer => offer.State != DirectBarterState.Open))
                break;
            _ = await world.AdvanceOneTickAsync();
        }

        var final = world.ExportState();
        var physical = Assert.Single(final.Society.Society.Inventory.Lots, lot => lot.ItemKind == kind);
        Assert.Equal((artifact.LotId, buyerId, 1), (physical.Id, physical.OwnerId, physical.Quantity));
        Assert.Equal(ArtifactKey(artifact), ArtifactKey(Assert.Single(final.Knowledge!.Artifacts)));
        Assert.Contains(final.Society.Society.Inventory.Lots,
            lot => lot.ItemKind == "clothing" && lot.OwnerId == sellerId);
        var learned = Assert.Single(final.Knowledge!.Facts, fact => fact.OwnerId == buyerId);
        Assert.Equal(sellerId, learned.DiscovererId);
        Assert.Equal(sellerId, learned.SourceAgentId);
        Assert.Equal("read", learned.Acquisition);
        Assert.DoesNotContain(final.Knowledge.Facts, fact => fact.OwnerId == otherId);
        Assert.Contains(final.Events, item => item.Kind == "agent_knowledge_artifact_read");
    }

    [Fact]
    public void RestoreRejectsMoreKnowledgeArtifactsThanThePerAgentLimit()
    {
        using var seed = new WorldRuntime("knowledge-artifact-bound");
        var state = seed.ExportState();
        var creatorId = state.Agents[0].AgentId;
        var position = state.Agents[0].Position;
        var terrain = state.Map.Tiles.Single(tile => tile.Position == position).Terrain.ToString();
        var fact = new AgentKnowledgeFact(
            $"knowledge-fact:{creatorId}:{position.X}:{position.Y}", creatorId, creatorId,
            position, terrain, [], state.Society.Society.WorldTick, "firsthand");
        var artifacts = Enumerable.Range(1, 9) // The world allows eight artifacts per creator.
            .Select(index => new AgentKnowledgeArtifact(
                $"knowledge-artifact-{index:D6}", creatorId, $"bounded-lot-{index:D6}",
                "field_record", "Field record · 1 site", state.Society.Society.WorldTick, [fact]))
            .ToArray();
        var inventory = state.Society.Society.Inventory;
        for (var index = 0; index < artifacts.Length; index++)
        {
            var artifact = artifacts[index];
            var projectId = $"bounded-writing-{index}";
            var paperId = $"bounded-paper-{index}";
            var reservationId = projectId + ":paper";
            inventory = InventoryAuthority.AddLot(inventory, paperId, "paper", creatorId, 1,
                state.Society.Society.WorldTick);
            inventory = InventoryAuthority.Reserve(inventory, reservationId, creatorId, paperId, 1,
                $"knowledge_writing:{projectId}:paper", long.MaxValue);
            inventory = InventoryAuthority.ConsumeReservation(inventory, reservationId);
            inventory = InventoryAuthority.AddLot(inventory, artifact.LotId, artifact.Kind, creatorId, 1,
                state.Society.Society.WorldTick);
            artifacts[index] = artifact with
            {
                WritingProjectId = projectId,
                Materials = [new(reservationId, paperId, "paper", 1)],
            };
        }
        var malformed = state with
        {
            Knowledge = new WorldKnowledgeState([fact], artifacts),
            Society = state.Society with
            {
                Society = state.Society.Society with { Inventory = inventory },
            },
        };

        Assert.Throws<InvalidDataException>(() => WorldRuntime.Restore(malformed));
        using var atLimit = WorldRuntime.Restore(malformed with
        {
            Knowledge = malformed.Knowledge! with { Artifacts = artifacts.Take(8).ToArray() },
            Society = malformed.Society with
            {
                Society = malformed.Society.Society with
                {
                    Inventory = inventory with { Lots = inventory.Lots.Where(lot => lot.Id != artifacts[8].LotId).ToArray() },
                },
            },
        });
        atLimit.Validate();
    }

    private static WorldRuntimeState WithWritingPaper(WorldRuntimeState state, string actor) =>
        state with
        {
            Society = state.Society with
            {
                Society = state.Society.Society with
                {
                    Inventory = InventoryAuthority.AddLot(state.Society.Society.Inventory,
                        "writing-paper", "paper", actor, 1),
                },
            },
        };

    private static async Task WriteFieldRecord(WorldRuntime world, CandidateProvider provider, string actor)
    {
        provider.PreferredPrefix = "knowledge_write:field_record";
        // A real owner message requests a fresh planning turn after this fixture's
        // completed exploration phase; changing a test provider alone does not.
        _ = world.SubmitInstruction(new OwnerInstructionRequest("write-discoveries", "owner:test", actor,
            OwnerInstructionKind.Suggestion, "Write a field record of what you discovered."));
        for (var tick = 0; tick < 64 && !world.ExportState().Knowledge!.Artifacts.Any(item => item.CreatorId == actor); tick++)
            Assert.True((await world.AdvanceOneTickAsync()).Advanced);
    }

    private static WorldRuntimeState WithArtifact(
        WorldRuntimeState state,
        string creatorId,
        GridPoint position,
        string kind = "field_record")
    {
        var sequence = (state.Knowledge?.Artifacts.Count ?? 0) + 1;
        var terrain = state.Map.Tiles.Single(tile => tile.Position == position).Terrain.ToString();
        var fact = new AgentKnowledgeFact(
            $"knowledge-fact:{creatorId}:{position.X}:{position.Y}", creatorId, creatorId,
            position, terrain, ["private-secret-resource"], state.Society.Society.WorldTick, "firsthand");
        var artifactId = $"knowledge-artifact-{sequence:D6}";
        var lotId = $"knowledge-lot-{sequence:D6}";
        var projectId = $"writing-fixture-{sequence:D6}";
        var paperId = $"fixture-paper-{sequence:D6}";
        var reservationId = projectId + ":paper";
        var paperQuantity = kind == "book" ? 2 : 1;
        var materials = new List<AgentKnowledgeMaterial> { new(reservationId, paperId, "paper", paperQuantity) };
        var inventory = InventoryAuthority.AddLot(state.Society.Society.Inventory, paperId, "paper", creatorId, paperQuantity,
            state.Society.Society.WorldTick);
        inventory = InventoryAuthority.Reserve(inventory, reservationId, creatorId, paperId, paperQuantity,
            $"knowledge_writing:{projectId}:paper", long.MaxValue);
        inventory = InventoryAuthority.ConsumeReservation(inventory, reservationId);
        if (kind == "book")
        {
            var clothId = $"fixture-cloth-{sequence:D6}";
            var clothReservationId = projectId + ":cloth";
            inventory = InventoryAuthority.AddLot(inventory, clothId, "cloth", creatorId, 1, state.Society.Society.WorldTick);
            inventory = InventoryAuthority.Reserve(inventory, clothReservationId, creatorId, clothId, 1,
                $"knowledge_writing:{projectId}:cloth", long.MaxValue);
            inventory = InventoryAuthority.ConsumeReservation(inventory, clothReservationId);
            materials.Add(new(clothReservationId, clothId, "cloth", 1));
        }
        var artifact = new AgentKnowledgeArtifact(artifactId, creatorId, lotId, kind,
            kind == "book" ? "Book · 1 site" : kind == "field_map" ? "Field map · 1 site" : "Field record · 1 site",
            state.Society.Society.WorldTick, [fact])
        {
            WritingProjectId = projectId,
            Materials = materials,
        };
        inventory = InventoryAuthority.AddLot(inventory,
            lotId, kind, creatorId, 1, state.Society.Society.WorldTick);
        return state with
        {
            Knowledge = new WorldKnowledgeState(
                (state.Knowledge?.Facts ?? []).Append(fact).ToArray(),
                (state.Knowledge?.Artifacts ?? []).Append(artifact).ToArray()),
            Society = state.Society with
            {
                Society = state.Society.Society with { Inventory = inventory },
            },
        };
    }

    private static (string Id, string OwnerId, string DiscovererId, GridPoint Position,
        string Terrain, string Resources, string Acquisition, string? SourceAgentId, string? SourceArtifactId, long LearnedTick) FactKey(
        AgentKnowledgeFact fact) =>
        (fact.Id, fact.OwnerId, fact.DiscovererId, fact.Position, fact.Terrain,
            string.Join(',', fact.ResourceKinds), fact.Acquisition, fact.SourceAgentId, fact.SourceArtifactId, fact.LearnedTick);

    private static (string Id, string CreatorId, string LotId, string Kind, long CreatedTick,
        string Sites) ArtifactKey(AgentKnowledgeArtifact artifact) =>
        (artifact.Id, artifact.CreatorId, artifact.LotId, artifact.Kind, artifact.CreatedTick,
            string.Join(';', artifact.Facts.Select(fact =>
                $"{fact.Position.X},{fact.Position.Y}:{fact.Terrain}:{string.Join(',', fact.ResourceKinds)}")));

    private sealed class CandidateProvider(string targetId, string prefix, bool onlyTarget = true) : IDecisionProvider
    {
        public string PreferredPrefix { get; set; } = prefix;
        public ConcurrentDictionary<string, IReadOnlyList<CognitionKnowledgeFact>> KnownMapFactsByAgent { get; } = new(StringComparer.Ordinal);

        public DecisionProviderKind Kind => DecisionProviderKind.Deterministic;
        public long ProviderEpoch => 0;

        public ValueTask<CognitionDecisionResponse> DecideAsync(
            CognitionDecisionRequest request,
            CancellationToken cancellationToken = default)
        {
            KnownMapFactsByAgent[request.Observation.AgentId] = request.Observation.KnownMapFacts ?? [];
            var selected = onlyTarget && request.Observation.AgentId != targetId
                ? null
                : request.Observation.Candidates.FirstOrDefault(item => item.Id.StartsWith(PreferredPrefix, StringComparison.Ordinal))
                  ?? request.Observation.Candidates.FirstOrDefault(item => item.Id == "knowledge_continue");
            selected ??= request.Observation.Candidates.SingleOrDefault(item => item.Id == "safe_idle")
                ?? request.Observation.Candidates[0];
            return new DeterministicDecisionProvider().DecideAsync(request with
            {
                Observation = request.Observation with { Candidates = [selected] },
            }, cancellationToken);
        }
    }
}
