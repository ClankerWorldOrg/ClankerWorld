using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.World;
using ClankerWorld.Simulation.Society;
using ClankerWorld.GodotClient.UI;
using ClankerWorld.GodotClient.Protocol;
using ClankerWorld.Server.Observation;
using ClankerWorld.Server.Runtime;
using ClankerWorld.Server.Saves;

namespace ClankerWorld.Tests;

public sealed class PostDeathWillTests
{
    private const string Words = "Keep the orchard going.";

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task WillRecallsOnlyItsOwnersSavedSummaryWithAttributionAndCorrections(bool helperEnabled, bool corrected)
    {
        var idle = new ActionCoverageRecorder(chooseIdle: true);
        using var seed = NormalPathWorld.CreateGenerated("will-summary-recall", _ => idle, ticksPerDay: 2);
        var state = seed.ExportState();
        var owner = state.Agents[0].AgentId;
        var other = state.Agents[1].AgentId;
        var checkpoint = state.Society.Society;
        foreach (var actor in new[] { owner, other })
            for (var index = 0; index < 4; index++)
                checkpoint = SocietyRules.RecordSocialMemory(checkpoint, new($"walk-{actor}-{index}", actor, other,
                    "I walked past the orchard and thought about sharing my belongings with my heirs. " + index, "private", 0)).Checkpoint;
        const string rumorId = "orchard-rumor";
        checkpoint = SocietyRules.RecordAgentBelief(checkpoint, new(rumorId, owner,
            "Someone told me the orchard needs stone from my belongings; I have not checked it myself.",
            SocietyBeliefProvenance.Hearsay, 4_200, 0, AboutAgentId: other, SourceAgentId: other));
        using var aging = WorldRuntime.Restore(state with
        {
            JevEnabled = false,
            RoutineHelper = RoutineHelperSettings.Off,
            Society = state.Society with { Society = checkpoint },
        }, _ => idle);
        for (var tick = 0; tick < 6; tick++) Assert.True((await aging.AdvanceOneTickAsync()).Advanced);
        checkpoint = aging.Society;
        Assert.Equal(4, checkpoint.ArchivedMemories.Count(item => item.Memory.OwnerId == owner));
        Assert.Single(checkpoint.ArchivedBeliefs, item => item.Belief.Id == rumorId);
        foreach (var actor in new[] { owner, other })
            checkpoint = SocietyRules.RecordAgentMemorySummary(checkpoint, actor, SocietyMemorySummaryRules.Options(checkpoint, actor)[0]);
        var summary = Assert.Single(checkpoint.MemorySummaries, item => item.OwnerId == owner);
        var foreign = Assert.Single(checkpoint.MemorySummaries, item => item.OwnerId == other);
        if (corrected)
            checkpoint = SocietyRules.CorrectAgentBelief(checkpoint, owner, rumorId, new("orchard-correction", owner,
                "I checked the orchard and it does not need stone.", SocietyBeliefProvenance.Firsthand, 9_600, checkpoint.WorldTick));
        for (var index = 0; index < 4; index++)
            checkpoint = SocietyRules.RecordSocialMemory(checkpoint, new($"recent-{index}", owner, owner,
                "A quiet evening by the riverbank.", "private", checkpoint.WorldTick)).Checkpoint;
        state = aging.ExportState() with
        {
            JevEnabled = helperEnabled,
            RoutineHelper = helperEnabled ? new("jev", "jev-1.13.0") : RoutineHelperSettings.Off,
            Society = aging.ExportState().Society with { Society = checkpoint },
        };
        state = WorldRuntimeCodec.Decode(WorldRuntimeCodec.Encode(state));
        checkpoint = state.Society.Society;
        var ordinary = WorldMemoryRetrieval.Retrieve(checkpoint.Memories, checkpoint.Beliefs ?? [], null, owner,
            checkpoint.WorldTick, [new("remember", "Remember my orchard belongings and heirs.")], checkpoint);
        var expected = Assert.Single(ordinary, item => item.Id == summary.Id);
        Assert.Equal(4, ordinary.Count);

        var physical = state.Agents.Single(item => item.AgentId == owner);
        checkpoint = checkpoint with
        {
            Inventory = InventoryAuthority.AddLot(checkpoint.Inventory, "will-stone", "stone", owner, 1,
                groundPosition: new InventoryGroundPosition(physical.Position.X, physical.Position.Y))
        };
        checkpoint = SocietyRules.Kill(checkpoint, owner, SocietyDeathCause.Accident).Checkpoint;
        if (checkpoint.Inventory.Lots.Any(item => item.CarrierId == owner))
            checkpoint = checkpoint with
            {
                Inventory = InventoryAuthority.DropCarrierGoods(checkpoint.Inventory, owner,
                    new InventoryGroundPosition(physical.Position.X, physical.Position.Y))
            };
        state = state with
        {
            Society = state.Society with { Society = checkpoint },
            Agents = state.Agents.Where(item => item.AgentId != owner).ToArray(),
            DeceasedAgents = [new(owner, checkpoint.WorldTick, checkpoint.AgeAt(checkpoint.GetAgent(owner), checkpoint.WorldTick),
                physical, state.Towns!.Single(town => town.ResidentIds.Contains(owner)).Id)],
            // Direct death must also refresh the actual Town's adult council before strict reload.
            Towns = state.Towns!.Select(town => town with
            {
                ResidentIds = town.ResidentIds.Where(id => id != owner).ToArray(),
                Council = TownCouncilRules.Advance(town.Council!, town.Id, state.WorldSeed,
                    town.ResidentIds.Where(id => checkpoint.GetAgent(id).Status == SocietyAgentStatus.Active &&
                        checkpoint.GetAgent(id).AgeBand is SocietyAgeBand.Adult or SocietyAgeBand.Elder),
                    checkpoint.WorldTick, checkpoint.Config.TicksPerWorldDay),
            }).ToArray(),
        };
        using var deceasedWorld = WorldRuntime.Restore(WorldRuntimeCodec.Decode(WorldRuntimeCodec.Encode(state)), _ => idle);
        var bytes = WorldRuntimeCodec.Encode(deceasedWorld.ExportState());
        var provider = new WillProvider(CognitionWillContext.HeirsCandidateId, observation =>
            new CognitionWillChoice([observation.Will!.Heirs[0].Key], CognitionWillContext.EqualSplit, FinalWords: Words));
        using var world = WorldRuntime.Restore(WorldRuntimeCodec.Decode(bytes), id => id == owner ? provider : idle);
        Assert.Equal(bytes, WorldRuntimeCodec.Encode(world.ExportState()));
        Assert.True((await world.AdvanceOneTickNonBlockingAsync()).Advanced);
        Assert.True(world.Society.Estates.Single(item => item.DeceasedId == owner).WillStatus == "pending",
            string.Join(" | ", world.ExportState().Events.Where(item => item.Kind.StartsWith("estate_will", StringComparison.Ordinal))));
        await provider.Called.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var will = Assert.Single(provider.Observations);
        will.Validate();
        var recalled = Assert.Single(will.RetrievedMemories!, item => item.Id == summary.Id);
        Assert.Equal(4, will.RetrievedMemories!.Count);
        Assert.Equal((owner, summary.Text, summary.CreatedTick, "private", corrected),
            (recalled.OwnerId, recalled.Summary, recalled.SourceTick, recalled.Visibility, recalled.IsCorrected));
        Assert.Equal(expected.SummarySources, recalled.SummarySources);
        var rumor = Assert.Single(recalled.SummarySources!, item => item.Id == rumorId);
        Assert.Equal(("hearsay", 4_200, other, other, 0L, corrected),
            (rumor.Provenance, rumor.ConfidenceBasisPoints, rumor.SourceAgentId, rumor.SubjectId, rumor.SourceTick, rumor.IsCorrected));
        Assert.DoesNotContain(will.RetrievedMemories!, item => item.Id == foreign.Id || item.OwnerId != owner);
        await AdvanceUntilResolved(world, owner);
        Assert.Equal("accepted", world.Society.Estates.Single(item => item.DeceasedId == owner).WillStatus);
        var accepted = WorldRuntimeCodec.Encode(world.ExportState());
        using var restored = WorldRuntime.Restore(WorldRuntimeCodec.Decode(accepted), id => id == owner ? provider : idle);
        Assert.Equal(accepted, WorldRuntimeCodec.Encode(restored.ExportState()));
        Assert.True((await world.AdvanceOneTickAsync()).Advanced);
        Assert.True((await restored.AdvanceOneTickAsync()).Advanced);
        Assert.Equal(WorldRuntimeCodec.Encode(world.ExportState()), WorldRuntimeCodec.Encode(restored.ExportState()));
        Assert.Equal(1, provider.CallCount);
    }

