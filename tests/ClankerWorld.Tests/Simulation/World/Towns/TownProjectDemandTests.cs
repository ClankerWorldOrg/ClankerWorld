using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;
using ClankerWorld.Simulation.World;

namespace ClankerWorld.Tests;

public sealed class TownProjectDemandTests
{
    [Fact]
    public async Task AStallReturnedEarlierInTheSameTickStopsAnAlreadyAdmittedExtraStallProposal()
    {
        var state = await PaidMarketWorld.StateAsync();
        var adults = state.Society.Society.Agents.Where(person => person.Status == SocietyAgentStatus.Active &&
                person.AgeBand == SocietyAgeBand.Adult && PaidMarketWorld.Town(state).ResidentIds.Contains(person.Id))
            .Select(person => person.Id).Order(StringComparer.Ordinal).ToArray();
        Assert.True(adults.Length >= 3);
        var leaving = adults[0];
        var staying = adults[1];
        var proposer = adults[^1];
        var market = PaidMarketWorld.Market(state);
        var sellers = new[] { leaving, staying };
        var stalls = market.Stalls.OrderBy(stall => stall.SlotIndex).ToArray();
        Assert.Equal(2, stalls.Length);
        var inventory = state.Society.Society.Inventory;
        for (var index = 0; index < sellers.Length; index++)
        {
            inventory = InventoryAuthority.AddLot(inventory, "demand-sale-cloth:" + sellers[index], "cloth", sellers[index], 1);
            state = PaidMarketWorld.At(state, sellers[index], MarketContent.StallEntrance(market.Site, stalls[index].SlotIndex));
        }
        state = PaidMarketWorld.WithInventory(state, inventory);
        var borrowing = new MarketRulesPolicy
        {
            Choose = (actor, candidates) => Array.IndexOf(sellers, actor) is var index && index >= 0
                ? candidates.FirstOrDefault(candidate => candidate.Id.StartsWith("market_borrow:", StringComparison.Ordinal) &&
                    candidate.DestinationId == stalls[index].BuildingId) : null,
        };
        using (var setup = WorldRuntime.Restore(state, borrowing.CreateProvider))
        {
            for (var tick = 0; tick < 12 && PaidMarketWorld.Market(setup).Occupancies.Count(item => item.EndedTick is null) < 2; tick++)
                Assert.True((await setup.AdvanceOneTickAsync()).Advanced);
            Assert.All(sellers, seller =>
            {
                Assert.Contains(borrowing.Chosen, choice => choice.Actor == seller && choice.Id.StartsWith("market_borrow:", StringComparison.Ordinal));
                Assert.Single(PaidMarketWorld.Market(setup).Occupancies, occupancy => occupancy.SellerAgentId == seller && occupancy.EndedTick is null);
            });
            state = WorldRuntimeCodec.Decode(WorldRuntimeCodec.Encode(setup.ExportState()));
        }
        var town = PaidMarketWorld.Town(state);
        var prefix = $"civic|{town.Id}|project|{MarketContent.Stall1x1().LocalId}|";
        // Preserve actual borrowing and make only the proposer stand at the public board.
        state = state with
        {
            Agents = state.Agents.Select(person => person with
            {
                Position = person.AgentId == proposer ? town.OriginSite!.Value : person.Position,
                LastDecisionContext = null,
            }).ToArray(),
        };
        MarketRulesPolicy Choices() => new()
        {
            Choose = (actor, candidates) => actor == leaving
                ? candidates.FirstOrDefault(candidate => candidate.Id.StartsWith("market_leave:", StringComparison.Ordinal))
                : actor == proposer ? candidates.FirstOrDefault(candidate => candidate.Id.StartsWith(prefix, StringComparison.Ordinal)) : null,
        };
        var policy = Choices();
        using var world = WorldRuntime.Restore(state, policy.CreateProvider);
        using var replay = WorldRuntime.Restore(WorldRuntimeCodec.Decode(WorldRuntimeCodec.Encode(state)), Choices().CreateProvider);
        var before = WorldRuntimeCodec.Encode(world.ExportState());
        Assert.False((await world.AdvanceOneTickAsync(commitPermitted: () => false)).Advanced);
        Assert.Equal(before, WorldRuntimeCodec.Encode(world.ExportState()));
        var step = await world.AdvanceOneTickAsync();
        Assert.True(step.Advanced);
        var leave = Assert.Single(step.Decisions, decision => decision.AgentId == leaving &&
            decision.Admission.Intention?.CandidateId.StartsWith("market_leave:", StringComparison.Ordinal) == true);
        var proposal = Assert.Single(step.Decisions, decision => decision.AgentId == proposer &&
            decision.Admission.Intention?.CandidateId.StartsWith(prefix, StringComparison.Ordinal) == true);
        Assert.True(string.CompareOrdinal(leave.AgentId, proposal.AgentId) < 0);
        Assert.All(new[] { leave, proposal }, decision =>
        {
            Assert.True(decision.Admission.Accepted);
            Assert.False(decision.Admission.FellBack);
            Assert.Null(decision.PlanRefusal);
        });
        var plan = Assert.IsType<TownProjectProposalActionPlan>(proposal.ActionPlan);
        Assert.True(plan.Bound);
        Assert.Equal(town.Council!.Revision, plan.CouncilRevision);
        Assert.Single(PaidMarketWorld.Market(world).Occupancies, occupancy => occupancy.SellerAgentId == leaving && occupancy.EndedTick == world.WorldTick);
        Assert.Single(PaidMarketWorld.Market(world).Occupancies, occupancy => occupancy.SellerAgentId == staying && occupancy.EndedTick is null);

        Assert.DoesNotContain(world.Towns[0].Council!.Proposals, item => item.Project?.DefinitionId == MarketContent.Stall1x1().CanonicalId);
        Assert.Equal(ActionRefusalReason.TargetUnavailable, world.CheckActionPlan(plan).Refusal!.Reason);
        Assert.Single(world.Towns[0].Projects);
        Assert.Equal(2, PaidMarketWorld.Market(world).Stalls.Count);
        Assert.True((await replay.AdvanceOneTickAsync()).Advanced);
        Assert.Equal(WorldRuntimeCodec.Encode(world.ExportState()), WorldRuntimeCodec.Encode(replay.ExportState()));
        var encoded = WorldRuntimeCodec.Encode(world.ExportState());
        using var restored = WorldRuntime.Restore(WorldRuntimeCodec.Decode(encoded));
        Assert.Equal(encoded, WorldRuntimeCodec.Encode(restored.ExportState()));
    }
}
