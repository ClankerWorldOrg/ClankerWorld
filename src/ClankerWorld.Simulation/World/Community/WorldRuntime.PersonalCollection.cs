using ClankerWorld.Simulation.Kernel;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private sealed record PersonalCollectionEffect(string MoveId, string ItemKind, int Quantity);

    private PersonalCollectionEffect? CollectPersonalGoods(string actor, string lotId, int maximumQuantity = int.MaxValue)
    {
        if (!IsAdult(actor) || !ReadyForBriefInteraction(actor) || maximumQuantity <= 0 || FreeCarryCapacity(actor) <= 0)
            return null;
        var stored = InventoryIndex.For(society.Checkpoint.Inventory).Find(lotId);
        if (stored is null || PersonalRecoveryRequest(actor, GoodsUse.Recover, stored.ItemKind) is not { } request ||
            RecheckGoods(request, lotId) is not { } match) return null;
        var lot = match.Lot;
        var destination = HouseholdStockPosition(lot);
        var range = lot.GroundPosition is not null ? ResourceInteractionRange : 1;
        // Owners collect at the entrance, including former members and heirs; this grants no other private access.
        if (!IsWithinInteractionRange(agents[actor].Position, destination, range))
        {
            MoveToward(actor, agents[actor], destination, "personal_goods", range);
            return null;
        }
        var quantity = match.Quantity;
        quantity = Math.Min(quantity, maximumQuantity);
        if (quantity <= 0) return null;
        var moveId = PersonalMoveId(actor, WorldTick, lot.Id);
        ApplyInventoryTransition(inventory => InventoryAuthority.Relocate(inventory,
            moveId, lot.Id, lot.OwnerId, quantity, actor, holdAccess: InventoryHoldRules.BorrowedBy(actor)));
        AppendEvent("personal_goods_collected", $"{actor}|{lot.ItemKind}|{quantity}|{lot.OwnerId}");
        return new(moveId, lot.ItemKind, quantity);
    }
}
