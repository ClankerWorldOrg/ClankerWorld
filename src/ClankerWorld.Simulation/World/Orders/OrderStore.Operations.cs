namespace ClankerWorld.Simulation.World;

internal sealed partial class OrderStore
{
    internal static global::ClankerWorld.Simulation.World.OrderTask CreateOrderTask(global::ClankerWorld.Simulation.World.OrderTaskDefinition Definition, string Status, int RequestedUnits, int CompletedUnits, string ProgressUnit, bool RepeatUntilCancelled, bool QuantityIsExplicit = false, string? BlockedReason = null, global::ClankerWorld.Simulation.Identity? LastEffectId = null, bool WaitForDecisionAfterFailure = false) => new(Definition, Status, RequestedUnits, CompletedUnits, ProgressUnit, RepeatUntilCancelled, QuantityIsExplicit, BlockedReason, LastEffectId, WaitForDecisionAfterFailure);

    internal static global::ClankerWorld.Simulation.World.AnimalCareOrderDefinition CreateAnimalCareOrderDefinition(string TargetAnimalId) => new(TargetAnimalId);

    internal static global::ClankerWorld.Simulation.World.AnimalCollectOrderDefinition CreateAnimalCollectOrderDefinition(string TargetAnimalId) => new(TargetAnimalId);

    internal static global::ClankerWorld.Simulation.World.AnimalTameOrderDefinition CreateAnimalTameOrderDefinition(string TargetAnimalId) => new(TargetAnimalId);

    internal static global::ClankerWorld.Simulation.World.AnimalLeadHomeOrderDefinition CreateAnimalLeadHomeOrderDefinition(string TargetAnimalId) => new(TargetAnimalId);

    internal static global::ClankerWorld.Simulation.World.AnimalSaddleOrderDefinition CreateAnimalSaddleOrderDefinition(string TargetAnimalId) => new(TargetAnimalId);

    internal static global::ClankerWorld.Simulation.World.AnimalMountOrderDefinition CreateAnimalMountOrderDefinition(string TargetAnimalId) => new(TargetAnimalId);

    internal static global::ClankerWorld.Simulation.World.AnimalDismountOrderDefinition CreateAnimalDismountOrderDefinition(string TargetAnimalId) => new(TargetAnimalId);

    internal static global::ClankerWorld.Simulation.World.OwnerQueuedInstruction PrepareExecuteBoatOrderOwnerQueuedInstruction(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction basis, global::ClankerWorld.Simulation.World.OrderTask? value0) => basis with { Task = value0 };

    internal static global::ClankerWorld.Simulation.World.TravelByBoatOrderDefinition PrepareExecuteBoatOrderTravelByBoatOrderDefinition(global::ClankerWorld.Simulation.World.TravelByBoatOrderDefinition basis, global::ClankerWorld.Simulation.World.OwnerBoatTravelBinding value0) => basis with { BoatTravel = value0 };

    internal static global::ClankerWorld.Simulation.World.OwnerBoatTravelBinding PrepareExecuteBoatOrderOwnerBoatTravelBinding(global::ClankerWorld.Simulation.World.OwnerBoatTravelBinding basis, string? value0) => basis with { RequestId = value0 };

    internal static global::ClankerWorld.Simulation.World.ConstructBuildingOrderDefinition PrepareExecuteConstructionOrderStepConstructBuildingOrderDefinition(global::ClankerWorld.Simulation.World.ConstructBuildingOrderDefinition basis, string? value0, global::ClankerWorld.Simulation.World.GridPoint? value1, long? value2, string? value3) => basis with { ConstructionOwnerId = value0, ConstructionPosition = value1, ConstructionStartedTick = value2, ConstructionInstanceId = value3 };

    internal static global::ClankerWorld.Simulation.World.StoreMaterialOrderDefinition PrepareBindCustodyOrderStoreMaterialOrderDefinition(global::ClankerWorld.Simulation.World.StoreMaterialOrderDefinition basis, string? value0, string? value1, global::ClankerWorld.Simulation.World.GridPoint? value2) => basis with { TargetStorageBuildingId = value0, TargetStorageOwnerId = value1, TargetStoragePosition = value2 };

    internal static global::ClankerWorld.Simulation.World.StoreEquipmentOrderDefinition PrepareBindCustodyOrderStoreEquipmentOrderDefinition(global::ClankerWorld.Simulation.World.StoreEquipmentOrderDefinition basis, string? value0, string? value1, global::ClankerWorld.Simulation.World.GridPoint? value2) => basis with { TargetStorageBuildingId = value0, TargetStorageOwnerId = value1, TargetStoragePosition = value2 };

