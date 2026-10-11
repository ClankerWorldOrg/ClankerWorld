using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Playtest;

namespace ClankerWorld.Simulation.Tests;

public sealed class ContentIdImportTests
{
    [Theory]
    [InlineData("definition", 512, true)]
    [InlineData("definition", 513, false)]
    [InlineData("resource", 512, true)]
    [InlineData("resource", 513, false)]
    [InlineData("package", 512, true)]
    [InlineData("package", 513, false)]
    public void ImportChecksTheSharedLimitBeforeRecordingAnyPackage(string lane, int length, bool accepted)
    {
        var workstation = HouseContent.Create().Definitions.Single(definition => definition.Kind == "building")
            .CanonicalId(HouseContent.Create().PackageDigest);
        var package = ContentIdFixture.Package(workstation, lane == "resource" ? new string('x', length) : "wood");
        if (lane == "definition")
        {
            var overhead = package.Definitions[0].CanonicalId(package.PackageDigest).Length - package.Definitions[0].LocalId.Length;
            package = ContentIdFixture.Package(workstation, "wood", new string('a', length - overhead));
        }
        if (lane == "package") package = package with { PackageId = new string('a', length) };
        var registry = new ContentPackageRegistry();
        if (accepted)
        {
            var record = registry.Propose(package);
            Assert.Equal(package, record.Manifest);
            var bytes = ContentPackageManifestCodec.Encode(package);
            Assert.Equal(bytes, ContentPackageManifestCodec.Encode(ContentPackageManifestCodec.Decode(bytes)));
        }
        else
        {
            var error = Assert.Throws<ArgumentException>(() => registry.Propose(package));
            Assert.Contains("512", error.Message, StringComparison.Ordinal);
            Assert.Empty(registry.ExportState().Packages);
            Assert.Empty(registry.ExportState().Events);
        }
    }
}
