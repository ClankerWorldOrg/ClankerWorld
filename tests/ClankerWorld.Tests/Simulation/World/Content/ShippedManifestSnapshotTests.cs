using System.Collections;
using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.World;

namespace ClankerWorld.Tests;

public sealed class ShippedManifestSnapshotTests
{
    [Fact]
    public void CatalogueReadersCannotReplaceAWholeShippedPackage()
    {
        var catalogue = StarterContent.InitialTownManifests;
        var before = catalogue.Select(ContentPackageManifestCodec.Encode).ToArray();
        var entries = Assert.IsAssignableFrom<IList<ContentPackageManifest>>(catalogue);
        Assert.NotEmpty(entries);

        Assert.Throws<NotSupportedException>(() => entries[0] = entries[0]);

        for (var index = 0; index < before.Length; index++)
            Assert.Equal(before[index], ContentPackageManifestCodec.Encode(catalogue[index]));
    }

    [Fact]
    public void ShippedManifestCollectionsCannotBeChangedThroughAnExportedWorld()
    {
        using var world = Create();
        world.InitializeFirstTownContent();
        var before = WorldRuntimeCodec.Encode(world.ExportState());
        foreach (var package in world.Content.Packages)
        {
            var manifest = package.Manifest;
            var definitions = Assert.IsAssignableFrom<IList<ContentDefinition>>(manifest.Definitions);
            Assert.NotEmpty(definitions);
            Assert.Throws<NotSupportedException>(() => definitions[0] = definitions[0]);
            if (manifest.Dependencies.Count > 0)
            {
                var dependencies = Assert.IsAssignableFrom<IList<ContentDependency>>(manifest.Dependencies);
                Assert.Throws<NotSupportedException>(() => dependencies[0] = dependencies[0]);
            }
            Assert.True(Assert.IsAssignableFrom<IList>(manifest.DeclaredCapabilities).IsReadOnly);
            if (manifest.AssetReservations is { } assets)
                Assert.True(Assert.IsAssignableFrom<IList>(assets).IsReadOnly);
        }
        Assert.Equal(before, WorldRuntimeCodec.Encode(world.ExportState()));
        using var restored = WorldRuntime.Restore(WorldRuntimeCodec.Decode(before));
        Assert.Equal(before, WorldRuntimeCodec.Encode(restored.ExportState()));
    }

    [Fact]
    public void NewWorldsKeepTheirOwnContentLifecycle()
    {
        using var first = Create();
        using var second = Create();
        first.InitializeFirstTownContent();
        second.InitializeFirstTownContent();
        var before = WorldRuntimeCodec.Encode(second.ExportState());
        var package = new ContentPackageManifest("snapshot-isolated", new(1, 0, 0),
            "sha256:" + new string('a', 64), [], [], []);

        first.ProposeContent(package);

        Assert.Contains(first.Content.Packages, item => item.Manifest.PackageId == package.PackageId);
        Assert.DoesNotContain(second.Content.Packages, item => item.Manifest.PackageId == package.PackageId);
        Assert.Equal(before, WorldRuntimeCodec.Encode(second.ExportState()));
        first.Validate();
        second.Validate();
    }

    [Fact]
    public void WarmInitialContentUsesBoundedTemporaryMemory()
    {
        using var warm = Create();
        warm.InitializeFirstTownContent();
        using var world = Create();
        var allocated = GC.GetAllocatedBytesForCurrentThread();
        world.InitializeFirstTownContent();
        allocated = GC.GetAllocatedBytesForCurrentThread() - allocated;

        Assert.InRange(allocated, 0, 28L * 1024 * 1024);
    }

    private static WorldRuntime Create() => new("town-project-real-donation",
        startPace: WorldStartPace.FounderSetup,
        geographyOptions: new("town-project-real-donation", WorldSizePreset.Small));
}