    internal static global::ClankerWorld.Simulation.World.StoreGoodsOrderDefinition PrepareBindCustodyOrderStoreGoodsOrderDefinition(global::ClankerWorld.Simulation.World.StoreGoodsOrderDefinition basis, string? value0, string? value1, global::ClankerWorld.Simulation.World.GridPoint? value2) => basis with { TargetStorageBuildingId = value0, TargetStorageOwnerId = value1, TargetStoragePosition = value2 };

    internal static global::ClankerWorld.Simulation.World.ReturnBorrowedOrderDefinition PrepareBindCustodyOrderReturnBorrowedOrderDefinition(global::ClankerWorld.Simulation.World.ReturnBorrowedOrderDefinition basis, string? value0, string? value1, global::ClankerWorld.Simulation.World.GridPoint? value2) => basis with { TargetStorageBuildingId = value0, TargetStorageOwnerId = value1, TargetStoragePosition = value2 };

    internal static global::ClankerWorld.Simulation.World.DeliverStockOrderDefinition PrepareBindCustodyOrderDeliverStockOrderDefinition(global::ClankerWorld.Simulation.World.DeliverStockOrderDefinition basis, string? value0, string? value1, global::ClankerWorld.Simulation.World.GridPoint? value2) => basis with { TargetStorageBuildingId = value0, TargetStorageOwnerId = value1, TargetStoragePosition = value2 };

    internal static global::ClankerWorld.Simulation.World.ReturnBorrowedOrderDefinition PrepareBindCustodyOrderReturnBorrowedOrderDefinitionTargetLotId(global::ClankerWorld.Simulation.World.ReturnBorrowedOrderDefinition basis, string? value0) => basis with { TargetLotId = value0 };

    internal static global::ClankerWorld.Simulation.World.LoadHandcartOrderDefinition CreateLoadHandcartOrderDefinition(string? TargetItemKind = null, string? TargetCartLotId = null, string? TargetLotId = null) => new(TargetItemKind, TargetCartLotId, TargetLotId);

    internal static global::ClankerWorld.Simulation.World.UnloadHandcartOrderDefinition CreateUnloadHandcartOrderDefinition(string? TargetItemKind = null, string? TargetCartLotId = null, string? TargetLotId = null) => new(TargetItemKind, TargetCartLotId, TargetLotId);

    internal static global::ClankerWorld.Simulation.World.UnloadHandcartGroundOrderDefinition CreateUnloadHandcartGroundOrderDefinition(string? TargetItemKind = null, string? TargetCartLotId = null, string? TargetLotId = null) => new(TargetItemKind, TargetCartLotId, TargetLotId);

    internal static global::ClankerWorld.Simulation.World.LoadHandcartOrderDefinition PrepareExecuteCartCargoOrderStepLoadHandcartOrderDefinition(global::ClankerWorld.Simulation.World.LoadHandcartOrderDefinition basis, string? value0) => basis with { TargetCartLotId = value0 };

    internal static global::ClankerWorld.Simulation.World.UnloadHandcartOrderDefinition PrepareExecuteCartCargoOrderStepUnloadHandcartOrderDefinition(global::ClankerWorld.Simulation.World.UnloadHandcartOrderDefinition basis, string? value0) => basis with { TargetCartLotId = value0 };

    internal static global::ClankerWorld.Simulation.World.UnloadHandcartGroundOrderDefinition PrepareExecuteCartCargoOrderStepUnloadHandcartGroundOrderDefinition(global::ClankerWorld.Simulation.World.UnloadHandcartGroundOrderDefinition basis, string? value0) => basis with { TargetCartLotId = value0 };

    internal static global::ClankerWorld.Simulation.World.LoadHandcartOrderDefinition PrepareExecuteCartCargoOrderStepLoadHandcartOrderDefinitionTargetLotId(global::ClankerWorld.Simulation.World.LoadHandcartOrderDefinition basis, string? value0) => basis with { TargetLotId = value0 };

    internal static global::ClankerWorld.Simulation.World.UnloadHandcartOrderDefinition PrepareExecuteCartCargoOrderStepUnloadHandcartOrderDefinitionTargetLotId(global::ClankerWorld.Simulation.World.UnloadHandcartOrderDefinition basis, string? value0) => basis with { TargetLotId = value0 };

    internal static global::ClankerWorld.Simulation.World.UnloadHandcartGroundOrderDefinition PrepareExecuteCartCargoOrderStepUnloadHandcartGroundOrderDefinitionTargetLotId(global::ClankerWorld.Simulation.World.UnloadHandcartGroundOrderDefinition basis, string? value0) => basis with { TargetLotId = value0 };

