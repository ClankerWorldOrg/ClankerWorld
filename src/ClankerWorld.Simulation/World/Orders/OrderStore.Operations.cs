namespace ClankerWorld.Simulation.World;

internal sealed partial class OrderStore
{
    internal static OrderTask PrepareAnimalTarget(OrderTask basis, string? targetAnimalId) => basis with { TargetAnimalId = targetAnimalId };

    internal static OwnerBoatTravelBinding PrepareBoatRequest(OwnerBoatTravelBinding basis, string? requestId) => basis with { RequestId = requestId };

    internal static OrderTask PrepareBoatTravel(OrderTask basis, global::ClankerWorld.Simulation.World.OwnerBoatTravelBinding? boatTravel) => basis with { BoatTravel = boatTravel };

    internal static OrderTask PrepareCartBinding(OrderTask basis, string? targetCartLotId) => basis with { TargetCartLotId = targetCartLotId };

    internal static OrderTask PrepareConstructionBinding(OrderTask basis, string? constructionOwnerId, global::ClankerWorld.Simulation.World.GridPoint? constructionPosition, long? constructionStartedTick, string? constructionInstanceId) => basis with { ConstructionOwnerId = constructionOwnerId, ConstructionPosition = constructionPosition, ConstructionStartedTick = constructionStartedTick, ConstructionInstanceId = constructionInstanceId };

    internal static OrderTask PrepareConversationBinding(OrderTask basis, string? talkConversationId, string status, string? blockedReason) => basis with { TalkConversationId = talkConversationId, Status = status, BlockedReason = blockedReason };

    internal static OrderTask PrepareConversationOutcome(OrderTask basis, string? talkOutcome) => basis with { TalkOutcome = talkOutcome };

    internal static OrderTask PrepareConversationTarget(OrderTask basis, string? targetAgentId) => basis with { TargetAgentId = targetAgentId };

    internal static OrderTask PrepareCustodyDestination(OrderTask basis, string? targetStorageBuildingId, string? targetStorageOwnerId, global::ClankerWorld.Simulation.World.GridPoint? targetStoragePosition) => basis with { TargetStorageBuildingId = targetStorageBuildingId, TargetStorageOwnerId = targetStorageOwnerId, TargetStoragePosition = targetStoragePosition };

    internal static OrderTask PrepareDeliveryBinding(OrderTask basis, string? targetStorageBuildingId, string? targetStorageOwnerId, global::ClankerWorld.Simulation.World.GridPoint? targetStoragePosition, string? deliveryRoute, string? deliveryLotId, int? deliveryQuantity) => basis with { TargetStorageBuildingId = targetStorageBuildingId, TargetStorageOwnerId = targetStorageOwnerId, TargetStoragePosition = targetStoragePosition, DeliveryRoute = deliveryRoute, DeliveryLotId = deliveryLotId, DeliveryQuantity = deliveryQuantity };

    internal static OrderTask PrepareDeliveryLoad(OrderTask basis, string? deliveryLotId, int? deliveryQuantity) => basis with { DeliveryLotId = deliveryLotId, DeliveryQuantity = deliveryQuantity };

    internal static OrderTask PrepareExpansionBinding(OrderTask basis, global::ClankerWorld.Simulation.World.OwnerBuildingExpansionBinding? expansionBinding) => basis with { ExpansionBinding = expansionBinding };

    internal static OwnerBuildingExpansionBinding PrepareExpansionJob(OwnerBuildingExpansionBinding basis, string? jobId) => basis with { JobId = jobId };

    internal static OrderTask PrepareKnowledgeArtifact(OrderTask basis, string? targetKnowledgeArtifactId) => basis with { TargetKnowledgeArtifactId = targetKnowledgeArtifactId };

    internal static OrderTask PrepareKnowledgeCopy(OrderTask basis, string? knowledgeCopySourceArtifactId) => basis with { KnowledgeCopySourceArtifactId = knowledgeCopySourceArtifactId };

    internal static OrderTask PrepareKnowledgeKind(OrderTask basis, string? targetKnowledgeKind) => basis with { TargetKnowledgeKind = targetKnowledgeKind };

    internal static OrderTask PrepareKnowledgeProject(OrderTask basis, string? knowledgeWritingProjectId) => basis with { KnowledgeWritingProjectId = knowledgeWritingProjectId };

    internal static OrderTask PrepareKnowledgeWriting(OrderTask basis, string? knowledgeWritingProjectId, string? knowledgeCopySourceArtifactId) => basis with { KnowledgeWritingProjectId = knowledgeWritingProjectId, KnowledgeCopySourceArtifactId = knowledgeCopySourceArtifactId };

