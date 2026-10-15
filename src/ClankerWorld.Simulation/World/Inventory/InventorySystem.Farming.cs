using System.Collections.ObjectModel;
using System.Globalization;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

internal static partial class InventorySystem
{
    internal static Command ApplyFieldCandidate(IFarmingWorld world, string actor, FarmFieldState field, InventoryLot seed) => new PreparedCommand(inventory =>
{
    if (field.ReplantingReservationId is { } id && inventory.Reservations.Any(item => item.Id == id && item.LotId == seed.Id && item.State == InventoryReservationState.Reserved))
        inventory = InventoryAuthority.ReleaseReservation(inventory, id, "planting_stock_collected");
    return InventoryAuthority.Transfer(inventory, $"field-seed-pickup:{world.WorldTick}:{actor}", field.HouseholdId, actor, seed.Id, 1, "field_seed_picked_up", holdAccess: InventoryHoldRules.BorrowedBy(actor));
});

    internal static Command ApplyFieldPlan(IFarmingWorld world, FieldActionPlan plan, PlannedFieldSeed seed) => new PreparedCommand(inventory =>
{
    if (seed.ReplantingReservationId is { } id)
        inventory = InventoryAuthority.ReleaseReservation(inventory, id, "planting_stock_collected");
    return InventoryAuthority.Transfer(inventory, $"field-seed-pickup:{world.WorldTick}:{plan.Actor}", plan.HouseholdId, plan.Actor, seed.StockId, 1, "field_seed_picked_up", holdAccess: InventoryHoldRules.BorrowedBy(plan.Actor));
});

    internal static Command PlantTreeCore(InventoryReservation reservation) => new PreparedCommand(inventory => InventoryAuthority.ReleaseReservation(inventory, reservation.Id));

    internal static Command StartFieldWorkCore(string workerId, string? reservationId, InventoryLot seed) => new PreparedCommand(inventory => InventoryAuthority.Reserve(inventory, reservationId!, workerId, seed.Id, 1, "field_planting", long.MaxValue));

    internal static Command IsolateFieldToolForWork(string workerId, InventoryLot lot, string isolatedId) => new PreparedCommand(current => InventoryAuthority.SplitLot(current, lot.Id, 1, isolatedId, holdAccess: InventoryHoldRules.BorrowedBy(workerId)));

    internal static Command ReturnFieldToGrass(string reserve) => new PreparedCommand(inventory => InventoryAuthority.ReleaseReservation(inventory, reserve, "field_returned_to_grass"));

    internal static Command CancelFarmWork(string reservationId) => new PreparedCommand(inventory => InventoryAuthority.ReleaseReservation(inventory, reservationId, "field_work_interrupted"));

    internal static Command ContinueFarmWork(IFarmingWorld world, FarmFieldWork work, FarmFieldState field) => new PreparedCommand(inventory =>
{
    if (field.ReplantingReservationId is { } previous && FarmingSystem.ActiveFarmReservation(world, previous))
        inventory = InventoryAuthority.ReleaseReservation(inventory, previous, "field_replanting_completed");
    return InventoryAuthority.ConsumeReservation(inventory, work.SeedReservationId!);
});

    internal static Command CompleteFieldHarvest(IFarmingWorld world, FarmFieldState field, IReadOnlyList<ToolWorkPlan> toolPlans, int cycle, string crop, string plantingItem, string plantingLotId, string replantId, int quantity) => new PreparedCommand(inventory =>
{
    var next = InventoryAuthority.AddLot(inventory, WorldRuntime.FarmingSharedRules.FieldHarvestId(field.Position, cycle, "crop"), crop, field.HouseholdId, quantity, world.WorldTick, groundPosition: new(field.Position.X, field.Position.Y));
    if (plantingItem != crop)
        next = InventoryAuthority.AddLot(next, plantingLotId, plantingItem, field.HouseholdId, 2, world.WorldTick, groundPosition: new(field.Position.X, field.Position.Y));
    next = InventoryAuthority.Reserve(next, replantId, field.HouseholdId, plantingLotId, 1, "field_replanting", long.MaxValue);
    return WorldRuntime.FarmingSharedRules.ApplyToolWorkToInventory(next, field.Work!.WorkerId, world.WorldTick, toolPlans);
});
}