    internal static global::ClankerWorld.Simulation.World.AcceptGuardianshipOrderDefinition CreateAcceptGuardianshipOrderDefinition(string TargetAgentId) => new(TargetAgentId);

    internal static global::ClankerWorld.Simulation.World.ProposeMarriageOrderDefinition CreateProposeMarriageOrderDefinition(string? TargetAgentId = null, string? TalkConversationId = null, string? TalkOutcome = null) => new(TargetAgentId, TalkConversationId, TalkOutcome);

    internal static global::ClankerWorld.Simulation.World.MoveToOrderDefinition CreateMoveToOrderDefinition(global::ClankerWorld.Simulation.World.GridPoint TargetPosition) => new(TargetPosition);

    internal static global::ClankerWorld.Simulation.World.SeekFoodOrderDefinition CreateSeekFoodOrderDefinition(string? TargetFoodKind = null, string? TargetResourceId = null, global::ClankerWorld.Simulation.World.GridPoint? TargetPosition = null) => new(TargetFoodKind, TargetResourceId, TargetPosition);

    internal static global::ClankerWorld.Simulation.World.HarvestFoodOrderDefinition CreateHarvestFoodOrderDefinition(string? TargetFoodKind = null, string? TargetResourceId = null, global::ClankerWorld.Simulation.World.GridPoint? TargetPosition = null) => new(TargetFoodKind, TargetResourceId, TargetPosition);

    internal static global::ClankerWorld.Simulation.World.GatherMaterialOrderDefinition CreateGatherMaterialOrderDefinition(string TargetMaterialKind, string? TargetResourceId = null, global::ClankerWorld.Simulation.World.GridPoint? TargetPosition = null) => new(TargetMaterialKind, TargetResourceId, TargetPosition);

    internal static global::ClankerWorld.Simulation.World.CollectMaterialOrderDefinition CreateCollectMaterialOrderDefinition(string TargetMaterialKind, global::ClankerWorld.Simulation.World.GridPoint? TargetPosition = null) => new(TargetMaterialKind, TargetPosition);

    internal static global::ClankerWorld.Simulation.World.StoreMaterialOrderDefinition CreateStoreMaterialOrderDefinition(string TargetMaterialKind, global::ClankerWorld.Simulation.World.GridPoint? TargetPosition = null, string? TargetStorageBuildingId = null, string? TargetStorageOwnerId = null, global::ClankerWorld.Simulation.World.GridPoint? TargetStoragePosition = null) => new(TargetMaterialKind, TargetPosition, TargetStorageBuildingId, TargetStorageOwnerId, TargetStoragePosition);

    internal static global::ClankerWorld.Simulation.World.StoreEquipmentOrderDefinition CreateStoreEquipmentOrderDefinition(string TargetEquipmentKind, global::ClankerWorld.Simulation.World.GridPoint? TargetPosition = null, string? TargetStorageBuildingId = null, string? TargetStorageOwnerId = null, global::ClankerWorld.Simulation.World.GridPoint? TargetStoragePosition = null) => new(TargetEquipmentKind, TargetPosition, TargetStorageBuildingId, TargetStorageOwnerId, TargetStoragePosition);

    internal static global::ClankerWorld.Simulation.World.CollectFoodOrderDefinition CreateCollectFoodOrderDefinition(string? TargetFoodKind = null, global::ClankerWorld.Simulation.World.GridPoint? TargetPosition = null) => new(TargetFoodKind, TargetPosition);

    internal static global::ClankerWorld.Simulation.World.CollectEquipmentOrderDefinition CreateCollectEquipmentOrderDefinition(string TargetEquipmentKind, global::ClankerWorld.Simulation.World.GridPoint? TargetPosition = null) => new(TargetEquipmentKind, TargetPosition);

    internal static global::ClankerWorld.Simulation.World.RepairEquipmentOrderDefinition CreateRepairEquipmentOrderDefinition(string TargetEquipmentKind) => new(TargetEquipmentKind);

    internal static global::ClankerWorld.Simulation.World.RepairToolOrderDefinition CreateRepairToolOrderDefinition(string TargetEquipmentKind) => new(TargetEquipmentKind);

    internal static global::ClankerWorld.Simulation.World.ConsumeFoodOrderDefinition CreateConsumeFoodOrderDefinition(string? TargetFoodKind = null) => new(TargetFoodKind);