    [Fact]
    public async Task TwoHeirsShareEquallyAndHearFinalWordsThatSurviveSaveAndReload()
    {
        var provider = new WillProvider(CognitionWillContext.HeirsCandidateId, observation =>
            new CognitionWillChoice([HeirKey(observation, "Mira"), HeirKey(observation, "Rowan")],
                CognitionWillContext.EqualSplit, FinalWords: Words));
        using var world = NewWorld(provider);
        Assert.True((await world.AdvanceOneTickNonBlockingAsync()).Advanced);
        await provider.Called.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await AdvanceUntilResolved(world);

        var estate = Assert.Single(world.Society.Estates, item => item.DeceasedId == "founder-scout");
        Assert.Equal("accepted", estate.WillStatus);
        Assert.Equal(["founder-mira", "founder-rowan"], estate.WillHeirIds);
        Assert.Equal(Words, estate.FinalWords);
        // Lot-ID order: rope 1, seed 3, stone 5. Leftover units go one at a time
        // in will order, continuing from where the previous lot stopped.
        Assert.Equal(
        [
            new SocietyWillBequest("rope-lot", "founder-mira", 1),
            new SocietyWillBequest("seed-lot", "founder-mira", 1),
            new SocietyWillBequest("seed-lot", "founder-rowan", 2),
            new SocietyWillBequest("stone-lot", "founder-mira", 3),
            new SocietyWillBequest("stone-lot", "founder-rowan", 2),
        ], estate.WillBequests);
        var will = Assert.Single(provider.Observations).Will!;
        Assert.Equal(["rope", "seed", "stone"], will.Items.Select(item => item.Kind));
        Assert.DoesNotContain(will.Heirs, heir => heir.Key.EndsWith("founder-scout", StringComparison.Ordinal));
        Assert.Contains(world.ExportState().Events, item => item.Kind == "estate_will_accepted");
        Assert.True(GameUiText.IsPlayerFacingEvent("estate_will_accepted"));
        Assert.False(GameUiText.IsPlayerFacingEvent("estate_will_started"));

        var profile = new OwnerWorldObservationStore(world).GetSnapshot().Agents
            .Single(item => item.Id == "founder-scout").FinalWill!;
        Assert.Equal(("accepted", "equal", Words), (profile.Status, profile.Split, profile.FinalWords));
        Assert.Equal(["Mira", "Rowan"], profile.Heirs.Select(heir => heir.Name));
        Assert.Equal([new InventoryEntryView("rope", 1), new InventoryEntryView("seed", 1), new InventoryEntryView("stone", 3)],
            profile.Heirs[0].Items);

        var saved = WorldRuntimeCodec.Encode(world.ExportState());
        using var restored = WorldRuntime.Restore(WorldRuntimeCodec.Decode(saved),
            id => id == "founder-scout" ? provider : new DeterministicDecisionProvider());
        Assert.Equal(saved, WorldRuntimeCodec.Encode(restored.ExportState()));
        var original = await AdvanceUntilSettled(world, estate.Id);
        var reloaded = await AdvanceUntilSettled(restored, estate.Id);
        Assert.Equal(Inherited(original), Inherited(reloaded));
        Assert.Equal(FinalWordMemories(original), FinalWordMemories(reloaded));

        Assert.Equal(
        [
            ("founder-mira", "rope-lot", 1), ("founder-mira", "seed-lot", 1), ("founder-mira", "stone-lot", 3),
            ("founder-rowan", "seed-lot", 2), ("founder-rowan", "stone-lot", 2),
        ], Inherited(reloaded));
        Assert.Equal(
        [
            ("founder-mira", "Scout's final words were: 'Keep the orchard going.'"),
            ("founder-rowan", "Scout's final words were: 'Keep the orchard going.'"),
        ], FinalWordMemories(reloaded));
        Assert.All(reloaded.Memories.Where(memory => memory.Id.StartsWith("final-words:", StringComparison.Ordinal)),
            memory => Assert.Equal("private", memory.Visibility));
        Assert.DoesNotContain(reloaded.Memories, memory => memory.OwnerId == "founder-ilya" &&
            memory.Summary.Contains("final words", StringComparison.Ordinal));
        Assert.Single(reloaded.Events, item => item.Kind == "estate_settled" && item.Detail == estate.Id);
        Assert.Equal(1, provider.CallCount);
    }

