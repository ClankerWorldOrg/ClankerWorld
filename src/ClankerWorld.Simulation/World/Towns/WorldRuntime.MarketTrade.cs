using MarketGoodsBasis = ClankerWorld.Simulation.World.MarketSystem.MarketGoodsBasis;
using MarketChoice = ClankerWorld.Simulation.World.MarketSystem.MarketChoice;
using System.Globalization;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{

    private const int MarketCandidateLimit = 16;

    private static bool IsMarketCandidate(string id) => id.StartsWith("market_", StringComparison.Ordinal);

    // A settled or cancelled trade is history; only the others still need their offer looked up.
    private static bool IsOpenMarketTrade(MarketTradeState trade) =>
        MarketSystem.IsOpenMarketTrade(trade);

    /// <summary>The Hall, stall slots and aisles of every standing Market, which other building and planting leave clear.</summary>
    private IEnumerable<GridPoint> MarketSiteTiles() => Ground.MarketSites;

    /// <summary>Household goods stay on sale while one of the household's members borrows the stall they lie on.</summary>
    private bool OnBorrowedMarketStall(InventoryLot lot) =>
        InventoryHoldRules.For(InventoryIndex.For(society.Checkpoint.Inventory), lot)
            .Any(hold => hold.Purpose == InventoryHoldPurpose.MarketStall);

    private bool MarketAdult(string actor) =>
        MarketSystem.MarketAdult(MarketStallWorld, actor);

    private bool ReadyForMarket(string actor) =>
        MarketSystem.ReadyForMarket(MarketStallWorld, actor);

    private void SetMarket(string townId, TownMarketState market) =>
        MarketSystem.SetMarket(MarketStallWorld, townId, market);

    private bool LiveMarketStall(TownRuntimeState town, TownMarketState market, MarketStallState stall) =>
        MarketSystem.LiveMarketStall(MarketStallWorld, town, market, stall);

    private static MarketStallOccupancy? MarketOccupant(TownMarketState market, MarketStallState stall) =>
        MarketSystem.MarketOccupant(market, stall);

    private bool HasMarketOccupancy(string actor) =>
        MarketSystem.HasMarketOccupancy(MarketStallWorld, actor);

    private static string MarketChoiceId(string kind, params string[] parts) =>
        MarketSystem.MarketChoiceId(kind, parts);

    private string MarketPlaceMode(string actor, GridPoint destination, int range) =>
        MarketSystem.MarketPlaceMode(MarketStallWorld, actor, destination, range);

    private string MarketTradeMode(string actor, MarketTradeState trade) =>
        MarketSystem.MarketTradeMode(MarketStallWorld, actor, trade);

    private int MarketStockQuantity(TownMarketState market, MarketStallState stall, InventoryCheckpoint? inventory = null) =>
        MarketSystem.MarketStockQuantity(MarketStallWorld, market, stall, inventory);

    private int MarketIncomingPayment(TownMarketState market, MarketStallState stall,
        InventoryCheckpoint? inventory = null) =>
        MarketSystem.MarketIncomingPayment(MarketStallWorld, market, stall, inventory);

    private bool OwnMarketGoods(string actor, InventoryLot lot) =>
        MarketSystem.OwnMarketGoods(MarketStallWorld, actor, lot);


    private MarketGoodsBasis MarketGoodsFor(string actor) =>
        MarketSystem.MarketGoodsFor(MarketStallWorld, actor);

    private bool MarketReserveStock(string actor, InventoryLot lot) =>
        MarketSystem.MarketReserveStock(MarketStallWorld, actor, lot);

    private int MarketSurplus(string actor, InventoryLot lot, MarketGoodsBasis? goods = null) =>
        MarketSystem.MarketSurplus(MarketStallWorld, actor, lot, goods);

    private bool OnMarketStall(InventoryLot lot) =>
        MarketSystem.OnMarketStall(MarketStallWorld, lot);

    // Household goods a member carries away need the household's House to come back to.
    private bool CanCarryMarketGoods(string actor, InventoryLot lot) =>
        MarketSystem.CanCarryMarketGoods(MarketStallWorld, actor, lot);

    private bool ProtectedMarketItem(string actor, InventoryLot lot) =>
        MarketSystem.ProtectedMarketItem(MarketStallWorld, actor, lot);

    private IEnumerable<InventoryLot> MarketLoadSources(string actor, MarketGoodsBasis goods) =>
        MarketSystem.MarketLoadSources(MarketStallWorld, actor, goods);

    private (InventoryLot Goods, InventoryLot Payment)? MarketQuote(string buyer, TownMarketState market,
        MarketStallState stall, MarketStallOccupancy occupancy, MarketGoodsBasis? buyerGoods = null,
        string? selectedGoodsId = null, string? selectedPaymentId = null) =>
        MarketSystem.MarketQuote(MarketStallWorld, buyer, market, stall, occupancy, buyerGoods, selectedGoodsId, selectedPaymentId);

    private MarketExchangeActionPlan PlanMarketExchange(string actor, MarketChoice choice) =>
        MarketSystem.PlanMarketExchange(MarketStallWorld, actor, choice);

    private bool MarketOfferIsCurrent(MarketExchangeActionPlan plan) =>
        MarketSystem.MarketOfferIsCurrent(MarketStallWorld, plan);

    private bool CanCollectMarketStock(string actor, InventoryLot lot, MarketStallOccupancy? occupancy, InventoryCheckpoint inventory) =>
        MarketSystem.CanCollectMarketStock(MarketStallWorld, actor, lot, occupancy, inventory);

    private MarketStockActionPlan PlanMarketStock(string actor, MarketChoice choice) =>
        MarketSystem.PlanMarketStock(MarketStallWorld, actor, choice);

    private bool MarketStockFamilyMatches(InventoryLot lot, IReadOnlyList<PlannedStockMember>? family) =>
        MarketSystem.MarketStockFamilyMatches(MarketStallWorld, lot, family);

    private InventoryLot? CurrentMarketStock(MarketStockActionPlan plan, TownMarketState market, MarketStallState stall, MarketStallOccupancy? occupancy) =>
        MarketSystem.CurrentMarketStock(MarketStallWorld, plan, market, stall, occupancy);

    private IEnumerable<MarketChoice> MarketChoices(string actor) =>
        MarketSystem.MarketChoices(MarketStallWorld, actor);

    private bool IsMarketFoodCandidate(string actor, string candidateId) =>
        MarketSystem.IsMarketFoodCandidate(MarketStallWorld, actor, candidateId);

    private void AddMarketCandidates(List<CognitionCandidate> candidates, string actor)
    {
        var urgentFood = NeedsUrgentFood(agents[actor]);
        // Trade answers and leaving come first, so the limit never hides them behind stock choices.
        var offered = 0;
        foreach (var choice in MarketChoices(actor)
                     .Where(item => !urgentFood || item.Kind == "collect" && IsEdibleFood(item.Lot!.ItemKind) &&
                         AvailableLotQuantity(item.Lot) > 0)
                     .OrderBy(item => item.Kind is "continue" or "cancel" or "leave" ? 0 : 1)
                     )
        {
            ActionPlan? plan = choice.Kind switch
            {
                "borrow" or "leave" => PlanMarketOccupancy(actor, choice),
                "load" or "deposit" or "collect" => PlanMarketStock(actor, choice),
                "buy" or "continue" or "cancel" => PlanMarketExchange(actor, choice),
                _ => null,
            };
            if (choice.Kind is "borrow" or "leave" && plan is null || plan is not null && !CheckActionPlan(plan).Allowed) continue;
            var offer = choice.Trade is { } trade ? society.Checkpoint.Inventory.GetOffer(trade.OfferId) : null;
            var text = choice.Kind switch
            {
                "borrow" => "Walk to the Market and borrow this empty Town-owned stall while you remain at the Market.",
                "load" => $"Collect {choice.Quantity} household-owned {choice.Lot!.ItemKind} to carry to an empty Market stall.",
                "deposit" => $"Place {choice.Quantity} {choice.Lot!.ItemKind} at your borrowed stall; its owner stays the same.",
                "collect" => $"Collect {choice.Quantity} of your {choice.Lot!.ItemKind} from this Market stall.",
                "leave" => "Give up this stall. Goods left here keep their owner and cannot be sold by its next borrower.",
                "buy" => $"Walk to this Market stall and offer 1 {choice.Payment!.ItemKind} for 1 {choice.Lot!.ItemKind}; its named seller may refuse.",
                "cancel" => "Cancel this Market exchange and release its exact goods.",
                _ => actor == choice.Trade!.BuyerId
                    ? $"Wait at the stall for {offer!.FirstQuantity} {choice.Trade.GoodsKind} in exchange for {offer.SecondQuantity} {choice.Trade.PaymentKind}."
                    : MarketTradeMode(actor, choice.Trade) == "waiting-for-buyer"
                        ? $"Wait at your stall for the buyer, who offers {offer!.SecondQuantity} {choice.Trade.PaymentKind} for {offer.FirstQuantity} {choice.Trade.GoodsKind}."
                        : $"Meet the buyer and accept {offer!.SecondQuantity} {choice.Trade.PaymentKind} for {offer.FirstQuantity} {choice.Trade.GoodsKind}; the payment goes to your household.",
            };
            // Above safe idle, like Town project donations: built-in rules cannot act on a Market choice.
            candidates.Add(new(choice.Id, text, choice.Kind switch
            {
                "cancel" or "leave" => 175,
                "collect" => 174,
                "load" or "deposit" or "borrow" => 173,
                "buy" => 172,
                _ => 171,
            }, choice.Stall.BuildingId)
            { ActionPlan = plan });
            if (++offered == MarketCandidateLimit) break;
        }
    }

    private bool ApplyMarketCandidate(string actor, AgentState state, string id, ActionPlan? originalPlan = null)
    {
        if (!IsMarketCandidate(id)) return false;
        TryRegisteredAction(ActionPhase.FreshAdmission, new(actor, state, id, false, originalPlan));
        return true;
    }

    private void PickUpMarketStock(string actor, MarketChoice choice, GridPoint position) =>
        MarketSystem.PickUpMarketStock(MarketStallWorld, actor, choice, position);

    private void BorrowMarketStall(string actor, MarketChoice choice, GridPoint position) =>
        MarketSystem.BorrowMarketStall(MarketStallWorld, actor, choice, position);

    /// <summary>A continued personal intention may walk, but physical arrival needs a new admitted choice.</summary>
    private void ContinueMarketWalk(string actor, string id) =>
        MarketSystem.ContinueMarketWalk(MarketStallWorld, actor, id);

    private void DepositMarketStock(string actor, MarketChoice choice, GridPoint position) =>
        MarketSystem.DepositMarketStock(MarketStallWorld, actor, choice, position);

    private void OfferMarketTrade(string buyer, MarketChoice choice, GridPoint position) =>
        MarketSystem.OfferMarketTrade(MarketStallWorld, buyer, choice, position);

    private string? MarketTradeFailure(TownRuntimeState town, TownMarketState market, MarketTradeState trade, DirectBarterOffer offer) =>
        MarketSystem.MarketTradeFailure(MarketStallWorld, town, market, trade, offer);

    private void ContinueMarketTrade(string actor, AgentState state, MarketChoice choice, bool cancel, DirectBarterOffer? originalOffer = null) =>
        MarketSystem.ContinueMarketTrade(MarketStallWorld, actor, state, choice, cancel, originalOffer);

    private void CancelMarketTrade(string townId, TownMarketState market, MarketTradeState trade, string reason, string? actor = null) =>
        MarketSystem.CancelMarketTrade(MarketStallWorld, townId, market, trade, reason, actor);

    private void EndMarketOccupancy(string townId, TownMarketState market, MarketStallOccupancy occupancy, string reason, string? actor = null) =>
        MarketSystem.EndMarketOccupancy(MarketStallWorld, townId, market, occupancy, reason, actor);

    private void MaintainMarkets() =>
        MarketSystem.MaintainMarkets(MarketStallWorld);
}
