using ClankerWorld.Simulation.Kernel;

namespace ClankerWorld.Simulation.World;

// Inventory-only selection is also used by captured-checkpoint guidance. Context
// callbacks add native permissions and routes; none are retained or cached.
internal static class GoodsInventoryQuery
{
    /// <summary>Retains meal-source evidence for usable food, including stock held by a live promise.</summary>
    internal static IEnumerable<WorldProductionJob> FindFoodProductionSources(InventoryCheckpoint inventory,
        IReadOnlyList<WorldProductionJob> jobs)
    {
        var index = InventoryIndex.For(inventory);
        foreach (var lot in index.Lots.Where(lot => lot.FreshnessBasisPoints > 0 && lot.ConditionBasisPoints > 0 &&
                     FoodRules.IsEdible(lot.ItemKind) && !FoodRules.PreparedKinds.Contains(lot.ItemKind, StringComparer.Ordinal)))
        {
            var origin = index.ProvenanceOrigin(lot);
            if (WorldProductionJob.OutputSource(jobs, origin.Id) is { } source) yield return source;
        }
    }

    internal static GoodsAnswer FindGoods(InventoryCheckpoint inventory, GoodsRequest request,
        Func<InventoryIndex, InventoryLot, GoodsReason?> context,
        Func<InventoryIndex, InventoryLot, GoodsMatch> match)
    {
        Validate(request);
        var index = InventoryIndex.For(inventory);
        var matches = new List<GoodsMatch>();
        var excluded = new List<(string LotId, GoodsReason Reason)>();
        long coveredReservations = 0;
        IEnumerable<InventoryLot> candidates = request.Explain ? index.Lots : request.ContainerId is { } container
            ? index.ContentsOf(container) : request.AtBuilding is { } building
            ? index.StoredAt(building) : request.Owners.Ids.Distinct(StringComparer.Ordinal)
                .SelectMany(index.OwnedBy).OrderBy(lot => lot.Id, StringComparer.Ordinal);
        foreach (var lot in candidates)
        {
            var reason = Exclusion(request, index, lot);
            var contextReason = reason is null || request.DemandCoverage != GoodsDemandCoverage.None && reason == GoodsReason.Reservation
                ? context(index, lot) : null;
            if (request.DemandCoverage != GoodsDemandCoverage.None && (reason is null or GoodsReason.Reservation) &&
                contextReason is null)
                coveredReservations += CoveredReservationQuantity(request, index, lot);
            reason ??= contextReason;
            if (reason is { } failure)
            {
                if (request.Explain) excluded.Add((lot.Id, failure));
                continue;
            }
            matches.Add(match(index, lot));
        }
        return new(matches.AsReadOnly(), excluded.AsReadOnly(), coveredReservations);
    }

    internal static GoodsAnswer FindConsumableGoods(InventoryCheckpoint inventory, GoodsRequest request,
        Func<InventoryIndex, InventoryLot, GoodsReason?> context)
    {
        Validate(request);
        if (request.Use != GoodsUse.ConsumeAt)
            throw new ArgumentException("Inventory-only selection requires ConsumeAt; other uses need native world rules.", nameof(request));
        return FindGoods(inventory, request, context, (index, lot) => MakeMatch(index, lot, request));
    }

