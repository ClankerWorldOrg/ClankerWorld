using ClankerWorld.Simulation.Kernel;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private sealed record PersonalStorageEffect(string MoveId, string ItemKind, int Quantity);

    private IEnumerable<InventoryLot> PersonalStorageLots(string actor, string houseId) =>
        SpendingGoods(actor, GoodsUse.StorePersonal, [actor]).Select(match => match.Lot)
            .Where(lot => !IsEdibleFood(lot.ItemKind) &&
            VesselFits(lot, DestinationRoom(houseId))).OrderBy(lot => lot.Id, StringComparer.Ordinal);

    private PersonalStorageEffect? StorePersonalGoods(string actor, string lotId, int maximumQuantity = int.MaxValue,
        string? requiredHouseId = null, string? requiredHouseholdId = null, GridPoint? requiredPosition = null)
    {
        if (!AdultResident(actor) || !ReadyForBriefInteraction(actor) || maximumQuantity <= 0 ||
            HouseForHousehold(HouseholdFor(actor)) is not { } house || DestinationRoom(house.InstanceId) <= 0)
            return null;
        if (requiredHouseId is not null && (house.InstanceId != requiredHouseId ||
            house.HouseholdId != requiredHouseholdId || house.Position != requiredPosition)) return null;
        var lot = PersonalStorageLots(actor, house.InstanceId).FirstOrDefault(item => item.Id == lotId);
        if (lot is null) return null;
        if (requiredHouseId is not null) InterruptOrdinaryFieldWorkForCustody(actor, lotId);
        var person = inhabitants[actor];
        if (!IsWithinInteractionRange(person.Position, house.Position, 1))
        {
            MoveToward(actor, person, house.Position, "personal_goods", 1);
            return null;
        }
        var room = DestinationRoom(house.InstanceId);
        var quantity = InventoryContainerRules.IsContainer(lot.ItemKind)
            ? VesselFits(lot, room) ? 1 : 0
            : Math.Min(PhysicalUnreservedQuantity(lot), room);
        quantity = Math.Min(quantity, maximumQuantity);
        if (quantity <= 0) return null;
        var moveId = PersonalMoveId(actor, WorldTick, lot.Id);
        ApplyInventoryTransition(inventory => InventoryAuthority.Relocate(inventory,
            moveId, lot.Id, actor, quantity, storageBuildingId: house.InstanceId));
        AppendEvent("personal_goods_stored", $"{actor}|{lot.ItemKind}|{quantity}|{actor}");
        return new(moveId, lot.ItemKind, quantity);
    }
}
