using ClankerWorld.GodotClient.ClientState;

namespace ClankerWorld.Tests;

public sealed class ServerOriginTests
{
    [Theory]
    [InlineData("http://[::1]:5188/")]
    public void AcceptsAnHttpsOriginOrLiteralLoopbackDevelopmentOrigin(string value)
    {
        var accepted = ServerOrigin.TryResolve(value, out var origin);

        Assert.True(accepted);
        Assert.True(origin.IsAbsoluteUri);
        Assert.Equal("/", origin.AbsolutePath);
    }

    [Theory]
    [InlineData("https://example.test/path")]
    [InlineData("https://example.test/?redirect=https://other.test")]
    [InlineData("not a url")]
    public void RejectsAmbiguousOrUnapprovedOrigins(string value)
    {
        Assert.False(ServerOrigin.TryResolve(value, out _));
    }

    [Fact]
    public void ComparesCanonicalServerOriginsRatherThanPresentationText()
    {
        Assert.True(ServerOrigin.TryResolve("https://EXAMPLE.test/", out var left));
        Assert.True(ServerOrigin.TryResolve("https://example.test:443/", out var same));
        Assert.True(ServerOrigin.TryResolve("https://example.test:8443/", out var differentPort));

        Assert.True(ServerOrigin.Same(left, same));
        Assert.False(ServerOrigin.Same(left, differentPort));
    }
}