    internal static global::ClankerWorld.Simulation.World.SeekShelterOrderDefinition CreateSeekShelterOrderDefinition(global::ClankerWorld.Simulation.World.GridPoint? TargetPosition = null, string? TargetBuildingKind = null, global::ClankerWorld.Simulation.World.OwnerShelterBinding? ShelterBinding = null, global::ClankerWorld.Simulation.World.OwnerShelterCompletion? ShelterCompletion = null) => new(TargetPosition, TargetBuildingKind, ShelterBinding, ShelterCompletion);

    internal static global::ClankerWorld.Simulation.World.TendFireOrderDefinition CreateTendFireOrderDefinition(global::ClankerWorld.Simulation.World.GridPoint? TargetPosition = null, string? TargetBuildingKind = null, global::ClankerWorld.Simulation.World.OwnerShelterBinding? ShelterBinding = null, global::ClankerWorld.Simulation.World.OwnerShelterCompletion? ShelterCompletion = null) => new(TargetPosition, TargetBuildingKind, ShelterBinding, ShelterCompletion);

    internal static global::ClankerWorld.Simulation.World.ConstructBuildingOrderDefinition CreateConstructBuildingOrderDefinition(string TargetDefinitionId, string TargetBuildingKind, global::ClankerWorld.Simulation.World.GridPoint? TargetPosition = null, string? ConstructionOwnerId = null, global::ClankerWorld.Simulation.World.GridPoint? ConstructionPosition = null, long? ConstructionStartedTick = null, string? ConstructionInstanceId = null) => new(TargetDefinitionId, TargetBuildingKind, TargetPosition, ConstructionOwnerId, ConstructionPosition, ConstructionStartedTick, ConstructionInstanceId);

    internal static global::ClankerWorld.Simulation.World.ExpandBuildingOrderDefinition CreateExpandBuildingOrderDefinition(string TargetBuildingKind, global::ClankerWorld.Simulation.World.GridPoint? TargetPosition = null, global::ClankerWorld.Simulation.World.OwnerBuildingExpansionBinding? ExpansionBinding = null) => new(TargetBuildingKind, TargetPosition, ExpansionBinding);

    internal static global::ClankerWorld.Simulation.World.DeliverStockOrderDefinition CreateDeliverStockOrderDefinition(string TargetItemKind, string DeliveryPurpose, global::ClankerWorld.Simulation.World.GridPoint? TargetPosition = null, string? TargetStorageBuildingId = null, string? TargetStorageOwnerId = null, global::ClankerWorld.Simulation.World.GridPoint? TargetStoragePosition = null, string? TargetBuildingKind = null, string? DeliveryRoute = null, string? DeliveryLotId = null, int? DeliveryQuantity = null) => new(TargetItemKind, DeliveryPurpose, TargetPosition, TargetStorageBuildingId, TargetStorageOwnerId, TargetStoragePosition, TargetBuildingKind, DeliveryRoute, DeliveryLotId, DeliveryQuantity);

    internal static global::ClankerWorld.Simulation.World.CollectGoodsOrderDefinition CreateCollectGoodsOrderDefinition(global::ClankerWorld.Simulation.World.GridPoint? TargetPosition = null, string? TargetItemKind = null) => new(TargetPosition, TargetItemKind);

    internal static global::ClankerWorld.Simulation.World.StoreGoodsOrderDefinition CreateStoreGoodsOrderDefinition(global::ClankerWorld.Simulation.World.GridPoint? TargetPosition = null, string? TargetItemKind = null, string? TargetStorageBuildingId = null, string? TargetStorageOwnerId = null, global::ClankerWorld.Simulation.World.GridPoint? TargetStoragePosition = null) => new(TargetPosition, TargetItemKind, TargetStorageBuildingId, TargetStorageOwnerId, TargetStoragePosition);

    internal static global::ClankerWorld.Simulation.World.ReturnBorrowedOrderDefinition CreateReturnBorrowedOrderDefinition(global::ClankerWorld.Simulation.World.GridPoint? TargetPosition = null, string? TargetItemKind = null, string? TargetStorageBuildingId = null, string? TargetStorageOwnerId = null, global::ClankerWorld.Simulation.World.GridPoint? TargetStoragePosition = null, string? TargetLotId = null) => new(TargetPosition, TargetItemKind, TargetStorageBuildingId, TargetStorageOwnerId, TargetStoragePosition, TargetLotId);

    internal static global::ClankerWorld.Simulation.World.CopyKnowledgeOrderDefinition CreateCopyKnowledgeOrderDefinition(string TargetKnowledgeKind, string? KnowledgeWritingProjectId = null, string? KnowledgeCopySourceArtifactId = null) => new(TargetKnowledgeKind, KnowledgeWritingProjectId, KnowledgeCopySourceArtifactId);

