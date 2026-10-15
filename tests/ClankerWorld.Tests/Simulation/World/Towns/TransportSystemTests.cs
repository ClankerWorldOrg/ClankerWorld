using ClankerWorld.Simulation.World;

namespace ClankerWorld.Tests;

public sealed class TransportSystemTests
{
    [Fact]
    public void DuplicateAttachmentFailsAtItsOwnerBeforePublishing()
    {
        var system = new TransportSystem();
        system.AttachHandcart("cart-a", "owner-a");
        var original = Assert.Single(system.Hitches);
        Assert.Throws<InvalidDataException>(() => system.AttachHandcart("cart-a", "owner-b"));
        Assert.Throws<InvalidDataException>(() => system.AttachHandcart("cart-b", "owner-a"));
        Assert.Same(original, Assert.Single(system.Hitches));
    }

    [Fact]
    public void InvalidBoatJourneyFailsBeforeReplacingTheDockedAsset()
    {
        var system = new TransportSystem();
        var boat = new BoatState("boat-a", "town-a", "project-a", new(1, 2), "port-a");
        system.RecordBoat(boat);
        var invalid = boat with { Journey = new("request-a", "passenger", "port-a", "port-b", new(2, 2), [new(1, 2)], 1, 0, 3) };
        Assert.Throws<InvalidDataException>(() => system.RecordBoat(invalid));
        Assert.Same(boat, Assert.Single(system.Boats.Boats));
    }

    [Fact]
    public void RestoredTransportKeepsIndependentMutableCollectionsAndSelfRestoreKeepsTheirOrder()
    {
        var original = new TransportSystem();
        original.AttachHandcart("cart-b", "owner-b");
        original.AttachHandcart("cart-a", "owner-a");
        original.RestoreRoads([new(2, 1), new(1, 1)]);
        var restored = new TransportSystem();
        restored.RestoreVehicles(original.Hitches, original.Boats);
        restored.RestoreRoads(original.Roads);
        restored.RestoreVehicles(restored.Hitches, restored.Boats);
        restored.RestoreRoads(restored.Roads);
        Assert.Equal(original.Hitches, restored.Hitches);
        Assert.Equal(original.Roads.ToArray(), restored.Roads.ToArray());
        restored.DetachHandcart(restored.Hitches[0]);
        restored.LayRoadTile(new(3, 1));
        Assert.Equal(2, original.Hitches.Count);
        Assert.Equal(2, original.Roads.Count);
    }
}
