using ClankerWorld.Simulation.Kernel;
using Retention = ClankerWorld.Simulation.World.MarketHistory.Retention;

namespace ClankerWorld.Simulation.World;

internal sealed partial class MarketSystem
{
    internal static void CompletePaidMarketConstruction(IMarketAdministrationWorld world, string townId, TownProject project, PlacedBuilding placed)
    {
        var town = world.Towns.Single(item => item.Id == townId);
        if (project.Plan.DefinitionId == MarketContent.Hall2x2().CanonicalId)
        {
            var definition = MarketContent.Stall1x1();
            var stalls = MarketContent.StarterSlotIndexes.Select(slot => new MarketStallState(
                MarketContent.StarterStallBuildingId(project.Id, slot), slot, project.Id, world.WorldTick)).ToArray();
            var buildings = stalls.Select(stall => new PlacedBuilding(stall.BuildingId, definition.CanonicalId,
                MarketContent.StallSite(project.Plan.Site, stall.SlotIndex), world.WorldTick, townId,
                Entrance: MarketContent.StallEntrance(project.Plan.Site, stall.SlotIndex))).ToArray();
            if (buildings.Any(building => world.Buildings.Any(existing => existing.InstanceId == building.InstanceId)))
                throw new InvalidDataException("A paid Market starter stall already exists.");
            world.PlaceStarterStalls(buildings);
            world.RecordConstructedMarkets(town, town.Markets.Append(new TownMarketState(MarketContent.MarketId(project.Id), project.Id,
                    placed.InstanceId, project.Plan.Site, stalls, [], [])).OrderBy(market => market.Id, StringComparer.Ordinal).ToArray());
            foreach (var building in buildings) world.AssignBuildingToTown(building, definition);
            world.AppendEvent(new MarketBuiltEventPayload(null, townId, MarketContent.MarketId(project.Id), placed.InstanceId, stalls.Length), project.Plan.Site);
        }
        else if (project.Plan.DefinitionId == MarketContent.Stall1x1().CanonicalId)
        {
            var target = MarketForStallPlan(town, project.Plan)
                ?? throw new InvalidDataException("The paid stall lost its approved Market slot.");
            var market = target.Market with
            {
                Stalls = target.Market.Stalls.Append(new MarketStallState(placed.InstanceId, target.Slot, project.Id, world.WorldTick))
                    .OrderBy(stall => stall.SlotIndex).ToArray(),
            };
            world.RecordConstructedMarkets(town, town.Markets.Select(item => item.Id == market.Id ? market : item).ToArray());
            world.AppendEvent(new MarketStallBuiltEventPayload(null, townId, market.Id, placed.InstanceId, target.Slot), placed.Position);
        }
    }

    internal static void RecordMarketBuildingRemoval(IMarketAdministrationWorld world, string buildingId)
    {
        var removed = false;
        foreach (var town in world.Towns.ToArray())
        {
            var changed = town.Markets.Any(market => market.HallBuildingId == buildingId || market.Stalls.Any(stall => stall.BuildingId == buildingId));
            if (!changed) continue;
            removed = true;
            world.RecordRemovedMarketBuildings(town, town.Markets.Select(market => market with
            {
                RemovedTick = market.HallBuildingId == buildingId ? world.WorldTick : market.RemovedTick,
                Stalls = market.Stalls.Select(stall => stall.BuildingId == buildingId ? stall with { RemovedTick = world.WorldTick } : stall).ToArray(),
            }).ToArray());
        }
        MaintainMarkets(world.Stalls);
        // A stall project of a removed Market has lost its site, and the world may be saved before the next tick.
        if (removed) world.MaintainTownProjects();
    }

    internal static void RetireMarketHistory(IMarketAdministrationWorld world)
    {
        foreach (var town in world.Towns.ToArray())
            foreach (var market in town.Markets)
            {
                var plan = MarketHistory.Retain(market, world.Society.Inventory);
                if (ReferenceEquals(plan.Market, market)) continue;
                SetMarket(world.Stalls, town.Id, plan.Market);
                if (plan.RetiredOfferIds.Count == 0) continue;
                var claims = plan.RetiredOfferIds.SelectMany(id => new[] { DerivedId.Create(id + ":first"), DerivedId.Create(id + ":second") })
                    .ToHashSet(StringComparer.Ordinal);
                world.RetireMarketOffers(plan.RetiredOfferIds, claims);
            }
    }