    internal static global::ClankerWorld.Simulation.World.WriteKnowledgeOrderDefinition CreateWriteKnowledgeOrderDefinition(string TargetKnowledgeKind, string? KnowledgeWritingProjectId = null) => new(TargetKnowledgeKind, KnowledgeWritingProjectId);

    internal static global::ClankerWorld.Simulation.World.ProduceItemOrderDefinition CreateProduceItemOrderDefinition(string TargetRecipeId, string TargetOutputKind, global::ClankerWorld.Simulation.World.GridPoint? TargetPosition = null, string? ProductionBuildingId = null, string? ProductionJobId = null, long? ProductionProjectStartedTick = null) => new(TargetRecipeId, TargetOutputKind, TargetPosition, ProductionBuildingId, ProductionJobId, ProductionProjectStartedTick);

    internal static global::ClankerWorld.Simulation.World.TillFieldOrderDefinition CreateTillFieldOrderDefinition(global::ClankerWorld.Simulation.World.GridPoint? TargetPosition = null) => new(TargetPosition);

    internal static global::ClankerWorld.Simulation.World.PlantFieldOrderDefinition CreatePlantFieldOrderDefinition(string TargetCropKind, global::ClankerWorld.Simulation.World.GridPoint? TargetPosition = null) => new(TargetCropKind, TargetPosition);

    internal static global::ClankerWorld.Simulation.World.TendFieldOrderDefinition CreateTendFieldOrderDefinition(global::ClankerWorld.Simulation.World.GridPoint? TargetPosition = null, string? TargetCropKind = null) => new(TargetPosition, TargetCropKind);

    internal static global::ClankerWorld.Simulation.World.HarvestFieldOrderDefinition CreateHarvestFieldOrderDefinition(global::ClankerWorld.Simulation.World.GridPoint? TargetPosition = null, string? TargetCropKind = null) => new(TargetPosition, TargetCropKind);

    internal static global::ClankerWorld.Simulation.World.PlantTreeOrderDefinition CreatePlantTreeOrderDefinition(global::ClankerWorld.Simulation.World.GridPoint? TargetPosition = null) => new(TargetPosition);

    internal static global::ClankerWorld.Simulation.World.PlantBroadleafOrderDefinition CreatePlantBroadleafOrderDefinition(global::ClankerWorld.Simulation.World.GridPoint? TargetPosition = null) => new(TargetPosition);

    internal static global::ClankerWorld.Simulation.World.PlantConiferOrderDefinition CreatePlantConiferOrderDefinition(global::ClankerWorld.Simulation.World.GridPoint? TargetPosition = null) => new(TargetPosition);

    internal static global::ClankerWorld.Simulation.World.PlantOrchardOrderDefinition CreatePlantOrchardOrderDefinition(global::ClankerWorld.Simulation.World.GridPoint? TargetPosition = null) => new(TargetPosition);

    internal static global::ClankerWorld.Simulation.World.TravelByBoatOrderDefinition CreateTravelByBoatOrderDefinition(global::ClankerWorld.Simulation.World.OwnerBoatTravelBinding BoatTravel, global::ClankerWorld.Simulation.World.GridPoint? TargetPosition = null) => new(BoatTravel, TargetPosition);

    internal static global::ClankerWorld.Simulation.World.OwnerBoatTravelBinding CreateOwnerBoatTravelBinding(string DestinationPortId, string? RequestId = null) => new(DestinationPortId, RequestId);

    internal static global::ClankerWorld.Simulation.World.TalkToOrderDefinition CreateTalkToOrderDefinition(string TargetAgentId, string? TalkConversationId = null, string? TalkOutcome = null) => new(TargetAgentId, TalkConversationId, TalkOutcome);

    internal static global::ClankerWorld.Simulation.World.TalkToOrderDefinition PrepareExecuteTalkOrderStepTalkToOrderDefinition(global::ClankerWorld.Simulation.World.TalkToOrderDefinition basis, string value0) => basis with { TargetAgentId = value0 };

    internal static global::ClankerWorld.Simulation.World.ProposeMarriageOrderDefinition PrepareExecuteTalkOrderStepProposeMarriageOrderDefinition(global::ClankerWorld.Simulation.World.ProposeMarriageOrderDefinition basis, string? value0) => basis with { TargetAgentId = value0 };

    internal static global::ClankerWorld.Simulation.World.TalkToOrderDefinition PrepareExecuteTalkOrderStepTalkToOrderDefinitionTalkConversationId(global::ClankerWorld.Simulation.World.TalkToOrderDefinition basis, string? value0) => basis with { TalkConversationId = value0 };

