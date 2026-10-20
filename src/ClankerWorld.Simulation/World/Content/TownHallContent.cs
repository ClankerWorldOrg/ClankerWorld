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
