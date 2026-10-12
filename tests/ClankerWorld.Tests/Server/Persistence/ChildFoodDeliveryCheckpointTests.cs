using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.World;
using ClankerWorld.Simulation.Society;
using ClankerWorld.Server.Runtime;
using ClankerWorld.Server.Saves;

namespace ClankerWorld.Tests;

public sealed class ChildFoodDeliveryCheckpointTests
{
    private const string Adult = "founder:00000000000000000000000000000001";
    private const string Child = "founder:00000000000000000000000000000002";
    private const string Household = "household:camp-alpha";
    private const string House = "first-town-house-a";
    private const string Food = "child-spare-food";
    private static readonly Lazy<byte[]> Generated = new(() =>
    {
        using var world = NormalPathWorld.CreateGenerated("social-audit-d9efa858", _ => new ActionCoverageRecorder(chooseIdle: true));
        return WorldRuntimeCodec.Encode(world.ExportState());
    });

    [Theory]
    [InlineData(0, false, false)]
    [InlineData(1, false, true)]
    [InlineData(1, true, false)]
    public async Task ChildKeepsFoodWhenHouseRoomIsFullOrPromisedAndCanDeliverWhenRoomExists(
        int room, bool inbound, bool delivers)
    {
        var state = Prepared(room, inbound);
        var child = new ActionCoverageRecorder();
        IDecisionProvider Providers(string id) => id == Child ? child : new ActionCoverageRecorder(chooseIdle: true);
        using var world = WorldRuntime.Restore(state, Providers);
        using var host = new CheckpointHost(world);
        await host.AdvanceAndCheckSaved();
        if (delivers)
        {
            Assert.True(child.Chosen.ContainsKey("child_help_food"));
            Assert.Equal((Child, 1), (world.Society.Inventory.GetLot(Food).OwnerId, world.Society.Inventory.GetLot(Food).Quantity));
            var donated = Assert.Single(world.Society.Inventory.Lots, lot => lot.ProvenanceLotId == Food);
            Assert.Equal((Household, House, 1), (donated.OwnerId, donated.StorageBuildingId, donated.Quantity));
            Assert.Single(world.ExportState().Events, item => item.Kind == "child_helped_household" && item.Detail == Child);
        }
        else
        {
            Assert.DoesNotContain("child_help_food", child.OfferedByAgent[Child].Keys);
            Assert.Equal((Child, 2), (world.Society.Inventory.GetLot(Food).OwnerId, world.Society.Inventory.GetLot(Food).Quantity));
            Assert.DoesNotContain(world.ExportState().Events, item => item.Kind == "child_helped_household" && item.Detail == Child);
        }
        Assert.Equal(64 - room + (delivers ? 1 : 0), Stored(world));
        if (inbound)
            Assert.Equal((Adult, House, 1), (world.Society.Inventory.GetLot("promised-wood").OwnerId,
                world.Society.Inventory.GetLot("promised-wood").DeliveryBuildingId,
                world.Society.Inventory.GetLot("promised-wood").Quantity));
        await ReplaySavedSteps(world, host, Providers);
    }

    [Fact]
    public async Task ChildRechecksRoomAfterAnEarlierAdultStoresWoodInTheSameTick()
    {
        var state = Prepared(room: 1);
        var inventory = InventoryAuthority.AddLot(state.Society.Society.Inventory, "adult-spare-wood", "wood", Adult, 1);
        state = WithInventory(state, inventory);
        var child = new ActionCoverageRecorder();
        IDecisionProvider Providers(string id) => id == Child ? child : id == Adult
            ? new DeterministicDecisionProvider() : new ActionCoverageRecorder(chooseIdle: true);
        using var world = WorldRuntime.Restore(state, Providers);
        var receipt = world.SubmitInstruction(new OwnerInstructionRequest("store-last-slot", "owner:test", Adult,
            OwnerInstructionKind.Order, "store wood"));
        using var host = new CheckpointHost(world);
        await host.AdvanceAndCheckSaved();

        // Both choices were offered before either action. The first actor's
        // real storage order fills the House before the child's admitted action.
        Assert.True(child.Chosen.ContainsKey("child_help_food"));
        var order = Assert.Single(world.ExportState().Instructions!, item => item.InstructionId == receipt.InstructionId).Task!;
        Assert.Equal(("finished", 1), (order.Status, order.CompletedUnits));
        Assert.Equal(House, world.Society.Inventory.GetLot("adult-spare-wood").StorageBuildingId);
        Assert.Equal((Child, 2), (world.Society.Inventory.GetLot(Food).OwnerId, world.Society.Inventory.GetLot(Food).Quantity));
        Assert.DoesNotContain(world.ExportState().Events, item => item.Kind == "child_helped_household" && item.Detail == Child);
        Assert.Equal(64, Stored(world));
        await ReplaySavedSteps(world, host, Providers);
    }

