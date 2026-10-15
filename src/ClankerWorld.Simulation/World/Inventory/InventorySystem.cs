using System.Collections.ObjectModel;
using System.Globalization;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

internal static partial class InventorySystem
{
    internal abstract class Command
    {
        private protected Command() { }
        internal abstract InventoryCheckpoint Transform(InventoryCheckpoint inventory);
    }

    private sealed class PreparedCommand(Func<InventoryCheckpoint, InventoryCheckpoint> transform) : Command
    {
        internal override InventoryCheckpoint Transform(InventoryCheckpoint inventory) => transform(inventory);
    }

    private static T RecordPreparedValue<T>(T value, Action<T> record)
    {
        record(value);
        return value;
    }

    private static T RecordPreparedOutputs<T>(Func<T> operation, Action record)
    {
        try { return operation(); }
        finally { record(); }
    }

    internal static SocietyOperationResult Execute(IInventoryExecutionWorld world, SocietyCheckpoint checkpoint, Command command)
    {
        var equipmentBefore = world.Agents.Values.ToDictionary(person => person.AgentId, person => person.Equipment, StringComparer.Ordinal);
        var updated = InventoryStorageHistory.RecordTransition(checkpoint.Inventory, MarketStockHolds.Update(command.Transform(checkpoint.Inventory), world.Towns));
        foreach (var building in world.Simulation.Buildings)
        {
            var definition = world.Content.Buildings.Single(item => item.CanonicalId == building.DefinitionId);
            if (BuildingStorageRules.Capacity(definition, building) is not { } capacity)
                continue;
            var before = checkpoint.Inventory.Lots.Where(lot => lot.StorageBuildingId == building.InstanceId).Sum(lot => lot.Quantity) + world.ReservedShopStorageSpace(building.InstanceId, checkpoint.Inventory);
            var after = updated.Lots.Where(lot => lot.StorageBuildingId == building.InstanceId).Sum(lot => lot.Quantity) + world.ReservedShopStorageSpace(building.InstanceId, updated);
            if (after > capacity && after > before)
                throw new InvalidOperationException("The building's storage is full; carry the remaining stock or expand it first.");
        }

        foreach (var person in world.Agents.Values)
        {
            var before = PersonalEquipmentRules.CarriedQuantity(checkpoint.Inventory, person.AgentId, equipmentBefore[person.AgentId]) + world.ReservedShopCarrySpace(person.AgentId, checkpoint.Inventory);
            var after = PersonalEquipmentRules.CarriedQuantity(updated, person.AgentId, person.Equipment) + world.ReservedShopCarrySpace(person.AgentId, updated);
            if (after > before && after > PersonalEquipmentRules.Capacity(updated, person.AgentId, person.Equipment) + world.HorseCargoCapacity(person.AgentId))
                throw new InvalidOperationException("The person is carrying as much as they can; store or set down a load first.");
        }

        return new SocietyOperationResult(checkpoint with { Inventory = updated }, null, []);
    }
}
