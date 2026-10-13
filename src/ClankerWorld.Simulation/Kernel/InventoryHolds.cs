namespace ClankerWorld.Simulation.Kernel;

public enum InventoryHoldPurpose { MarketStall, Delivery, SupplyTrip, Borrowed }

/// <summary>A promise covering every unit of a physical lot, including its vessel family.</summary>
public sealed record InventoryHold(InventoryHoldPurpose Purpose, string HolderId);

public static partial class InventoryAuthority
{
    /// <summary>Move a boat's physical ground cargo without adding inventory history events.</summary>
    public static InventoryCheckpoint MoveGroundCargo(InventoryCheckpoint checkpoint, IReadOnlyList<string> rootIds,
        InventoryGroundPosition position, bool preserveCustody = false, IReadOnlyList<InventoryHold>? holdAccess = null)
    {
        ValidateCheckpoint(checkpoint);
        var index = InventoryIndex.For(checkpoint);
        foreach (var id in rootIds) EnsureFamilyHoldAccess(checkpoint, index.Find(id) ??
            throw new InvalidOperationException("The exact cargo root is unavailable."), holdAccess);
        return Commit(checkpoint, lots: checkpoint.Lots.Select(lot => rootIds.Contains(lot.Id, StringComparer.Ordinal)
            ? lot with { GroundPosition = position, StorageBuildingId = null, CarrierId = null, DeliveryBuildingId = null }
            : preserveCustody && lot.ContainerLotId is { } container && rootIds.Contains(container, StringComparer.Ordinal)
                ? lot with { GroundPosition = null, StorageBuildingId = null, CarrierId = null, DeliveryBuildingId = null } : lot).ToArray());
    }

    public static InventoryCheckpoint AssignDelivery(InventoryCheckpoint checkpoint, string lotId, string? buildingId)
    {
        ValidateCheckpoint(checkpoint);
        var root = InventoryIndex.For(checkpoint).Root(checkpoint.GetLot(lotId));
        return Commit(checkpoint, lots: checkpoint.Lots.Select(lot => lot.Id == root.Id || lot.ContainerLotId == root.Id
            ? lot with { DeliveryBuildingId = buildingId } : lot).ToArray());
    }

    public static InventoryCheckpoint HoldStock(InventoryCheckpoint checkpoint, string lotId, InventoryHold hold)
    {
        ValidateCheckpoint(checkpoint);
        ValidateHold(hold);
        var lot = checkpoint.GetLot(lotId);
        if ((lot.Holds ?? []).Contains(hold)) return checkpoint;
        var holds = (lot.Holds ?? []).Append(hold).OrderBy(item => item.Purpose)
            .ThenBy(item => item.HolderId, StringComparer.Ordinal).ToArray();
        return Commit(checkpoint, lots: checkpoint.Lots.Select(item => item.Id == lotId
            ? item with { Holds = new InventoryHoldSet(holds) } : item).ToArray());
    }

    public static InventoryCheckpoint ReleaseStock(InventoryCheckpoint checkpoint, string lotId, InventoryHold hold)
    {
        ValidateCheckpoint(checkpoint);
        ValidateHold(hold);
        var lot = checkpoint.GetLot(lotId);
        var holds = (lot.Holds ?? []).Where(item => item != hold).ToArray();
        if (holds.Length == (lot.Holds?.Count ?? 0)) return checkpoint;
        return Commit(checkpoint, lots: checkpoint.Lots.Select(item => item.Id == lotId
            ? item with { Holds = holds.Length == 0 ? null : new InventoryHoldSet(holds) } : item).ToArray());
    }

    private static void ValidateHold(InventoryHold hold)
    {
        if (hold is null || !Enum.IsDefined(hold.Purpose) || string.IsNullOrWhiteSpace(hold.HolderId) ||
            hold.HolderId != hold.HolderId.Trim())
            throw new InvalidDataException("Stock holds require a known purpose and an exact holder.");
    }

    private static void ValidateHolds(IReadOnlyList<InventoryHold>? holds)
    {
        if (holds is null) return;
        foreach (var hold in holds) ValidateHold(hold);
        if (holds.Count == 0 || holds.Distinct().Count() != holds.Count ||
            !holds.SequenceEqual(holds.OrderBy(item => item.Purpose).ThenBy(item => item.HolderId, StringComparer.Ordinal)))
            throw new InvalidDataException("Stock holds must be unique and in canonical order.");
    }

