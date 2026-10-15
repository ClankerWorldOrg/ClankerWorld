using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;
using ClankerWorld.Simulation.World;

namespace ClankerWorld.Tests;

public sealed class InventoryCommandTests
{
    [Fact]
    public void PreparedCommandUsesTheCheckpointSuppliedAtExecution()
    {
        var original = InventoryAuthority.CreateGenesis([new InventoryLot("stock", "wood", "alice", 4, 10_000, 10_000, 0)]);
        var command = InventorySystem.DiscardDeveloperStock("alice", original.GetLot("stock"), 1);
        var current = InventoryAuthority.Discard(original, "alice", "stock", 1);

        var updated = command.Transform(current);

        Assert.Equal(4, original.GetLot("stock").Quantity);
        Assert.Equal(3, current.GetLot("stock").Quantity);
        Assert.Equal(2, updated.GetLot("stock").Quantity);
    }

    [Fact]
    public void RelocationResultIsRecordedDuringExecutionBeforeCapacityQueries()
    {
        var before = SocietyRules.CreateGenesis("inventory-command");
        before = before with { Inventory = InventoryAuthority.CreateGenesis([new InventoryLot("stock", "wood", "alice", 1, 10_000, 10_000, 0)]) };
        long recorded = 0;
        var command = InventorySystem.DepositMarketLoad("alice", before.Inventory.GetLot("stock"), 1, "deposit", new(2, 3), 0, value => recorded = value);
        Assert.Equal(0, recorded);

        var result = InventorySystem.Execute(new EmptyWorld(() => Assert.True(recorded > 0)), before, command);

        Assert.Equal(result.Checkpoint.Inventory.Events[^1].EventId, recorded);
        Assert.Equal(new InventoryGroundPosition(2, 3), result.Checkpoint.Inventory.GetLot("stock").GroundPosition);
        Assert.Null(before.Inventory.GetLot("stock").GroundPosition);
    }

    private sealed class EmptyWorld(Action observeResult) : IInventoryExecutionWorld
    {
        private int reads;
        public IReadOnlyDictionary<string, AgentState> Agents
        {
            get
            {
                if (++reads > 1) observeResult();
                return new Dictionary<string, AgentState>();
            }
        }
        public IReadOnlyList<TownRuntimeState> Towns => [];
        public WorldContentSimulationState Simulation => WorldContentSimulationState.Empty;
        public DeclarativeWorldContentState Content { get; } = new([], []);
        public int ReservedShopStorageSpace(string building, InventoryCheckpoint inventory) => 0;
        public int ReservedShopCarrySpace(string actor, InventoryCheckpoint inventory) => 0;
        public int HorseCargoCapacity(string actor) => 0;
    }
}
