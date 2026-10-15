using ClankerWorld.Simulation.Kernel;

namespace ClankerWorld.Tests;

public sealed class InventoryStoreTests
{
    private static InventoryCheckpoint Checkpoint => InventoryAuthority.CreateGenesis(
        [new InventoryLot("stock", "wood", "alice", 4, 10_000, 10_000, 0)]);

    [Fact]
    public void DetachedPreparationDoesNotPublishBeforeTheOriginalAuthorityCommit()
    {
        var before = Checkpoint;
        var stock = before.GetLot("stock");
        var prepared = InventoryStore.PrepareRelocation(stock, "alice", null, null, null);
        Assert.Same(stock, before.GetLot("stock"));
        Assert.Null(stock.CarrierId);

        var committed = InventoryStore.CommitAuthorityTransition(before, lots: [prepared]);

        Assert.Equal("alice", committed.GetLot("stock").CarrierId);
        Assert.Same(stock, before.GetLot("stock"));
        Assert.Empty(before.Events);
    }

    [Fact]
    public void InvalidPreparedQuantityFailsAtStorePublicationWithoutChangingItsBasis()
    {
        var before = Checkpoint;
        var stock = before.GetLot("stock");
        var prepared = InventoryStore.PrepareQuantity(stock, -1);
        Assert.Equal(-1, prepared.Quantity);
        Assert.Throws<ArgumentOutOfRangeException>(() => InventoryStore.CommitAuthorityTransition(before, lots: [prepared]));
        Assert.Equal(4, before.GetLot("stock").Quantity);
        Assert.Same(stock, before.GetLot("stock"));
        Assert.Empty(before.Events);
    }

    [Fact]
    public void DuplicateLotsFailAtTheStoreBeforeAnUpdatedCheckpointCanEscape()
    {
        var before = Checkpoint;
        var stock = before.GetLot("stock");
        var failure = Assert.Throws<InvalidDataException>(() => InventoryStore.CommitAuthorityTransition(before, lots: [stock, stock]));
        Assert.Contains("canonical IDs", failure.Message, StringComparison.Ordinal);
        Assert.Same(stock, Assert.Single(before.Lots));
    }
}
