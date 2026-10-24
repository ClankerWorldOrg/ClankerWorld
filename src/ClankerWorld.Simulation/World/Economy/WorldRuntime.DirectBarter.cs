using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Kernel;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{

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
                     offer.Id.Value.StartsWith(global::ClankerWorld.Simulation.World.MarketSystem.DirectBarterPrefix, StringComparison.Ordinal) &&
                     offer.ExpiryTick >= WorldTick && (offer.FirstPartyId == actor || offer.SecondPartyId == actor)))
        {
            if (offer.AcceptedBy.Contains(actor, StringComparer.Ordinal))
            {
                OfferDirectBarter(candidates, MarketSystem.PlanDirectBarter(MarketBarterWorld, actor, new(DirectBarterKind.Wait, offer.Id), null, offer),
                    "Meet the other trader and wait for their answer.", 12);
                OfferDirectBarter(candidates, MarketSystem.PlanDirectBarter(MarketBarterWorld, actor, new(DirectBarterKind.Decline, offer.Id), null, offer),
                    "Withdraw the pending exchange and release both reserved items.", 110);
                continue;
            }
            var giveId = offer.FirstPartyId == actor ? offer.FirstLotId : offer.SecondLotId;
            var takeId = offer.FirstPartyId == actor ? offer.SecondLotId : offer.FirstLotId;
            var give = society.Checkpoint.Inventory.GetLot(giveId);
            var take = society.Checkpoint.Inventory.GetLot(takeId);
            var useful = MarketSystem.WantsTradeItem(MarketBarterWorld, actor, take) && (!IsEdibleFood(give.ItemKind) || agents[actor].FullnessBasisPoints >= NeedThresholds.TradeFoodReserveFullness);
            OfferDirectBarter(candidates, MarketSystem.PlanDirectBarter(MarketBarterWorld, actor, new(DirectBarterKind.Accept, offer.Id), null, offer),
                $"Accept exchange: give one {give.ItemKind}, receive one {take.ItemKind}.", useful ? 12 : 60);
            OfferDirectBarter(candidates, MarketSystem.PlanDirectBarter(MarketBarterWorld, actor, new(DirectBarterKind.Decline, offer.Id), null, offer),
                "Decline this exchange and release both reserved items.", useful ? 60 : 12);
        }
        foreach (var other in agents.Keys.Where(other => other != actor)
                     .OrderByDescending(other => CommunitySystem.TrustScore(this.CommunitySkillWorld, actor, other)).ThenBy(other => other, StringComparer.Ordinal))
        {
            if (MarketSystem.TradeOpportunity(MarketBarterWorld, actor, other, null, null) is { } trade)
            {
                OfferDirectBarter(candidates, MarketSystem.PlanDirectBarter(MarketBarterWorld, actor, new(DirectBarterKind.Propose, other), trade, null),
                    $"Offer {society.Checkpoint.GetAgent(other).Name} one {trade.Give.ItemKind} for one {trade.Take.ItemKind}; they may refuse.", 14);
            }
        }
    }
}
