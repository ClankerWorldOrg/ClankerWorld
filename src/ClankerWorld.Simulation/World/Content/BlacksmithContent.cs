using System.Security.Cryptography;
using System.Text;
using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Kernel;

namespace ClankerWorld.Simulation.World;

/// <summary>Incremental household Blacksmith and distinct starter-tool content.</summary>
public static class BlacksmithContent
{
    public const string PackageId = "clankerworld-blacksmith-v1";

    // All rates and trial costs are provisional. The digest changes because
    // the immutable package now contains the complete agreed tool roster.
    private const string ContentRevision =
        "clankerworld-blacksmith-v1:1.2.0:axes-pickaxes-hoes-hammers-wooden-iron-sickles-knife-refined-iron-handcart-fittings";

    public static BuildingDefinition Blacksmith1x2()
    {
        var digest = PackageDigest();
        var version = ContentVersion.Parse("1.2.0");
        return new BuildingDefinition(digest, "blacksmith-1x2", version, "Blacksmith", 1, 2, 1,
            [new("wood", 12), new("stone", 4)], ["blacksmith", "metalworking"]);
    }

    public static ContentPackageManifest Create()
    {
        var blacksmith = Blacksmith1x2();
        var version = blacksmith.Version;
        var digest = blacksmith.PackageDigest;
        RecipeDefinition[] recipes =
        [
            new(digest, "wooden-axe", version, "Make wooden axe",
                [new("wood", 3)], [new("wooden_axe", 1)], 20, blacksmith.CanonicalId, ["tool", "woodcutting"]),
            new(digest, "wooden-pickaxe", version, "Make wooden pickaxe",
                [new("wood", 3)], [new("wooden_pickaxe", 1)], 20, blacksmith.CanonicalId, ["tool", "mining"]),
            new(digest, "stone-axe", version, "Make stone axe",
                [new("wood", 2), new("stone", 2)], [new("stone_axe", 1)], 24,
                blacksmith.CanonicalId, ["tool", "woodcutting"]),
            new(digest, "stone-pickaxe", version, "Make stone pickaxe",
                [new("wood", 2), new("stone", 2)], [new("stone_pickaxe", 1)], 24,
                blacksmith.CanonicalId, ["tool", "mining"]),
            new(digest, "iron-axe", version, "Make iron axe",
                [new("wood", 2), new("iron", 2)], [new("iron_axe", 1)], 30,
                blacksmith.CanonicalId, ["tool", "woodcutting"]),
            new(digest, "iron-pickaxe", version, "Make iron pickaxe",
                [new("wood", 2), new("iron", 2)], [new("iron_pickaxe", 1)], 30,
                blacksmith.CanonicalId, ["tool", "mining"]),
            new(digest, "wooden-hoe", version, "Make wooden hoe",
                [new("wood", 3)], [new("wooden_hoe", 1)], 20, blacksmith.CanonicalId, ["tool", "farming"]),
            new(digest, "iron-hoe", version, "Make iron hoe",
                [new("wood", 1), new("iron", 1)], [new("iron_hoe", 1)], 30,
                blacksmith.CanonicalId, ["tool", "farming"]),
            new(digest, "wooden-hammer", version, "Make wooden hammer",
                [new("wood", 2)], [new("wooden_hammer", 1)], 20,
                blacksmith.CanonicalId, ["tool", "building"]),
            new(digest, "stone-hammer", version, "Make stone hammer",
                [new("wood", 1), new("stone", 2)], [new("stone_hammer", 1)], 24,
                blacksmith.CanonicalId, ["tool", "building"]),
            new(digest, "wooden-sickle", version, "Make wooden sickle",
                [new("wood", 2)], [new("wooden_sickle", 1)], 20,
                blacksmith.CanonicalId, ["tool", "harvesting"]),
            new(digest, "iron-sickle", version, "Make iron sickle",
                [new("wood", 1), new("iron", 1)], [new("iron_sickle", 1)], 30,
                blacksmith.CanonicalId, ["tool", "harvesting"]),
            new(digest, "iron-knife", version, "Make iron knife",
                [new("iron", 1)], [new("iron_knife", 1)], 30,
                blacksmith.CanonicalId, ["tool", "preparation"]),
            new(digest, "iron-fittings", version, "Make iron fittings",
                [new("iron", 1)], [new("iron_fittings", 2)], 20, blacksmith.CanonicalId, ["metalworking"]),
            new(digest, "handcart", version, "Build handcart",
                [new("wood", 4), new("iron_fittings", 2), new("rope", 1)], [new("handcart", 1)], 40,
                blacksmith.CanonicalId, ["vehicle", "carrying"]),
            new(digest, "refine-iron", version, "Refine iron ore",
                [new("iron_ore", 2), new("wood", 1)], [new("iron", 1)], 24,
                blacksmith.CanonicalId, ["metalworking"]),
        ];
        return StarterContent.BuildManifest(PackageId, version, digest, [blacksmith], recipes,
            [new ContentDependency(HouseContent.PackageId,
                new ContentVersionRange(ContentVersion.Parse("1.0.0"), ContentVersion.Parse("2.0.0")))]);
    }