    internal static Retention Retain(TownMarketState market, InventoryCheckpoint inventory)
    {
        var keepReceipts = market.StockReceipts.TakeLast(MarketHistory.RecentRecordLimit).Select(receipt => receipt.Sequence).ToHashSet();
        foreach (var occupancy in market.Occupancies.Where(item => item.EndedTick is null))
        {
            var stall = market.Stalls.Single(item => item.BuildingId == occupancy.StallBuildingId);
            var position = MarketContent.StallSite(market.Site, stall.SlotIndex);
            foreach (var lot in inventory.Lots.Where(lot => MarketTradeRules.IsAt(lot, position)))
                // Repeated collection/deposit of the same goods must not keep every old receipt.
                foreach (var receipt in market.StockReceipts.Where(receipt => receipt.OccupancyId == occupancy.Id &&
                             receipt.ItemKind == lot.ItemKind && MarketTradeRules.IsReceiptLot(receipt, lot))
                             .GroupBy(receipt => receipt.OwnerId).Select(group => group.Last()))
                    keepReceipts.Add(receipt.Sequence);
        }
        var open = market.Trades.Where(trade => trade.SettledTick is null && trade.CancellationReason is null).ToArray();
        foreach (var trade in open) keepReceipts.Add(trade.StockReceiptSequence);

        var activeClaims = inventory.Reservations.Where(claim => claim.State is InventoryReservationState.Reserved or
                InventoryReservationState.PartiallyConsumed or InventoryReservationState.Committed)
            .Select(claim => claim.Id.Value).ToHashSet(StringComparer.Ordinal);
        var keepTrades = market.Trades.Where(trade => trade.SettledTick is not null || trade.CancellationReason is not null)
            .OrderByDescending(trade => trade.SettledTick ?? trade.ProposedTick)
            .ThenBy(trade => trade.OfferId.Value, StringComparer.Ordinal).Take(MarketHistory.RecentRecordLimit)
            .Concat(open).Select(trade => trade.OfferId.Value).ToHashSet(StringComparer.Ordinal);
        foreach (var receipt in market.StockReceipts.Where(receipt => keepReceipts.Contains(receipt.Sequence)))
            if (receipt.TradeOfferId is { } offer) keepTrades.Add(offer);
        foreach (var trade in market.Trades)
            if (activeClaims.Contains(trade.OfferId + ":first") || activeClaims.Contains(trade.OfferId + ":second"))
                keepTrades.Add(trade.OfferId);
        // A retained settlement keeps its exact payment and acceptances. Its old source receipt
        // may retire below the explicit boundary; only open offers still exercise that authority.
        foreach (var receipt in market.StockReceipts)
            if (receipt.TradeOfferId is { } offer && keepTrades.Contains(offer)) keepReceipts.Add(receipt.Sequence);

        var keepOccupancies = market.Occupancies.Where(item => item.EndedTick is null)
            .Concat(market.Occupancies.Where(item => item.EndedTick is not null)
                .OrderByDescending(item => item.EndedTick).ThenBy(item => item.Id, StringComparer.Ordinal).Take(MarketHistory.RecentRecordLimit))
            .Select(item => item.Id).ToHashSet(StringComparer.Ordinal);
        foreach (var receipt in market.StockReceipts.Where(receipt => keepReceipts.Contains(receipt.Sequence)))
            keepOccupancies.Add(receipt.OccupancyId);
        foreach (var trade in market.Trades.Where(trade => keepTrades.Contains(trade.OfferId)))
            keepOccupancies.Add(trade.OccupancyId);

        var retiredOffers = market.Trades.Where(trade => !keepTrades.Contains(trade.OfferId))
            .Select(trade => trade.OfferId.Value).ToHashSet(StringComparer.Ordinal);
        var removedReceipts = market.StockReceipts.Where(receipt => !keepReceipts.Contains(receipt.Sequence)).ToArray();
        if (retiredOffers.Count == 0 && removedReceipts.Length == 0 && keepOccupancies.Count == market.Occupancies.Count)
            return new(market, retiredOffers);
        return new(market with
        {
            Occupancies = market.Occupancies.Where(item => keepOccupancies.Contains(item.Id)).ToArray(),
            StockReceipts = market.StockReceipts.Where(receipt => keepReceipts.Contains(receipt.Sequence)).ToArray(),
            Trades = market.Trades.Where(trade => keepTrades.Contains(trade.OfferId)).ToArray(),
            RetiredStockReceiptThrough = removedReceipts.Length == 0 ? market.RetiredStockReceiptThrough :
                Math.Max(market.RetiredStockReceiptThrough, removedReceipts.Max(receipt => receipt.Sequence)),
        }, retiredOffers);
    }

    internal static (TownMarketState Market, int Slot)? MarketForStallPlan(TownRuntimeState town, TownProjectPayload plan)
    {
        foreach (var market in town.Markets.Where(item => item.RemovedTick is null))
            for (var slot = 0; slot < MarketContent.MaximumStalls; slot++)
                if (MarketContent.StallSite(market.Site, slot) == plan.Site &&
                    MarketContent.StallEntrance(market.Site, slot) == plan.Entrance &&
                    !market.Stalls.Any(stall => stall.SlotIndex == slot))
                    return (market, slot);
        return null;
    }
}