    internal static OwnerKnowledgeReadCompletion PrepareLearnedRecipes(OwnerKnowledgeReadCompletion basis, global::System.Collections.Generic.IReadOnlyList<string> learnedRecipes) => basis with { LearnedRecipes = learnedRecipes };

    internal static OwnerQueuedInstruction PrepareObservedReply(OwnerQueuedInstruction basis, long? observedTick, string? observerReply) => basis with { ObservedTick = observedTick, ObserverReply = observerReply };

    internal static OrderTask PrepareOrderStatus(OrderTask basis, string status, string? blockedReason, bool waitForDecisionAfterFailure) => basis with { Status = status, BlockedReason = blockedReason, WaitForDecisionAfterFailure = waitForDecisionAfterFailure };

    internal static OrderTask PrepareProductionBinding(OrderTask basis, string? productionBuildingId, string? productionJobId, long? productionProjectStartedTick) => basis with { ProductionBuildingId = productionBuildingId, ProductionJobId = productionJobId, ProductionProjectStartedTick = productionProjectStartedTick };

    internal static OrderTask PrepareProductionJob(OrderTask basis, string? productionJobId) => basis with { ProductionJobId = productionJobId };

    internal static OrderTask PrepareProgressCredit(OrderTask basis, int completedUnits, string? lastEffectId, string status, string? blockedReason) => basis with { CompletedUnits = completedUnits, LastEffectId = lastEffectId, Status = status, BlockedReason = blockedReason };

    internal static OwnerQueuedInstruction PreparePromptedGuidance(OwnerQueuedInstruction basis, long? guidancePromptedTick) => basis with { GuidancePromptedTick = guidancePromptedTick };

    internal static OrderTask PrepareQueuedStatus(OrderTask basis, string status) => basis with { Status = status };

    internal static OrderTask PrepareReadingCompletion(OrderTask basis, string? targetKnowledgeArtifactId, global::ClankerWorld.Simulation.World.OwnerKnowledgeReadCompletion? knowledgeReadCompletion) => basis with { TargetKnowledgeArtifactId = targetKnowledgeArtifactId, KnowledgeReadCompletion = knowledgeReadCompletion };

    internal static OrderTask PrepareShelterBinding(OrderTask basis, global::ClankerWorld.Simulation.World.OwnerShelterBinding? shelterBinding) => basis with { ShelterBinding = shelterBinding };

    internal static OrderTask PrepareShelterCompletion(OrderTask basis, global::ClankerWorld.Simulation.World.OwnerShelterCompletion? shelterCompletion) => basis with { ShelterCompletion = shelterCompletion };

    internal static OrderTask PrepareStockBinding(OrderTask basis, string? targetLotId) => basis with { TargetLotId = targetLotId };

    internal static OwnerQueuedInstruction PrepareTaskBinding(OwnerQueuedInstruction basis, global::ClankerWorld.Simulation.World.OrderTask? task) => basis with { Task = task };

    internal static OrderTask CreateOrderTask(string Action, string Status, int RequestedUnits, int CompletedUnits, string ProgressUnit, bool RepeatUntilCancelled, bool QuantityIsExplicit = false, string? TargetFoodKind = null, string? TargetResourceId = null, global::ClankerWorld.Simulation.World.GridPoint? TargetPosition = null, string? BlockedReason = null, string? LastEffectId = null, bool WaitForDecisionAfterFailure = false, string? TargetAgentId = null, string? TargetMaterialKind = null, string? TargetEquipmentKind = null, string? TargetCropKind = null, string? TargetRecipeId = null, string? TargetOutputKind = null, string? ProductionBuildingId = null, string? ProductionJobId = null, long? ProductionProjectStartedTick = null, string? TargetItemKind = null, string? TargetStorageBuildingId = null, string? TargetStorageOwnerId = null, global::ClankerWorld.Simulation.World.GridPoint? TargetStoragePosition = null, string? TargetLotId = null, string? DeliveryPurpose = null, string? TargetBuildingKind = null, string? DeliveryRoute = null, string? DeliveryLotId = null, int? DeliveryQuantity = null, string? TargetDefinitionId = null, string? ConstructionOwnerId = null, global::ClankerWorld.Simulation.World.GridPoint? ConstructionPosition = null, long? ConstructionStartedTick = null, string? ConstructionInstanceId = null, global::ClankerWorld.Simulation.World.OwnerBuildingExpansionBinding? ExpansionBinding = null, global::ClankerWorld.Simulation.World.OwnerShelterBinding? ShelterBinding = null, global::ClankerWorld.Simulation.World.OwnerShelterCompletion? ShelterCompletion = null) => new(Action, Status, RequestedUnits, CompletedUnits, ProgressUnit, RepeatUntilCancelled, QuantityIsExplicit, TargetFoodKind, TargetResourceId, TargetPosition, BlockedReason, LastEffectId, WaitForDecisionAfterFailure, TargetAgentId, TargetMaterialKind, TargetEquipmentKind, TargetCropKind, TargetRecipeId, TargetOutputKind, ProductionBuildingId, ProductionJobId, ProductionProjectStartedTick, TargetItemKind, TargetStorageBuildingId, TargetStorageOwnerId, TargetStoragePosition, TargetLotId, DeliveryPurpose, TargetBuildingKind, DeliveryRoute, DeliveryLotId, DeliveryQuantity, TargetDefinitionId, ConstructionOwnerId, ConstructionPosition, ConstructionStartedTick, ConstructionInstanceId, ExpansionBinding, ShelterBinding, ShelterCompletion);