    [Fact]
    public async Task ItemByItemWillGivesListedItemsWholeSharesTheRestAndAChildOwnsTheirPart()
    {
        var provider = new WillProvider(CognitionWillContext.HeirsCandidateId, observation =>
        {
            var mira = HeirKey(observation, "Mira");
            var ilya = HeirKey(observation, "Ilya");
            return new CognitionWillChoice([mira, ilya, HeirKey(observation, "Rowan")], CognitionWillContext.ItemSplit,
                new Dictionary<string, string>
                {
                    [ItemKey(observation, "stone")] = ilya,
                    [ItemKey(observation, "rope")] = mira,
                });
        });
        using var world = NewWorld(provider, childHeir: true);
        Assert.True((await world.AdvanceOneTickNonBlockingAsync()).Advanced);
        await provider.Called.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await AdvanceUntilResolved(world);

        var estate = world.Society.Estates.Single(item => item.DeceasedId == "founder-scout");
        Assert.Equal("accepted", estate.WillStatus);
        Assert.Null(estate.FinalWords);
        Assert.Equal(
        [
            new SocietyWillBequest("rope-lot", "founder-mira", 1),
            new SocietyWillBequest("seed-lot", "founder-mira", 1),
            new SocietyWillBequest("seed-lot", "founder-ilya", 1),
            new SocietyWillBequest("seed-lot", "founder-rowan", 1),
            new SocietyWillBequest("stone-lot", "founder-ilya", 5),
        ], estate.WillBequests);
        Assert.Equal("your household", Assert.Single(provider.Observations).Will!.Heirs
            .Single(heir => heir.Name == "Mira").Relation);

        var settled = await AdvanceUntilSettled(world, estate.Id);
        Assert.Equal(SocietyAgeBand.Child, settled.GetAgent("founder-ilya").AgeBand);
        var childStone = Assert.Single(settled.Inventory.Lots, lot => lot.ProvenanceLotId == "stone-lot");
        var deathPosition = world.ExportState().DeceasedAgents!.Single(person => person.AgentId == "founder-scout")
            .LastPhysical.Position;
        Assert.Equal(("founder-ilya", 5, (string?)null, (string?)null,
                (InventoryGroundPosition?)new InventoryGroundPosition(deathPosition.X, deathPosition.Y)),
            (childStone.OwnerId, childStone.Quantity, childStone.CarrierId, childStone.StorageBuildingId, childStone.GroundPosition));
        Assert.Equal(5 + 3 + 1, settled.Inventory.Lots.Where(lot => lot.ProvenanceLotId is "rope-lot" or "seed-lot" or "stone-lot")
            .Sum(lot => lot.Quantity));
        Assert.DoesNotContain(settled.Memories, memory => memory.Id.StartsWith("final-words:", StringComparison.Ordinal));
    }

