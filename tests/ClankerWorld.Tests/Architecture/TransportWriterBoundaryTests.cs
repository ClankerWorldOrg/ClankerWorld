namespace ClankerWorld.Tests;

public sealed class TransportWriterBoundaryTests
{
    private static readonly HashSet<string> VehicleRecords = new(StringComparer.Ordinal)
    {
        "BoatState", "BoatJourney", "BoatTripRequest", "HandcartHitch", "RetiredBoatRequestRange",
    };

    [Fact]
    public void VehiclesAndSavedTransportStateHaveOneWriter() =>
        RuntimeWriterScan.AssertNamedWriters(VehicleRecords, "TransportSystem", "BoatTransportState");
}
