namespace ClankerWorld.Tests;

public sealed class ServerHostConfigurationTests
{
    [Fact]
    public void HostRefusesRemovedFixtureWorldMode()
    {
        using var factory = new ServerWebApplicationFactory(null, gameWorld: false);
        var exception = Assert.Throws<InvalidOperationException>(() => factory.CreateClient());
        Assert.Contains("fixture world mode has been removed", exception.Message, StringComparison.Ordinal);
    }
}
