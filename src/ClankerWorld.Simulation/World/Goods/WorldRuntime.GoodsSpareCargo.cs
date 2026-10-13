using ClankerWorld.Simulation.Kernel;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private GoodsReason? SpareCargoGoodsExclusion(GoodsRequest request, InventoryIndex index, InventoryLot lot)
    {
        var actor = request.Actor!;
        if (!agents.TryGetValue(actor, out var state) || !PersonalEquipmentRules.IsCarried(lot, actor) || lot.ContainerLotId is not null)
            return GoodsReason.Custody;
        if (lot.DeliveryBuildingId is not null) return GoodsReason.Delivery;
        if (PersonalEquipmentRules.IsSelected(state.Equipment, lot.Id) || AgentKnowledgeRules.IsArtifactKind(lot.ItemKind))
            return GoodsReason.Kind;
        if (request.Use == GoodsUse.OrchardFoodRecovery && OrchardReservationForFood(society.Checkpoint.Inventory, actor, lot) is null)
            return GoodsReason.Reservation;
        if (InventoryContainerRules.IsContainer(lot.ItemKind) && HasActiveContainerReservation(society.Checkpoint.Inventory, lot.Id))
            return GoodsReason.Reservation;
        var access = InventoryHoldRules.BorrowedBy(actor);
        return index.ContentsOf(lot.Id).Prepend(lot).All(member => InventoryHoldRules.Allows(index, member, access))
            ? null : GoodsReason.Custody;
    }

    private GoodsRequest? SpareCargoGoodsRequest(string actor)
    {
        var owners = new[] { actor, HouseholdFor(actor) }.OfType<string>().Distinct(StringComparer.Ordinal).ToArray();
        var kinds = owners.SelectMany(InventoryIndex.For(society.Checkpoint.Inventory).OwnedBy)
            .Select(lot => lot.ItemKind).Distinct(StringComparer.Ordinal).ToArray();
        return kinds.Length == 0 ? null : new(GoodsUse.SpareCargo, actor, new(owners), new(kinds));
    }

    private IEnumerable<InventoryLot> SpareCargoGoods(string actor) => SpareCargoGoodsRequest(actor) is { } request
        ? FindGoods(request).Matches.Select(match => match.Lot) : [];

    private static GoodsRequest OrchardFoodRecoveryRequest(string actor) => new(GoodsUse.OrchardFoodRecovery,
        actor, GoodsOwners.One(actor), GoodsKinds.One(TreeGrowthRules.OrchardSeedItem));

    private IEnumerable<InventoryLot> OrchardFoodRecoveryGoods(string actor) =>
        FindGoods(OrchardFoodRecoveryRequest(actor)).Matches.Select(match => match.Lot);
}
