namespace ClankerWorld.Simulation.Kernel;

internal sealed partial class InventoryStore
{
    internal static InventoryCheckpoint PrepareArchivedEventWindow(InventoryCheckpoint basis, global::System.Collections.Generic.IReadOnlyList<global::ClankerWorld.Simulation.Kernel.InventoryEvent> events, long eventHistoryFloor) => basis with { Events = events, EventHistoryFloor = eventHistoryFloor };

    internal static DirectBarterOffer PrepareBarterAcceptance(DirectBarterOffer basis, global::System.Collections.Generic.IReadOnlyList<string> acceptedBy) => basis with { AcceptedBy = acceptedBy };

    internal static InventoryCheckpoint PrepareBarterLedger(InventoryCheckpoint basis, global::System.Collections.Generic.IReadOnlyList<global::ClankerWorld.Simulation.Kernel.DirectBarterOffer> offers, global::System.Collections.Generic.IReadOnlyList<global::ClankerWorld.Simulation.Kernel.InventoryReservation> reservations) => basis with { Offers = offers, Reservations = reservations };

    internal static DirectBarterOffer PrepareBarterState(DirectBarterOffer basis, global::ClankerWorld.Simulation.Kernel.DirectBarterState state) => basis with { State = state };

    internal static InventoryLot PrepareCartCargoSplit(InventoryLot basis, string id, int quantity, string? containerLotId, global::ClankerWorld.Simulation.Kernel.InventoryHoldSet? holds, global::ClankerWorld.Simulation.Kernel.InventoryGroundPosition? groundPosition, string? provenanceLotId) => basis with { Id = id, Quantity = quantity, ContainerLotId = containerLotId, Holds = holds, GroundPosition = groundPosition, ProvenanceLotId = provenanceLotId };

    internal static InventoryLot PrepareCondition(InventoryLot basis, int conditionBasisPoints) => basis with { ConditionBasisPoints = conditionBasisPoints };

    internal static InventoryLot PrepareContainerLocation(InventoryLot basis, string? containerLotId, global::ClankerWorld.Simulation.Kernel.InventoryGroundPosition? groundPosition) => basis with { ContainerLotId = containerLotId, GroundPosition = groundPosition };

    internal static InventoryLot PrepareContainerSplit(InventoryLot basis, string id, int quantity, string? provenanceLotId, string? containerLotId, global::ClankerWorld.Simulation.Kernel.InventoryGroundPosition? groundPosition) => basis with { Id = id, Quantity = quantity, ProvenanceLotId = provenanceLotId, ContainerLotId = containerLotId, GroundPosition = groundPosition };

    internal static InventoryLot PrepareContainerTransfer(InventoryLot basis, string ownerId, string? carrierId, string? storageBuildingId, string? deliveryBuildingId) => basis with { OwnerId = ownerId, CarrierId = carrierId, StorageBuildingId = storageBuildingId, DeliveryBuildingId = deliveryBuildingId };

    internal static InventoryLot PrepareCustody(InventoryLot basis, string? carrierId, string? deliveryBuildingId) => basis with { CarrierId = carrierId, DeliveryBuildingId = deliveryBuildingId };

    internal static InventoryLot PrepareDeliveryDesignation(InventoryLot basis, string? deliveryBuildingId) => basis with { DeliveryBuildingId = deliveryBuildingId };

    internal static InventoryLot PrepareDroppedCargo(InventoryLot basis, string? carrierId, global::ClankerWorld.Simulation.Kernel.InventoryGroundPosition? groundPosition, string? storageBuildingId, string? deliveryBuildingId) => basis with { CarrierId = carrierId, GroundPosition = groundPosition, StorageBuildingId = storageBuildingId, DeliveryBuildingId = deliveryBuildingId };

    internal static InventoryLot PrepareEstateCustody(InventoryLot basis, string ownerId, string? carrierId, string? deliveryBuildingId) => basis with { OwnerId = ownerId, CarrierId = carrierId, DeliveryBuildingId = deliveryBuildingId };

    internal static InventoryCheckpoint PrepareEventWindow(InventoryCheckpoint basis, global::System.Collections.Generic.IReadOnlyList<global::ClankerWorld.Simulation.Kernel.InventoryEvent> events) => basis with { Events = events };

    internal static InventoryLot PrepareExactTransferSplit(InventoryLot basis, string id, string ownerId, string? carrierId, int quantity, string? provenanceLotId, string? storageBuildingId, string? deliveryBuildingId) => basis with { Id = id, OwnerId = ownerId, CarrierId = carrierId, Quantity = quantity, ProvenanceLotId = provenanceLotId, StorageBuildingId = storageBuildingId, DeliveryBuildingId = deliveryBuildingId };

    internal static InventoryLot PrepareGroundCargo(InventoryLot basis, global::ClankerWorld.Simulation.Kernel.InventoryGroundPosition? groundPosition, string? storageBuildingId, string? carrierId, string? deliveryBuildingId) => basis with { GroundPosition = groundPosition, StorageBuildingId = storageBuildingId, CarrierId = carrierId, DeliveryBuildingId = deliveryBuildingId };