    [Fact]
    public async Task AVesselAndItsContentsGoToOneHeirAndKeepTheirIdentity()
    {
        var provider = new WillProvider(CognitionWillContext.HeirsCandidateId, observation =>
            new CognitionWillChoice([HeirKey(observation, "Mira"), HeirKey(observation, "Rowan")],
                CognitionWillContext.EqualSplit, FinalWords: Words));
        using var world = NewWorld(provider, vessels: true);
        Assert.True((await world.AdvanceOneTickNonBlockingAsync()).Advanced);
        await provider.Called.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await AdvanceUntilResolved(world);

        var will = Assert.Single(provider.Observations).Will!;
        Assert.Equal(["water_jug", "storage_pot", "rope", "seed", "stone"], will.Items.Select(item => item.Kind));
        Assert.Equal(["3 fresh_water", "2 berries", null, null, null], will.Items.Select(item => item.Contents));
        var estate = world.Society.Estates.Single(item => item.DeceasedId == "founder-scout");
        Assert.Equal("accepted", estate.WillStatus);
        // Each vessel is one unit in the rotation, and its contents follow it.
        Assert.Equal(
        [
            new SocietyWillBequest("jug-lot", "founder-mira", 1),
            new SocietyWillBequest("jug-water", "founder-mira", 3),
            new SocietyWillBequest("pot-lot", "founder-rowan", 1),
            new SocietyWillBequest("pot-berries", "founder-rowan", 2),
            new SocietyWillBequest("rope-lot", "founder-mira", 1),
            new SocietyWillBequest("seed-lot", "founder-mira", 1),
            new SocietyWillBequest("seed-lot", "founder-rowan", 2),
            new SocietyWillBequest("stone-lot", "founder-mira", 3),
            new SocietyWillBequest("stone-lot", "founder-rowan", 2),
        ], estate.WillBequests);

        var settled = await AdvanceUntilSettled(world, estate.Id);
        var jug = settled.Inventory.GetLot("jug-lot");
        var water = settled.Inventory.GetLot("jug-water");
        Assert.Equal(("founder-mira", "founder-mira", "jug-lot", 3), (jug.OwnerId, water.OwnerId, water.ContainerLotId, water.Quantity));
        var pot = settled.Inventory.GetLot("pot-lot");
        var berries = settled.Inventory.GetLot("pot-berries");
        Assert.Equal(("founder-rowan", "founder-rowan", "pot-lot", 2), (pot.OwnerId, berries.OwnerId, berries.ContainerLotId, berries.Quantity));
        Assert.Equal(2, FinalWordMemories(settled).Length);
    }

    [Theory]
    [InlineData("unknown_heir")]
    [InlineData("unoffered_item")]
    [InlineData("four_heirs")]
    [InlineData("item_to_unnamed_heir")]
    [InlineData("markup_words")]
    public async Task InvalidWillChoicesKeepTheHouseholdDefault(string fault)
    {
        var provider = new WillProvider(CognitionWillContext.HeirsCandidateId, observation =>
        {
            var mira = HeirKey(observation, "Mira");
            var rowan = HeirKey(observation, "Rowan");
            return fault switch
            {
                "unknown_heir" => new CognitionWillChoice(["will:heir:ghost"], CognitionWillContext.EqualSplit, FinalWords: Words),
                "unoffered_item" => new CognitionWillChoice([mira], CognitionWillContext.ItemSplit,
                    new Dictionary<string, string> { ["item:99"] = mira }, Words),
                "four_heirs" => new CognitionWillChoice([mira, rowan, HeirKey(observation, "Ilya"), "will:heir:extra"],
                    CognitionWillContext.EqualSplit, FinalWords: Words),
                "item_to_unnamed_heir" => new CognitionWillChoice([mira], CognitionWillContext.ItemSplit,
                    new Dictionary<string, string> { [ItemKey(observation, "seed")] = rowan }, Words),
                _ => new CognitionWillChoice([mira], CognitionWillContext.EqualSplit, FinalWords: "[b]Keep it[/b]"),
            };
        });
        using var world = NewWorld(provider);
        var householdBeneficiaries = world.Society.Estates.Single(item => item.DeceasedId == "founder-scout")
            .BeneficiaryIds.ToArray();
        Assert.True((await world.AdvanceOneTickNonBlockingAsync()).Advanced);
        await AdvanceUntilResolved(world);
        var estate = world.Society.Estates.Single(item => item.DeceasedId == "founder-scout");
        Assert.Equal("default", estate.WillStatus);
        Assert.Null(estate.WillHeirIds);
        Assert.Null(estate.WillBequests);
        Assert.Equal(householdBeneficiaries, estate.BeneficiaryIds);
        // A reply the host admits keeps its words even when its division is unusable;
        // a malformed reply is discarded whole.
        Assert.Equal(fault is "unknown_heir" or "unoffered_item" ? Words : null, estate.FinalWords);
        Assert.Equal(1, provider.CallCount);
    }