    private static WorldRuntimeState Prepared(int room, bool inbound = false)
    {
        var state = WorldRuntimeCodec.Decode(Generated.Value);
        var society = state.Society.Society;
        var house = state.WorldSimulation!.Buildings.Single(item => item.InstanceId == House);
        var definition = state.WorldContent!.Buildings.Single(item => item.CanonicalId == house.DefinitionId);
        var capacity = BuildingStorageRules.Capacity(definition, house)!.Value;
        Assert.Equal(64, capacity);
        var stored = society.Inventory.Lots.Where(lot => lot.StorageBuildingId == House).Sum(lot => lot.Quantity);
        var inventory = InventoryAuthority.AddLot(society.Inventory, "house-padding", "wood", Household,
            capacity - stored - room, storageBuildingId: House);
        inventory = InventoryAuthority.AddLot(inventory, Food, "food", Child, 2);
        if (inbound)
        {
            inventory = InventoryAuthority.AddLot(inventory, "promised-wood", "wood", Household, 1);
            inventory = InventoryAuthority.Transfer(inventory, "promised-pickup", Household, Adult, "promised-wood", 1,
                "household_stock_picked_up", destinationDeliveryBuildingId: House);
        }
        // Controlled starting age and supplies. No birth, trip or delivery
        // progress is claimed; all subsequent choices use the actual runtime.
        var birth = society.LifeTickAt(society.WorldTick) - 4 * society.Config.TicksPerLifecycleAge;
        state = state with
        {
            Society = state.Society with
            {
                Society = society with
                {
                    Inventory = inventory,
                    Agents = society.Agents.Select(person => person.Id == Child ? person with
                    {
                        BirthTick = birth,
                        BirthLifeTick = society.LifeClock is null ? null : birth,
                        AgeBand = SocietyAgeBand.Child,
                        LastLifecycleYearChecked = 4,
                        CurrentRole = SocietyWorkRole.Unassigned,
                    } : person).ToArray(),
                },
            },
            Agents = state.Agents.Select(person => person.AgentId is Child or Adult ? person with
            {
                Position = house.Position,
                FullnessBasisPoints = 9_000,
                LastDecisionContext = null,
            } : person).ToArray(),
            Towns = state.Towns!.Select(town => town.Council is { } council ? town with
            {
                Council = council with { Members = council.Members.Where(id => id != Child).ToArray() },
            } : town).ToArray(),
        };
        var bytes = WorldRuntimeCodec.Encode(state);
        using var validated = WorldRuntime.Restore(WorldRuntimeCodec.Decode(bytes));
        Assert.Equal(bytes, WorldRuntimeCodec.Encode(validated.ExportState()));
        return validated.ExportState();
    }

    private static WorldRuntimeState WithInventory(WorldRuntimeState state, InventoryCheckpoint inventory) =>
        state with { Society = state.Society with { Society = state.Society.Society with { Inventory = inventory } } };

    private static int Stored(WorldRuntime world) =>
        world.Society.Inventory.Lots.Where(lot => lot.StorageBuildingId == House).Sum(lot => lot.Quantity);

    private static async Task ReplaySavedSteps(WorldRuntime world, CheckpointHost host, Func<string, IDecisionProvider> providers)
    {
        using var replay = WorldRuntime.Restore(WorldRuntimeCodec.Decode(host.Saved()), providers);
        Assert.Equal(host.Saved(), WorldRuntimeCodec.Encode(replay.ExportState()));
        using var replayHost = new CheckpointHost(replay);
        world.Pause();
        replay.Pause();
        world.Resume();
        replay.Resume();
        for (var step = 0; step < 3; step++)
        {
            await host.AdvanceAndCheckSaved();
            await replayHost.AdvanceAndCheckSaved();
            Assert.Equal(host.Saved(), replayHost.Saved());
        }
    }

    private sealed class CheckpointHost : IDisposable
    {
        private readonly DirectoryInfo directory = Directory.CreateTempSubdirectory("clanker-child-food-checkpoint-");
        private readonly WorldRuntime world;
        private readonly WorldRuntimeService service;
        private readonly RecordingLogger<WorldRuntimeService> logger = new();
        private readonly WorldStateFile file;

        public CheckpointHost(WorldRuntime world)
        {
            this.world = world;
            file = new WorldStateFile(Path.Combine(directory.FullName, "world.json"));
            file.Save(world);
            var presence = new OwnerClientPresenceLease(TimeSpan.FromMinutes(5));
            presence.RecordAuthenticatedReconnect("test-owner");
            service = new WorldRuntimeService(world, file, presence, logger);
        }

        public byte[] Saved() => File.ReadAllBytes(file.Path);

        public async Task AdvanceAndCheckSaved()
        {
            Assert.True(await service.TryAdvanceOnceAsync(), string.Join("\n", logger.Messages));
            Assert.False(world.Society.IsPaused);
            Assert.Equal(world.WorldTick, WorldRuntimeCodec.Decode(Saved()).Society.Society.WorldTick);
            Assert.Equal(WorldRuntimeCodec.Encode(world.ExportState()), Saved());
        }

        public void Dispose()
        {
            service.Dispose();
            directory.Delete(recursive: true);
        }
    }
}
