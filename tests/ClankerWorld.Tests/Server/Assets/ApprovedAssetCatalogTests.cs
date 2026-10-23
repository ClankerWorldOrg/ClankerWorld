using ClankerWorld.Simulation.World;
using ClankerWorld.Server.Content;

namespace ClankerWorld.Tests;

public sealed class ApprovedAssetCatalogTests
{
    private const string PortraitAliceDigest =
        "sha256:0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

    [Fact]
    public void MissingCatalogFailsClosedToNoApprovedReferences()
    {
        var path = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            $"clankerworld-missing-catalog-{Guid.NewGuid():N}.json");

        var catalog = ApprovedAssetCatalog.LoadOrDeny(path);

        Assert.Empty(catalog.GetApprovedReferences());
        Assert.False(catalog.IsApproved(new OwnerApprovedAssetReference("portrait-alice", PortraitAliceDigest)));
    }

    [Fact]
    public void CompleteCanonicalCatalogAllowsOnlyItsExactReferences()
    {
        var directory = Directory.CreateTempSubdirectory("clankerworld-approved-assets-");
        try
        {
            var path = System.IO.Path.Combine(directory.FullName, "approved-assets.json");
            File.WriteAllText(path, $$"""
                {"schemaVersion":1,"references":[{"assetId":"portrait-alice","assetDigest":"{{PortraitAliceDigest}}"}]}
                """);

            var catalog = ApprovedAssetCatalog.LoadOrDeny(path);

            Assert.True(catalog.IsApproved(new OwnerApprovedAssetReference("portrait-alice", PortraitAliceDigest)));
            Assert.False(catalog.IsApproved(new OwnerApprovedAssetReference(
                "portrait-alice",
                "sha256:ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff")));
            Assert.False(catalog.IsApproved(new OwnerApprovedAssetReference("portrait-bob", PortraitAliceDigest)));
        }
        finally { directory.Delete(recursive: true); }
    }

    [Theory]
    [InlineData("{\"schemaVersion\":1,\"references\":[{\"assetId\":\"portrait-alice\",\"assetDigest\":\"sha256:bad\"}]}")]
    [InlineData("{\"schemaVersion\":2,\"references\":[]}")]
    public void ExistingInvalidCatalogRefusesToStart(string content)
    {
        var directory = Directory.CreateTempSubdirectory("clankerworld-approved-assets-");
        try
        {
            var path = System.IO.Path.Combine(directory.FullName, "approved-assets.json");
            File.WriteAllText(path, content);

            Assert.Throws<InvalidDataException>(() => ApprovedAssetCatalog.LoadOrDeny(path));
        }
        finally { directory.Delete(recursive: true); }
    }
}
