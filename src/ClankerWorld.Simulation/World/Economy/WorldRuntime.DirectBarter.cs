using System.Security.Cryptography;
using System.Text;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private const string DirectBarterPrefix = MarketSystem.DirectBarterPrefix;

    private bool HasTradeResponse(string actor) =>
        MarketSystem.HasTradeResponse(MarketBarterWorld, actor);

    private bool WantsTradeFoodKind(string actor, string kind) =>
        MarketSystem.WantsTradeFoodKind(MarketBarterWorld, actor, kind);

    private bool PersonalTradeReceivingSpace(string first, string second, int firstQuantity, int secondQuantity) =>
        MarketSystem.PersonalTradeReceivingSpace(MarketBarterWorld, first, second, firstQuantity, secondQuantity);

    private bool WantsTradeItem(string actor, InventoryLot item) =>
        MarketSystem.WantsTradeItem(MarketBarterWorld, actor, item);

    private (InventoryLot Give, InventoryLot Take)? TradeOpportunity(string actor, string other, string? giveId = null, string? takeId = null) =>
        MarketSystem.TradeOpportunity(MarketBarterWorld, actor, other, giveId, takeId);

    private DirectBarterActionPlan? PlanDirectBarter(string actor, DirectBarterChoice choice,
        (InventoryLot Give, InventoryLot Take)? selected = null, DirectBarterOffer? selectedOffer = null) =>
        MarketSystem.PlanDirectBarter(MarketBarterWorld, actor, choice, selected, selectedOffer);

    private bool DirectBarterOfferIsCurrent(DirectBarterActionPlan plan) =>
        MarketSystem.DirectBarterOfferIsCurrent(MarketBarterWorld, plan);

    private void OfferDirectBarter(List<CognitionCandidate> candidates, DirectBarterActionPlan? plan, string text, int priority)
    {
        if (plan is not null && CheckActionPlan(plan).Allowed) candidates.Add(new(plan.CandidateId, text, priority) { ActionPlan = plan });
    }

    private void AddTradeCandidates(List<CognitionCandidate> candidates, string actor)
    {
        if (NeedsUrgentWarmth(agents[actor]))
        {
            return;
        }
        foreach (var offer in society.Checkpoint.Inventory.Offers.Where(offer => offer.State == DirectBarterState.Open &&
                     offer.Id.Value.StartsWith(DirectBarterPrefix, StringComparison.Ordinal) &&
                     offer.ExpiryTick >= WorldTick && (offer.FirstPartyId == actor || offer.SecondPartyId == actor)))
        {
            if (offer.AcceptedBy.Contains(actor, StringComparer.Ordinal))
            {
                OfferDirectBarter(candidates, PlanDirectBarter(actor, new(DirectBarterKind.Wait, offer.Id), selectedOffer: offer),
                    "Meet the other trader and wait for their answer.", 12);
                OfferDirectBarter(candidates, PlanDirectBarter(actor, new(DirectBarterKind.Decline, offer.Id), selectedOffer: offer),
                    "Withdraw the pending exchange and release both reserved items.", 110);
                continue;
            }
            var giveId = offer.FirstPartyId == actor ? offer.FirstLotId : offer.SecondLotId;
            var takeId = offer.FirstPartyId == actor ? offer.SecondLotId : offer.FirstLotId;
            var give = society.Checkpoint.Inventory.GetLot(giveId);
            var take = society.Checkpoint.Inventory.GetLot(takeId);
            var useful = WantsTradeItem(actor, take) && (!IsEdibleFood(give.ItemKind) || agents[actor].FullnessBasisPoints >= NeedThresholds.TradeFoodReserveFullness);
            OfferDirectBarter(candidates, PlanDirectBarter(actor, new(DirectBarterKind.Accept, offer.Id), selectedOffer: offer),
                $"Accept exchange: give one {give.ItemKind}, receive one {take.ItemKind}.", useful ? 12 : 60);
            OfferDirectBarter(candidates, PlanDirectBarter(actor, new(DirectBarterKind.Decline, offer.Id), selectedOffer: offer),
                "Decline this exchange and release both reserved items.", useful ? 60 : 12);
        }
        foreach (var other in agents.Keys.Where(other => other != actor)
                     .OrderByDescending(other => TrustScore(actor, other)).ThenBy(other => other, StringComparer.Ordinal))
        {
            if (TradeOpportunity(actor, other) is { } trade)
            {
                OfferDirectBarter(candidates, PlanDirectBarter(actor, new(DirectBarterKind.Propose, other), trade),
                    $"Offer {society.Checkpoint.GetAgent(other).Name} one {trade.Give.ItemKind} for one {trade.Take.ItemKind}; they may refuse.", 14);
            }
        }
    }

    private void ApplyTradeCandidate(string actor, AgentState state, string candidate, ActionPlan? originalPlan = null)
    {
        var plan = originalPlan as DirectBarterActionPlan ?? (DirectBarterChoice.Decode(candidate) is { } choice ? PlanDirectBarter(actor, choice) : null);
        if (plan is not null) ApplyDirectBarter(plan);
    }

    private void ApplyRetainedTradeCandidate(string actor, AgentState state, DirectBarterActionPlan plan) =>
        MarketSystem.ApplyRetainedTradeCandidate(MarketBarterWorld, actor, state, plan);

    private bool TradeQuantityAvailable(InventoryLot lot) =>
        MarketSystem.TradeQuantityAvailable(MarketBarterWorld, lot);

    private bool UsablePersonalTradeLot(InventoryLot lot) =>
        MarketSystem.UsablePersonalTradeLot(MarketBarterWorld, lot);

    private void MaintainDirectBarters() =>
        MarketSystem.MaintainDirectBarters(MarketBarterWorld);
}