    [Fact]
    public void DeadUnknownOrSelfHeirsAndItemsTheAgentDidNotOwnCannotBeCommitted()
    {
        using var world = NewWorld(new WillProvider(CognitionWillContext.HouseholdCandidateId), vessels: true);
        var started = SocietyRules.MarkWillStarted(world.Society,
            world.Society.Estates.Single(item => item.DeceasedId == "founder-scout").Id).Checkpoint;
        var estateId = started.Estates.Single(item => item.DeceasedId == "founder-scout").Id;
        var deadRowan = SocietyRules.Kill(started, "founder-rowan", SocietyDeathCause.Accident).Checkpoint;
        var forged = started with
        {
            Inventory = started.Inventory with
            {
                Lots = started.Inventory.Lots.Select(lot => lot.Id == "seed-lot"
                    ? lot with { OwnerId = "founder-rowan" } : lot).ToArray(),
            },
        };
        var cases = new (SocietyCheckpoint Checkpoint, SocietyWillDirective Directive)[]
        {
            (deadRowan, new(["founder-rowan"], "equal")),
            (started, new(["founder-ghost"], "equal")),
            (started, new(["founder-scout"], "equal")),
            (started, new(["town:first"], "equal")),
            (started, new(["founder-mira"], "items", new Dictionary<string, string> { ["not-owned"] = "founder-mira" })),
            (forged, new(["founder-mira"], "equal")),
            (started, new(["founder-mira", "founder-mira"], "equal")),
            // A vessel's contents cannot be left apart from the vessel.
            (started, new(["founder-mira", "founder-rowan"], "items", new Dictionary<string, string>
            {
                ["jug-lot"] = "founder-mira",
                ["jug-water"] = "founder-rowan",
            })),
        };
        foreach (var (checkpoint, directive) in cases)
        {
            var resolved = SocietyRules.ResolveWill(checkpoint, estateId, directive, "accepted", Words).Checkpoint;
            var estate = resolved.GetEstate(estateId);
            Assert.Equal("default", estate.WillStatus);
            Assert.Null(estate.WillBequests);
            Assert.Equal(Words, estate.FinalWords);
        }
    }

    [Fact]
    public void FinalWordsArePlainShortSingleLineText()
    {
        Assert.Equal("Keep the orchard going.", CognitionWillChoice.NormalizeFinalWords("  Keep the\norchard\t going.  "));
        Assert.Equal("Look after them", CognitionWillChoice.NormalizeFinalWords("Look‮ after​ them"));
        Assert.Null(CognitionWillChoice.NormalizeFinalWords("<b>Goodbye</b>"));
        Assert.Null(CognitionWillChoice.NormalizeFinalWords("{\"say\":\"hi\"}"));
        Assert.Null(CognitionWillChoice.NormalizeFinalWords(" \n "));
        Assert.Null(CognitionWillChoice.NormalizeFinalWords(new string('a', CognitionWillChoice.MaximumFinalWordsLength + 1)));
        Assert.NotNull(CognitionWillChoice.NormalizeFinalWords(new string('a', CognitionWillChoice.MaximumFinalWordsLength)));
        Assert.Equal("Remember 𠀀 and 😀.", CognitionWillChoice.NormalizeFinalWords("Remember 𠀀 and 😀."));
        var atLimit = new string('a', CognitionWillChoice.MaximumFinalWordsLength - 2) + "😀";
        Assert.Equal(atLimit, CognitionWillChoice.NormalizeFinalWords(atLimit));
        Assert.Null(CognitionWillChoice.NormalizeFinalWords(atLimit + "a"));
        Assert.Equal("a b c", CognitionWillChoice.NormalizeFinalWords("a\uD800b\uDC00c"));
        Assert.Equal("a b c", CognitionWillChoice.NormalizeFinalWords("a\U000E0001b\U000F0000c"));
        var lines = GameUiText.FinalWillLines("accepted", new OwnerWorldFinalWill("accepted", "equal",
            [new("founder-mira", "Mira", false, [new("seed", 2)]), new("town:first", "First Town", true, [])], Words));
        Assert.Equal(
        [
            "Final will: belongings shared equally between Mira and First Town.",
            "To Mira: 2 seed",
            "To First Town: nothing listed",
            "Final words: “Keep the orchard going.”",
        ], lines);
        Assert.Equal(["Personal estate follows default inheritance rules.", "Final words: “Be kind.”"],
            GameUiText.FinalWillLines("default", new OwnerWorldFinalWill("default", null, [], "Be kind.")));
    }