    internal static global::ClankerWorld.Simulation.World.ProposeMarriageOrderDefinition PrepareExecuteTalkOrderStepProposeMarriageOrderDefinitionTalkConversationId(global::ClankerWorld.Simulation.World.ProposeMarriageOrderDefinition basis, string? value0) => basis with { TalkConversationId = value0 };

    internal static global::ClankerWorld.Simulation.World.TalkToOrderDefinition PrepareCompleteClosedTalkOrdersTalkToOrderDefinition(global::ClankerWorld.Simulation.World.TalkToOrderDefinition basis, string? value0) => basis with { TalkOutcome = value0 };

    internal static global::ClankerWorld.Simulation.World.ProposeMarriageOrderDefinition PrepareCompleteClosedTalkOrdersProposeMarriageOrderDefinition(global::ClankerWorld.Simulation.World.ProposeMarriageOrderDefinition basis, string? value0) => basis with { TalkOutcome = value0 };

    internal static global::ClankerWorld.Simulation.World.ProduceItemOrderDefinition PrepareExecuteProductionOrderStepProduceItemOrderDefinition(global::ClankerWorld.Simulation.World.ProduceItemOrderDefinition basis, string? value0, string? value1, long? value2) => basis with { ProductionBuildingId = value0, ProductionJobId = value1, ProductionProjectStartedTick = value2 };

    internal static global::ClankerWorld.Simulation.World.ProduceItemOrderDefinition PrepareExecuteProductionOrderStepProduceItemOrderDefinitionProductionJobId(global::ClankerWorld.Simulation.World.ProduceItemOrderDefinition basis, string? value0) => basis with { ProductionJobId = value0 };

    internal static global::ClankerWorld.Simulation.World.OwnerQueuedInstruction CreateOwnerQueuedInstruction(string InstructionId, string IdempotencyKey, string IssuerId, string TargetAgentId, global::ClankerWorld.Simulation.World.OwnerInstructionKind Kind, string Text, long SubmittedTick, long RunEpoch, long SubmissionSequence, global::ClankerWorld.Simulation.World.OwnerInstructionState State, long? ObservedTick = null, string? ObserverReply = null, long? GuidancePromptedTick = null, bool Queue = false, global::ClankerWorld.Simulation.World.OrderTask? Task = null) => new(InstructionId, IdempotencyKey, IssuerId, TargetAgentId, Kind, Text, SubmittedTick, RunEpoch, SubmissionSequence, State, ObservedTick, ObserverReply, GuidancePromptedTick, Queue, Task);

    internal static global::ClankerWorld.Simulation.World.UnknownOrderDefinition CreateUnknownOrderDefinition() => new();

    internal static global::ClankerWorld.Simulation.World.OwnerInstructionReceipt CreateOwnerInstructionReceipt(global::ClankerWorld.Simulation.Identity InstructionId, string IdempotencyKey, long SubmittedTick, long RunEpoch, long Revision) => new(InstructionId, IdempotencyKey, SubmittedTick, RunEpoch, Revision);

    internal static global::ClankerWorld.Simulation.World.RecordedInstructionReceipt CreateRecordedInstructionReceipt(global::ClankerWorld.Simulation.World.OwnerInstructionReceipt Receipt, string RequestDigest) => new(Receipt, RequestDigest);

    internal static global::ClankerWorld.Simulation.World.OwnerOrderControlReceipt CreateOwnerOrderControlReceipt(global::ClankerWorld.Simulation.Identity OrderId, string Status, bool Changed, long WorldTick, long LatestEventId) => new(OrderId, Status, Changed, WorldTick, LatestEventId);

    internal static global::ClankerWorld.Simulation.World.OwnerOrderCancellation CreateOwnerOrderCancellation(string IdempotencyKey, string IssuerId, string WorldId, string TargetAgentId, string OrderId, global::ClankerWorld.Simulation.World.OwnerOrderControlReceipt Receipt) => new(IdempotencyKey, IssuerId, WorldId, TargetAgentId, OrderId, Receipt);

    internal static global::ClankerWorld.Simulation.World.DeliverStockOrderDefinition PrepareBindDeliveryOrderDeliverStockOrderDefinition(global::ClankerWorld.Simulation.World.DeliverStockOrderDefinition basis, string? value0, string? value1, global::ClankerWorld.Simulation.World.GridPoint? value2, string? value3, string? value4, int? value5) => basis with { TargetStorageBuildingId = value0, TargetStorageOwnerId = value1, TargetStoragePosition = value2, DeliveryRoute = value3, DeliveryLotId = value4, DeliveryQuantity = value5 };

