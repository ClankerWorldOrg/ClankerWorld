using System.Security.Cryptography;
using System.Text;
using ClankerWorld.Simulation.Content;

namespace ClankerWorld.Simulation.World;

/// <summary>The first Council-approved shared building; costs and work are provisional.</summary>
public static class TownHallContent
{
    public const string PackageId = "clankerworld-town-hall-v1";
    public const string HallTag = "town_hall";

    private static readonly BuildingDefinition HallDefinition = new(
        "sha256:" + Convert.ToHexStringLower(SHA256.HashData(
            Encoding.UTF8.GetBytes("clankerworld-town-hall-v1:1.0.0:3x4-south1-wood24-stone12"))),
        "town-hall-3x4", ContentVersion.Parse("1.0.0"), "Town Hall",
        3, 4, 0, [new("wood", 24), new("stone", 12)], [HallTag, "civic", "communal"]);

    public static BuildingDefinition Hall3x4() => HallDefinition;

    public static GridPoint Entrance(GridPoint site) => new(site.X + 1, site.Y + 4);

    public static ContentPackageManifest Create() => StarterContent.BuildManifest(
        PackageId, HallDefinition.Version, HallDefinition.PackageDigest, [HallDefinition], [],
        [new(HouseContent.PackageId, new(HallDefinition.Version, ContentVersion.Parse("2.0.0")))]);
}

/// <summary>Town Warehouse; the retired Storehouse definition is not offered for new projects.</summary>
public static class WarehouseContent
{
    public const string PackageId = "clankerworld-warehouse-v1";

    public static BuildingDefinition Warehouse2x2()
    {
        var digest = "sha256:" + Convert.ToHexStringLower(SHA256.HashData(
            Encoding.UTF8.GetBytes("clankerworld-warehouse-v1:1.0.0:warehouse-2x2")));
        var version = ContentVersion.Parse("1.0.0");
        return new BuildingDefinition(digest, "warehouse-2x2", version, "Warehouse", 2, 2, 1,
            [new("wood", 12)], ["warehouse"]);
    }

    public static ContentPackageManifest Create()
    {
        var warehouse = Warehouse2x2();
        return StarterContent.BuildManifest(PackageId, warehouse.Version, warehouse.PackageDigest,
            [warehouse], [], [new ContentDependency(HouseContent.PackageId,
                new ContentVersionRange(warehouse.Version, ContentVersion.Parse("2.0.0")))]);
    }
}

/// <summary>Paid communal posts; their Road edge is bound by the project Site and Entrance.</summary>
public static class StreetLanternContent
{
    public const string PackageId = "clankerworld-street-lanterns-v1";
    public const string LanternTag = "street_lantern";
    public const string StoneTag = "stone_lantern";
    public const string HangingTag = "hanging_lantern";

    private static readonly ContentVersion Version = ContentVersion.Parse("1.0.0");
    private static readonly string Digest = "sha256:" + Convert.ToHexStringLower(SHA256.HashData(
        Encoding.UTF8.GetBytes("clankerworld-street-lanterns-v1:1.0.0:1x1-stone-street-lamp-stone4-hanging-street-lantern-wood4-iron1")));
    private static readonly BuildingDefinition StoneDefinition = new(Digest, "stone-lantern", Version,
        "Stone street lamp", 1, 1, 0, [new("stone", 4)], [LanternTag, StoneTag, "civic", "communal"]);
    private static readonly BuildingDefinition HangingDefinition = new(Digest, "hanging-lantern", Version,
        "Hanging street lantern", 1, 1, 0, [new("wood", 4), new("iron", 1)], [LanternTag, HangingTag, "civic", "communal"]);

    public static BuildingDefinition Stone() => StoneDefinition;

    public static BuildingDefinition Hanging() => HangingDefinition;

    public static BuildingDefinition? Definition(string? definitionId) =>
        definitionId == StoneDefinition.CanonicalId ? Stone() :
        definitionId == HangingDefinition.CanonicalId ? Hanging() : null;

    public static bool IsLantern(string? definitionId) =>
        definitionId == StoneDefinition.CanonicalId || definitionId == HangingDefinition.CanonicalId;

    public static bool IsRoadEdge(GridPoint site, GridPoint road) =>
        Math.Abs((long)site.X - road.X) + Math.Abs((long)site.Y - road.Y) == 1;

    public static ContentPackageManifest Create() => StarterContent.BuildManifest(PackageId, Version, Digest,
        [Stone(), Hanging()], [],
        [new(HouseContent.PackageId, new(Version, ContentVersion.Parse("2.0.0")))]);
}
