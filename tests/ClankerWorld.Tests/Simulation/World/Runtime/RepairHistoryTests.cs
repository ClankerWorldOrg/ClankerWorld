using System.Text.Json;
using ClankerWorld.Server.Saves;
using ClankerWorld.Simulation;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.World;

namespace ClankerWorld.Tests;

public sealed class RepairHistoryTests
{
    private static readonly Lazy<byte[]> PaidHistory = new(() =>
    {
        using var world = NormalPathWorld.CreateGenerated("repair-history", _ => new ActionCoverageRecorder(chooseIdle: true));
        return WorldRuntimeCodec.Encode(AddPaidRepairs(world.ExportState()));
    });

    [Fact]
    public void RepeatedPaidRepairsHaveABoundedLiveReceiptWindow()
    {
        var state = WorldRuntimeCodec.Decode(PaidHistory.Value);
        var plan = WorldHistory.Prepare(state);
        Assert.Equal(32, plan.State.Society.Society.Inventory.Reservations.Count(item => item.Purpose == "equipment_repair"));
        Assert.Equal(36, state.Society.Society.Inventory.Reservations.Count(item => item.Purpose == "equipment_repair"));
    }

    [Fact]
    public async Task SavingArchivesExactReceiptsAndKeepsClaimsGoodsAndReplay()
    {
        var state = WorldRuntimeCodec.Decode(PaidHistory.Value);
        var inventory = state.Society.Society.Inventory;
        var actor = state.Agents[0].AgentId;
        inventory = InventoryAuthority.AddLot(inventory, "history-pot", "storage_pot", actor, 1);
        inventory = InventoryAuthority.AddLot(inventory, "history-seed", "berries", actor, 1, containerLotId: "history-pot");
        inventory = InventoryAuthority.Reserve(inventory, "active-family", actor, "history-seed", 1, "equipment_repair", inventory.WorldTick + 100);
        inventory = InventoryAuthority.AddLot(inventory, "other-input", "wood", actor, 1);
        inventory = InventoryAuthority.Reserve(inventory, "other-work", actor, "other-input", 1, "other_work", inventory.WorldTick + 100);
        inventory = InventoryAuthority.ConsumeReservation(inventory, "other-work");
        state = FarmFieldTests.WithInventory(state, inventory);
        using var world = Restore(state);
        var directory = Directory.CreateTempSubdirectory("clanker-repair-history-");
        try
        {
            var file = new WorldStateFile(Path.Combine(directory.FullName, "world.json"), _ => new ActionCoverageRecorder(chooseIdle: true));
            Assert.True(file.Save(world));
            var saved = world.ExportState();
            var compacted = saved.Society.Society.Inventory;
            Assert.Equal(4, compacted.ArchivedRepairReservationCount);
            Assert.Equal(32, compacted.Reservations.Count(item => item.Purpose == "equipment_repair" && item.State == InventoryReservationState.Completed));
            Assert.Equal(inventory.GetReservation("active-family"), compacted.GetReservation("active-family"));
            Assert.Equal(inventory.GetReservation("other-work"), compacted.GetReservation("other-work"));
            Assert.Equal(JsonSerializer.Serialize(inventory.Lots), JsonSerializer.Serialize(compacted.Lots));
            Assert.Equal(JsonSerializer.Serialize(inventory.Offers), JsonSerializer.Serialize(compacted.Offers));
            Assert.Equal(JsonSerializer.Serialize(inventory.Events), JsonSerializer.Serialize(compacted.Events));
            Assert.Equal(0, InventoryRules.AvailableQuantity(InventoryIndex.For(compacted), compacted.GetLot("history-seed")));
            Assert.Throws<InvalidOperationException>(() => InventoryAuthority.Reserve(compacted, "history:repair:35", actor,
                "history-seed", 1, "equipment_repair", compacted.WorldTick + 100));
            var archive = Path.Combine(file.Path + ".history", saved.HistoryArchiveHead + ".json");
            var segment = JsonSerializer.Deserialize<WorldHistorySegment>(File.ReadAllBytes(archive))!;
            Assert.Equal(JsonSerializer.Serialize(inventory.Reservations.Where(item => item.Purpose == "equipment_repair" &&
                item.State == InventoryReservationState.Completed).OrderBy(item => item.ExpiryTick).Take(4)),
                segment.Streams.Single(batch => batch.Name == "repair-reservations").Events.GetRawText());
            var bytes = WorldRuntimeCodec.Encode(saved);
            Assert.Equal(bytes, File.ReadAllBytes(file.Path));
            Assert.Equal(InventoryCheckpointCodec.Encode(compacted), InventoryCheckpointCodec.Encode(
                InventoryCheckpointCodec.Decode(InventoryCheckpointCodec.Encode(compacted))));
            using var loaded = file.LoadOrCreate(state.WorldSeed);
            Assert.Equal(bytes, WorldRuntimeCodec.Encode(loaded.ExportState()));
            Assert.False((await world.AdvanceOneTickAsync(() => false)).Advanced);
            Assert.Equal(bytes, WorldRuntimeCodec.Encode(world.ExportState()));
            Assert.True((await world.AdvanceOneTickAsync()).Advanced);
            Assert.True((await loaded.AdvanceOneTickAsync()).Advanced);
            Assert.Equal(WorldRuntimeCodec.Encode(world.ExportState()), WorldRuntimeCodec.Encode(loaded.ExportState()));
            Assert.Equal(4, world.Society.Inventory.ArchivedRepairReservationCount);
            using var later = Restore(AddPaidRepairs(world.ExportState(), "later"));
            Assert.True(file.Save(later));
            Assert.Equal(40, later.Society.Inventory.ArchivedRepairReservationCount);
            using var continued = file.LoadOrCreate(state.WorldSeed);
            Assert.Equal(WorldRuntimeCodec.Encode(later.ExportState()), WorldRuntimeCodec.Encode(continued.ExportState()));
            File.WriteAllText(archive, "corrupt");
            Assert.Throws<InvalidDataException>(() => file.LoadOrCreate(state.WorldSeed));
            Assert.Equal(WorldRuntimeCodec.Encode(later.ExportState()), File.ReadAllBytes(file.Path));
        }
        finally { directory.Delete(recursive: true); }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FailedWritesLeaveEveryReceiptAndThePreviousSave(bool failCheckpoint)
    {
        var original = PaidHistory.Value;
        using var world = Restore(WorldRuntimeCodec.Decode(original));
        var directory = Directory.CreateTempSubdirectory("clanker-repair-failure-");
        try
        {
            var file = new WorldStateFile(Path.Combine(directory.FullName, "world.json"));
            File.WriteAllBytes(file.Path, original);
            if (failCheckpoint)
            {
                File.Move(file.Path, file.Path + ".previous");
                Directory.CreateDirectory(file.Path);
            }
            else File.WriteAllText(file.Path + ".history", "block archive creation");
            Assert.Throws<IOException>(() => file.Save(world));
            Assert.Equal(original, WorldRuntimeCodec.Encode(world.ExportState()));
            Assert.Equal(original, File.ReadAllBytes(failCheckpoint ? file.Path + ".previous" : file.Path));
        }
        finally { directory.Delete(recursive: true); }
    }

    [Fact]
    public void TheCheckpointMustAccountForExactlyItsArchivedReceipts()
    {
        using var world = Restore(WorldRuntimeCodec.Decode(PaidHistory.Value));
        var directory = Directory.CreateTempSubdirectory("clanker-repair-count-");
        try
        {
            var file = new WorldStateFile(Path.Combine(directory.FullName, "world.json"));
            Assert.True(file.Save(world));
            var saved = world.ExportState();
            var damaged = FarmFieldTests.WithInventory(saved, saved.Society.Society.Inventory with { ArchivedRepairReservationCount = 3 });
            var bytes = WorldRuntimeCodec.Encode(damaged);
            File.WriteAllBytes(file.Path, bytes);
            Assert.Throws<InvalidDataException>(() => file.LoadOrCreate(saved.WorldSeed));
            Assert.Equal(bytes, File.ReadAllBytes(file.Path));
            Assert.Throws<ArgumentOutOfRangeException>(() => WorldRuntimeCodec.Encode(FarmFieldTests.WithInventory(saved,
                saved.Society.Society.Inventory with { ArchivedRepairReservationCount = -1 })));
        }
        finally { directory.Delete(recursive: true); }
    }

    internal static WorldRuntimeState SaveAfterLaterRepairs(WorldRuntimeState state)
    {
        using var world = Restore(AddPaidRepairs(state));
        var directory = Directory.CreateTempSubdirectory("clanker-repair-proof-");
        try
        {
            var file = new WorldStateFile(Path.Combine(directory.FullName, "world.json"));
            Assert.True(file.Save(world));
            using var loaded = file.LoadOrCreate(state.WorldSeed);
            Assert.Equal(WorldRuntimeCodec.Encode(world.ExportState()), WorldRuntimeCodec.Encode(loaded.ExportState()));
            return world.ExportState();
        }
        finally { directory.Delete(recursive: true); }
    }

    private static WorldRuntime Restore(WorldRuntimeState state) => WorldRuntime.Restore(state,
        _ => new ActionCoverageRecorder(chooseIdle: true));

    internal static WorldRuntimeState AddPaidRepairs(WorldRuntimeState state, string scope = "history")
    {
        var actor = state.Agents[0].AgentId;
        var inventory = state.Society.Society.Inventory;
        var target = DerivedId.Create(scope + ":target");
        inventory = InventoryAuthority.AddLot(inventory, target, "clothing", actor, 1);
        // Only supplies are controlled. Wear, exact reservation, consumption and
        // condition restoration use the real inventory transitions.
        for (var index = 0; index < 36; index++)
        {
            var input = DerivedId.Create(scope + ":cloth:" + index);
            var claim = DerivedId.Create(scope + ":repair:" + index);
            inventory = InventoryAuthority.WearSingleUnit(inventory, target, 6_000);
            inventory = InventoryAuthority.AddLot(inventory, input, "cloth", actor, 1);
            inventory = InventoryAuthority.Reserve(inventory, claim, actor, input, 1, "equipment_repair", inventory.WorldTick + 1_000 + index);
            inventory = InventoryAuthority.RepairSingleUnit(inventory, target, 6_000, [claim]);
            Assert.DoesNotContain(inventory.Lots, lot => lot.Id == input);
        }
        return FarmFieldTests.WithInventory(state, inventory);
    }
}