    internal static void Validate(GoodsRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Owners);
        ArgumentNullException.ThrowIfNull(request.Kinds);
        if (request.Use is not (GoodsUse.Holdings or GoodsUse.ConsumeAt or GoodsUse.Collect or GoodsUse.ReachableHoldings or
                GoodsUse.RecoveryHoldings or GoodsUse.Recover or GoodsUse.Spend or GoodsUse.Sell or GoodsUse.StorePersonal or GoodsUse.Deliver or
                GoodsUse.Eat or GoodsUse.VesselFor or GoodsUse.ReturnVessel or GoodsUse.Cargo or GoodsUse.Salvage or GoodsUse.CarriedDelivery or GoodsUse.SpareCargo or GoodsUse.OrchardFoodRecovery or GoodsUse.CarriedInputs or GoodsUse.FieldPlanting or GoodsUse.TreePlanting or GoodsUse.Equip or GoodsUse.EquipmentRepair or GoodsUse.AnimalProduct or GoodsUse.AnimalHide) ||
            request.Owners.Ids is not { Count: > 0 } || request.Kinds.Ids is not { Count: > 0 } ||
            request.Owners.Ids.Any(string.IsNullOrWhiteSpace) || request.Kinds.Ids.Any(string.IsNullOrWhiteSpace) ||
            request.ExtraUnits < 0 ||
            request.Use is not (GoodsUse.FieldPlanting or GoodsUse.AnimalProduct) && request.ReservationId is not null ||
            request.Use == GoodsUse.AnimalProduct && string.IsNullOrWhiteSpace(request.ReservationId) ||
            request.Use != GoodsUse.VesselFor && (request.ExtraUnits != 0 || request.ContentsKind is not null) ||
            request.ContentsKind is not null && string.IsNullOrWhiteSpace(request.ContentsKind) ||
            (request.Use == GoodsUse.Cargo ? string.IsNullOrWhiteSpace(request.ContainerId) : request.ContainerId is not null) ||
            (request.Use == GoodsUse.Deliver
                ? request.DeliveryPurpose == GoodsDeliveryPurpose.TownProject
                    ? request.Near is null || request.Destination is not null
                    : string.IsNullOrWhiteSpace(request.Destination) || request.Near is not null
                : request.Near is not null || request.Destination is not null || request.DeliveryPurpose != GoodsDeliveryPurpose.Stock) ||
            !Enum.IsDefined(request.DeliveryPurpose) ||
            request.DemandCoverage is not (GoodsDemandCoverage.None or GoodsDemandCoverage.KnowledgeWriting) ||
            request.DemandCoverage != GoodsDemandCoverage.None && request.Use is not (GoodsUse.Holdings or GoodsUse.ReachableHoldings) ||
            request.Use is (GoodsUse.Collect or GoodsUse.ReachableHoldings or GoodsUse.RecoveryHoldings or GoodsUse.Recover or GoodsUse.Spend or GoodsUse.Sell or GoodsUse.StorePersonal or GoodsUse.Deliver or GoodsUse.Eat or GoodsUse.Cargo or GoodsUse.Salvage or GoodsUse.CarriedDelivery or GoodsUse.SpareCargo or GoodsUse.OrchardFoodRecovery or GoodsUse.CarriedInputs or GoodsUse.FieldPlanting or GoodsUse.TreePlanting or GoodsUse.Equip or GoodsUse.EquipmentRepair or GoodsUse.AnimalProduct or GoodsUse.AnimalHide) &&
                string.IsNullOrWhiteSpace(request.Actor))
            throw new ArgumentException("Use exact owners and kinds, an actor for personal uses and Deliver, and a container for Cargo. Contents and extra units apply only to VesselFor; Deliver requires its native destination and purpose.", nameof(request));
    }

    internal static bool IsRecovery(GoodsUse use) => use is GoodsUse.RecoveryHoldings or GoodsUse.Recover;

    private static int CoveredReservationQuantity(GoodsRequest request, InventoryIndex index, InventoryLot lot)
    {
        if (request.DemandCoverage != GoodsDemandCoverage.KnowledgeWriting || lot.ItemKind != KnowledgeContent.Paper)
            return 0;
        var reserved = index.ActiveReservationsFor(lot.Id).Where(reservation =>
                request.Owners.Ids.Contains(reservation.OwnerId, StringComparer.Ordinal) &&
                reservation.Purpose.StartsWith("knowledge_writing:", StringComparison.Ordinal) &&
                reservation.Purpose.EndsWith(":" + lot.ItemKind, StringComparison.Ordinal))
            .Sum(reservation => reservation.Quantity);
        return Math.Min(lot.Quantity - Math.Max(0, InventoryRules.UnreservedQuantity(index, lot)), reserved);
    }

    internal static int Quantity(InventoryIndex index, InventoryLot lot, GoodsUse use) => use is GoodsUse.OrchardFoodRecovery or GoodsUse.FieldPlanting or GoodsUse.TreePlanting or GoodsUse.EquipmentRepair or GoodsUse.AnimalProduct ? lot.Quantity : IsRecovery(use) || use is GoodsUse.StorePersonal or GoodsUse.Cargo or GoodsUse.ReturnVessel or GoodsUse.Salvage or GoodsUse.CarriedDelivery or GoodsUse.SpareCargo
        ? Math.Max(0, InventoryRules.UnreservedQuantity(index, lot))
        : use is GoodsUse.ConsumeAt or GoodsUse.Eat ? InventoryRules.ReservableQuantity(index, lot, InventoryHoldRules.For(index, lot).ToArray()) : InventoryRules.UsableQuantity(index, lot);

    internal static GoodsReason? PromiseExclusion(GoodsRequest request, InventoryIndex index, InventoryLot lot)
    {
        var holds = InventoryHoldRules.For(index, lot).ToArray();
        if (request.Use is not (GoodsUse.ConsumeAt or GoodsUse.Cargo or GoodsUse.CarriedDelivery) &&
            holds.Any(hold => hold.Purpose == InventoryHoldPurpose.Delivery)) return GoodsReason.Delivery;
        if (request.Use is GoodsUse.Holdings or GoodsUse.Collect or GoodsUse.ReachableHoldings or GoodsUse.Deliver or GoodsUse.Spend or GoodsUse.FieldPlanting or GoodsUse.TreePlanting or GoodsUse.Equip &&
            holds.Any(hold => hold.Purpose == InventoryHoldPurpose.MarketStall)) return GoodsReason.MarketStall;
        return null;
    }

    internal static GoodsReason? Exclusion(GoodsRequest request, InventoryIndex index, InventoryLot lot)
    {
        if (!request.Owners.Ids.Contains(lot.OwnerId, StringComparer.Ordinal) ||
            IsRecovery(request.Use) && lot.OwnerId != request.Actor) return GoodsReason.Owner;
        if (!request.Kinds.Ids.Contains(lot.ItemKind, StringComparer.Ordinal)) return GoodsReason.Kind;
        if (request.AtBuilding is { } building && lot.StorageBuildingId != building) return GoodsReason.Place;
        if (request.Use == GoodsUse.Cargo && lot.ContainerLotId != request.ContainerId) return GoodsReason.Place;
        if (!IsRecovery(request.Use) && request.Use is not (GoodsUse.StorePersonal or GoodsUse.Cargo or GoodsUse.ReturnVessel or GoodsUse.Salvage or GoodsUse.CarriedDelivery or GoodsUse.SpareCargo or GoodsUse.OrchardFoodRecovery or GoodsUse.EquipmentRepair))
        {
            if (lot.ConditionBasisPoints <= 0) return GoodsReason.Damaged;
            if (lot.FreshnessBasisPoints <= 0) return GoodsReason.Spoiled;
            if (!InventoryRules.HasUsableContainer(index, lot) ||
                request.Use is GoodsUse.ConsumeAt or GoodsUse.Eat && InventoryContainerRules.IsContainer(lot.ItemKind)) return GoodsReason.Vessel;
        }
        if (lot.Quantity == 0) return GoodsReason.Empty;
        if (Quantity(index, lot, request.Use) <= 0) return GoodsReason.Reservation;
        if (request.Use is not (GoodsUse.ConsumeAt or GoodsUse.Cargo) &&
            InventoryHoldRules.For(index, lot).Any(hold => hold.Purpose == InventoryHoldPurpose.SupplyTrip))
            return GoodsReason.Reservation;
        if (request.Use is GoodsUse.VesselFor or GoodsUse.ReturnVessel)
        {
            if (!InventoryContainerRules.IsContainer(lot.ItemKind) || lot.ContainerLotId is not null) return GoodsReason.Vessel;
            if (!InventoryRules.CanMoveFamily(index, lot, InventoryHoldRules.Family(index, lot))) return GoodsReason.Reservation;
            if (request.ContentsKind is { } kind && (!InventoryContainerRules.Allows(lot.ItemKind, kind) ||
                index.ContentsOf(lot.Id).Any(contents => contents.ItemKind != kind))) return GoodsReason.Kind;
            if (request.ExtraUnits > InventoryContainerRules.Capacity(lot.ItemKind) - index.ContentsQuantity(lot.Id))
                return GoodsReason.CarryRoom;
        }
        if (request.Use == GoodsUse.Cargo && !InventoryRules.CanMoveFamily(index, index.Root(lot), InventoryHoldRules.Family(index, index.Root(lot))))
            return GoodsReason.Reservation;
        return null;
    }

    private static GoodsMatch MakeMatch(InventoryIndex index, InventoryLot lot, GoodsRequest request)
    {
        var root = index.Root(lot);
        var place = root.StorageBuildingId is { } building ? new GoodsPlace(GoodsPlaceKind.Stored, building,
            DeliveryBuildingId: root.DeliveryBuildingId) :
            root.GroundPosition is { } ground ? new GoodsPlace(GoodsPlaceKind.Ground,
                Position: new(ground.X, ground.Y), DeliveryBuildingId: root.DeliveryBuildingId) :
            new GoodsPlace(root.CarrierId is null ? GoodsPlaceKind.Unlocated : GoodsPlaceKind.Carried,
                CarrierId: root.CarrierId, DeliveryBuildingId: root.DeliveryBuildingId);
        var quantity = Quantity(index, lot, request.Use);
        return new(lot, root, place, quantity,
            InventoryContainerRules.IsContainer(lot.ItemKind) ? index.FamilyQuantity(lot.Id) : quantity);
    }
}
