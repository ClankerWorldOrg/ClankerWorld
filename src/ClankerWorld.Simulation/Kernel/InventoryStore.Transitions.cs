namespace ClankerWorld.Simulation.Kernel;

internal sealed partial class InventoryStore
{
    internal static InventoryCheckpoint CommitAuthorityTransition(InventoryCheckpoint checkpoint, long? targetTick = null, IReadOnlyList<InventoryLot>? lots = null, IReadOnlyList<InventoryReservation>? reservations = null, IReadOnlyList<DirectBarterOffer>? offers = null, string? eventKind = null, string? detail = null, IEnumerable<(string Kind, string Detail)>? pendingEvents = null)
    {
        var nextTick = targetTick ?? checkpoint.WorldTick;
        var events = checkpoint.Events.ToList();
        if (eventKind is not null)
        {
            events.Add(InventoryStore.CreateInventoryEvent(checked(checkpoint.EventHistoryFloor + events.Count + 1L), nextTick, eventKind, detail ?? string.Empty));
        }

        if (pendingEvents is not null)
        {
            foreach (var pending in pendingEvents.OrderBy(item => item.Detail, StringComparer.Ordinal))
            {
                events.Add(InventoryStore.CreateInventoryEvent(checked(checkpoint.EventHistoryFloor + events.Count + 1L), nextTick, pending.Kind, pending.Detail));
            }
        }

        var next = InventoryStore.CreateInventoryCheckpoint(nextTick, lots is null ? checkpoint.Lots : InventoryAuthority.UpdateCustodyHolds(checkpoint, lots), reservations ?? checkpoint.Reservations, offers ?? checkpoint.Offers, events, checkpoint.EventHistoryFloor);
        ValidateSnapshot(next);
        return InventoryStorageHistory.RecordTransition(checkpoint, next);
    }

    internal static void ValidateSnapshot(InventoryCheckpoint checkpoint)
    {
        ArgumentNullException.ThrowIfNull(checkpoint);
        if (checkpoint.WorldTick < 0 || checkpoint.EventHistoryFloor < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(checkpoint));
        }

        ValidateLots(checkpoint.Lots);
        ValidateContainerGroups(checkpoint.Lots);
        EnsureCanonicalIds(checkpoint.Reservations.Select(reservation => reservation.Id), "reservations");
        EnsureCanonicalIds(checkpoint.Offers.Select(offer => offer.Id), "offers");
        var lotsById = checkpoint.Lots.ToDictionary(lot => lot.Id, StringComparer.Ordinal);
        foreach (var reservation in checkpoint.Reservations)
            ValidateHolds(reservation.HoldAccess);
        foreach (var reservation in checkpoint.Reservations.Where(reservation => reservation.State is InventoryReservationState.Reserved or InventoryReservationState.PartiallyConsumed or InventoryReservationState.Committed))
        {
            if (!lotsById.TryGetValue(reservation.LotId, out var lot) || lot.OwnerId != reservation.OwnerId || InventoryContainerRules.IsContainer(lot.ItemKind))
                throw new InvalidDataException($"Active inventory reservation '{reservation.Id}' has no movable content lot.");
        }

