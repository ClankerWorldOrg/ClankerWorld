using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.World;
using ClankerWorld.Simulation.Society;
using ClankerWorld.Server.Observation;

namespace ClankerWorld.Tests;

public sealed class RecipeKnowledgePipelineTests
{
    [Theory]
    [InlineData("field_record", 1, 0)]
    [InlineData("book", 2, 1)]
    public async Task PaidWritingReadingAndCopyingCarryOnlyPersonalRecipesAcrossReload(string kind, int paper, int cloth)
    {
        var (state, author, recipeId) = await ProducedRecipe();
        var recipe = Assert.Single(state.Knowledge!.Recipes);
        Assert.Equal((author, author, "practice", recipeId),
            (recipe.OwnerId, recipe.DiscovererId, recipe.Acquisition, recipe.RecipeId));
        Assert.Empty(state.Knowledge.Facts);
        var reader = state.Agents.First(person => person.AgentId != author &&
            state.Society.Society.GetAgent(person.AgentId).HouseholdId !=
            state.Society.Society.GetAgent(author).HouseholdId).AgentId;
        var untouched = state.Agents.First(person => person.AgentId != author && person.AgentId != reader).AgentId;
        var skills = state.Agents.Single(person => person.AgentId == reader).Skills ?? [];
        Assert.Empty(skills);
        using var writing = Restore(Supply(state, author, paper, cloth), author, "knowledge_write:" + kind, "knowledge_continue");
        await Until(writing, () => writing.ExportState().Knowledge!.WritingProjects.Count == 1);
        var pending = writing.ExportState();
        var project = Assert.Single(pending.Knowledge!.WritingProjects);
        Assert.Empty(project.Facts);
        Assert.Equal(recipe, Assert.Single(project.Recipes));
        var before = WorldRuntimeCodec.Encode(pending);
        Assert.False((await writing.AdvanceOneTickAsync(() => false)).Advanced);
        Assert.Equal(before, WorldRuntimeCodec.Encode(writing.ExportState()));
        writing.Pause();
        before = WorldRuntimeCodec.Encode(writing.ExportState());
        Assert.False((await writing.AdvanceOneTickAsync()).Advanced);
        Assert.Equal(before, WorldRuntimeCodec.Encode(writing.ExportState()));
        using var resumed = Restore(WorldRuntimeCodec.Decode(before), author, "knowledge_write:" + kind, "knowledge_continue");
        Assert.False((await resumed.AdvanceOneTickAsync()).Advanced);
        Assert.Equal(before, WorldRuntimeCodec.Encode(resumed.ExportState()));
        writing.Resume();
        resumed.Resume();
        await PairedUntil(writing, resumed, () => writing.ExportState().Knowledge!.Artifacts.Count == 1);
        var written = writing.ExportState();
        var original = Assert.Single(written.Knowledge!.Artifacts);
        Assert.Equal(recipe, Assert.Single(original.Recipes));
        Assert.Empty(original.Facts);
        Assert.DoesNotContain("0 sites", original.Title, StringComparison.Ordinal);
        Assert.All(project.MaterialReservationIds, id => Assert.Equal(InventoryReservationState.Completed,
            written.Society.Society.Inventory.GetReservation(id).State));
        Assert.DoesNotContain(written.Society.Society.Inventory.Lots, lot => lot.Id is "recipe-paper" or "recipe-cloth");
        var transfer = InventoryAuthority.Transfer(written.Society.Society.Inventory, "give-recipe-source", author,
            reader, original.LotId, 1, "give the actual recipe account");
        var handed = FarmFieldTests.WithInventory(written, transfer);
        using (var uninformed = Restore(Supply(handed, reader, paper, cloth), reader, "knowledge_copy:" + original.Id, "knowledge_continue"))
        {
            for (var tick = 0; tick < 3; tick++) Assert.True((await uninformed.AdvanceOneTickAsync()).Advanced);
            Assert.Single(uninformed.ExportState().Knowledge!.Artifacts);
            Assert.Empty(uninformed.ExportState().Knowledge!.WritingProjects);
            Assert.DoesNotContain(uninformed.ExportState().Knowledge!.Recipes, item => item.OwnerId == reader);
        }
        using var reading = Restore(handed, reader, "knowledge_read:" + original.Id);
        var workstation = reading.WorldSimulation.Buildings.First(item => item.DefinitionId ==
            reading.WorldContent.Recipes.Single(item => item.CanonicalId == recipeId).WorkstationBuildingId);
        var beforeRead = WorldRuntimeCodec.Encode(reading.ExportState());
        Assert.False(reading.StartProduction(recipeId, workstation.InstanceId, reader).Applied);
        Assert.Equal(beforeRead, WorldRuntimeCodec.Encode(reading.ExportState()));
        await Until(reading, () => reading.ExportState().Knowledge!.Recipes.Any(item => item.OwnerId == reader));
        var read = reading.ExportState();
        var learned = Assert.Single(read.Knowledge!.Recipes, item => item.OwnerId == reader);
        Assert.Equal((recipeId, author, original.Id, recipe.SourceProductionJobId, "read"),
            (learned.RecipeId, learned.DiscovererId, learned.SourceArtifactId, learned.SourceProductionJobId, learned.Acquisition));
        Assert.Equal(skills, read.Agents.Single(person => person.AgentId == reader).Skills ?? []);
        Assert.DoesNotContain(read.WorldSimulation!.ProductionJobs, item => item.WorkerId == reader);
        Assert.DoesNotContain(read.Knowledge.Recipes, item => item.OwnerId == untouched);
        var cards = new OwnerWorldObservationStore(reading).GetSnapshot().Agents;
        Assert.Single(cards.Single(person => person.Id == reader).KnownRecipes);
        Assert.Empty(cards.Single(person => person.Id == untouched).KnownRecipes);
        Assert.Single(Assert.Single(cards.Single(person => person.Id == reader).KnowledgeArtifacts).RecipeNames);
        using var copying = Restore(Supply(read, reader, paper, cloth), reader, "knowledge_copy:" + original.Id, "knowledge_continue");
        await Until(copying, () => copying.ExportState().Knowledge!.WritingProjects.Count == 1);
        var copyPending = WorldRuntimeCodec.Encode(copying.ExportState());
        using var copyReload = Restore(WorldRuntimeCodec.Decode(copyPending), reader, "knowledge_copy:" + original.Id, "knowledge_continue");
        await PairedUntil(copying, copyReload, () => copying.ExportState().Knowledge!.Artifacts.Count == 2);
        var copied = copying.ExportState();
        var copy = Assert.Single(copied.Knowledge!.Artifacts, item => item.Id != original.Id);
        var account = Assert.Single(copy.Recipes);
        Assert.Equal((reader, recipeId, author, original.Id, recipe.SourceProductionJobId),
            (account.OwnerId, account.RecipeId, account.DiscovererId, account.SourceArtifactId, account.SourceProductionJobId));
        Assert.Equal(original.Id, copy.SourceArtifactId);
        Assert.NotEqual(original.LotId, copy.LotId);
        Assert.Equal(skills, copied.Agents.Single(person => person.AgentId == reader).Skills ?? []);
        var forged = copied with
        {
            Knowledge = copied.Knowledge with
            {
                Artifacts = copied.Knowledge.Artifacts.Select(item => item.Id == copy.Id ? item with
                {
                    Recipes = [account with { SourceArtifactId = "missing-written-source" }],
                } : item).ToArray(),
            }
        };
        Assert.Throws<InvalidDataException>(() => Restore(forged, reader));
        Assert.DoesNotContain(copied.Society.Society.Inventory.Lots, lot => lot.Id is "recipe-paper" or "recipe-cloth");
        copying.Validate();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OwnerReadOrderCreditsActualRecipesAndSitesOnlyOnceAcrossRefusedTicksAndReload(bool includeSites)
        => await CheckOwnerReadOrderAsync(includeSites);

    [Fact]
    public async Task OwnerReadOrderRetainsALongCanonicalRecipeAcrossReload()
        => await CheckOwnerReadOrderAsync(includeSites: false, longRecipeId: true);

    private static async Task CheckOwnerReadOrderAsync(bool includeSites, bool longRecipeId = false)
    {
        var (state, author, recipeId) = await ProducedRecipe(longRecipeId: longRecipeId);
        if (longRecipeId) Assert.Equal(129, recipeId.Length);
        if (includeSites)
        {
            var scout = new SelectingProvider(author, ["explore"]);
            using var exploring = WorldRuntime.Restore(state, _ => scout);
            exploring.SubmitInstruction(new("scout-before-recipe-book", "owner:test", author,
                OwnerInstructionKind.Suggestion, "Scout nearby before writing the book."));
            await Until(exploring, () => exploring.Knowledge.Facts.Any(fact => fact.OwnerId == author));
            scout.Prefixes = ["explore_return"];
            exploring.SubmitInstruction(new("return-before-recipe-book", "owner:test", author,
                OwnerInstructionKind.Suggestion, "Return from scouting before writing the book."));
            await Until(exploring, () => exploring.ExportState().Agents.Single(person =>
                person.AgentId == author).Exploration is { OutingPath.Count: 0 });
            Assert.Contains(exploring.ExportState().Events, item => item.Kind == "exploration_completed");
            state = exploring.ExportState();
        }
        using var writing = Restore(Supply(state, author, 2, 1), author, "knowledge_write:book", "knowledge_continue");
        await Until(writing, () => writing.Knowledge.Artifacts.Count == 1);
        var written = writing.ExportState();
        var artifact = Assert.Single(written.Knowledge!.Artifacts);
        Assert.Equal(includeSites, artifact.Facts.Count > 0);
        Assert.Single(artifact.Recipes);
        var reader = written.Agents.First(person => person.AgentId != author &&
            written.Society.Society.GetAgent(person.AgentId).HouseholdId !=
            written.Society.Society.GetAgent(author).HouseholdId).AgentId;
        using (var withheld = Restore(written, reader))
        {
            withheld.SubmitInstruction(new("read-withheld-recipe-book", "owner:test", reader,
                OwnerInstructionKind.Order, "Read " + artifact.Id));
            for (var tick = 0; tick < 3; tick++) Assert.True((await withheld.AdvanceOneTickAsync()).Advanced);
            var blocked = Assert.Single(withheld.ExportState().Instructions!, item => item.IdempotencyKey == "read-withheld-recipe-book").Task!;
            Assert.Equal(("blocked", 0), (blocked.Status, blocked.CompletedUnits));
            Assert.DoesNotContain(withheld.Knowledge.Recipes, recipe => recipe.OwnerId == reader);
            Assert.DoesNotContain(withheld.Knowledge.Facts, fact => fact.OwnerId == reader);
        }
        var handed = FarmFieldTests.WithInventory(written, InventoryAuthority.Transfer(written.Society.Society.Inventory,
            "hand-owner-read-book", author, reader, artifact.LotId, 1, "give the actual recipe book"));
        using var reading = Restore(handed, reader);
        reading.SubmitInstruction(new("read-real-recipe-book", "owner:test", reader, OwnerInstructionKind.Order,
            includeSites ? "Read " + artifact.Id : "Read a book"));
        var before = WorldRuntimeCodec.Encode(reading.ExportState());
        Assert.False((await reading.AdvanceOneTickAsync(() => false)).Advanced);
        Assert.Equal(before, WorldRuntimeCodec.Encode(reading.ExportState()));
        using var replay = Restore(WorldRuntimeCodec.Decode(before), reader);
        await PairedUntil(reading, replay, () => reading.ExportState().Instructions!.Single(item =>
            item.IdempotencyKey == "read-real-recipe-book").Task!.Status == "finished");
        var completed = reading.ExportState();
        var instruction = Assert.Single(completed.Instructions!, item => item.IdempotencyKey == "read-real-recipe-book");
        Assert.Equal(1, instruction.Task!.CompletedUnits);
        Assert.Equal(artifact.Id, instruction.Task.TargetKnowledgeArtifactId);
        var receipt = Assert.IsType<OwnerKnowledgeReadCompletion>(instruction.Task.KnowledgeReadCompletion);
        Assert.Equal([recipeId], receipt.LearnedRecipes);
        Assert.Equal(artifact.Facts.Count, receipt.LearnedSites.Count);
        var learned = Assert.Single(completed.Knowledge!.Recipes, recipe => recipe.OwnerId == reader);
        Assert.Equal(("read", artifact.Id, author, receipt.WorldTick),
            (learned.Acquisition, learned.SourceArtifactId, learned.SourceAgentId, learned.LearnedTick));
        Assert.Empty(completed.Agents.Single(person => person.AgentId == reader).Skills ?? []);
        Assert.Throws<InvalidDataException>(() => WorldRuntimeCodec.Encode(completed with
        {
            Knowledge = completed.Knowledge with
            { Recipes = completed.Knowledge.Recipes.Where(recipe => recipe.OwnerId != reader).ToArray() },
        }));
        Assert.Throws<InvalidDataException>(() => WorldRuntimeCodec.Encode(completed with
        {
            Instructions = completed.Instructions!.Select(item => item.InstructionId == instruction.InstructionId
                ? item with { Task = item.Task! with { KnowledgeReadCompletion = receipt with { LearnedRecipes = [] } } } : item).ToArray(),
        }));
        Assert.True((await reading.AdvanceOneTickAsync()).Advanced);
        Assert.Equal(1, reading.ExportState().Instructions!.Single(item => item.InstructionId == instruction.InstructionId).Task!.CompletedUnits);
        Assert.Single(reading.ExportState().Events, item => item.Kind == "agent_knowledge_artifact_read" &&
            item.Detail.Contains(artifact.Id, StringComparison.Ordinal));
        reading.Validate();
    }

    [Fact]
    public async Task SharingAHeldRecipeTeachesOnlyTheNearbyRecipientWithoutCreatingAnotherGood()
    {
        var (state, author, recipeId) = await ProducedRecipe();
        using var writing = Restore(Supply(state, author, 1, 0), author, "knowledge_write:field_record", "knowledge_continue");
        await Until(writing, () => writing.ExportState().Knowledge!.Artifacts.Count == 1);
        var written = writing.ExportState();
        var artifact = Assert.Single(written.Knowledge!.Artifacts);
        var reader = written.Agents.First(person => person.AgentId != author).AgentId;
        var position = written.Agents.Single(person => person.AgentId == author).Position;
        var adjacent = written.Map.FootNeighbors(position).First(point => written.Map.IsPassable(point) &&
            written.Agents.All(person => person.Position != point));
        var nearby = written with
        {
            Agents = written.Agents.Select(person => person.AgentId == reader
            ? person with { Position = adjacent } : person).ToArray()
        };
        using var sharing = Restore(nearby, author, "knowledge_share:" + artifact.Id + "|" + reader);
        await Until(sharing, () => sharing.ExportState().Knowledge!.Recipes.Any(item => item.OwnerId == reader));
        var shared = sharing.ExportState();
        var learned = Assert.Single(shared.Knowledge!.Recipes, item => item.OwnerId == reader);
        Assert.Equal((recipeId, author, artifact.Id, "shared"),
            (learned.RecipeId, learned.SourceAgentId, learned.SourceArtifactId, learned.Acquisition));
        Assert.Equal(author, shared.Society.Society.Inventory.GetLot(artifact.LotId).OwnerId);
        Assert.Single(shared.Knowledge.Artifacts);
        Assert.Empty(shared.Agents.Single(person => person.AgentId == reader).Skills ?? []);
        Assert.Equal(2, shared.Knowledge.Recipes.Count);
        using var reloaded = Restore(WorldRuntimeCodec.Decode(WorldRuntimeCodec.Encode(shared)), author);
        Assert.Equal(WorldRuntimeCodec.Encode(shared), WorldRuntimeCodec.Encode(reloaded.ExportState()));
    }

    [Fact]
    public async Task ReloadRefusesForgedProductionEvidenceAndMissingRequiredRecipeContents()
    {
        var (state, author, _) = await ProducedRecipe();
        var known = Assert.Single(state.Knowledge!.Recipes);
        var other = state.Agents.First(person => person.AgentId != author).AgentId;
        foreach (var forged in new[]
        {
            known with { SourceProductionJobId = "missing-completed-job" },
            known with { DiscovererId = other },
            known with { OwnerId = state.Society.Society.GetAgent(author).HouseholdId! },
            known with { LearnedTick = state.Society.Society.WorldTick + 1 },
            known with { RecipeId = "unknown-recipe" },
            known with { Acquisition = "read", SourceAgentId = other, SourceArtifactId = "missing-written-source" },
        })
        {
            var invalid = state with { Knowledge = state.Knowledge with { Recipes = [forged] } };
            Assert.Throws<InvalidDataException>(() => Restore(WorldRuntimeCodec.Decode(
                WorldRuntimeCodec.Encode(invalid)), author));
        }
        var json = System.Text.Json.Nodes.JsonNode.Parse(WorldRuntimeCodec.Encode(state))!.AsObject();
        Assert.True(json["state"]!["knowledge"]!.AsObject().Remove("recipes"));
        Assert.Throws<InvalidDataException>(() => WorldRuntimeCodec.Decode(System.Text.Encoding.UTF8.GetBytes(json.ToJsonString())));
        var duplicateJob = System.Text.Json.Nodes.JsonNode.Parse(WorldRuntimeCodec.Encode(state))!.AsObject();
        var jobs = duplicateJob["state"]!["worldSimulation"]!["productionJobs"]!.AsArray();
        jobs.Add(jobs[0]!.DeepClone());
        Assert.Throws<InvalidDataException>(() => WorldRuntimeCodec.Decode(
            System.Text.Encoding.UTF8.GetBytes(duplicateJob.ToJsonString())));
        var previousSchema = System.Text.Json.Nodes.JsonNode.Parse(WorldRuntimeCodec.Encode(state))!.AsObject();
        previousSchema["state"]!["schemaVersion"] = 113;
        Assert.Throws<InvalidDataException>(() => WorldRuntimeCodec.Decode(
            System.Text.Encoding.UTF8.GetBytes(previousSchema.ToJsonString())));
        using var restored = Restore(state, author);
        Assert.Equal(WorldRuntimeCodec.Encode(state), WorldRuntimeCodec.Encode(restored.ExportState()));
    }

    [Fact]
    public async Task WorkerDeathCancelsProductionWithoutTeachingTheHistoricalProfile()
    {
        var (state, actor, _) = await ProducedRecipe(complete: false);
        var society = SocietyRules.Kill(state.Society.Society, actor, SocietyDeathCause.NaturalAge).Checkpoint;
        var person = society.GetAgent(actor);
        state = state with
        {
            Society = state.Society with { Society = society },
            Agents = state.Agents.Where(item => item.AgentId != actor).ToArray(),
            DeceasedAgents = [.. state.DeceasedAgents ?? [], new DeceasedAgentState(actor,
                person.DeathTick!.Value, society.AgeAt(person, person.DeathTick.Value),
                state.Agents.Single(item => item.AgentId == actor))],
            Towns = state.Towns!.Select(town =>
            {
                var residents = town.ResidentIds.Where(id => id != actor).ToArray();
                var adults = residents.Where(id => society.GetAgent(id).Status == SocietyAgentStatus.Active &&
                    society.GetAgent(id).AgeBand is SocietyAgeBand.Adult or SocietyAgeBand.Elder);
                return town with
                {
                    ResidentIds = residents,
                    Council = TownCouncilRules.Advance(town.Council!,
                    town.Id, state.WorldSeed, adults, society.WorldTick, state.WorldSystems!.Config.TicksPerDay)
                };
            }).ToArray(),
        };
        using var world = Restore(state, "");
        var job = Assert.Single(world.WorldSimulation.ProductionJobs);
        await Until(world, () => Assert.Single(world.WorldSimulation.ProductionJobs).State != WorldProductionJobState.Running);
        Assert.Equal(WorldProductionJobState.Cancelled, Assert.Single(world.WorldSimulation.ProductionJobs).State);
        Assert.All(job.InputReservationIds, id => Assert.Equal(InventoryReservationState.Released, world.Society.Inventory.GetReservation(id).State));
        Assert.Empty(world.ExportState().Knowledge!.Recipes);
        Assert.Empty(new OwnerWorldObservationStore(world).GetSnapshot().Agents.Single(item => item.Id == actor).KnownRecipes);
        using var reloaded = Restore(WorldRuntimeCodec.Decode(WorldRuntimeCodec.Encode(world.ExportState())), "");
        Assert.Equal(WorldRuntimeCodec.Encode(world.ExportState()), WorldRuntimeCodec.Encode(reloaded.ExportState()));
    }

    internal static async Task<(WorldRuntimeState State, string Actor, string RecipeId)> ProducedRecipe(bool complete = true, bool longRecipeId = false)
    {
        using var seed = NormalPathWorld.CreateGenerated("written-recipe-production", _ => new SelectingProvider("", []));
        var state = seed.ExportState();
        var recipe = state.WorldContent!.Recipes.Single(item => item.LocalId == "mill-grain");
        if (longRecipeId)
        {
            var version = ContentVersion.Parse("1.0.0");
            var digest = "sha256:" + new string('d', 64);
            recipe = new RecipeDefinition(digest, new string('r', 44), version, "Long recipe identity",
                recipe.Inputs, recipe.Outputs, recipe.DurationTicks, recipe.WorkstationBuildingId, recipe.Tags);
            var payload = System.Text.Json.JsonSerializer.Serialize(new
            {
                schema = "recipe/v1",
                recipe.Inputs,
                recipe.Outputs,
                recipe.DurationTicks,
                recipe.WorkstationBuildingId,
                recipe.Tags,
            }, System.Text.Json.JsonSerializerOptions.Web);
            var package = new ContentPackageManifest("long-written-recipe", version, digest,
                [new(FarmContent.PackageId, new(version, ContentVersion.Parse("2.0.0")))],
                [new(RecipeDefinition.SchemaKind, recipe.LocalId, version, recipe.DisplayName, recipe.PayloadDigest, payload)], []);
            seed.ProposeContent(package);
            seed.ValidateContent(package.PackageId, seed.ResolveContent(package.PackageId));
            seed.ApproveContent(package.PackageId);
            seed.StageContent(package.PackageId);
            Assert.True((await seed.AdvanceOneTickAsync()).Advanced);
            Assert.Contains(seed.WorldContent.Recipes, item => item.CanonicalId == recipe.CanonicalId);
            state = seed.ExportState();
        }
        var building = state.WorldSimulation!.Buildings.First(item => item.DefinitionId == recipe.WorkstationBuildingId);
        var actor = state.Society.Society.Agents.First(item => item.HouseholdId == building.HouseholdId).Id;
        var inventory = state.Society.Society.Inventory;
        foreach (var input in recipe.Inputs)
            inventory = InventoryAuthority.AddLot(inventory, "recipe-input:" + input.ResourceId, input.ResourceId,
                building.HouseholdId!, input.Amount, storageBuildingId: building.InstanceId);
        state = FarmFieldTests.WithInventory(state, inventory) with
        {
            Agents = state.Agents.Select(person => person.AgentId == actor
                ? person with { Position = building.Position, FullnessBasisPoints = 10_000 } : person).ToArray(),
        };
        using var world = Restore(state, actor);
        var start = world.StartProduction(recipe.CanonicalId, building.InstanceId, actor);
        Assert.True(start.Applied, start.Failure);
        var job = Assert.Single(world.WorldSimulation.ProductionJobs);
        Assert.Empty(world.ExportState().Knowledge!.Recipes);
        if (!complete) return (world.ExportState(), actor, recipe.CanonicalId);
        while (world.WorldTick < job.CompletionTick) Assert.True((await world.AdvanceOneTickAsync()).Advanced);
        Assert.Equal(WorldProductionJobState.Completed, Assert.Single(world.WorldSimulation.ProductionJobs).State);
        Assert.All(job.InputReservationIds, id => Assert.Equal(InventoryReservationState.Completed,
            world.Society.Inventory.GetReservation(id).State));
        Assert.All(recipe.Outputs, output => Assert.Equal(output.Amount, world.Society.Inventory.Lots
            .Where(lot => lot.Id.StartsWith(job.JobId + ":output:", StringComparison.Ordinal) && lot.ItemKind == output.ResourceId)
            .Sum(lot => lot.Quantity)));
        return (WorldRuntimeCodec.Decode(WorldRuntimeCodec.Encode(world.ExportState())), actor, recipe.CanonicalId);
    }

    private static WorldRuntimeState Supply(WorldRuntimeState state, string actor, int paper, int cloth)
    {
        var inventory = InventoryAuthority.AddLot(state.Society.Society.Inventory, "recipe-paper", "paper", actor, paper, state.Society.Society.WorldTick);
        if (cloth > 0) inventory = InventoryAuthority.AddLot(inventory, "recipe-cloth", "cloth", actor, cloth, state.Society.Society.WorldTick);
        return FarmFieldTests.WithInventory(state, inventory);
    }

    private static WorldRuntime Restore(WorldRuntimeState state, string actor, params string[] choices) =>
        WorldRuntime.Restore(state, _ => new SelectingProvider(actor, choices));

    private static async Task Until(WorldRuntime world, Func<bool> done)
    {
        for (var tick = 0; tick < 64 && !done(); tick++) Assert.True((await world.AdvanceOneTickAsync()).Advanced);
        Assert.True(done(), string.Join(" | ", world.ExportState().Events.TakeLast(8).Select(item => item.Kind + ":" + item.Detail)));
    }

    private static async Task PairedUntil(WorldRuntime first, WorldRuntime second, Func<bool> done)
    {
        for (var tick = 0; tick < 64 && !done(); tick++)
        {
            Assert.True((await first.AdvanceOneTickAsync()).Advanced);
            Assert.True((await second.AdvanceOneTickAsync()).Advanced);
            Assert.Equal(WorldRuntimeCodec.Encode(first.ExportState()), WorldRuntimeCodec.Encode(second.ExportState()));
        }
        Assert.True(done());
    }

    private sealed class SelectingProvider(string actor, string[] prefixes) : IDecisionProvider
    {
        public DecisionProviderKind Kind => DecisionProviderKind.Deterministic;
        public long ProviderEpoch => 0;
        public string[] Prefixes { get; set; } = prefixes;
        public ValueTask<CognitionDecisionResponse> DecideAsync(CognitionDecisionRequest request, CancellationToken cancellationToken = default)
        {
            var selected = request.Observation.AgentId == actor
                ? Prefixes.Select(prefix => request.Observation.Candidates.FirstOrDefault(item => item.Id.StartsWith(prefix, StringComparison.Ordinal)))
                    .FirstOrDefault(item => item is not null) : null;
            selected ??= request.Observation.Candidates.Single(item => item.Id == "safe_idle");
            return new DeterministicDecisionProvider().DecideAsync(request with
            {
                Observation = request.Observation with { Candidates = [selected] },
            }, cancellationToken);
        }
    }
}
