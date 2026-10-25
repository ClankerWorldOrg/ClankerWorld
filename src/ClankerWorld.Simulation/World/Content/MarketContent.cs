using System.Security.Cryptography;
using System.Text;
using ClankerWorld.Simulation.Content;

namespace ClankerWorld.Simulation.World;

/// <summary>A bounded paid Market; starter count, material costs and work are provisional.</summary>
public static class MarketContent
{
    public const string PackageId = "clankerworld-market-v1";
    public const string HallTag = "market";
    public const string StallTag = "market_stall";
    public const int PlazaWidth = 7;
    public const int PlazaHeight = 4;
    public const int MaximumStalls = 8;
    public static IReadOnlyList<int> StarterSlotIndexes { get; } = Array.AsReadOnly(new[] { 0, 4 });

    private static readonly string Digest = "sha256:" + Convert.ToHexStringLower(SHA256.HashData(
        Encoding.UTF8.GetBytes("clankerworld-market-v1:1.0.0:hall2x2-plaza7x4-starters0,4-wood24-stone8-fiber4:stall1x1-wood4-fiber2")));

    private static readonly BuildingDefinition HallDefinition = new(Digest, "market-2x2", ContentVersion.Parse("1.0.0"),
        "Market", 2, 2, 0, [new("wood", 24), new("stone", 8), new("fiber", 4)],
        [HallTag, "civic", "communal"]);

    private static readonly BuildingDefinition StallDefinition = new(Digest, "market-stall-1x1", ContentVersion.Parse("1.0.0"),
        "Market stall", 1, 1, 0, [new("wood", 4), new("fiber", 2)], [StallTag, "communal"]);

    public static BuildingDefinition Hall2x2() => HallDefinition;

    public static BuildingDefinition Stall1x1() => StallDefinition;

    public static GridPoint HallEntrance(GridPoint hallSite) => new(hallSite.X + 1, hallSite.Y + 2);
    public static GridPoint PlazaOrigin(GridPoint hallSite) => new(hallSite.X - 2, hallSite.Y + 2);

    public static GridPoint StallSite(GridPoint hallSite, int slot)
    {
        if (slot is < 0 or >= MaximumStalls) throw new ArgumentOutOfRangeException(nameof(slot));
        var plaza = PlazaOrigin(hallSite);
        var column = new[] { 1, 2, 4, 5 }[slot % 4];
        return new(plaza.X + column, plaza.Y + (slot < 4 ? 1 : 2));
    }

    public static GridPoint StallEntrance(GridPoint hallSite, int slot)
    {
        var site = StallSite(hallSite, slot);
        return new(site.X, site.Y + (slot < 4 ? -1 : 1));
    }

    public static IEnumerable<GridPoint> PlazaTiles(GridPoint hallSite)
    {
        var plaza = PlazaOrigin(hallSite);
        return Enumerable.Range(0, PlazaHeight).SelectMany(y => Enumerable.Range(0, PlazaWidth)
            .Select(x => new GridPoint(plaza.X + x, plaza.Y + y)));
    }

    public static IEnumerable<GridPoint> SiteTiles(GridPoint hallSite) =>
        WorldContentSimulationRules.Footprint(Hall2x2(), hallSite).Concat(PlazaTiles(hallSite));

    public static string MarketId(string projectId) => DerivedId.Create(projectId + ":market");
    public static string StarterStallBuildingId(string projectId, int slot) =>
        TownProjectRules.BuildingId(projectId + ":starter-stall:" + slot.ToString(System.Globalization.CultureInfo.InvariantCulture));

    public static ContentPackageManifest Create() => StarterContent.BuildManifest(
        PackageId, HallDefinition.Version, HallDefinition.PackageDigest, [HallDefinition, StallDefinition], [],
        [new(TownHallContent.PackageId, new(HallDefinition.Version, ContentVersion.Parse("2.0.0")))]);
}

/// <summary>Private Restaurant stock and cooking; physical sales belong to business trade.</summary>
public static class RestaurantContent
{
    public const string PackageId = "clankerworld-restaurant-v1";

    public static BuildingDefinition Restaurant1x2()
    {
        var digest = "sha256:" + Convert.ToHexStringLower(SHA256.HashData(
            Encoding.UTF8.GetBytes("clankerworld-restaurant-v1:1.0.1:restaurant-1x2-porridge-bread-stew-meals")));
        return new BuildingDefinition(digest, "restaurant-1x2", ContentVersion.Parse("1.0.1"),
            "Restaurant", 1, 2, 1, [new("wood", 8), new("stone", 2)], ["restaurant", "food-stock"]);
    }

    public static ContentPackageManifest Create()
    {
        var restaurant = Restaurant1x2();
        var recipes = HouseCookingContent.Recipes(restaurant.PackageDigest, restaurant.Version,
            restaurant.CanonicalId, false).Append(new RecipeDefinition(restaurant.PackageDigest,
                "restaurant-meal", restaurant.Version, "Prepare a Restaurant meal",
                [new("bread", 1), new("cultivated_greens", 1), new("wood", 1)],
                [new("restaurant_meal", 2)], 12, restaurant.CanonicalId, ["food", "named-meal"])).ToArray();
        return StarterContent.BuildManifest(PackageId, restaurant.Version, restaurant.PackageDigest,
            [restaurant], recipes,
            [new ContentDependency(HouseContent.PackageId,
                new ContentVersionRange(ContentVersion.Parse("1.0.0"), ContentVersion.Parse("2.0.0")))]);
    }
}

/// <summary>A larger Restaurant with all six current named-meal recipes.</summary>
public static class RestaurantVariantContent
{
    public const string PackageId = "clankerworld-restaurant-2x2-v1";

    public static ContentPackageManifest Create() => WorkstationVariantContent.Create(
        PackageId, "restaurant-2x2", "Restaurant (2×2)", 2, 2,
        [new("wood", 16), new("stone", 4)], RestaurantContent.Restaurant1x2(),
        [RestaurantContent.Create()],
        [WorkstationVariantContent.Require(RestaurantContent.PackageId, "1.0.1")]);
}

/// <summary>Optional household shops. Construction and stock quantities are provisional.</summary>
public static class StoreContent
{
    public const string PackageId = "clankerworld-business-v1";

    public static ContentPackageManifest Create()
    {
        var digest = "sha256:" + Convert.ToHexStringLower(SHA256.HashData(
            Encoding.UTF8.GetBytes("clankerworld-business-v1:1.0.0:store-1x1-1x2")));
        var version = ContentVersion.Parse("1.0.0");
        BuildingDefinition[] buildings =
        [
            new(digest, "store-1x1", version, "Store", 1, 1, 1,
                [new("wood", 8), new("stone", 2)], ["store", "business", "storage"]),
            new(digest, "store-1x2", version, "Store", 1, 2, 1,
                [new("wood", 16), new("stone", 4)], ["store", "business", "storage"]),
        ];
        return StarterContent.BuildManifest(PackageId, version, digest, buildings, [],
            [new ContentDependency(HouseContent.PackageId,
                new ContentVersionRange(version, ContentVersion.Parse("2.0.0")))]);
    }
}
