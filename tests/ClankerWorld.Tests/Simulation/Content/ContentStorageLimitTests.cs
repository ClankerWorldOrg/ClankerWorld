using System.Text;
using System.Text.Json;
using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.World;

namespace ClankerWorld.Tests;

public sealed class ContentStorageLimitTests
{
    private static ContentPackageManifest Package(string id = "bounded") => new(id, new(1, 0, 0),
        "sha256:" + new string('a', 64), [], [], []);
    private static ContentDefinition Definition(string id = "sample") => new("item", id, new(1, 0, 0),
        "Sample", "sha256:" + new string('b', 64));

    [Theory]
    [InlineData("definitions")]
    [InlineData("dependencies")]
    [InlineData("capabilities")]
    [InlineData("display")]
    [InlineData("payload")]
    [InlineData("total-payload")]
    public void OversizedManifestCannotCreateAProposalOrGovernanceEvent(string boundary)
    {
        var package = boundary switch
        {
            "definitions" => Package() with { Definitions = Enumerable.Range(0, ContentPackageRules.MaximumDefinitions + 1).Select(i => Definition("item-" + i)).ToArray() },
            "dependencies" => Package() with { Dependencies = Enumerable.Range(0, ContentPackageRules.MaximumDependencies + 1).Select(i => new ContentDependency("package-" + i, new(new(1, 0, 0), new(2, 0, 0)))).ToArray() },
            "capabilities" => Package() with { DeclaredCapabilities = Enumerable.Range(0, ContentPackageRules.MaximumCapabilities + 1).Select(i => "capability-" + i).ToArray() },
            "display" => Package() with { Definitions = [Definition() with { DisplayName = new string('a', ContentPackageRules.MaximumDisplayNameLength + 1) }] },
            "payload" => Package() with { Definitions = [Definition() with { PayloadJson = new string('a', ContentPackageRules.MaximumPayloadBytes + 1) }] },
            _ => Package() with { Definitions = Enumerable.Range(0, 9).Select(i => Definition("item-" + i) with { PayloadJson = new string('a', ContentPackageRules.MaximumPayloadBytes) }).ToArray() },
        };
        var registry = new ContentPackageRegistry();
        Assert.Throws<ArgumentException>(() => registry.Propose(package));
        Assert.Empty(registry.ExportState().Packages);
        Assert.Empty(registry.ExportState().Events);
    }

    [Fact]
    public void FullRegistryRefusesNewPackagesAndRemovedQuarantineFreesCapacityAcrossReload()
    {
        var registry = new ContentPackageRegistry();
        var first = Package("first");
        registry.Propose(first);
        registry.Validate(first.PackageId, ContentPackageResolver.Resolve([first], [first.PackageId]), 0);
        registry.Approve(first.PackageId, 0);
        registry.Stage(first.PackageId, 0);
        registry.Rollback(first.PackageId, 0, "owner quarantine");
        for (var i = 1; i < ContentPackageRules.MaximumPackages; i++) registry.Propose(Package("package-" + i));
        var before = JsonSerializer.SerializeToUtf8Bytes(registry.ExportState());
        Assert.Throws<InvalidOperationException>(() => registry.Propose(Package("one-too-many")));
        var full = registry.ExportState();
        Assert.Throws<InvalidDataException>(() => ContentPackageRegistry.Restore(full with
        { Packages = [.. full.Packages, full.Packages[0] with { Manifest = Package("overflow") }] }));
        Assert.Throws<InvalidOperationException>(() => registry.RemoveQuarantined("package-1", 0));
        Assert.Equal(before, JsonSerializer.SerializeToUtf8Bytes(registry.ExportState()));
        registry.RemoveQuarantined(first.PackageId, 0);
        registry = ContentPackageRegistry.Restore(registry.ExportState());
        registry.Propose(Package("replacement"));
        Assert.Equal(ContentPackageRules.MaximumPackages, registry.ExportState().Packages.Count);
        Assert.Contains(registry.ExportState().Events, item => item.PackageId == first.PackageId && item.Kind == "package_removed");
        Assert.DoesNotContain(registry.ExportState().Packages, item => item.Manifest.PackageId == first.PackageId);
        Assert.Throws<InvalidOperationException>(() => registry.Propose(first));
    }

    [Fact]
    public void RemovingQuarantinedContentPersistsBeforeChangingTheLiveWorld()
    {
        using var world = new WorldRuntime("bounded-content");
        world.Pause();
        var package = Package();
        world.ProposeContent(package);
        world.ValidateContent(package.PackageId, world.ResolveContent(package.PackageId));
        world.ApproveContent(package.PackageId);
        world.StageContent(package.PackageId);
        world.RollbackContent(package.PackageId, "owner quarantine");
        var before = WorldRuntimeCodec.Encode(world.ExportState());
        Assert.Throws<IOException>(() => world.RemoveQuarantinedContent(package.PackageId, _ => throw new IOException("controlled write refusal")));
        Assert.Equal(before, WorldRuntimeCodec.Encode(world.ExportState()));
        byte[]? persisted = null;
        world.RemoveQuarantinedContent(package.PackageId, state => persisted = WorldRuntimeCodec.Encode(state));
        Assert.Equal(persisted, WorldRuntimeCodec.Encode(world.ExportState()));
        using var restored = WorldRuntime.Restore(WorldRuntimeCodec.Decode(persisted!));
        Assert.Equal(persisted, WorldRuntimeCodec.Encode(restored.ExportState()));
        Assert.DoesNotContain(restored.Content.Packages, item => item.Manifest.PackageId == package.PackageId);
    }
}
