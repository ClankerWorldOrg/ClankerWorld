using ClankerWorld.Simulation.World;

namespace ClankerWorld.Tests;

public sealed class RoadCostPolicyTests
{
    [Fact]
    public void RoadReuseCostsReadTheCurrentBorrowedRoadSetForEveryProposal()
    {
        var map = RiverBridgeTests.Map(".........", ".........", ".........");
        var start = new GridPoint(0, 2);
        var end = new GridPoint(8, 2);
        var roads = new HashSet<GridPoint>();
        var request = new RoadRouteRequest(map, [start], new HashSet<GridPoint> { end },
            new HashSet<GridPoint>(), [], roads, ReuseRoads: true);
        var direct = Enumerable.Range(0, 9).Select(x => new GridPoint(x, 2)).ToArray();
        AssertProposal(direct);
        roads.UnionWith(Enumerable.Range(0, 9).Select(x => new GridPoint(x, 1)));
        roads.Add(start);
        roads.Add(end);
        GridPoint[] discounted = [start, .. Enumerable.Range(1, 7).Select(x => new GridPoint(x, 1)), end];
        AssertProposal(discounted);
        roads.Clear();
        AssertProposal(direct);

        void AssertProposal(GridPoint[] expected)
        {
            var result = RoadRoutePlanner.Plan(request);
            Assert.Equal(RoadRouteOutcomes.Connected, result.Outcome);
            var proposal = Assert.IsType<RoadRouteProposal>(result.Proposal);
            Assert.Equal(expected, proposal.RoadTiles);
            Assert.Empty(proposal.NewCrossings);
            Assert.Empty(proposal.UsedBridgeIds);
            Assert.Null(RoadRoutePlanner.Validate(request, proposal));
        }
    }
}
