namespace ClankerWorld.Tests;

public sealed class InventoryWriterBoundaryTests
{
    private static readonly HashSet<string> InventoryRecords = new(StringComparer.Ordinal)
    {
        "InventoryLot", "InventoryReservation", "DirectBarterOffer", "InventoryStorageChange",
        "InventoryEvent", "InventoryCheckpoint", "InventoryHold", "DirectBarterProposal",
    };

    [Fact]
    public void SharedInventoryRecordsHaveOnlyTheirNamedStoreWriter() =>
        RuntimeWriterScan.AssertNamedWriters(InventoryRecords, "InventoryStore");
}