    private static string PackageDigest() => "sha256:" + Convert.ToHexStringLower(SHA256.HashData(
        Encoding.UTF8.GetBytes(ContentRevision)));
}

/// <summary>A larger Blacksmith with the same tools, refining, ornaments and personal cart work.</summary>
public static class BlacksmithVariantContent
{
    public const string PackageId = "clankerworld-blacksmith-2x2-v1";

    public static ContentPackageManifest Create() => WorkstationVariantContent.Create(
        PackageId, "blacksmith-2x2", "Blacksmith (2×2)", 2, 2,
        [new("wood", 24), new("stone", 8)], BlacksmithContent.Blacksmith1x2(),
        [BlacksmithContent.Create(), OrnamentContent.Create()],
        [WorkstationVariantContent.Require(BlacksmithContent.PackageId, "1.2.0"),
            WorkstationVariantContent.Require(OrnamentContent.PackageId, "1.0.0")]);
}

/// <summary>Physical ornaments, with provisional material costs and work times.</summary>
public static class OrnamentContent
{
    public const string PackageId = "clankerworld-ornaments-v1";
    public const string Gold = "gold";
    public const string GoldOrnament = "gold_ornament";
    public const string DiamondOrnament = "diamond_ornament";

    public static bool IsOrnament(string kind) => kind is GoldOrnament or DiamondOrnament;

    public static ContentPackageManifest Create()
    {
        var version = ContentVersion.Parse("1.0.0");
        var digest = "sha256:" + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(
            "clankerworld-ornaments-v1:1.0.0:refine-gold-2ore-1wood-make-2gold-set-1diamond")));
        var smith = BlacksmithContent.Blacksmith1x2();
        RecipeDefinition[] recipes =
        [
            new(digest, "refine-gold", version, "Refine gold ore",
                [new("gold_ore", 2), new("wood", 1)], [new(Gold, 1)], 24,
                smith.CanonicalId, ["metalworking", "ornament"]),
            new(digest, "gold-ornament", version, "Make a gold ornament",
                [new(Gold, 2)], [new(GoldOrnament, 1)], 24,
                smith.CanonicalId, ["ornament"]),
            new(digest, "set-diamond", version, "Set a diamond in an ornament",
                [new(GoldOrnament, 1), new("diamond", 1)], [new(DiamondOrnament, 1)], 28,
                smith.CanonicalId, ["ornament"]),
        ];
        var externalDefinitions = ContentDefinitionPayloadCodec.ApplyPackage(
            new DeclarativeWorldContentState([], []), BlacksmithContent.Create());
        return StarterContent.BuildManifest(PackageId, version, digest, [], recipes,
            [new(BlacksmithContent.PackageId, new(version, ContentVersion.Parse("2.0.0")))], externalDefinitions);
    }
}

/// <summary>Household-made storage pots and reusable water jugs.</summary>
public static class PotteryContent
{
    public const string PackageId = "clankerworld-pottery-v1";

    public static ContentPackageManifest Create()
    {
        var digest = "sha256:" + Convert.ToHexStringLower(SHA256.HashData(
            Encoding.UTF8.GetBytes("clankerworld-pottery-v1:1.0.0:storage-pot-water-jug")));
        var version = ContentVersion.Parse("1.0.0");
        RecipeDefinition[] recipes =
        [
            new(digest, "storage-pot", version, "Make a storage pot",
                [new("clay", 2), new("wood", 1)], [new(InventoryContainerRules.StoragePot, 1)], 24,
                HouseContent.House1x1().CanonicalId, ["container", "pottery", "food-storage"]),
            new(digest, "water-jug", version, "Make a water jug",
                [new("clay", 2), new("wood", 1)], [new(InventoryContainerRules.WaterJug, 1)], 24,
                HouseContent.House1x1().CanonicalId, ["container", "pottery", "fresh-water"]),
        ];
        var houseDefinitions = ContentDefinitionPayloadCodec.ApplyPackage(
            new DeclarativeWorldContentState([], []), HouseContent.Create());
        return StarterContent.BuildManifest(PackageId, version, digest, [], recipes,
            [new ContentDependency(HouseContent.PackageId,
                new ContentVersionRange(version, ContentVersion.Parse("2.0.0")))], houseDefinitions);
    }
}
