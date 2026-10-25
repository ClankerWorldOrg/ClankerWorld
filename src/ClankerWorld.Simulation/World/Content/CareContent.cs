using System.Security.Cryptography;
using System.Text;
using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Kernel;

namespace ClankerWorld.Simulation.World;

/// <summary>Household care supplies; recipes, costs and work times are provisional.</summary>
public static class CareContent
{
    public const string PackageId = "clankerworld-care-v1";
    public const string MedicinalHerbs = "medicinal_herbs";
    public const string Bandage = "bandage";
    public const string Medicine = "medicine";

    private static string PackageDigest => "sha256:" + Convert.ToHexStringLower(SHA256.HashData(
        Encoding.UTF8.GetBytes("clankerworld-care-v1:1.0.0:clinic1x2-wood10-stone4-bandage2-medicine2-fresh-water")));

    public static BuildingDefinition Clinic1x2() => new(PackageDigest, "clinic-1x2", ContentVersion.Parse("1.0.0"),
        "Clinic", 1, 2, 1, [new("wood", 10), new("stone", 4)], ["clinic", "care", "storage"]);

    public static ContentPackageManifest Create()
    {
        var version = ContentVersion.Parse("1.0.0");
        var house = HouseContent.House1x1();
        var tailor = TailorContent.TailorShop1x1();
        var clinic = Clinic1x2();
        var externalDefinitions = ContentDefinitionPayloadCodec.ApplyPackage(
            ContentDefinitionPayloadCodec.ApplyPackage(new DeclarativeWorldContentState([], []), HouseContent.Create()),
            TailorContent.Create());
        RecipeDefinition[] recipes =
        [
            new(PackageDigest, "house-bandages", version, "Cut bandages", [new("cloth", 1)], [new(Bandage, 2)],
                8, house.CanonicalId, ["care", "bandage"]),
            new(PackageDigest, "tailor-bandages", version, "Cut bandages", [new("cloth", 1)], [new(Bandage, 2)],
                8, tailor.CanonicalId, ["care", "bandage"]),
            new(PackageDigest, "clinic-medicine", version, "Prepare herbal medicine",
                [new(MedicinalHerbs, 2), new(InventoryContainerRules.FreshWater, 1), new("wood", 1)],
                [new(Medicine, 2)], 16, clinic.CanonicalId, ["care", "medicine"]),
        ];
        return StarterContent.BuildManifest(PackageId, version, PackageDigest, [clinic], recipes,
            [new(HouseContent.PackageId, new(version, ContentVersion.Parse("2.0.0"))),
                new(TailorContent.PackageId, new(version, ContentVersion.Parse("2.0.0")))], externalDefinitions);
    }
}

/// <summary>A smaller Clinic with the unchanged physical herbal medicine recipe.</summary>
public static class ClinicVariantContent
{
    public const string PackageId = "clankerworld-clinic-1x1-v1";

    public static ContentPackageManifest Create() => WorkstationVariantContent.Create(
        PackageId, "clinic-1x1", "Clinic (1×1)", 1, 1,
        [new("wood", 5), new("stone", 2)], CareContent.Clinic1x2(),
        [HouseContent.Create(), TailorContent.Create(), CareContent.Create()],
        [WorkstationVariantContent.Require(CareContent.PackageId, "1.0.0")]);
}

/// <summary>
/// Household Tailor Shop: plant fiber becomes cloth, an item of its own, and
/// cloth becomes clothing. Costs, ratios and work times
/// are provisional playtest values; six fiber per garment matches the retired
/// Weaving frame.
/// </summary>
public static class TailorContent
{
    public const string PackageId = "clankerworld-tailor-v1";

    public static BuildingDefinition TailorShop1x1()
    {
        var digest = PackageDigest();
        var version = ContentVersion.Parse("1.0.0");
        return new BuildingDefinition(digest, "tailor-shop-1x1", version, "Tailor Shop", 1, 1, 1,
            [new("wood", 8), new("fiber", 2)], ["tailor", "clothing-making"]);
    }

    public static ContentPackageManifest Create()
    {
        var shop = TailorShop1x1();
        var version = shop.Version;
        var digest = shop.PackageDigest;
        RecipeDefinition[] recipes =
        [
            new(digest, "weave-cloth", version, "Weave cloth",
                [new("fiber", 3)], [new("cloth", 1)], 20, shop.CanonicalId, ["cloth"]),
            new(digest, "sew-clothing", version, "Sew clothing",
                [new("cloth", 2)], [new("clothing", 1)], 24, shop.CanonicalId, ["clothing"]),
            new(digest, "sew-padded-coat", version, "Sew padded coat",
                [new("cloth", 4)], [new("padded_coat", 1)], 32, shop.CanonicalId, ["clothing"]),
            new(digest, "sew-rain-cloak", version, "Sew rain cloak",
                [new("cloth", 2), new("fiber", 1)], [new("rain_cloak", 1)], 28, shop.CanonicalId, ["clothing"]),
            new(digest, "sew-sack", version, "Sew sack",
                [new("cloth", 2), new("rope", 1)], [new("sack", 1)], 24, shop.CanonicalId, ["carry-aid"]),
        ];
        return StarterContent.BuildManifest(PackageId, version, digest, [shop], recipes,
            [new ContentDependency(HouseContent.PackageId,
                new ContentVersionRange(version, ContentVersion.Parse("2.0.0")))]);
    }

    private static string PackageDigest() => "sha256:" + Convert.ToHexStringLower(SHA256.HashData(
        Encoding.UTF8.GetBytes("clankerworld-tailor-v1:1.0.0:tailor-shop-cloth-clothing-coat-cloak-sack")));
}

/// <summary>A larger Tailor Shop, including the ordinary cloth bandage recipe.</summary>
public static class TailorVariantContent
{
    public const string PackageId = "clankerworld-tailor-shop-2x2-v1";

    public static ContentPackageManifest Create() => WorkstationVariantContent.Create(
        PackageId, "tailor-shop-2x2", "Tailor Shop (2×2)", 2, 2,
        [new("wood", 32), new("fiber", 8)], TailorContent.TailorShop1x1(),
        [HouseContent.Create(), TailorContent.Create(), CareContent.Create()],
        [WorkstationVariantContent.Require(TailorContent.PackageId, "1.0.0"),
            WorkstationVariantContent.Require(CareContent.PackageId, "1.0.0")]);
}
