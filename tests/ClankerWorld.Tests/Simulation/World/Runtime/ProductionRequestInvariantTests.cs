using ClankerWorld.Simulation.World;

namespace ClankerWorld.Tests;

public sealed class ProductionRequestInvariantTests
{
    private static ToolMakingRequestState Request(string actor = "customer") => new(
        ProductionSystem.ToolMakingRequestId(actor, "shop", "recipe:test", 0), actor, "home", "shop", "recipe:test", "iron_knife", 0, 0);

    [Theory]
    [InlineData("identity")]
    [InlineData("status")]
    [InlineData("accepted")]
    public void InvalidRequestFailsAtProductionBeforePublication(string field)
    {
        var system = new ProductionSystem(WorldContentSimulationState.Empty, []);
        var before = system.State;
        var request = field switch
        {
            "identity" => Request() with { Id = "forged" },
            "status" => Request() with { Status = ToolMakingRequestStatus.Ready },
            "accepted" => Request() with { AcceptedTick = 0 },
            _ => throw new ArgumentOutOfRangeException(nameof(field)),
        };

        Assert.Throws<InvalidDataException>(() => system.PlaceRequest(request));
        Assert.Same(before, system.State);
    }

    [Fact]
    public void TerminalRetentionIsCheckedAfterItsExistingMaintenancePoint()
    {
        var requests = Enumerable.Range(0, ToolMakingRequestRules.MaximumTerminalRequests + 1)
            .Select(index => Request("customer" + index) with { Status = ToolMakingRequestStatus.Withdrawn }).ToArray();
        var system = new ProductionSystem(WorldContentSimulationState.Empty, requests);

        // An intermediate owner operation must permit the record that maintenance retires.
        system.RetireRequests(new HashSet<string>(StringComparer.Ordinal));
        Assert.Throws<InvalidDataException>(system.CheckMaintainedRequests);
        system.RetireRequests(new HashSet<string>(StringComparer.Ordinal) { requests[0].Id });
        system.CheckMaintainedRequests();

        Assert.Equal(ToolMakingRequestRules.MaximumTerminalRequests, system.State.Requests.Count);
        Assert.Equal(ToolMakingRequestRules.MaximumTerminalRequests + 1, requests.Length);
    }
}