    private static InventoryHoldSet? CanonicalHoldAccess(IReadOnlyList<InventoryHold>? access)
    {
        if (access is not { Count: > 0 }) return null;
        var holds = access.Distinct().OrderBy(item => item.Purpose)
            .ThenBy(item => item.HolderId, StringComparer.Ordinal).ToArray();
        ValidateHolds(holds);
        return new InventoryHoldSet(holds);
    }

    // Custody transitions update their own promises atomically with the physical move.
    private static IReadOnlyList<InventoryLot> UpdateCustodyHolds(InventoryCheckpoint before, IReadOnlyList<InventoryLot> lots)
    {
        InventoryIndex? prior = null;
        InventoryLot[]? updated = null;
        for (var i = 0; i < lots.Count; i++)
        {
            var lot = lots[i];
            if (lot.Holds is null && lot.DeliveryBuildingId is null && lot.CarrierId is null) continue;
            prior ??= InventoryIndex.For(before);
            var source = prior.Find(lot.Id) ?? (lot.ProvenanceLotId is { } id ? prior.Find(id) : null);
            var holds = (lot.Holds ?? []).ToList();
            if (source?.DeliveryBuildingId is { } oldDelivery && lot.DeliveryBuildingId != oldDelivery)
                holds.Remove(new(InventoryHoldPurpose.Delivery, oldDelivery));
            if (source?.CarrierId is { } oldBorrower && (lot.CarrierId != oldBorrower || lot.OwnerId == oldBorrower))
                holds.Remove(new(InventoryHoldPurpose.Borrowed, oldBorrower));
            if (lot.DeliveryBuildingId is { } delivery) holds.Add(new(InventoryHoldPurpose.Delivery, delivery));
            if (lot.CarrierId is { } borrower && borrower != lot.OwnerId) holds.Add(new(InventoryHoldPurpose.Borrowed, borrower));
            var canonical = CanonicalHoldAccess(holds);
            if ((lot.Holds ?? []).SequenceEqual(canonical ?? [])) continue;
            updated ??= lots.ToArray();
            updated[i] = lot with { Holds = canonical };
        }
        return updated ?? lots;
    }

    private static void EnsureHoldAccess(InventoryCheckpoint checkpoint, InventoryLot lot,
        IReadOnlyList<InventoryHold>? holdAccess)
    {
        if (!InventoryHoldRules.Allows(InventoryIndex.For(checkpoint), lot, holdAccess))
            throw new InvalidOperationException("The stock is held for another purpose or holder.");
    }

    private static void EnsureFamilyHoldAccess(InventoryCheckpoint checkpoint, InventoryLot root,
        IReadOnlyList<InventoryHold>? holdAccess)
    {
        foreach (var member in InventoryIndex.For(checkpoint).ContentsOf(root.Id).Prepend(root))
            EnsureHoldAccess(checkpoint, member, holdAccess);
    }
}

public static class InventoryHoldRules
{
    public static IReadOnlyList<InventoryHold> BorrowedBy(string actor) => [new(InventoryHoldPurpose.Borrowed, actor)];

    public static IReadOnlyList<InventoryHold> DeliveryFor(string actor, string? building) => building is null
        ? BorrowedBy(actor) : [new(InventoryHoldPurpose.Delivery, building), new(InventoryHoldPurpose.Borrowed, actor)];

    public static IReadOnlyList<InventoryHold> SupplyTripFor(string actor) =>
        [new(InventoryHoldPurpose.SupplyTrip, actor), new(InventoryHoldPurpose.Borrowed, actor)];

    public static IReadOnlyList<InventoryHold> Family(InventoryIndex index, InventoryLot root) =>
        index.ContentsOf(root.Id).Prepend(root).SelectMany(lot => For(index, lot)).Distinct().ToArray();

    public static IEnumerable<InventoryHold> For(InventoryIndex index, InventoryLot lot) =>
        lot.ContainerLotId is null ? lot.Holds ?? [] : (lot.Holds ?? []).Concat(index.Root(lot).Holds ?? []).Distinct();

    public static bool Allows(InventoryIndex index, InventoryLot lot, IReadOnlyList<InventoryHold>? access = null) =>
        For(index, lot).All(hold => access?.Contains(hold) == true);
}