    internal static global::ClankerWorld.Simulation.World.DeliverStockOrderDefinition PrepareExecuteDeliveryOrderStepDeliverStockOrderDefinition(global::ClankerWorld.Simulation.World.DeliverStockOrderDefinition basis, string? value0, int? value1) => basis with { DeliveryLotId = value0, DeliveryQuantity = value1 };

    internal static global::ClankerWorld.Simulation.World.OwnerQueuedInstruction PrepareEnqueueDueCognitionOwnerQueuedInstruction(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction basis, long? value0) => basis with { GuidancePromptedTick = value0 };

    internal static global::ClankerWorld.Simulation.World.OwnerQueuedInstruction PrepareApplyObserverGuidanceResultOwnerQueuedInstruction(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction basis, long? value0, string? value1) => basis with { ObservedTick = value0, ObserverReply = value1 };

    internal static global::ClankerWorld.Simulation.World.AttachHandcartOrderDefinition CreateAttachHandcartOrderDefinition(string? TargetCartLotId = null) => new(TargetCartLotId);

    internal static global::ClankerWorld.Simulation.World.ParkHandcartOrderDefinition CreateParkHandcartOrderDefinition(string? TargetCartLotId = null) => new(TargetCartLotId);

    internal static global::ClankerWorld.Simulation.World.RepairHandcartOrderDefinition CreateRepairHandcartOrderDefinition(string? TargetCartLotId = null) => new(TargetCartLotId);

    internal static global::ClankerWorld.Simulation.World.AttachHandcartOrderDefinition PrepareExecuteCartOrderStepAttachHandcartOrderDefinition(global::ClankerWorld.Simulation.World.AttachHandcartOrderDefinition basis, string? value0) => basis with { TargetCartLotId = value0 };

    internal static global::ClankerWorld.Simulation.World.ParkHandcartOrderDefinition PrepareExecuteCartOrderStepParkHandcartOrderDefinition(global::ClankerWorld.Simulation.World.ParkHandcartOrderDefinition basis, string? value0) => basis with { TargetCartLotId = value0 };

    internal static global::ClankerWorld.Simulation.World.RepairHandcartOrderDefinition PrepareExecuteCartOrderStepRepairHandcartOrderDefinition(global::ClankerWorld.Simulation.World.RepairHandcartOrderDefinition basis, string? value0) => basis with { TargetCartLotId = value0 };

    internal static global::ClankerWorld.Simulation.World.ExpandBuildingOrderDefinition PrepareExecuteExpansionOrderStepExpandBuildingOrderDefinition(global::ClankerWorld.Simulation.World.ExpandBuildingOrderDefinition basis, global::ClankerWorld.Simulation.World.OwnerBuildingExpansionBinding? value0) => basis with { ExpansionBinding = value0 };

    internal static global::ClankerWorld.Simulation.World.OwnerBuildingExpansionBinding CreateOwnerBuildingExpansionBinding(string BuildingInstanceId, string DefinitionId, string OwnerId, global::ClankerWorld.Simulation.World.GridPoint ExpectedPosition, int ExpectedRevision, global::ClankerWorld.Simulation.World.GridPoint TargetPosition, global::ClankerWorld.Simulation.World.BuildingFootprintRevision TargetFootprint, string? JobId = null) => new(BuildingInstanceId, DefinitionId, OwnerId, ExpectedPosition, ExpectedRevision, TargetPosition, TargetFootprint, JobId);

    internal static global::ClankerWorld.Simulation.World.OwnerBuildingExpansionBinding PrepareBindStartedExpansionOrderOwnerBuildingExpansionBinding(global::ClankerWorld.Simulation.World.OwnerBuildingExpansionBinding basis, string? value0) => basis with { JobId = value0 };

    internal static global::ClankerWorld.Simulation.World.OwnerShelterBinding CreateOwnerShelterBinding(string Kind, global::ClankerWorld.Simulation.World.GridPoint Position, string? BuildingInstanceId = null, string? DefinitionId = null, string? OwnerId = null, global::ClankerWorld.Simulation.World.GridPoint? BuildingPosition = null, long? BuildingPlacedTick = null) => new(Kind, Position, BuildingInstanceId, DefinitionId, OwnerId, BuildingPosition, BuildingPlacedTick);

    internal static global::ClankerWorld.Simulation.World.SeekShelterOrderDefinition PrepareShelterOrderCandidateForSeekShelterOrderDefinition(global::ClankerWorld.Simulation.World.SeekShelterOrderDefinition basis, global::ClankerWorld.Simulation.World.OwnerShelterBinding? value0) => basis with { ShelterBinding = value0 };