    internal static InventoryLot PrepareGroundPosition(InventoryLot basis, global::ClankerWorld.Simulation.Kernel.InventoryGroundPosition? groundPosition) => basis with { GroundPosition = groundPosition };

    internal static InventoryLot PrepareHolds(InventoryLot basis, global::ClankerWorld.Simulation.Kernel.InventoryHoldSet? holds) => basis with { Holds = holds };

    internal static InventoryLot PrepareLotSplit(InventoryLot basis, string id, int quantity, string? provenanceLotId) => basis with { Id = id, Quantity = quantity, ProvenanceLotId = provenanceLotId };

    internal static InventoryCheckpoint PrepareLots(InventoryCheckpoint basis, global::System.Collections.Generic.IReadOnlyList<global::ClankerWorld.Simulation.Kernel.InventoryLot> lots) => basis with { Lots = lots };

    internal static InventoryLot PrepareOwnerTransfer(InventoryLot basis, string ownerId, string? carrierId, string? storageBuildingId, string? deliveryBuildingId, global::ClankerWorld.Simulation.Kernel.InventoryGroundPosition? groundPosition) => basis with { OwnerId = ownerId, CarrierId = carrierId, StorageBuildingId = storageBuildingId, DeliveryBuildingId = deliveryBuildingId, GroundPosition = groundPosition };

    internal static InventoryLot PrepareQuantity(InventoryLot basis, int quantity) => basis with { Quantity = quantity };

    internal static InventoryLot PrepareRelocatedSplit(InventoryLot basis, string id, int quantity, string? provenanceLotId, string? carrierId, string? storageBuildingId, global::ClankerWorld.Simulation.Kernel.InventoryGroundPosition? groundPosition, string? deliveryBuildingId) => basis with { Id = id, Quantity = quantity, ProvenanceLotId = provenanceLotId, CarrierId = carrierId, StorageBuildingId = storageBuildingId, GroundPosition = groundPosition, DeliveryBuildingId = deliveryBuildingId };

    internal static InventoryLot PrepareRelocation(InventoryLot basis, string? carrierId, string? storageBuildingId, global::ClankerWorld.Simulation.Kernel.InventoryGroundPosition? groundPosition, string? deliveryBuildingId) => basis with { CarrierId = carrierId, StorageBuildingId = storageBuildingId, GroundPosition = groundPosition, DeliveryBuildingId = deliveryBuildingId };

    internal static InventoryReservation PrepareReservationDeadline(InventoryReservation basis, long expiryTick) => basis with { ExpiryTick = expiryTick };

    internal static InventoryCheckpoint PrepareArchivedRepairReservations(InventoryCheckpoint basis, IReadOnlyList<InventoryReservation> reservations, long archivedRepairReservationCount) => basis with { Reservations = reservations, ArchivedRepairReservationCount = archivedRepairReservationCount };

    internal static InventoryCheckpoint PrepareReservationLedger(InventoryCheckpoint basis, global::System.Collections.Generic.IReadOnlyList<global::ClankerWorld.Simulation.Kernel.InventoryReservation> reservations) => basis with { Reservations = reservations };

    internal static InventoryReservation PrepareReservationPurpose(InventoryReservation basis, string purpose) => basis with { Purpose = purpose };

    internal static InventoryReservation PrepareReservationQuantity(InventoryReservation basis, int quantity) => basis with { Quantity = quantity };

    internal static InventoryReservation PrepareReservationState(InventoryReservation basis, global::ClankerWorld.Simulation.Kernel.InventoryReservationState state) => basis with { State = state };

    internal static InventoryLot PrepareSplitTransfer(InventoryLot basis, string id, string ownerId, string? carrierId, int quantity, string? provenanceLotId, string? storageBuildingId, string? deliveryBuildingId, global::ClankerWorld.Simulation.Kernel.InventoryGroundPosition? groundPosition) => basis with { Id = id, OwnerId = ownerId, CarrierId = carrierId, Quantity = quantity, ProvenanceLotId = provenanceLotId, StorageBuildingId = storageBuildingId, DeliveryBuildingId = deliveryBuildingId, GroundPosition = groundPosition };

    internal static InventoryLot PrepareSpoilage(InventoryLot basis, int freshnessBasisPoints, long lastProcessedTick) => basis with { FreshnessBasisPoints = freshnessBasisPoints, LastProcessedTick = lastProcessedTick };

    internal static InventoryEvent PrepareStorageHistory(InventoryEvent basis, global::System.Collections.Generic.IReadOnlyList<global::ClankerWorld.Simulation.Kernel.InventoryStorageChange>? storageChanges) => basis with { StorageChanges = storageChanges };

    internal static InventoryLot PrepareStoredStock(InventoryLot basis, string? storageBuildingId) => basis with { StorageBuildingId = storageBuildingId };

