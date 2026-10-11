namespace ClankerWorld.Simulation.Tests;

public sealed class DerivedIdTests
{
    [Fact]
    public void BoundaryPreservesTheWholeShortValueAndHashesTheWholeLongValue()
    {
        var shortId = "lot:" + new string('x', 124);
        Assert.Equal(shortId, DerivedId.Create(shortId));
        Assert.Equal("lot:sha256:2f8e9f595279cc4c9a852c09f29d06f9beaa5d739d1e6ec62de9d08ad4fb5466",
            DerivedId.Create(shortId + "x"));
        Assert.NotEqual(DerivedId.Create(shortId + "x"), DerivedId.Create(shortId + "y"));
    }

    [Fact]
    public void ExistingLandGrantDigestsKeepTheirUrlSafeShortSpelling()
    {
        Assert.Equal("council:ungWv48Bz-pBQUDeXa4iI7ADYaOWF3qctBD_YfIAFa0", DerivedId.HashBase64Url("abc", "council:"));
        Assert.Throws<ArgumentException>(() => DerivedId.HashBase64Url("abc", new string('p', DerivedId.MaximumLength)));
    }

    [Fact]
    public void LongPrefixesAndUnicodeRemainDistinctAndWithinTheSharedBoundary()
    {
        var first = new string('界', 128) + ":child:" + "é";
        var second = new string('界', 128) + ":child:" + "e";
        var identity = DerivedId.Create(first);
        Assert.InRange(identity.Length, 1, DerivedId.MaximumLength);
        Assert.Equal(identity, DerivedId.Create(first));
        Assert.NotEqual(identity, DerivedId.Create(second));
        Assert.Throws<ArgumentException>(() => DerivedId.Create(first + "\n"));
    }

    [Fact]
    public void NumberedRecordsKeepTheirCounterAndValidateTheirCompleteOriginalIdentity()
    {
        Assert.Equal("town:proposal:7", DerivedId.Numbered("town:proposal:", 7));
        var prefix = new string('t', 128) + ":proposal:";
        var identity = DerivedId.Numbered(prefix, long.MaxValue);
        Assert.InRange(identity.Length, 1, DerivedId.MaximumLength);
        Assert.EndsWith(":" + long.MaxValue, identity, StringComparison.Ordinal);
        Assert.True(DerivedId.MatchesNumbered(identity, prefix, long.MaxValue));
        Assert.False(DerivedId.MatchesNumbered(identity, prefix, long.MaxValue - 1));
        Assert.False(DerivedId.MatchesNumbered(identity, prefix + "other:", long.MaxValue));
        Assert.False(DerivedId.MatchesNumbered("town:proposal:007", "town:proposal:", 7));
        Assert.Throws<ArgumentException>(() => DerivedId.Numbered(prefix + "\n", 1));
    }
}