    internal static global::ClankerWorld.Simulation.World.TendFireOrderDefinition PrepareShelterOrderCandidateForTendFireOrderDefinition(global::ClankerWorld.Simulation.World.TendFireOrderDefinition basis, global::ClankerWorld.Simulation.World.OwnerShelterBinding? value0) => basis with { ShelterBinding = value0 };

    internal static global::ClankerWorld.Simulation.World.OwnerShelterCompletion CreateOwnerShelterCompletion(long WorldTick, global::ClankerWorld.Simulation.World.GridPoint Position, global::ClankerWorld.Simulation.Identity? FuelReservationId = null) => new(WorldTick, Position, FuelReservationId);

    internal static global::ClankerWorld.Simulation.World.SeekShelterOrderDefinition PrepareExecuteShelterOrderStepSeekShelterOrderDefinition(global::ClankerWorld.Simulation.World.SeekShelterOrderDefinition basis, global::ClankerWorld.Simulation.World.OwnerShelterCompletion? value0) => basis with { ShelterCompletion = value0 };

    internal static global::ClankerWorld.Simulation.World.TendFireOrderDefinition PrepareExecuteShelterOrderStepTendFireOrderDefinition(global::ClankerWorld.Simulation.World.TendFireOrderDefinition basis, global::ClankerWorld.Simulation.World.OwnerShelterCompletion? value0) => basis with { ShelterCompletion = value0 };

    internal static global::ClankerWorld.Simulation.World.CopyKnowledgeOrderDefinition PrepareExecuteKnowledgeOrderStepCopyKnowledgeOrderDefinition(global::ClankerWorld.Simulation.World.CopyKnowledgeOrderDefinition basis, string? value0) => basis with { KnowledgeCopySourceArtifactId = value0 };

    internal static global::ClankerWorld.Simulation.World.WriteKnowledgeOrderDefinition PrepareExecuteKnowledgeOrderStepWriteKnowledgeOrderDefinition(global::ClankerWorld.Simulation.World.WriteKnowledgeOrderDefinition basis, string? value0) => basis with { KnowledgeWritingProjectId = value0 };

    internal static global::ClankerWorld.Simulation.World.CopyKnowledgeOrderDefinition PrepareExecuteKnowledgeOrderStepCopyKnowledgeOrderDefinitionKnowledgeWritingProjectId(global::ClankerWorld.Simulation.World.CopyKnowledgeOrderDefinition basis, string? value0, string? value1) => basis with { KnowledgeWritingProjectId = value0, KnowledgeCopySourceArtifactId = value1 };

    internal static global::ClankerWorld.Simulation.World.CopyKnowledgeOrderDefinition PrepareClearKnowledgeOrderBindingCopyKnowledgeOrderDefinition(global::ClankerWorld.Simulation.World.CopyKnowledgeOrderDefinition basis, string? value0) => basis with { KnowledgeWritingProjectId = value0 };

    internal static global::ClankerWorld.Simulation.World.OrderTask PrepareCreditOrderEffectOrderTask(global::ClankerWorld.Simulation.World.OrderTask basis, int value0, global::ClankerWorld.Simulation.Identity? value1) => basis with { CompletedUnits = value0, LastEffectId = value1 };

    internal static global::ClankerWorld.Simulation.World.ReadKnowledgeOrderDefinition CreateReadKnowledgeOrderDefinition(string? TargetItemKind = null, string? TargetKnowledgeArtifactId = null, global::ClankerWorld.Simulation.World.OwnerKnowledgeReadCompletion? KnowledgeReadCompletion = null) => new(TargetItemKind, TargetKnowledgeArtifactId, KnowledgeReadCompletion);

    internal static global::ClankerWorld.Simulation.World.ReadKnowledgeOrderDefinition PrepareExecuteKnowledgeReadOrderReadKnowledgeOrderDefinition(global::ClankerWorld.Simulation.World.ReadKnowledgeOrderDefinition basis, string? value0, global::ClankerWorld.Simulation.World.OwnerKnowledgeReadCompletion? value1) => basis with { TargetKnowledgeArtifactId = value0, KnowledgeReadCompletion = value1 };

    internal static global::ClankerWorld.Simulation.World.OwnerKnowledgeReadCompletion CreateOwnerKnowledgeReadCompletion(long WorldTick, global::System.Collections.Generic.IReadOnlyList<global::ClankerWorld.Simulation.World.GridPoint> LearnedSites, global::System.Collections.Generic.IReadOnlyList<string> LearnedRecipes) => new(WorldTick, LearnedSites) { LearnedRecipes = LearnedRecipes };

}