        foreach (var offer in checkpoint.Offers.Where(offer => offer.State == DirectBarterState.Open))
        {
            if (!lotsById.TryGetValue(offer.FirstLotId, out var firstLot) || !lotsById.TryGetValue(offer.SecondLotId, out var secondLot))
                throw new InvalidDataException($"Open barter offer '{offer.Id}' references a missing lot.");
            if (IsContainerRelated(firstLot) || IsContainerRelated(secondLot))
                throw new InvalidDataException($"Open barter offer '{offer.Id}' references a vessel or contained lot.");
        }
    }

    internal static void ValidateSnapshotForCodec(InventoryCheckpoint checkpoint)
    {
        ValidateSnapshot(checkpoint);
        InventoryStorageHistory.Validate(checkpoint.Events);
    }

    internal static bool IsContainerRelated(InventoryLot lot) => lot.ContainerLotId is not null || InventoryContainerRules.IsContainer(lot.ItemKind);

    internal static void ValidateContainerGroups(IReadOnlyList<InventoryLot> lots)
    {
        var byId = lots.ToDictionary(lot => lot.Id, StringComparer.Ordinal);
        foreach (var lot in lots)
        {
            if (InventoryContainerRules.IsContainer(lot.ItemKind) && (lot.Quantity != 1 || lot.ContainerLotId is not null))
                throw new InvalidDataException($"Reusable vessel lot '{lot.Id}' must be a single top-level item.");
            if (lot.ItemKind == InventoryContainerRules.Handcart && (lot.GroundPosition is null || lot.StorageBuildingId is not null || lot.DeliveryBuildingId is not null))
                throw new InvalidDataException($"Handcart lot '{lot.Id}' must have a physical ground position.");
            if (lot.ItemKind == InventoryContainerRules.FreshWater && lot.ContainerLotId is null)
                throw new InvalidDataException($"Fresh water lot '{lot.Id}' must be contained in a water jug.");
            if (lot.ContainerLotId is not { } containerId)
                continue;
            if (!byId.TryGetValue(containerId, out var container) || !InventoryContainerRules.IsContainer(container.ItemKind) || container.ContainerLotId is not null || !InventoryContainerRules.Allows(container.ItemKind, lot.ItemKind) || container.OwnerId != lot.OwnerId || container.StorageBuildingId != lot.StorageBuildingId || container.DeliveryBuildingId != lot.DeliveryBuildingId || container.CarrierId != lot.CarrierId || lot.GroundPosition is not null)
                throw new InvalidDataException($"Inventory lot '{lot.Id}' has an invalid container relationship.");
        }

        foreach (var container in lots.Where(lot => InventoryContainerRules.IsContainer(lot.ItemKind)))
        {
            var containedQuantity = lots.Where(lot => lot.ContainerLotId == container.Id).Sum(lot => lot.Quantity);
            if (containedQuantity > InventoryContainerRules.Capacity(container.ItemKind))
                throw new InvalidDataException($"Container lot '{container.Id}' exceeds its capacity.");
        }
    }

    internal static void ValidateLots(IReadOnlyList<InventoryLot> lots)
    {
        EnsureCanonicalIds(lots.Select(lot => lot.Id), "lots");
        foreach (var lot in lots)
        {
            ValidateHolds(lot.Holds);
            ArgumentException.ThrowIfNullOrWhiteSpace(lot.ItemKind);
            ArgumentException.ThrowIfNullOrWhiteSpace(lot.OwnerId);
            if (lot.StorageBuildingId is { } storageBuildingId && (string.IsNullOrWhiteSpace(storageBuildingId) || storageBuildingId != storageBuildingId.Trim()))
                throw new InvalidDataException($"Inventory lot '{lot.Id}' has an invalid storage building ID.");
            if (lot.DeliveryBuildingId is { } deliveryBuildingId && (string.IsNullOrWhiteSpace(deliveryBuildingId) || deliveryBuildingId != deliveryBuildingId.Trim() || lot.StorageBuildingId is not null))
                throw new InvalidDataException($"Inventory lot '{lot.Id}' has an invalid delivery building ID.");
            if (lot.ContainerLotId is { } containerLotId && (string.IsNullOrWhiteSpace(containerLotId) || containerLotId != containerLotId.Trim()))
                throw new InvalidDataException($"Inventory lot '{lot.Id}' has an invalid container ID.");
            if (lot.CarrierId is { } carrier && (string.IsNullOrWhiteSpace(carrier) || carrier != carrier.Trim() || lot.StorageBuildingId is not null || lot.GroundPosition is not null))
                throw new InvalidDataException($"Inventory lot '{lot.Id}' has invalid physical custody.");
            if (lot.Quantity <= 0 || lot.LastProcessedTick < 0 || lot.ConditionBasisPoints is < 0 or > 10_000 || lot.FreshnessBasisPoints is < 0 or > 10_000)
            {
                throw new ArgumentOutOfRangeException(nameof(lots));
            }

            if (lot.GroundPosition is { } ground && (ground.X < 0 || ground.Y < 0 || lot.StorageBuildingId is not null || lot.DeliveryBuildingId is not null))
                throw new InvalidDataException($"Inventory lot '{lot.Id}' has an invalid ground location.");
        }
    }

    internal static void EnsureCanonicalIds(IEnumerable<string> ids, string collectionName)
    {
        var actual = ids.ToArray();
        if (actual.Any(string.IsNullOrWhiteSpace) || actual.Distinct(StringComparer.Ordinal).Count() != actual.Length || !actual.SequenceEqual(actual.OrderBy(id => id, StringComparer.Ordinal)))
        {
            throw new InvalidDataException($"Inventory {collectionName} must have unique canonical IDs.");
        }
    }

    internal static void ValidateHold(InventoryHold hold)
    {
        if (hold is null || !Enum.IsDefined(hold.Purpose) || string.IsNullOrWhiteSpace(hold.HolderId) || hold.HolderId != hold.HolderId.Trim())
            throw new InvalidDataException("Stock holds require a known purpose and an exact holder.");
    }

    internal static void ValidateHolds(IReadOnlyList<InventoryHold>? holds)
    {
        if (holds is null)
            return;
        foreach (var hold in holds)
            ValidateHold(hold);
        if (holds.Count == 0 || holds.Distinct().Count() != holds.Count || !holds.SequenceEqual(holds.OrderBy(item => item.Purpose).ThenBy(item => item.HolderId, StringComparer.Ordinal)))
            throw new InvalidDataException("Stock holds must be unique and in canonical order.");
    }
}