    [Fact]
    public async Task HouseholdChoiceKeepsDefaultAndItsHeirsHearTheFinalWords()
    {
        var provider = new WillProvider(CognitionWillContext.HouseholdCandidateId, _ =>
            new CognitionWillChoice([], FinalWords: Words));
        using var world = NewWorld(provider);
        Assert.True((await world.AdvanceOneTickNonBlockingAsync()).Advanced);
        await AdvanceUntilResolved(world);
        var estate = world.Society.Estates.Single(item => item.DeceasedId == "founder-scout");
        Assert.Equal(("default", Words), (estate.WillStatus, estate.FinalWords));
        Assert.Contains(world.ExportState().Events,
            item => item.Kind == "estate_will_default" && item.Detail.EndsWith(":household_selected", StringComparison.Ordinal));
        var settled = await AdvanceUntilSettled(world, estate.Id);
        var heirs = estate.BeneficiaryIds.Where(id => settled.GetAgent(id).Status == SocietyAgentStatus.Active)
            .Order(StringComparer.Ordinal).ToArray();
        Assert.NotEmpty(heirs);
        Assert.Equal(heirs, FinalWordMemories(settled).Select(item => item.OwnerId));
        Assert.DoesNotContain(settled.Inventory.Lots, lot => lot.OwnerId == estate.Id);
    }

    [Fact]
    public async Task MissingPersonalModelKeepsHouseholdInheritanceDefault()
    {
        using var world = NewWorld(new DeterministicDecisionProvider());

        Assert.True((await world.AdvanceOneTickNonBlockingAsync()).Advanced);

        var estate = world.Society.Estates.Single(item => item.DeceasedId == "founder-scout");
        Assert.Equal("default", estate.WillStatus);
        Assert.Null(estate.WillHeirIds);
        Assert.Contains(world.ExportState().Events,
            item => item.Kind == "estate_will_default" && item.Detail.EndsWith(":no_personal_model", StringComparison.Ordinal));
    }

    [Fact]
    public async Task FailureAndInterruptedPendingCallUseDefaultWithoutRetryAfterReload()
    {
        var failing = new WillProvider(CognitionWillContext.HouseholdCandidateId, fail: true);
        var directory = Directory.CreateTempSubdirectory("postdeath-will-logs-");
        try
        {
            var presence = new OwnerClientPresenceLease(TimeSpan.FromMinutes(1));
            presence.RecordAuthenticatedReconnect("owner");
            var logger = new RecordingLogger<WorldRuntimeService>();
            using var world = NewWorld(failing);
            using var service = new WorldRuntimeService(world,
                new WorldStateFile(Path.Combine(directory.FullName, "world.json")), presence, logger);
            Assert.True(await service.TryAdvanceOnceAsync());
            await failing.Called.Task.WaitAsync(TimeSpan.FromSeconds(5));
            for (var attempt = 0; attempt < 8 &&
                 world.Society.Estates.Single(item => item.DeceasedId == "founder-scout").WillStatus == "pending"; attempt++)
            {
                Assert.True(await service.TryAdvanceOnceAsync());
                await Task.Delay(10);
            }
            Assert.Equal("default", world.Society.Estates.Single(item => item.DeceasedId == "founder-scout").WillStatus);
            Assert.Equal(1, failing.CallCount);
            Assert.DoesNotContain(world.ExportState().Events,
                item => item.Detail.Contains("provider secret", StringComparison.Ordinal));
            Assert.Contains(logger.Messages, message =>
                message.Contains("estate_will", StringComparison.Ordinal) &&
                message.Contains("outcome=default", StringComparison.Ordinal) &&
                message.Contains("reason=provider_InvalidOperationException", StringComparison.Ordinal));
            Assert.DoesNotContain(logger.Messages, message => message.Contains("provider secret", StringComparison.Ordinal));
        }
        finally
        {
            directory.Delete(recursive: true);
        }

        var held = new WillProvider(CognitionWillContext.HeirsCandidateId, observation =>
            new CognitionWillChoice([HeirKey(observation, "Mira")], CognitionWillContext.EqualSplit), hold: true);
        using var pending = NewWorld(held);
        Assert.True((await pending.AdvanceOneTickNonBlockingAsync()).Advanced);
        await held.Called.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var bytes = WorldRuntimeCodec.Encode(pending.ExportState());
        pending.CancelPendingModelCalls();
        await held.Cancelled.Task.WaitAsync(TimeSpan.FromSeconds(5));
        using var restored = WorldRuntime.Restore(WorldRuntimeCodec.Decode(bytes),
            id => id == "founder-scout" ? held : new DeterministicDecisionProvider());
        Assert.True((await restored.AdvanceOneTickNonBlockingAsync()).Advanced);
        Assert.Equal("default", restored.Society.Estates.Single(item => item.DeceasedId == "founder-scout").WillStatus);
        Assert.Equal(1, held.CallCount);
    }

