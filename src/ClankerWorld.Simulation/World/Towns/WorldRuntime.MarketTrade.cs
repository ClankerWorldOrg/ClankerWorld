using MarketGoodsBasis = ClankerWorld.Simulation.World.MarketSystem.MarketGoodsBasis;
using MarketChoice = ClankerWorld.Simulation.World.MarketSystem.MarketChoice;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Kernel;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{

    private const int MarketCandidateLimit = 16;

    private static bool IsMarketCandidate(string id) => id.StartsWith("market_", StringComparison.Ordinal);

    /// <summary>The Hall, stall slots and aisles of every standing Market, which other building and planting leave clear.</summary>
    private IEnumerable<GridPoint> MarketSiteTiles() => Ground.MarketSites;

    /// <summary>Household goods stay on sale while one of the household's members borrows the stall they lie on.</summary>
    private bool OnBorrowedMarketStall(InventoryLot lot) =>
        InventoryHoldRules.For(InventoryIndex.For(society.Checkpoint.Inventory), lot)
            .Any(hold => hold.Purpose == InventoryHoldPurpose.MarketStall);

    private static string MarketChoiceId(string kind, params string[] parts) =>
        MarketSystem.MarketChoiceId(kind, parts);

    private string MarketTradeMode(string actor, MarketTradeState trade) =>
        MarketSystem.MarketTradeMode(MarketStallWorld, actor, trade);

    private int MarketSurplus(string actor, InventoryLot lot, MarketGoodsBasis? goods = null) =>
        MarketSystem.MarketSurplus(MarketStallWorld, actor, lot, goods);

    private bool OnMarketStall(InventoryLot lot) =>
        MarketSystem.OnMarketStall(MarketStallWorld, lot);

    private bool ProtectedMarketItem(string actor, InventoryLot lot) =>
        MarketSystem.ProtectedMarketItem(MarketStallWorld, actor, lot);

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
                "borrow" or "leave" => MarketSystem.PlanMarketOccupancy(MarketStallWorld, actor, choice),
                "load" or "deposit" or "collect" => MarketSystem.PlanMarketStock(MarketStallWorld, actor, choice),
                "buy" or "continue" or "cancel" => MarketSystem.PlanMarketExchange(MarketStallWorld, actor, choice),
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

    /// <summary>A continued personal intention may walk, but physical arrival needs a new admitted choice.</summary>
    private void ContinueMarketWalk(string actor, string id) =>
        MarketSystem.ContinueMarketWalk(MarketStallWorld, actor, id);

    private void MaintainMarkets() =>
        MarketSystem.MaintainMarkets(MarketStallWorld);
}
