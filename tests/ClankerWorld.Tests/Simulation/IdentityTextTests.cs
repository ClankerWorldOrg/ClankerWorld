using System.Text.Json;
using ClankerWorld.Simulation;

namespace ClankerWorld.Tests;

public sealed class IdentityTextTests
{
    [Theory]
    [InlineData("receipt:é:😀")]
    [InlineData("  saved spelling  ")]
    public void ParsedIdentityPreservesJsonStringBytes(string spelling)
    {
        var identity = Identity.Parse(spelling);
        var expected = JsonSerializer.Serialize(new { Id = spelling });
        Assert.Equal(expected, JsonSerializer.Serialize(new { Id = identity }));
        Assert.Equal(identity, JsonSerializer.Deserialize<Identity>(JsonSerializer.Serialize(spelling)));
        var keyed = new Dictionary<Identity, int> { [identity] = 7 };
        Assert.Equal(JsonSerializer.Serialize(new Dictionary<string, int> { [spelling] = 7 }), JsonSerializer.Serialize(keyed));
        Assert.Equal(7, JsonSerializer.Deserialize<Dictionary<Identity, int>>(JsonSerializer.Serialize(keyed))![identity]);
    }

    [Fact]
    public void IdentityCreationKeepsTheExistingDerivedSpellingButParsingNeverHashes()
    {
        var longValue = "receipt:" + new string('é', 128);
        Assert.Equal(DerivedId.Create(longValue), Identity.Create(longValue).Value);
        Assert.Throws<ArgumentException>(() => Identity.Parse(longValue));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<Identity>(JsonSerializer.Serialize(longValue)));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<Identity>("42"));
        Assert.True(IdentityRules.Reference(longValue));
        Assert.False(IdentityRules.Reference("\nreceipt"));
        Assert.False(IdentityRules.Reference("   "));
        Assert.False(IdentityRules.Reference(" reference ", requireTrimmed: true));
        Assert.False(IdentityRules.Reference("two words", forbidWhitespace: true));
    }

    [Theory]
    [InlineData(0, "")]
    [InlineData(1, "a")]
    [InlineData(2, "a")]
    [InlineData(3, "a😀")]
    [InlineData(4, "a😀b")]
    public void TextCutsOnlyBetweenCompleteUnicodeScalars(int limit, string expected) =>
        Assert.Equal(expected, BoundedText.Create("a😀b", limit).Value);

    [Fact]
    public void ExcerptsKeepEachExistingNormalizationAndSuffixPolicy()
    {
        Assert.Equal("a…", BoundedText.Excerpt("a😀bc", 3, "…").Value);
        Assert.Equal("a b", BoundedText.Excerpt(" \ta\n  b  ", 10, normalization: TextNormalization.Whitespace).Value);
        Assert.Equal("a b", BoundedText.Excerpt("a\nb", 10, normalization: TextNormalization.ControlsAndSpaces).Value);
        Assert.Equal("a\tb", BoundedText.Excerpt("a\tb", 10).Value);
        Assert.Throws<ArgumentOutOfRangeException>(() => BoundedText.Create("a", -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => BoundedText.Excerpt("a", 0, "…"));
    }
}
