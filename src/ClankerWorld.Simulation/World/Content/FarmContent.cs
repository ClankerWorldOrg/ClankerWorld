using System.Security.Cryptography;
using System.Text;
using ClankerWorld.Simulation.Content;

namespace ClankerWorld.Simulation.World;

/// <summary>Incremental universal-grain and household Farmhouse content.</summary>
public static class FarmContent
{
    public const string PackageId = "clankerworld-farm-v1";

    public static BuildingDefinition Farmhouse1x1()
    {
        var digest = PackageDigest();
        var version = ContentVersion.Parse("1.0.0");
        return new BuildingDefinition(digest, "farmhouse-1x1", version, "Farmhouse", 1, 1, 1,
            [new("wood", 8), new("stone", 2)], ["farmhouse", "crop-processing"]);
    }

    public static ContentPackageManifest Create()
    {
        var farmhouse = Farmhouse1x1();
        var version = farmhouse.Version;
        var digest = farmhouse.PackageDigest;
        RecipeDefinition[] recipes =
        [
            new(digest, "mill-grain", version, "Mill grain into flour",
                [new("grain", 1)], [new("flour", 1)], 20, farmhouse.CanonicalId, ["grain-processing"]),
        ];
        return StarterContent.BuildManifest(PackageId, version, digest, [farmhouse], recipes,
            [new ContentDependency(HouseContent.PackageId,
                new ContentVersionRange(version, ContentVersion.Parse("2.0.0")))]);
    }

    private static string PackageDigest() => "sha256:" + Convert.ToHexStringLower(SHA256.HashData(
        Encoding.UTF8.GetBytes("clankerworld-farm-v1:1.0.0:tile-fields-named-planting-grain-flour")));
}

/// <summary>A separately buildable long Farmhouse; its trial cost is provisional.</summary>
public static class FarmhouseVariantContent
{
    public const string PackageId = "clankerworld-farmhouse-1x2-v1";

    public static ContentPackageManifest Create() => WorkstationVariantContent.Create(
        PackageId, "farmhouse-1x2", "Farmhouse (1×2)", 1, 2,
        [new("wood", 16), new("stone", 4)], FarmContent.Farmhouse1x1(),
        [FarmContent.Create()],
        [WorkstationVariantContent.Require(FarmContent.PackageId, "1.0.0")]);
}

/// <summary>
/// Private farm Silo, held by the household that holds the neighboring
/// Farmhouse. Its household's harvests are stored here. The cost is a
/// provisional playtest value; storage capacity is not decided yet.
/// </summary>
public static class SiloContent
{
    public const string PackageId = "clankerworld-silo-v1";

    public static BuildingDefinition Silo1x1()
    {
        var digest = PackageDigest();
        var version = ContentVersion.Parse("1.0.0");
        return new BuildingDefinition(digest, "silo-1x1", version, "Silo", 1, 1, 1,
            [new("wood", 6), new("stone", 2)], ["silo", "farm-storage"]);
    }

    public static ContentPackageManifest Create()
    {
        var silo = Silo1x1();
        return StarterContent.BuildManifest(PackageId, silo.Version, silo.PackageDigest, [silo], [],
            [new ContentDependency(FarmContent.PackageId,
                new ContentVersionRange(silo.Version, ContentVersion.Parse("2.0.0")))]);
    }

    private static string PackageDigest() => "sha256:" + Convert.ToHexStringLower(SHA256.HashData(
        Encoding.UTF8.GetBytes("clankerworld-silo-v1:1.0.0:silo-1x1")));
}