    internal static InventoryLot PrepareUncontainedSplit(InventoryLot basis, string id, string ownerId, string? carrierId, int quantity, string? provenanceLotId, string? containerLotId, global::ClankerWorld.Simulation.Kernel.InventoryHoldSet? holds, string? storageBuildingId, string? deliveryBuildingId, global::ClankerWorld.Simulation.Kernel.InventoryGroundPosition? groundPosition) => basis with { Id = id, OwnerId = ownerId, CarrierId = carrierId, Quantity = quantity, ProvenanceLotId = provenanceLotId, ContainerLotId = containerLotId, Holds = holds, StorageBuildingId = storageBuildingId, DeliveryBuildingId = deliveryBuildingId, GroundPosition = groundPosition };

    internal static InventoryLot PrepareUncontainedTransfer(InventoryLot basis, string ownerId, string? carrierId, string? containerLotId, global::ClankerWorld.Simulation.Kernel.InventoryHoldSet? holds, string? storageBuildingId, string? deliveryBuildingId, global::ClankerWorld.Simulation.Kernel.InventoryGroundPosition? groundPosition) => basis with { OwnerId = ownerId, CarrierId = carrierId, ContainerLotId = containerLotId, Holds = holds, StorageBuildingId = storageBuildingId, DeliveryBuildingId = deliveryBuildingId, GroundPosition = groundPosition };

    internal static DirectBarterOffer CreateDirectBarterOffer(Identity Id, int Revision, string FirstPartyId, string SecondPartyId, string FirstLotId, int FirstQuantity, string SecondLotId, int SecondQuantity, long ExpiryTick, global::ClankerWorld.Simulation.Kernel.DirectBarterState State, global::System.Collections.Generic.IReadOnlyList<string> AcceptedBy) => new(Id, Revision, FirstPartyId, SecondPartyId, FirstLotId, FirstQuantity, SecondLotId, SecondQuantity, ExpiryTick, State, AcceptedBy);

    internal static DirectBarterProposal CreateDirectBarterProposal(Identity Id, int Revision, string FirstPartyId, string SecondPartyId, string FirstLotId, int FirstQuantity, string SecondLotId, int SecondQuantity, long ExpiryTick) => new(Id, Revision, FirstPartyId, SecondPartyId, FirstLotId, FirstQuantity, SecondLotId, SecondQuantity, ExpiryTick);

    internal static InventoryCheckpoint CreateInventoryCheckpoint(long WorldTick, global::System.Collections.Generic.IReadOnlyList<global::ClankerWorld.Simulation.Kernel.InventoryLot> Lots, global::System.Collections.Generic.IReadOnlyList<global::ClankerWorld.Simulation.Kernel.InventoryReservation> Reservations, global::System.Collections.Generic.IReadOnlyList<global::ClankerWorld.Simulation.Kernel.DirectBarterOffer> Offers, global::System.Collections.Generic.IReadOnlyList<global::ClankerWorld.Simulation.Kernel.InventoryEvent> Events, long EventHistoryFloor = 0L, long ArchivedRepairReservationCount = 0L) => new(WorldTick, Lots, Reservations, Offers, Events, EventHistoryFloor) { ArchivedRepairReservationCount = ArchivedRepairReservationCount };

    internal static InventoryEvent CreateInventoryEvent(long EventId, long WorldTick, string Kind, string Detail) => new(EventId, WorldTick, Kind, Detail);

    internal static InventoryHold CreateInventoryHold(global::ClankerWorld.Simulation.Kernel.InventoryHoldPurpose Purpose, string HolderId) => new(Purpose, HolderId);

    internal static InventoryLot CreateInventoryLot(string Id, string ItemKind, string OwnerId, int Quantity, int ConditionBasisPoints, int FreshnessBasisPoints, long LastProcessedTick, string? ProvenanceLotId = null, string? StorageBuildingId = null, string? DeliveryBuildingId = null, string? ContainerLotId = null, global::ClankerWorld.Simulation.Kernel.InventoryGroundPosition? GroundPosition = null, string? CarrierId = null, global::ClankerWorld.Simulation.Kernel.InventoryHoldSet? Holds = null) => new(Id, ItemKind, OwnerId, Quantity, ConditionBasisPoints, FreshnessBasisPoints, LastProcessedTick, ProvenanceLotId, StorageBuildingId, DeliveryBuildingId, ContainerLotId, GroundPosition, CarrierId, Holds);

    internal static InventoryReservation CreateInventoryReservation(Identity Id, string OwnerId, string LotId, int Quantity, string Purpose, long ExpiryTick, bool IsExclusive, global::ClankerWorld.Simulation.Kernel.InventoryReservationState State, global::ClankerWorld.Simulation.Kernel.InventoryHoldSet? HoldAccess = null) => new(Id, OwnerId, LotId, Quantity, Purpose, ExpiryTick, IsExclusive, State, HoldAccess);

    internal static InventoryStorageChange CreateInventoryStorageChange(string BuildingId, string ItemKind, long QuantityChange) => new(BuildingId, ItemKind, QuantityChange);

}