    [Fact]
    public async Task LateProviderResponseCannotOverrideWorldDeadline()
    {
        var late = new WillProvider(CognitionWillContext.HeirsCandidateId, observation =>
            new CognitionWillChoice([HeirKey(observation, "Mira")], CognitionWillContext.EqualSplit),
            hold: true, ignoreCancellation: true);
        using var world = NewWorld(late);
        Assert.True((await world.AdvanceOneTickNonBlockingAsync()).Advanced);
        await late.Called.Task.WaitAsync(TimeSpan.FromSeconds(5));
        for (var tick = 0; tick < 28; tick++)
            Assert.True((await world.AdvanceOneTickNonBlockingAsync()).Advanced);
        Assert.Equal("pending", world.Society.Estates.Single(item => item.DeceasedId == "founder-scout").WillStatus);

        late.ReleaseHeldResponse();
        await late.Completed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await Task.Delay(50);
        Assert.True((await world.AdvanceOneTickNonBlockingAsync()).Advanced);

        Assert.Equal("default", world.Society.Estates.Single(item => item.DeceasedId == "founder-scout").WillStatus);
        Assert.Equal(1, late.CallCount);
        Assert.Contains(world.ExportState().Events,
            item => item.Kind == "estate_will_default" && item.Detail.EndsWith(":deadline", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ProviderEpochChangeCancelsPendingWillAndDefaultsWithoutWaitingForDeadline()
    {
        var held = new WillProvider(CognitionWillContext.HeirsCandidateId, observation =>
            new CognitionWillChoice([HeirKey(observation, "Mira")], CognitionWillContext.EqualSplit), hold: true);
        using var world = NewWorld(held);
        Assert.True((await world.AdvanceOneTickNonBlockingAsync()).Advanced);
        await held.Called.Task.WaitAsync(TimeSpan.FromSeconds(5));

        held.ProviderEpoch++;
        Assert.True((await world.AdvanceOneTickNonBlockingAsync()).Advanced);
        await held.Cancelled.Task.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal("default", world.Society.Estates.Single(item => item.DeceasedId == "founder-scout").WillStatus);
        Assert.Contains(world.ExportState().Events,
            item => item.Kind == "estate_will_default" && item.Detail.EndsWith(":provider_changed", StringComparison.Ordinal));
        Assert.Equal(1, held.CallCount);
    }

    internal static string HeirKey(AgentObservation observation, string name) =>
        observation.Will!.Heirs.Single(heir => heir.Name == name).Key;

    internal static string ItemKey(AgentObservation observation, string kind) =>
        observation.Will!.Items.Single(item => item.Kind == kind).Key;

    private static (string OwnerId, string LotId, int Quantity)[] Inherited(SocietyCheckpoint checkpoint) =>
        checkpoint.Inventory.Lots.Where(lot => lot.ProvenanceLotId is "rope-lot" or "seed-lot" or "stone-lot")
            .Select(lot => (lot.OwnerId, lot.ProvenanceLotId!, lot.Quantity))
            .OrderBy(item => item.OwnerId, StringComparer.Ordinal).ThenBy(item => item.Item2, StringComparer.Ordinal)
            .ToArray();

    private static (string OwnerId, string Summary)[] FinalWordMemories(SocietyCheckpoint checkpoint) =>
        checkpoint.Memories.Where(memory => memory.Id.StartsWith("final-words:", StringComparison.Ordinal))
            .Select(memory => (memory.OwnerId, memory.Summary))
            .OrderBy(item => item.OwnerId, StringComparer.Ordinal).ToArray();

    private static WorldRuntime NewWorld(IDecisionProvider provider, bool childHeir = false, bool vessels = false)
    {
        using var seed = new WorldRuntime("postdeath-will-seed");
        var state = seed.ExportState();
        var checkpoint = state.Society.Society;
        var inventory = checkpoint.Inventory;
        inventory = InventoryAuthority.AddLot(inventory, "seed-lot", "seed", "founder-scout", 3);
        inventory = InventoryAuthority.AddLot(inventory, "stone-lot", "stone", "founder-scout", 5);
        inventory = InventoryAuthority.AddLot(inventory, "rope-lot", "rope", "founder-scout", 1);
        if (vessels)
        {
            inventory = InventoryAuthority.AddLot(inventory, "jug-lot", InventoryContainerRules.WaterJug, "founder-scout", 1);
            inventory = InventoryAuthority.AddLot(inventory, "jug-water", InventoryContainerRules.FreshWater, "founder-scout", 3,
                containerLotId: "jug-lot");
            inventory = InventoryAuthority.AddLot(inventory, "pot-lot", InventoryContainerRules.StoragePot, "founder-scout", 1);
            inventory = InventoryAuthority.AddLot(inventory, "pot-berries", "berries", "founder-scout", 2, containerLotId: "pot-lot");
        }
        checkpoint = checkpoint with { Inventory = inventory };
        if (childHeir)
        {
            var birth = checkpoint.LifeTickAt(checkpoint.WorldTick) - 4 * checkpoint.Config.TicksPerLifecycleAge;
            checkpoint = checkpoint with
            {
                Agents = checkpoint.Agents.Select(person => person.Id == "founder-ilya" ? person with
                {
                    BirthTick = birth,
                    BirthLifeTick = checkpoint.LifeClock is null ? null : birth,
                    AgeBand = SocietyAgeBand.Child,
                    LastLifecycleYearChecked = 4,
                    CurrentRole = SocietyWorkRole.Unassigned,
                } : person).ToArray(),
            };
            checkpoint = SocietyRules.AcceptDependentGuardianship(checkpoint, "founder-mira", "founder-ilya").Checkpoint;
            Assert.True(SocietyRules.HasActivePrimaryCaregiver(checkpoint, "founder-ilya"));
            Assert.Equal(checkpoint.GetAgent("founder-mira").DomesticFamilyUnitId,
                checkpoint.GetAgent("founder-ilya").DomesticFamilyUnitId);
        }
        var physical = state.Agents.Single(item => item.AgentId == "founder-scout");
        checkpoint = SocietyRules.Kill(checkpoint, "founder-scout", SocietyDeathCause.Accident).Checkpoint;
        // This fixture archives the death directly, so perform the same physical
        // drop as the runtime before removing the deceased's physical state.
        var estateId = checkpoint.Estates.Single(estate => estate.DeceasedId == "founder-scout").Id;
        inventory = checkpoint.Inventory;
        if (inventory.Lots.Any(lot => lot.CarrierId == "founder-scout"))
            inventory = InventoryAuthority.DropCarrierGoods(inventory, "founder-scout",
                new InventoryGroundPosition(physical.Position.X, physical.Position.Y));
        foreach (var lot in inventory.Lots.Where(lot => lot.OwnerId == estateId && lot.ContainerLotId is null &&
                     lot.CarrierId is null && lot.StorageBuildingId is null && lot.GroundPosition is null).ToArray())
            inventory = InventoryAuthority.Relocate(inventory, $"death:founder-scout:{lot.Id}", lot.Id, estateId, lot.Quantity,
                groundPosition: new InventoryGroundPosition(physical.Position.X, physical.Position.Y));
        checkpoint = checkpoint with { Inventory = inventory };
        // Bounded test clock: settle a few ticks after the will instead of after a world day.
        checkpoint = checkpoint with
        {
            Estates = checkpoint.Estates.Select(estate => estate with { ExpiryTick = estate.CreatedTick + 4 }).ToArray(),
        };
        var deceased = checkpoint.GetAgent("founder-scout");
        state = state with
        {
            Society = state.Society with { Society = checkpoint },
            Agents = state.Agents.Where(item => item.AgentId != "founder-scout")
                .Select(item => childHeir && item.AgentId == "founder-ilya"
                    ? item with { LastDecisionContext = null } : item).ToArray(),
            DeceasedAgents = [new DeceasedAgentState("founder-scout", 0,
                checkpoint.AgeAt(deceased, 0), physical)],
        };
        return WorldRuntime.Restore(state, id => id == "founder-scout" ? provider : new DeterministicDecisionProvider());
    }

    private static async Task AdvanceUntilResolved(WorldRuntime world, string deceasedId = "founder-scout")
    {
        for (var attempt = 0; attempt < 8; attempt++)
        {
            if (world.Society.Estates.Single(item => item.DeceasedId == deceasedId).WillStatus != "pending") return;
            Assert.True((await world.AdvanceOneTickNonBlockingAsync()).Advanced);
            await Task.Delay(10);
        }
        Assert.Fail("The final will did not resolve within the bounded test window.");
    }

    private static async Task<SocietyCheckpoint> AdvanceUntilSettled(WorldRuntime world, string estateId)
    {
        for (var attempt = 0; attempt < 12 && !world.Society.GetEstate(estateId).Settled; attempt++)
            Assert.True((await world.AdvanceOneTickNonBlockingAsync()).Advanced);
        Assert.True(world.Society.GetEstate(estateId).Settled);
        return world.Society;
    }

    internal sealed class WillProvider(
        string choice,
        Func<AgentObservation, CognitionWillChoice?>? will = null,
        bool fail = false,
        bool hold = false,
        bool ignoreCancellation = false) : IDecisionProvider
    {
        private readonly TaskCompletionSource<bool> release = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public DecisionProviderKind Kind => DecisionProviderKind.LargeLanguageModel;
        public long ProviderEpoch { get; set; } = 42;
        public int CallCount { get; private set; }
        public List<AgentObservation> Observations { get; } = [];
        public TaskCompletionSource<bool> Called { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<bool> Cancelled { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<bool> Completed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public void ReleaseHeldResponse() => release.TrySetResult(true);

        public async ValueTask<CognitionDecisionResponse> DecideAsync(CognitionDecisionRequest request,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            Observations.Add(request.Observation);
            Called.TrySetResult(true);
            if (hold)
            {
                if (ignoreCancellation)
                {
                    await release.Task.ConfigureAwait(false);
                }
                else
                {
                    try { await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken); }
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                    {
                        Cancelled.TrySetResult(true);
                        throw;
                    }
                }
            }
            if (fail) throw new InvalidOperationException("provider secret must not enter world events");
            var response = new CognitionDecisionResponse(request.RequestId, request.Observation.AgentId,
                Kind, request.ProviderEpoch, request.Observation.RunEpoch,
                request.Observation.DecisionGeneration, request.Observation.ObservationDigest,
                choice, 1, new Dictionary<string, double> { [choice] = 1 },
                Will: will?.Invoke(request.Observation));
            Completed.TrySetResult(true);
            return response;
        }
    }
}
