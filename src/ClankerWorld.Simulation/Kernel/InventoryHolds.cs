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
            ? InventoryStore.PrepareGroundCargo(lot, position, null, null, null)
            : preserveCustody && lot.ContainerLotId is { } container && rootIds.Contains(container, StringComparer.Ordinal)
                ? InventoryStore.PrepareGroundCargo(lot, null, null, null, null) : lot).ToArray());
    }

    public static InventoryCheckpoint AssignDelivery(InventoryCheckpoint checkpoint, string lotId, string? buildingId)
    {
        ValidateCheckpoint(checkpoint);
        var root = InventoryIndex.For(checkpoint).Root(checkpoint.GetLot(lotId));
        return Commit(checkpoint, lots: checkpoint.Lots.Select(lot => lot.Id == root.Id || lot.ContainerLotId == root.Id
            ? InventoryStore.PrepareDeliveryDesignation(lot, buildingId) : lot).ToArray());
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
            ? InventoryStore.PrepareHolds(item, new InventoryHoldSet(holds)) : item).ToArray());
    }

    public static InventoryCheckpoint ReleaseStock(InventoryCheckpoint checkpoint, string lotId, InventoryHold hold)
    {
        ValidateCheckpoint(checkpoint);
        ValidateHold(hold);
        var lot = checkpoint.GetLot(lotId);
        var holds = (lot.Holds ?? []).Where(item => item != hold).ToArray();
        if (holds.Length == (lot.Holds?.Count ?? 0)) return checkpoint;
        return Commit(checkpoint, lots: checkpoint.Lots.Select(item => item.Id == lotId
            ? InventoryStore.PrepareHolds(item, holds.Length == 0 ? null : new InventoryHoldSet(holds)) : item).ToArray());
    }

    private static void ValidateHold(InventoryHold hold) => InventoryStore.ValidateHold(hold);

    private static void ValidateHolds(IReadOnlyList<InventoryHold>? holds) => InventoryStore.ValidateHolds(holds);

    private static InventoryHoldSet? CanonicalHoldAccess(IReadOnlyList<InventoryHold>? access)
    {
        if (access is not { Count: > 0 }) return null;
        var holds = access.Distinct().OrderBy(item => item.Purpose)
            .ThenBy(item => item.HolderId, StringComparer.Ordinal).ToArray();
        ValidateHolds(holds);
        return new InventoryHoldSet(holds);
    }

    // Custody transitions update their own promises atomically with the physical move.
    internal static IReadOnlyList<InventoryLot> UpdateCustodyHolds(InventoryCheckpoint before, IReadOnlyList<InventoryLot> lots)
    {
        InventoryIndex? prior = null;
        InventoryLot[]? updated = null;
        for (var i = 0; i < lots.Count; i++)
        {
            var lot = lots[i];
            if (lot.Holds is null && lot.DeliveryBuildingId is null && lot.CarrierId is null) continue;
            prior ??= InventoryIndex.For(before);
            var source = prior.Find(lot.Id) ?? (lot.ProvenanceLotId is { } id ? prior.Find(id) : null);
            var next = InventoryHoldRules.WithCustody(source ?? InventoryStore.PrepareCustody(lot, null, null), lot);
            if (ReferenceEquals(next, lot)) continue;
            updated ??= lots.ToArray();
            updated[i] = next;
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
    public static InventoryLot WithCustody(InventoryLot previous, InventoryLot next)
    {
        var holds = (next.Holds ?? []).ToList();
        if (previous.DeliveryBuildingId is { } oldDelivery && next.DeliveryBuildingId != oldDelivery)
            holds.Remove(InventoryStore.CreateInventoryHold(InventoryHoldPurpose.Delivery, oldDelivery));
        if (previous.CarrierId is { } oldBorrower && (next.CarrierId != oldBorrower || next.OwnerId == oldBorrower))
            holds.Remove(InventoryStore.CreateInventoryHold(InventoryHoldPurpose.Borrowed, oldBorrower));
        if (next.DeliveryBuildingId is { } delivery) holds.Add(InventoryStore.CreateInventoryHold(InventoryHoldPurpose.Delivery, delivery));
        if (next.CarrierId is { } borrower && borrower != next.OwnerId) holds.Add(InventoryStore.CreateInventoryHold(InventoryHoldPurpose.Borrowed, borrower));
        var canonical = holds.Count == 0 ? null : new InventoryHoldSet(holds.Distinct().OrderBy(hold => hold.Purpose)
            .ThenBy(hold => hold.HolderId, StringComparer.Ordinal));
        return (next.Holds ?? []).SequenceEqual(canonical ?? []) ? next : InventoryStore.PrepareHolds(next, canonical);
    }

    public static IReadOnlyList<InventoryHold> BorrowedBy(string actor) => [InventoryStore.CreateInventoryHold(InventoryHoldPurpose.Borrowed, actor)];

    public static IReadOnlyList<InventoryHold> DeliveryFor(string actor, string? building) => building is null
        ? BorrowedBy(actor) : [InventoryStore.CreateInventoryHold(InventoryHoldPurpose.Delivery, building), InventoryStore.CreateInventoryHold(InventoryHoldPurpose.Borrowed, actor)];

    public static IReadOnlyList<InventoryHold> SupplyTripFor(string actor) =>
        [InventoryStore.CreateInventoryHold(InventoryHoldPurpose.SupplyTrip, actor), InventoryStore.CreateInventoryHold(InventoryHoldPurpose.Borrowed, actor)];

    public static IReadOnlyList<InventoryHold> Family(InventoryIndex index, InventoryLot root) =>
        index.ContentsOf(root.Id).Prepend(root).SelectMany(lot => For(index, lot)).Distinct().ToArray();

    public static IEnumerable<InventoryHold> For(InventoryIndex index, InventoryLot lot) =>
        lot.ContainerLotId is null ? lot.Holds ?? [] : (lot.Holds ?? []).Concat(index.Root(lot).Holds ?? []).Distinct();

    public static bool Allows(InventoryIndex index, InventoryLot lot, IReadOnlyList<InventoryHold>? access = null) =>
        For(index, lot).All(hold => access?.Contains(hold) == true);
}
