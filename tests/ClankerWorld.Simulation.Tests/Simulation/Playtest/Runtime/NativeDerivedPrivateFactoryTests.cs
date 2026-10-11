using System.Reflection;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Harness;
using ClankerWorld.Simulation.Playtest;
using ClankerWorld.Simulation.Society;
using ClankerWorld.Simulation.World;

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
    [InlineData("MedicalDoseId")]
    [InlineData("FoodHarvestLotId")]
    [InlineData("OrchardSeedLotId")]
    [InlineData("MaterialLotId")]
    [InlineData("TreeSeedLotId")]
    [InlineData("UsedToolLotId")]
    [InlineData("RepairedToolLotId")]
    [InlineData("ToolRepairReservationId")]
    [InlineData("ToolRepairGiftId")]
    [InlineData("FieldPlantingReservationId")]
    [InlineData("FieldToolLotId")]
    [InlineData("FieldHarvestId-crop")]
    [InlineData("FieldHarvestId-seed")]
    [InlineData("FieldHarvestId-replant")]
    [InlineData("EquipmentTransferId")]
    [InlineData("EquippedUnitId")]
    [InlineData("EquipmentRepairReservationId")]
    [InlineData("OrnamentUnitId")]
    [InlineData("ProductionInputReservationId")]
    [InlineData("ProductionOutputLotId")]
    [InlineData("ProductionVesselUnitId")]
    [InlineData("ExpansionMaterialReservationId")]
    [InlineData("TreePlantingReceipt")]
    [InlineData("GuardianCareId")]
    [InlineData("GuardianMembershipId")]
    [InlineData("GuardianMembershipId-collision")]
    [InlineData("DependentCareId")]
    [InlineData("ReplacementCareId")]
    [InlineData("DomesticPartnershipId")]
    [InlineData("DomesticSeparatedId")]
    [InlineData("EstateId")]
    [InlineData("EstateSplitLotId")]
    [InlineData("EstateSplitLotId-storage")]
    [InlineData("FinalWordsMemoryId")]
    [InlineData("ParenthoodRequestId")]
    [InlineData("SettlementPartnershipId")]
    [InlineData("SettlementCareId")]
    [InlineData("SoloHouseholdId")]
    [InlineData("DepartureTransferId")]
    [InlineData("PersonalMoveId")]
    [InlineData("ChildSocialMemoryId-converse")]
    [InlineData("ChildSocialMemoryId-play")]
    [InlineData("ChildSocialMemoryId-learn")]
    [InlineData("LessonGratitudeMemoryId")]
    [InlineData("ProjectGratitudeMemoryId")]
    [InlineData("SettlementTrustMemoryId")]
    [InlineData("CartRepairReservationId")]
    [InlineData("NativeCartRepairId")]
    [InlineData("FoodPotTransferId")]
    [InlineData("RoadRouteId")]
    [InlineData("SettlementTradeOfferId")]
    [InlineData("BusinessTradeOfferId")]
    [InlineData("WritingProjectId")]
    [InlineData("WritingMaterialReservationId")]
    [InlineData("WritingMaterialTransferId")]
    [InlineData("ConductSeenId")]
    [InlineData("ConductInspectionId")]
    [InlineData("ConductRelayId")]
    [InlineData("NativeReturnId")]
    [InlineData("WarehouseStockId")]
    [InlineData("RemedyEffectId")]
    [InlineData("MaterialGatherReceipt")]
    [InlineData("FoodHarvestReceipt")]
    [InlineData("ConsumptionOrderReceipt")]
    [InlineData("FieldWorkOrderReceipt")]
    [InlineData("ToolRepairReceipt")]
    [InlineData("EquipmentRepairReceipt")]
    [InlineData("PersonalCollectionReceipt")]
    [InlineData("PersonalStorageReceipt")]
    [InlineData("BorrowedReturnReceipt")]
    [InlineData("StockDeliveryReceipt")]
    [InlineData("RenameMemoryId")]
    [InlineData("ConversationId")]
    [InlineData("ConversationMemoryClaimId")]
    [InlineData("ConversationChoiceContextId")]
    [InlineData("ConversationTurnId")]
    [InlineData("ConversationWrapUpId")]
    [InlineData("AnimalSupplyId")]
    [InlineData("MilkStockId")]
    [InlineData("MilkSaleId")]
    [InlineData("AnimalActionId")]
    [InlineData("AnimalAgreementId")]
    [InlineData("AnimalHideId")]
    [InlineData("AnimalProductId")]
    [InlineData("AnimalBornId")]
    [InlineData("AnimalHerdId")]
    [InlineData("AnimalProductReservationId")]
    [InlineData("AnimalSaddleOperationId")]
    [InlineData("AnimalSaddleReservationId")]
    [InlineData("MilkSaleReservationId")]
    [InlineData("DeveloperVesselUnitId")]
    [InlineData("BoatTripId")]
    [InlineData("NonviolentRemedyChoiceId")]
    [InlineData("NonviolentRelayChoiceId")]
    [InlineData("AnimalSupplyTripId")]
    [InlineData("SpoiledMilkChoiceId")]
    [InlineData("AnimalPickupId")]
    [InlineData("AnimalOrderReceiptId")]
    [InlineData("AnimalHideTransferId-collect")]
    [InlineData("AnimalHideTransferId-carry")]
    [InlineData("AnimalInputReservationId")]
    [InlineData("AnimalCargoDismountId")]
    [InlineData("AnimalOfferId")]
    [InlineData("MilkOfferId")]
    [InlineData("AutomaticBuildingId")]
    [InlineData("AutomaticBuildingId-legacy")]
    [InlineData("ReplacementBuildingId")]
    [InlineData("TownSalvageOperationId")]
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
        var toolId = "knife:" + new string('k', 122);
        society = society with
        {
            Inventory = ClankerWorld.Simulation.Kernel.InventoryFixture.AddLot(society.Inventory,
            toolId, "iron_knife", actor, 2, conditionBasisPoints: 3_000)
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
        var animalId = "animal:" + new string('a', 121);
        var building = state.WorldSimulation!.Buildings.OrderBy(record => record.InstanceId, StringComparer.Ordinal).Last().InstanceId;
        var buildingDefinition = state.WorldContent!.Buildings.OrderBy(record => record.CanonicalId, StringComparer.Ordinal).Last();
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
        var owner = factory switch
        {
            "CognitionAgentId" => typeof(PrivateWorldMemoryRetrieval),
            "ConversationId" or "ConversationMemoryClaimId" or "ConversationChoiceContextId" => typeof(ConversationSystem),
            "ConversationTurnId" or "ConversationWrapUpId" => typeof(AgentConversationRules),
            "GuardianCareId" or "GuardianMembershipId" or "GuardianMembershipId-collision" or "DependentCareId" or
                "ReplacementCareId" or "DomesticPartnershipId" or "DomesticSeparatedId" or "EstateId" or
                "EstateSplitLotId" or "EstateSplitLotId-storage" or "FinalWordsMemoryId" => typeof(SocietyFixture),
            _ => typeof(PrivateWorldRuntime),
        };
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
        var estateId = factory is "EstateSplitLotId" or "EstateSplitLotId-storage" or "FinalWordsMemoryId"
            ? Assert.IsType<string>(typeof(SocietyFixture).GetMethod("EstateId", BindingFlags.Static | BindingFlags.NonPublic)!
                .Invoke(null, [actor, society.WorldTick])) : null;
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
            "MedicalDoseId" => [society.WorldTick, society.Inventory.EventHistoryFloor + society.Inventory.Events.Count + 1, actor, relative],
            "FoodHarvestLotId" or "OrchardSeedLotId" or "MaterialLotId" or "TreeSeedLotId" => [society.WorldTick, actor],
            "UsedToolLotId" or "RepairedToolLotId" => [toolId, society.WorldTick, actor],
            "ToolRepairReservationId" or "ToolRepairGiftId" => [society.WorldTick, actor, toolId, "iron"],
            "FieldPlantingReservationId" => [position, int.MaxValue, society.WorldTick, actor],
            "FieldToolLotId" => [toolId, position, society.WorldTick, actor],
            "FieldHarvestId-crop" or "FieldHarvestId-seed" or "FieldHarvestId-replant" => [position, int.MaxValue, factory["FieldHarvestId-".Length..]],
            "EquipmentTransferId" or "EquippedUnitId" => [society.WorldTick, actor, toolId],
            "EquipmentRepairReservationId" => [society.WorldTick, actor, "iron"],
            "OrnamentUnitId" => [society.WorldTick, society.Inventory.EventHistoryFloor + society.Inventory.Events.Count + 1, actor, sourceLot],
            "ProductionInputReservationId" => ["production:inputs:" + building, 1, sourceLot],
            "ProductionOutputLotId" => ["production-" + long.MaxValue, 0],
            "ProductionVesselUnitId" => ["production-" + long.MaxValue + ":output:00", 1],
            "ExpansionMaterialReservationId" => ["expansion-" + long.MaxValue, 1],
            "TreePlantingReceipt" => [TreeGrowthRules.PlantedTreeId(position)],
            "GuardianCareId" => [actor, relative, society.WorldTick],
            "GuardianMembershipId" or "GuardianMembershipId-collision" => [home, actor, society.WorldTick,
                factory == "GuardianMembershipId" ? 0 : 1],
            "DependentCareId" or "ReplacementCareId" => [relative, actor, society.WorldTick],
            "DomesticPartnershipId" => [society.Relationships.Where(edge => edge.Type == SocietyRelationshipType.Partnership)
                .MaxBy(edge => edge.Id.Length)!.Id],
            "DomesticSeparatedId" => [society.Relationships.Where(edge => edge.Type == SocietyRelationshipType.Partnership)
                .MaxBy(edge => edge.Id.Length)!.Id, actor],
            "EstateId" => [actor, society.WorldTick],
            "EstateSplitLotId" or "EstateSplitLotId-storage" => [sourceLot, estateId, relative,
                factory == "EstateSplitLotId-storage" ? building : null],
            "FinalWordsMemoryId" => [estateId, relative],
            "ParenthoodRequestId" or "SoloHouseholdId" => [actor, society.WorldTick],
            "SettlementPartnershipId" or "SettlementCareId" => [actor, relative, society.WorldTick],
            "DepartureTransferId" => [actor, society.WorldTick, 1, sourceLot],
            "PersonalMoveId" => [actor, society.WorldTick, sourceLot],
            "ChildSocialMemoryId-converse" or "ChildSocialMemoryId-play" or "ChildSocialMemoryId-learn" =>
                [factory["ChildSocialMemoryId-".Length..], actor, relative, society.WorldTick],
            "LessonGratitudeMemoryId" => [actor, relative, SettlementSkillKind.Farming],
            "ProjectGratitudeMemoryId" or "SettlementTrustMemoryId" => [actor, relative],
            "CartRepairReservationId" => [society.WorldTick, actor, "wood"],
            "NativeCartRepairId" => [society.WorldTick, actor, cartId],
            "FoodPotTransferId" => [society.WorldTick, actor],
            "RoadRouteId" => [townId, building],
            "SettlementTradeOfferId" => [society.WorldTick, actor, relative],
            "BusinessTradeOfferId" => [society.WorldTick, actor, building],
            "WritingProjectId" => [actor, society.WorldTick, society.Inventory.Reservations.Count],
            "WritingMaterialReservationId" => [Assert.IsType<string>(typeof(PrivateWorldRuntime).GetMethod("WritingProjectId",
                BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, [actor, society.WorldTick, society.Inventory.Reservations.Count])), 1],
            "WritingMaterialTransferId" => [society.WorldTick, actor, "paper"],
            "ConductSeenId" => [sourceLot, actor],
            "ConductInspectionId" => [sourceLot, file.Id, actor],
            "ConductRelayId" => [sourceLot, society.WorldTick, actor, relative],
            "NativeReturnId" => [society.WorldTick, actor, society.Inventory.EventHistoryFloor + society.Inventory.Events.Count + 1],
            "WarehouseStockId" => [society.WorldTick, actor],
            "RemedyEffectId" or "MaterialGatherReceipt" or "FoodHarvestReceipt" or "PersonalCollectionReceipt" or
                "PersonalStorageReceipt" or "BorrowedReturnReceipt" or "StockDeliveryReceipt" => [sourceLot],
            "ConsumptionOrderReceipt" => [society.WorldTick, actor, sourceLot],
            "FieldWorkOrderReceipt" => [actor, society.WorldTick, position, FarmWorkKind.Harvest, int.MaxValue],
            "ToolRepairReceipt" or "EquipmentRepairReceipt" => [actor, society.WorldTick, toolId],
            "RenameMemoryId" => [actor, society.Inventory.EventHistoryFloor + society.Inventory.Events.Count + 1],
            "ConversationId" => [society.WorldTick, actor, relative],
            "ConversationMemoryClaimId" => [AgentConversationRules.ConversationTurnId(ConversationSystem.ConversationId(society.WorldTick, actor, relative),
                AgentConversationRules.MaximumPublicTurns), actor],
            "ConversationChoiceContextId" => [actor, ConversationSystem.ConversationId(society.WorldTick, actor, relative), long.MaxValue, AgentConversationStatus.WrapUp],
            "ConversationTurnId" => [ConversationSystem.ConversationId(society.WorldTick, actor, relative), AgentConversationRules.MaximumPublicTurns],
            "ConversationWrapUpId" => [ConversationSystem.ConversationId(society.WorldTick, actor, relative)],
            "AnimalSupplyId" => [sourceLot, building, animalId, "feed"],
            "MilkStockId" => [society.Inventory.GetLot(sourceLot).ContainerLotId, building],
            "MilkSaleId" => [actor, relative, sourceLot, society.Inventory.GetLot(sourceLot).ContainerLotId,
                society.Inventory.Lots.First(lot => lot.ItemKind == "wood" && lot.OwnerId == relative).Id, building],
            "AnimalActionId" => ["guide", animalId, actor],
            "AnimalAgreementId" => ["buy", animalId, actor, sourceLot],
            "AnimalHideId" => [animalId],
            "AnimalProductId" or "AnimalBornId" => [animalId, society.WorldTick],
            "AnimalHerdId" => [home],
            "AnimalProductReservationId" => [Assert.IsType<string>(typeof(PrivateWorldRuntime).GetMethod("AnimalProductId",
                BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, [animalId, society.WorldTick]))],
            "AnimalSaddleOperationId" => [animalId, society.WorldTick, society.Inventory.EventHistoryFloor + society.Inventory.Events.Count + 1],
            "AnimalSaddleReservationId" => [Assert.IsType<string>(typeof(PrivateWorldRuntime).GetMethod("AnimalSaddleOperationId",
                BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, [animalId, society.WorldTick,
                    society.Inventory.EventHistoryFloor + society.Inventory.Events.Count + 1]))],
            "MilkSaleReservationId" => [Assert.IsType<string>(typeof(PrivateWorldRuntime).GetMethod("MilkSaleId",
                BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, [actor, relative, sourceLot,
                    society.Inventory.GetLot(sourceLot).ContainerLotId,
                    society.Inventory.Lots.First(lot => lot.ItemKind == "wood" && lot.OwnerId == relative).Id, building]))],
            "DeveloperVesselUnitId" => [cartId, int.MaxValue],
            "BoatTripId" => [townId, building, cartId],
            "NonviolentRemedyChoiceId" => [townId, sourceLot, cartId],
            "NonviolentRelayChoiceId" => [townId, sourceLot, relative],
            "AnimalSupplyTripId" => [actor],
            "SpoiledMilkChoiceId" => [society.Inventory.GetLot(sourceLot).ContainerLotId],
            "AnimalPickupId" => [actor, society.WorldTick, long.MaxValue],
            "AnimalOrderReceiptId" => [instruction.InstructionId, animalId, sourceLot],
            "AnimalHideTransferId-collect" or "AnimalHideTransferId-carry" => [actor, animalId, society.WorldTick, factory.EndsWith("-carry", StringComparison.Ordinal)],
            "AnimalInputReservationId" => [actor, society.WorldTick, long.MaxValue, "feed", int.MaxValue],
            "AnimalCargoDismountId" => [actor, society.WorldTick, long.MaxValue, sourceLot],
            "AnimalOfferId" => [actor, animalId, society.WorldTick, long.MaxValue],
            "MilkOfferId" => [sourceLot, society.WorldTick, long.MaxValue],
            "AutomaticBuildingId" => [actor, buildingDefinition],
            "AutomaticBuildingId-legacy" => [new string('b', DerivedId.MaximumLength), buildingDefinition],
            "ReplacementBuildingId" => [Assert.IsType<string>(typeof(PrivateWorldRuntime).GetMethod("AutomaticBuildingId",
                BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, [actor, buildingDefinition])), int.MaxValue],
            "TownSalvageOperationId" => [society.WorldTick, actor, Assert.IsType<string>(typeof(PrivateWorldRuntime).GetMethod("TownSalvageId",
                BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, [townId, sourceLot]))],
            _ => throw new ArgumentOutOfRangeException(nameof(factory)),
        };
        var methodName = factory.Contains('-', StringComparison.Ordinal) ? factory[..factory.IndexOf('-', StringComparison.Ordinal)] : factory;
        var method = owner.GetMethod(methodName,
            BindingFlags.Static | BindingFlags.NonPublic)!;
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
        var subject = factory == "RenameMemoryId" ? actor : relative;
        var checkpoint = SocietyFixture.RecordSocialMemory(society, new(id, actor, subject, summary,
            "private", society.WorldTick, Permanent: factory == "RenameMemoryId")).Checkpoint;
        var bytes = PrivateWorldRuntimeCodec.Encode(state with { Society = state.Society with { Society = checkpoint } });
        using var restored = PrivateWorldRuntime.Restore(PrivateWorldRuntimeCodec.Decode(bytes));
        Assert.Equal(bytes, PrivateWorldRuntimeCodec.Encode(restored.ExportState()));
        var recalled = PrivateWorldMemoryRetrieval.Retrieve(restored.Society.Memories, restored.Society.Beliefs ?? [],
            null, actor, restored.WorldTick, [new("safe_idle", summary, 0)], restored.Society);
        var memory = Assert.Single(recalled, memory => memory.Id == id);
        Assert.Equal((actor, subject, summary), (memory.OwnerId, memory.SubjectId, memory.Summary));
        new InhabitantObservation(actor, restored.WorldTick, 0, 1, "sha256:factory-inventory", 0,
            [new("safe_idle", summary, 0)], RetrievedMemories: recalled).Validate();
    }
}
