using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using ClankerWorld.Server.Saves;
using ClankerWorld.Simulation;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;
using ClankerWorld.Simulation.World;

namespace ClankerWorld.Tests;

public sealed class TypedInventoryCompatibilityTests
{
    [Theory]
    [InlineData("reservation", "oversized")]
    [InlineData("reservation", "control")]
    [InlineData("reservation", "padded")]
    [InlineData("offer", "oversized")]
    [InlineData("offer", "control")]
    [InlineData("offer", "padded")]
    public void ExistingInventorySpellingReloadsThroughEveryCheckpointWithoutRewriting(string kind, string spelling)
    {
        using var world = new WorldRuntime("stored-inventory-spelling");
        var state = world.ExportState();
        var first = state.Agents[0];
        var second = state.Agents[1];
        var inventory = InventoryAuthority.AddLot(state.Society.Society.Inventory, "compat-first", "wood", first.AgentId, 1,
            groundPosition: new(first.Position.X, first.Position.Y));
        inventory = InventoryAuthority.AddLot(inventory, "compat-second", "stone", second.AgentId, 1,
            groundPosition: new(second.Position.X, second.Position.Y));
        inventory = AddRecord(inventory, kind, first.AgentId, second.AgentId, "compat-first", "compat-second");
        state = FarmFieldTests.WithInventory(state, inventory);
        var document = JsonNode.Parse(WorldRuntimeCodec.Encode(state))!;
        var collection = kind == "reservation" ? "reservations" : "offers";
        var key = Spelling(spelling);
        document["state"]!["society"]!["society"]!["inventory"]![collection]!.AsArray()[0]!["id"] = key;
        var bytes = JsonSerializer.SerializeToUtf8Bytes(document);
        var directory = Directory.CreateTempSubdirectory("stored-inventory-spelling-");
        try
        {
            var file = new WorldStateFile(Path.Combine(directory.FullName, "world.json"));
            File.WriteAllBytes(file.Path, bytes);
            using var loaded = file.LoadOrCreate(state.WorldSeed);
            var saved = loaded.ExportState();
            Assert.Equal(key, kind == "reservation" ? Assert.Single(loaded.Society.Inventory.Reservations).Id.Value
                : Assert.Single(loaded.Society.Inventory.Offers).Id.Value);
            Assert.Equal(bytes, File.ReadAllBytes(file.Path));
            Assert.Equal(bytes, WorldRuntimeCodec.Encode(saved));
            var kernelBytes = InventoryCheckpointCodec.Encode(saved.Society.Society.Inventory);
            Assert.Equal(kernelBytes, InventoryCheckpointCodec.Encode(InventoryCheckpointCodec.Decode(kernelBytes)));
            var societyBytes = SocietyCheckpointCodec.Encode(saved.Society.Society);
            Assert.Equal(societyBytes, SocietyCheckpointCodec.Encode(SocietyCheckpointCodec.Decode(societyBytes)));
            var runtimeBytes = SocietyRuntimeCodec.Encode(saved.Society);
            Assert.Equal(runtimeBytes, SocietyRuntimeCodec.Encode(SocietyRuntimeCodec.Decode(runtimeBytes)));
        }
        finally { directory.Delete(recursive: true); }
    }

    [Theory]
    [InlineData("reservation", "oversized")]
    [InlineData("reservation", "control")]
    [InlineData("reservation", "padded")]
    [InlineData("offer", "oversized")]
    [InlineData("offer", "control")]
    [InlineData("offer", "padded")]
    public void KernelAdmissionKeepsItsExistingNonblankContract(string kind, string spelling)
    {
        var stock = Stock();
        var key = Spelling(spelling);
        var admitted = kind == "reservation"
            ? InventoryAuthority.Reserve(stock, key, "alpha", "first", 1, "work", 20)
            : InventoryAuthority.CreateDirectBarterOffer(stock,
                new(Identity.ParseStored(key), 1, "alpha", "bravo", "first", 1, "second", 1, 20));
        Assert.Equal(key, kind == "reservation" ? Assert.Single(admitted.Reservations).Id.Value : Assert.Single(admitted.Offers).Id.Value);
        var bytes = InventoryCheckpointCodec.Encode(admitted);
        Assert.Equal(bytes, InventoryCheckpointCodec.Encode(InventoryCheckpointCodec.Decode(bytes)));
        if (kind == "offer")
        {
            admitted = InventoryAuthority.AcceptDirectBarterOffer(admitted, key, 1, "alpha");
            admitted = InventoryAuthority.AcceptDirectBarterOffer(admitted, key, 1, "bravo");
            Assert.Equal(DirectBarterState.Settled, admitted.GetOffer(key).State);
            Assert.All(admitted.Reservations, reservation => Assert.Equal(InventoryReservationState.Completed, reservation.State));
            Assert.Equal(1, Assert.Single(admitted.Lots, lot => lot.OwnerId == "alpha" && lot.ItemKind == "stone").Quantity);
            Assert.Equal(1, Assert.Single(admitted.Lots, lot => lot.OwnerId == "bravo" && lot.ItemKind == "wood").Quantity);
        }
    }

    [Theory]
    [InlineData("reservation")]
    [InlineData("offer")]
    public void NullRequiredInventoryIdsUseTheExistingRefusalInDomainAndJson(string kind)
    {
        var inventory = AddRecord(Stock(), kind, "alpha", "bravo", "first", "second");
        var damaged = kind == "reservation"
            ? inventory with { Reservations = [inventory.Reservations[0] with { Id = null! }] }
            : inventory with { Offers = [inventory.Offers[0] with { Id = null! }] };
        Assert.Throws<InvalidDataException>(() => InventoryCheckpointCodec.Encode(damaged));
        var text = Encoding.UTF8.GetString(InventoryCheckpointCodec.Encode(inventory));
        var separator = text.IndexOf('\n');
        var document = JsonNode.Parse(text[(separator + 1)..])!;
        document[kind == "reservation" ? "reservations" : "offers"]!.AsArray()[0]!["id"] = null;
        var bytes = Encoding.UTF8.GetBytes(text[..(separator + 1)] + document.ToJsonString());
        Assert.Throws<InvalidDataException>(() => InventoryCheckpointCodec.Decode(bytes));
    }

    private static InventoryCheckpoint Stock() => InventoryAuthority.CreateGenesis(
        [new("first", "wood", "alpha", 1, 10_000, 10_000, 0), new("second", "stone", "bravo", 1, 10_000, 10_000, 0)]);

    private static InventoryCheckpoint AddRecord(InventoryCheckpoint inventory, string kind,
        string first, string second, string firstLot, string secondLot)
    {
        if (kind == "reservation") return InventoryAuthority.Reserve(inventory, "compat-reservation", first, firstLot, 1, "work", 20);
        var offered = InventoryAuthority.CreateDirectBarterOffer(inventory,
            new(Identity.Parse("compat-offer"), 1, first, second, firstLot, 1, secondLot, 1, 20));
        return InventoryAuthority.CancelDirectBarterOffer(offered, "compat-offer", 1, first);
    }

    private static string Spelling(string spelling) => spelling switch
    { "oversized" => new string('x', 129), "control" => "control\0key", _ => " padded-key " };
}
