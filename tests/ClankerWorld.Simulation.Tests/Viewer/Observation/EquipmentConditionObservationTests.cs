using System.Text.Json;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Playtest;
using ClankerWorld.Viewer.Observation;
using Client = ClankerWorld.GodotClient.UI;

namespace ClankerWorld.Simulation.Tests;

public sealed class EquipmentConditionObservationTests
{
    [Theory]
    [InlineData(0, 0, 8)]
    [InlineData(1, 1, 24)]
    [InlineData(50, 1, 24)]
    [InlineData(99, 1, 24)]
    [InlineData(100, 1, 24)]
    [InlineData(199, 1, 24)]
    [InlineData(200, 2, 24)]
    [InlineData(10_000, 100, 24)]
    public void EquippedConditionSurvivesCheckpointAndClientProjection(
        int condition, int shownPercent, int capacity)
    {
        using var original = new PrivateWorldRuntime("equipment-condition-proof");
        original.Pause();
        var state = original.ExportState();
        var actor = state.Inhabitants[0].InhabitantId;
        var inventory = InventoryFixture.AddLot(state.Society.Society.Inventory,
            "condition-sack", "sack", actor, 1);
        inventory = InventoryFixture.AddLot(inventory, "condition-clothing", "clothing", actor, 1);
        if (condition < 10_000)
        {
            inventory = InventoryFixture.WearSingleUnit(inventory, "condition-sack", 10_000 - condition);
            inventory = InventoryFixture.WearSingleUnit(inventory, "condition-clothing", 10_000 - condition);
        }
        state = state with
        {
            Society = state.Society with { Society = state.Society.Society with { Inventory = inventory } },
            Inhabitants = state.Inhabitants.Select(person => person.InhabitantId == actor
                ? person with { Equipment = new PersonalEquipment("condition-clothing", "condition-sack") }
                : person).ToArray(),
        };
        var bytes = PrivateWorldRuntimeCodec.Encode(state);
        using var restored = PrivateWorldRuntime.Restore(PrivateWorldRuntimeCodec.Decode(bytes));

        var snapshot = new OwnerWorldObservationStore(restored).GetSnapshot();
        var equipment = snapshot.Inhabitants.Single(person => person.Id == actor).Equipment!;

        Assert.Equal("clothing", equipment.ClothingKind);
        Assert.Equal("sack", equipment.CarryAidKind);
        Assert.Equal(shownPercent, equipment.ClothingConditionPercent);
        Assert.Equal(shownPercent, equipment.CarryAidConditionPercent);
        Assert.Equal(capacity, equipment.Capacity);
        Assert.Equal(condition, restored.Society.Inventory.GetLot("condition-clothing").ConditionBasisPoints);
        Assert.Equal(condition, restored.Society.Inventory.GetLot("condition-sack").ConditionBasisPoints);
        Assert.Equal(bytes, PrivateWorldRuntimeCodec.Encode(restored.ExportState()));

        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var client = JsonSerializer.Deserialize<Client.OwnerWorldSnapshot>(
            JsonSerializer.Serialize(snapshot, options), options)!;
        var displayed = client.Inhabitants.Single(person => person.Id == actor).Equipment!;
        Assert.Equal(shownPercent, displayed.ClothingConditionPercent);
        Assert.Equal(shownPercent, displayed.CarryAidConditionPercent);
        Assert.Equal(capacity, displayed.Capacity);
    }
}