    internal static OwnerBoatTravelBinding CreateOwnerBoatTravelBinding(string DestinationPortId, string? RequestId = null) => new(DestinationPortId, RequestId);

    internal static OwnerBuildingExpansionBinding CreateOwnerBuildingExpansionBinding(string BuildingInstanceId, string DefinitionId, string OwnerId, global::ClankerWorld.Simulation.World.GridPoint ExpectedPosition, int ExpectedRevision, global::ClankerWorld.Simulation.World.GridPoint TargetPosition, global::ClankerWorld.Simulation.World.BuildingFootprintRevision TargetFootprint, string? JobId = null) => new(BuildingInstanceId, DefinitionId, OwnerId, ExpectedPosition, ExpectedRevision, TargetPosition, TargetFootprint, JobId);

    internal static OwnerInstructionReceipt CreateOwnerInstructionReceipt(string InstructionId, string IdempotencyKey, long SubmittedTick, long RunEpoch, long Revision) => new(InstructionId, IdempotencyKey, SubmittedTick, RunEpoch, Revision);

    internal static OwnerKnowledgeReadCompletion CreateOwnerKnowledgeReadCompletion(long WorldTick, global::System.Collections.Generic.IReadOnlyList<global::ClankerWorld.Simulation.World.GridPoint> LearnedSites) => new(WorldTick, LearnedSites);

    internal static OwnerOrderCancellation CreateOwnerOrderCancellation(string IdempotencyKey, string IssuerId, string WorldId, string TargetAgentId, string OrderId, global::ClankerWorld.Simulation.World.OwnerOrderControlReceipt Receipt) => new(IdempotencyKey, IssuerId, WorldId, TargetAgentId, OrderId, Receipt);

    internal static OwnerOrderControlReceipt CreateOwnerOrderControlReceipt(string OrderId, string Status, bool Changed, long WorldTick, long LatestEventId) => new(OrderId, Status, Changed, WorldTick, LatestEventId);

    internal static OwnerQueuedInstruction CreateOwnerQueuedInstruction(string InstructionId, string IdempotencyKey, string IssuerId, string TargetAgentId, global::ClankerWorld.Simulation.World.OwnerInstructionKind Kind, string Text, long SubmittedTick, long RunEpoch, long SubmissionSequence, global::ClankerWorld.Simulation.World.OwnerInstructionState State, long? ObservedTick = null, string? ObserverReply = null, long? GuidancePromptedTick = null, bool Queue = false, global::ClankerWorld.Simulation.World.OrderTask? Task = null) => new(InstructionId, IdempotencyKey, IssuerId, TargetAgentId, Kind, Text, SubmittedTick, RunEpoch, SubmissionSequence, State, ObservedTick, ObserverReply, GuidancePromptedTick, Queue, Task);

    internal static OwnerShelterBinding CreateOwnerShelterBinding(string Kind, global::ClankerWorld.Simulation.World.GridPoint Position, string? BuildingInstanceId = null, string? DefinitionId = null, string? OwnerId = null, global::ClankerWorld.Simulation.World.GridPoint? BuildingPosition = null, long? BuildingPlacedTick = null) => new(Kind, Position, BuildingInstanceId, DefinitionId, OwnerId, BuildingPosition, BuildingPlacedTick);

    internal static OwnerShelterCompletion CreateOwnerShelterCompletion(long WorldTick, global::ClankerWorld.Simulation.World.GridPoint Position, string? FuelReservationId = null) => new(WorldTick, Position, FuelReservationId);

}
