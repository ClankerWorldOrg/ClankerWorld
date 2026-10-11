using System.Reflection;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Harness;
using ClankerWorld.Simulation.Playtest;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.Tests;

public sealed partial class GrownAgentHelperMemoryTests
{
    [Theory]
    [InlineData("KnowledgeFactId")]
    [InlineData("RecipeKnowledgeId")]
    [InlineData("CognitionDiscovererId")]
    [InlineData("CognitionHouseholdId")]
    [InlineData("CognitionAgentId")]
    [InlineData("CivicAgentToken")]
    [InlineData("FoodArrivalReceipt")]
    [InlineData("BoatOrderArrivalEffectId")]
    [InlineData("GuardianOrderEffectId")]
    [InlineData("BoundedWillHeirKey")]
    [InlineData("ExpansionOrderReceipt")]
    [InlineData("ConstructionInstanceId")]
    [InlineData("ConstructionOrderReceipt")]
    [InlineData("ProductionOrderReceipt")]
    [InlineData("ToolMakingRequestId")]
    [InlineData("ToolRequestChoice")]
    [InlineData("OrnamentChoiceId")]
    [InlineData("TownProjectChoiceId")]
    [InlineData("MarketChoiceId")]
    [InlineData("TownSalvageId")]
    [InlineData("AnimalKey")]
    [InlineData("CartOrderEffectId")]
    [InlineData("CartCargoOrderEffectId")]
    [InlineData("CartRepairOrderReceipt")]
    [InlineData("TalkOrderEffectId")]
    [InlineData("ShelterOrderReceipt")]
    [InlineData("KnowledgeReadOrderEffectId")]
    [InlineData("CivicRoundToken")]
    [InlineData("NonviolentReportToken")]
    [InlineData("NonviolentOfferToken")]
    [InlineData("NonviolentCaseToken")]
    [InlineData("NonviolentActionToken")]
    [InlineData("LandHearingEvidenceId")]
    [InlineData("OrchardReplantingReservationId")]
    public async Task NativeDescendantPrivateFactoryInventoryKeepsBoundedRecallKeysAfterStrictReload(string factory)
    {
        var state = PrivateWorldRuntimeCodec.Decode(await NativeFiftyMilkTrace.Value);
        var births = state.Society.Society.Births.OrderBy(birth => birth.CommittedTick).ToArray();
        Assert.Equal(3, births.Length);
        var actor = births[^1].ChildId;
        var relative = births[^2].ChildId;
        Assert.Contains(state.Society.Society.Relationships, edge => edge.Type == SocietyRelationshipType.BiologicalParentage &&
            edge.ProposerId == relative && edge.TargetId == actor);
        var society = state.Society.Society;
        var position = state.Inhabitants.Single(person => person.InhabitantId == actor).Position;
        var cartId = "cart:" + new string('c', 123);
        society = society with
        {
            Inventory = ClankerWorld.Simulation.Kernel.InventoryFixture.AddLot(society.Inventory,
            cartId, "handcart", actor, 1, groundPosition: new(position.X, position.Y))
        };
        var seedId = "orchard-seed:" + new string('s', 115);
        if (factory == "OrchardReplantingReservationId")
            society = society with
            {
                Inventory = ClankerWorld.Simulation.Kernel.InventoryFixture.AddLot(society.Inventory,
                seedId, TreeGrowthRules.OrchardSeedItem, actor, TreeGrowthRules.OrchardSeedsPerPick)
            };
        var home = society.GetInhabitant(actor).HouseholdId!;
        society = society with
        {
            Households = society.Households.Select(household =>
            household.Id == home ? household with { Name = "Household " + new string('h', 118) } : household).ToArray()
        };
        var sourceLot = Assert.Single(society.Inventory.Lots, lot => lot.ItemKind == "milk").Id;
        var building = state.WorldSimulation!.Buildings.OrderBy(record => record.InstanceId, StringComparer.Ordinal).Last().InstanceId;
        var recipe = state.WorldContent!.Recipes.OrderBy(record => record.CanonicalId, StringComparer.Ordinal).Last().CanonicalId;
        // Pure factory inputs: the world/people/stock are native; the owner-task
        // record is a controlled supported shape, not an admitted model reply.
        var action = factory switch
        {
            "CartCargoOrderEffectId" => "load_handcart",
            "TalkOrderEffectId" => "talk",
            "ShelterOrderReceipt" => "tend_fire",
            "KnowledgeReadOrderEffectId" => "read_knowledge",
            _ => "repair_handcart",
        };
        var order = new OwnerInstructionOrder(action, "finished", 1, 1, "tasks", false, TargetAgentId: relative)
        {
            TargetCartLotId = cartId,
            TargetItemKind = "wood",
            TargetKnowledgeArtifactId = "knowledge-artifact-000512",
            KnowledgeReadCompletion = new(society.WorldTick, [position]) { LearnedRecipes = [recipe] },
        };
        var instruction = new OwnerQueuedInstruction("instruction:" + new string('i', 116), "inventory-task", "owner:test",
            actor, OwnerInstructionKind.MustDo, "Remember this completed work.", society.WorldTick,
            society.RunEpoch, 1, OwnerInstructionState.Queued, Order: order);
        var owner = factory == "CognitionAgentId" ? typeof(PrivateWorldMemoryRetrieval) : typeof(PrivateWorldRuntime);
        var townId = state.Towns![0].Id;
        var allegation = new TownViolationAllegation(sourceLot, actor, "taking", position, society.WorldTick,
            instruction.InstructionId, int.MaxValue, "The stock was moved.", [sourceLot]);
        var file = new TownViolationCase(instruction.InstructionId, "key", townId, allegation, society.WorldTick, "open",
            [new(int.MaxValue, [], "notice", society.WorldTick, society.WorldTick + 1)], [], [], [], [], [], null, [], [], null, [], [], []);
        var offer = new TownRemedyOffer(instruction.InstructionId, file.Id, sourceLot, int.MaxValue, [],
            TownHearingProcedure.Digest(new[] { actor, relative, sourceLot }), "Return the stock.", "notice", society.WorldTick,
            society.WorldTick + 1, 1, "pending", []);
        var landFile = new TownLandCase(file.Id, "key", "request", society.WorldTick, "open",
            [new(int.MaxValue, [position], [], [], "notice", society.WorldTick, society.WorldTick + 1, new("grant", home))],
            [], [], [], [], null, [], [], null, [], [])
        { TownId = townId };
        object?[] arguments = factory switch
        {
            "KnowledgeFactId" => [actor, position],
            "RecipeKnowledgeId" => [actor, recipe],
            "CognitionDiscovererId" or "CognitionAgentId" or "CivicAgentToken" or "AnimalKey" => [actor],
            "CognitionHouseholdId" => [home],
            "FoodArrivalReceipt" => [sourceLot, position],
            "BoatOrderArrivalEffectId" or "ExpansionOrderReceipt" or "ProductionOrderReceipt" => [sourceLot],
            "ConstructionInstanceId" => [instruction.InstructionId],
            "ConstructionOrderReceipt" => [building],
            "GuardianOrderEffectId" => [actor, relative],
            "BoundedWillHeirKey" => ["will:heir:", actor, new HashSet<string>(StringComparer.Ordinal)],
            "ToolMakingRequestId" => [actor, building, recipe, society.WorldTick],
            "ToolRequestChoice" => ["accept", new[] { actor, building, recipe, sourceLot }],
            "OrnamentChoiceId" => ["ornament:", new[] { actor, relative, sourceLot }],
            "TownProjectChoiceId" => ["town-project:", new[] { actor, building, sourceLot }],
            "MarketChoiceId" => ["deposit", new[] { actor, building, sourceLot }],
            "TownSalvageId" => [state.Towns![0].Id, sourceLot],
            "CartOrderEffectId" or "TalkOrderEffectId" or "KnowledgeReadOrderEffectId" => [instruction],
            "CartCargoOrderEffectId" => [instruction, int.MaxValue],
            "CartRepairOrderReceipt" => [instruction, society.WorldTick],
            "ShelterOrderReceipt" => [instruction.InstructionId, order],
            "CivicRoundToken" => [new TownElection(instruction.InstructionId, "regular", "main", society.WorldTick,
                society.WorldTick + 1, society.WorldTick + 10, 1, [actor, relative], [actor, relative], [], [], [], [])],
            "NonviolentReportToken" => [allegation],
            "NonviolentOfferToken" => [offer],
            "NonviolentCaseToken" or "NonviolentActionToken" => [file],
            "LandHearingEvidenceId" => [landFile, actor, "record", sourceLot],
            "OrchardReplantingReservationId" => [seedId],
            _ => throw new ArgumentOutOfRangeException(nameof(factory)),
        };
        var method = owner.GetMethod(factory, BindingFlags.Static | BindingFlags.NonPublic)!;
        Assert.NotNull(method);
        var id = Assert.IsType<string>(method.Invoke(null, arguments));
        Assert.InRange(id.Length, 1, DerivedId.MaximumLength);
        if (factory == "OrchardReplantingReservationId")
            society = society with
            {
                Inventory = ClankerWorld.Simulation.Kernel.InventoryFixture.Reserve(society.Inventory,
                id, actor, seedId, 1, "orchard_replanting", long.MaxValue)
            };
        var summary = "I remember factory" + factory + " work.";
        var checkpoint = SocietyFixture.RecordSocialMemory(society, new(id, actor, relative, summary,
            "private", society.WorldTick)).Checkpoint;
        var bytes = PrivateWorldRuntimeCodec.Encode(state with { Society = state.Society with { Society = checkpoint } });
        using var restored = PrivateWorldRuntime.Restore(PrivateWorldRuntimeCodec.Decode(bytes));
        Assert.Equal(bytes, PrivateWorldRuntimeCodec.Encode(restored.ExportState()));
        var recalled = PrivateWorldMemoryRetrieval.Retrieve(restored.Society.Memories, restored.Society.Beliefs ?? [],
            null, actor, restored.WorldTick, [new("safe_idle", summary, 0)], restored.Society);
        var memory = Assert.Single(recalled, memory => memory.Id == id);
        Assert.Equal((actor, relative, summary), (memory.OwnerId, memory.SubjectId, memory.Summary));
        new InhabitantObservation(actor, restored.WorldTick, 0, 1, "sha256:factory-inventory", 0,
            [new("safe_idle", summary, 0)], RetrievedMemories: recalled).Validate();
    }
}
