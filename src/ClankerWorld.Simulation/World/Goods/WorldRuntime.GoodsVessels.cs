using ClankerWorld.Simulation.Kernel;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private GoodsRequest? CartCargoRequest(string actor, string cartId)
    {
        var kinds = InventoryIndex.For(society.Checkpoint.Inventory).ContentsOf(cartId)
            .Select(lot => lot.ItemKind).Distinct(StringComparer.Ordinal).ToArray();
        return kinds.Length == 0 ? null : new(GoodsUse.Cargo, actor, GoodsOwners.One(actor), new(kinds), ContainerId: cartId);
    }

    private IEnumerable<InventoryLot> CartCargoLots(string actor, string cartId) =>
        CartCargoRequest(actor, cartId) is { } request ? FindGoods(request).Matches.Select(match => match.Lot) : [];

    private IReadOnlyList<GoodsMatch> DismountCargo(string actor)
    {
        var roots = InventoryIndex.For(society.Checkpoint.Inventory).Lots.Where(lot => lot.ContainerLotId is null &&
            PersonalEquipmentRules.IsCarried(lot, actor)).ToArray();
        return roots.Length == 0 ? [] : FindGoods(new(GoodsUse.DismountCargo, actor,
            new GoodsOwners(roots.Select(lot => lot.OwnerId).Distinct(StringComparer.Ordinal).ToArray()),
            new GoodsKinds(roots.Select(lot => lot.ItemKind).Distinct(StringComparer.Ordinal).ToArray()))).Matches;
    }

    private GoodsReason? DismountCargoExclusion(string actor, InventoryIndex index, InventoryLot lot)
    {
        if (!agents.TryGetValue(actor, out var person) || lot.ContainerLotId is not null || !PersonalEquipmentRules.IsCarried(lot, actor)) return GoodsReason.Custody;
        if (lot.DeliveryBuildingId is not null) return GoodsReason.Delivery;
        if (PersonalEquipmentRules.IsSelected(person.Equipment, lot.Id)) return GoodsReason.Custody;
        return InventoryContainerRules.IsContainer(lot.ItemKind)
            ? InventoryRules.CanMoveFamily(index, lot, InventoryHoldRules.SupplyTripFor(actor)) ? null : GoodsReason.Reservation
            : InventoryHoldRules.Allows(index, lot, InventoryHoldRules.SupplyTripFor(actor)) ? null : GoodsReason.Custody;
    }

    private static GoodsReason? EquipmentRepairGoodsExclusion(string actor, InventoryIndex index, InventoryLot lot)
    {
        if (lot.OwnerId != actor || !PersonalEquipmentRules.IsCarried(lot, actor)) return GoodsReason.Custody;
        if (lot.DeliveryBuildingId is not null) return GoodsReason.Delivery;
        if (lot.Quantity != 1 || lot.ConditionBasisPoints > 4_000 ||
            !(PersonalEquipmentRules.IsGarment(lot.ItemKind) || PersonalEquipmentRules.IsCarryAid(lot.ItemKind))) return GoodsReason.Kind;
        if (index.ActiveReservationsFor(lot.Id).Any()) return GoodsReason.Reservation;
        return InventoryHoldRules.Allows(index, lot, InventoryHoldRules.BorrowedBy(actor)) ? null : GoodsReason.Custody;
    }

    private GoodsReason? EquippingGoodsExclusion(GoodsRequest request, InventoryIndex index, InventoryLot lot,
        Dictionary<(GridPoint Position, int Range), bool>? routes)
    {
        var actor = request.Actor!;
        if (lot.ContainerLotId is not null) return GoodsReason.Vessel;
        if (!InventoryHoldRules.Allows(index, lot, InventoryHoldRules.BorrowedBy(actor))) return GoodsReason.Custody;
        if (lot.OwnerId == actor) return PersonalEquipmentRules.IsCarried(lot, actor) ? null : GoodsReason.Custody;
        if (lot.CarrierId is not null) return GoodsReason.Custody;
        if (!MayCollectGoodsAt(actor, lot)) return GoodsReason.Place;
        return GoodsRouteExclusion(actor, lot, routes);
    }

    private static GoodsReason? HandcartControlExclusion(string actor, InventoryLot lot) =>
        lot.OwnerId != actor || lot.ItemKind != InventoryContainerRules.Handcart ? GoodsReason.Owner :
        lot.ContainerLotId is not null || lot.GroundPosition is null || lot.CarrierId is not null ? GoodsReason.Place : null;

    private IEnumerable<InventoryLot> HandcartControlStock(string actor) => FindGoods(new(GoodsUse.HandcartControl,
        actor, GoodsOwners.One(actor), GoodsKinds.One(InventoryContainerRules.Handcart))).Matches.Select(match => match.Lot);

    private IEnumerable<InventoryLot> HandcartLoadStock(string actor)
    {
        var owners = HouseholdFor(actor) is { } home ? new[] { actor, home } : [actor];
        var kinds = owners.SelectMany(owner => InventoryIndex.For(society.Checkpoint.Inventory).OwnedBy(owner))
            .Select(lot => lot.ItemKind).Distinct(StringComparer.Ordinal).ToArray();
        return kinds.Length == 0 ? [] : FindGoods(new(GoodsUse.ConsumeAt, actor, new(owners), new(kinds))).Matches
            .Where(match => TransportSystem.CanLoadCartLot(HandcartWorld, actor, agents[actor], match.Lot)).Select(match => match.Lot);
    }

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
