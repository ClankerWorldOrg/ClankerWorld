namespace ClankerWorld.Simulation.Tests;

public sealed class ViewerHostConfigurationTests
{
    [Fact]
    public void HostRefusesRemovedFixtureWorldMode()
    {
        using var factory = new ViewerWebApplicationFactory(null, privateWorld: false);
        var exception = Assert.Throws<InvalidOperationException>(() => factory.CreateClient());
        Assert.Contains("fixture world mode has been removed", exception.Message, StringComparison.Ordinal);
    }
}
