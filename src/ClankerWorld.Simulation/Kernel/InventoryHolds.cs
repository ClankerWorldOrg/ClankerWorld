namespace ClankerWorld.Simulation.Kernel;

public enum InventoryHoldPurpose { MarketStall, Delivery, SupplyTrip, Borrowed }

/// <summary>A promise covering every unit of a physical lot, including its vessel family.</summary>
public sealed record InventoryHold(InventoryHoldPurpose Purpose, string HolderId);

public static partial class InventoryAuthority
{
    public static InventoryCheckpoint HoldStock(InventoryCheckpoint checkpoint, string lotId, InventoryHold hold)
    {
        ValidateCheckpoint(checkpoint);
        ValidateHold(hold);
        var lot = checkpoint.GetLot(lotId);
        if ((lot.Holds ?? []).Contains(hold)) return checkpoint;
        var holds = (lot.Holds ?? []).Append(hold).OrderBy(item => item.Purpose)
            .ThenBy(item => item.HolderId, StringComparer.Ordinal).ToArray();
        return Commit(checkpoint, lots: checkpoint.Lots.Select(item => item.Id == lotId
            ? item with { Holds = holds } : item).ToArray());
    }

    public static InventoryCheckpoint ReleaseStock(InventoryCheckpoint checkpoint, string lotId, InventoryHold hold)
    {
        ValidateCheckpoint(checkpoint);
        ValidateHold(hold);
        var lot = checkpoint.GetLot(lotId);
        var holds = (lot.Holds ?? []).Where(item => item != hold).ToArray();
        if (holds.Length == (lot.Holds?.Count ?? 0)) return checkpoint;
        return Commit(checkpoint, lots: checkpoint.Lots.Select(item => item.Id == lotId
            ? item with { Holds = holds.Length == 0 ? null : holds } : item).ToArray());
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

    private static InventoryHold[]? CanonicalHoldAccess(IReadOnlyList<InventoryHold>? access)
    {
        if (access is not { Count: > 0 }) return null;
        var holds = access.Distinct().OrderBy(item => item.Purpose)
            .ThenBy(item => item.HolderId, StringComparer.Ordinal).ToArray();
        ValidateHolds(holds);
        return holds;
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
    public static IEnumerable<InventoryHold> For(InventoryIndex index, InventoryLot lot) =>
        lot.ContainerLotId is null ? lot.Holds ?? [] : (lot.Holds ?? []).Concat(index.Root(lot).Holds ?? []).Distinct();

    public static bool Allows(InventoryIndex index, InventoryLot lot, IReadOnlyList<InventoryHold>? access = null) =>
        For(index, lot).All(hold => access?.Contains(hold) == true);
}
