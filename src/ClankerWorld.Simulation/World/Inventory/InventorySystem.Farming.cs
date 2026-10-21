using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;
using System.Globalization;
using ClankerWorld.Simulation.Content;
namespace ClankerWorld.Simulation.World;

internal static partial class InventorySystem
{
    internal static Command PlantTreeCore(InventoryReservation reservation) => new PreparedCommand(inventory => InventoryAuthority.ReleaseReservation(inventory, reservation.Id));

    internal static Command ApplyFieldCandidate(FarmFieldState field, InventoryLot seed, IFarmingWorld world, string actor) => new PreparedCommand(inventory =>
                    {
                        if (field.ReplantingReservationId is { } id && inventory.Reservations.Any(item =>
                            item.Id == id && item.LotId == seed.Id && item.State == InventoryReservationState.Reserved))
                            inventory = InventoryAuthority.ReleaseReservation(inventory, id, "planting_stock_collected");
                        return InventoryAuthority.Transfer(inventory, IdentityRules.PrefixedReference("field-seed-pickup:", $"{world.WorldTick}:{actor}"),
                            field.HouseholdId, actor, seed.Id, 1, "field_seed_picked_up", holdAccess: InventoryHoldRules.BorrowedBy(actor));
                    });

    internal static Command StartFieldWorkCore(string? reservationId, string workerId, InventoryLot seed) => new PreparedCommand(inventory => InventoryAuthority.Reserve(inventory, reservationId!,
                    workerId, seed.Id, 1, "field_planting", long.MaxValue));

    internal static Command IsolateFieldToolForWork(InventoryLot lot, string isolatedId, string workerId) => new PreparedCommand(current => InventoryAuthority.SplitLot(current, lot.Id, 1, isolatedId, holdAccess: InventoryHoldRules.BorrowedBy(workerId)));

    internal static Command ReturnFieldToGrass(Identity reserve) => new PreparedCommand(inventory => InventoryAuthority.ReleaseReservation(inventory, reserve, "field_returned_to_grass"));

    internal static Command CancelFarmWork(Identity reservationId) => new PreparedCommand(inventory => InventoryAuthority.ReleaseReservation(inventory, reservationId, "field_work_interrupted"));

    internal static Command ContinueFarmWork(FarmFieldState field, IFarmingWorld world, FarmFieldWork work) => new PreparedCommand(inventory =>
                    {
                        if (field.ReplantingReservationId is { } previous && FarmingSystem.ActiveFarmReservation(world, previous))
                            inventory = InventoryAuthority.ReleaseReservation(inventory, previous, "field_replanting_completed");
                        return InventoryAuthority.ConsumeReservation(inventory, work.SeedReservationId!);
                    });

    internal static Command CompleteFieldHarvest(FarmFieldState field, int cycle, string crop, int quantity, IFarmingWorld world, string plantingItem, string plantingLotId, string replantId, IReadOnlyList<ToolWorkPlan> toolPlans) => new PreparedCommand(inventory =>
            {
                var next = InventoryAuthority.AddLot(inventory, WorldRuntime.FarmingSharedRules.FieldHarvestId(field.Position, cycle, "crop"), crop, field.HouseholdId,
                    quantity, world.WorldTick,
                    groundPosition: new(field.Position.X, field.Position.Y));
                if (plantingItem != crop)
                    next = InventoryAuthority.AddLot(next, plantingLotId, plantingItem, field.HouseholdId, 2, world.WorldTick,
                        groundPosition: new(field.Position.X, field.Position.Y));
                next = InventoryAuthority.Reserve(next, replantId, field.HouseholdId, plantingLotId, 1,
                    "field_replanting", long.MaxValue);
                return WorldRuntime.FarmingSharedRules.ApplyToolWorkToInventory(next, field.Work!.WorkerId, world.WorldTick, toolPlans);
            });

    internal static Command ApplyFieldPlan(PlannedFieldSeed seed, IFarmingWorld world, FieldActionPlan plan) => new PreparedCommand(inventory =>
                {
                    if (seed.ReplantingReservationId is { } id)
                        inventory = InventoryAuthority.ReleaseReservation(inventory, id, "planting_stock_collected");
                    return InventoryAuthority.Transfer(inventory, DerivedId.Create($"field-seed-pickup:{world.WorldTick}:{plan.Actor}"), plan.HouseholdId,
                        plan.Actor, seed.StockId, 1, "field_seed_picked_up", holdAccess: InventoryHoldRules.BorrowedBy(plan.Actor));
                });
}
