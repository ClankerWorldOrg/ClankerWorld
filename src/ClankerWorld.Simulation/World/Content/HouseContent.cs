using System.Security.Cryptography;
using System.Text;
using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Kernel;

namespace ClankerWorld.Simulation.World;

/// <summary>The household-owned House and its on-site rope, basket and pottery crafts.</summary>
public static class HouseContent
{
    public const string PackageId = "clankerworld-house-v1";

    public static BuildingDefinition House1x1()
    {
        var digest = "sha256:" + Convert.ToHexStringLower(SHA256.HashData(
            Encoding.UTF8.GetBytes("clankerworld-house-v1:1.0.0:house-1x1-rope-basket")));
        var version = ContentVersion.Parse("1.0.0");
        return new BuildingDefinition(digest, "house-1x1", version, "House", 1, 1, 1,
            [new("wood", 8)], ["house", "shelter", "cooking", "storage"]);
    }

    public static ContentPackageManifest Create()
    {
        var house = House1x1();
        var version = house.Version;
        var digest = house.PackageDigest;
        RecipeDefinition[] recipes =
        [
            new(digest, "twist-rope", version, "Twist rope", [new("fiber", 3)], [new("rope", 1)],
                12, house.CanonicalId, ["rope"]),
            new(digest, "weave-basket", version, "Weave basket", [new("fiber", 3), new("rope", 1)],
                [new("basket", 1)], 20, house.CanonicalId, ["carry-aid"]),
        ];
        return StarterContent.BuildManifest(PackageId, version, digest, [house], recipes,
            [new ContentDependency(HearthContent.PackageId,
                new ContentVersionRange(version, ContentVersion.Parse("2.0.0")))]);
    }
}

/// <summary>Named household meals made from physical ingredients at a House.</summary>
public static class HouseCookingContent
{
    public const string PackageId = "clankerworld-house-cooking-v1";

    public static ContentPackageManifest Create()
    {
        var digest = "sha256:" + Convert.ToHexStringLower(SHA256.HashData(
            Encoding.UTF8.GetBytes("clankerworld-house-cooking-v1:1.0.1:potatoes-greens-porridge-bread-stew")));
        var version = ContentVersion.Parse("1.0.1");
        var houseDefinitions = ContentDefinitionPayloadCodec.ApplyPackage(
            new DeclarativeWorldContentState([], []), HouseContent.Create());
        return StarterContent.BuildManifest(PackageId, version, digest, [],
            Recipes(digest, version, HouseContent.House1x1().CanonicalId, true),
            [new ContentDependency(HouseContent.PackageId,
                new ContentVersionRange(ContentVersion.Parse("1.0.0"), ContentVersion.Parse("2.0.0")))], houseDefinitions);
    }

    internal static IReadOnlyList<RecipeDefinition> Recipes(string digest, ContentVersion version,
        string workstationId, bool simpleMeals)
    {
        var recipes = new List<RecipeDefinition>();
        void Add(string id, string name, IReadOnlyList<ContentQuantity> inputs, string output) =>
            recipes.Add(new RecipeDefinition(digest, id, version, name, inputs, [new(output, 2)],
                12, workstationId, ["food", "named-meal"]));
        if (simpleMeals)
        {
            Add("house-meal", "Cook a potato meal", [new("potatoes", 2), new("wood", 1)], "simple_meal");
            Add("wild-green-meal", "Cook a wild greens meal", [new("wild_greens", 2), new("wood", 1)], "simple_meal");
            Add("cultivated-green-meal", "Cook a cultivated greens meal", [new("cultivated_greens", 2), new("wood", 1)], "simple_meal");
        }
        Add("porridge", "Cook porridge", [new("grain", 1), new(InventoryContainerRules.FreshWater, 1), new("wood", 1)], "porridge");
        Add("berry-porridge", "Cook berry porridge", [new("grain", 1), new("berries", 1), new(InventoryContainerRules.FreshWater, 1), new("wood", 1)], "berry_porridge");
        Add("fruit-porridge", "Cook fruit porridge", [new("grain", 1), new("fruit", 1), new(InventoryContainerRules.FreshWater, 1), new("wood", 1)], "fruit_porridge");
        Add("bread", "Bake bread", [new("flour", 2), new(InventoryContainerRules.FreshWater, 1), new("wood", 1)], "bread");
        Add("vegetable-stew", "Cook vegetable stew", [new("potatoes", 1), new("cultivated_greens", 1), new(InventoryContainerRules.FreshWater, 1), new("wood", 1)], "stew");
        return recipes;
    }
}

/// <summary>Crude household tools; material costs and work times are provisional.</summary>
public static class HouseToolsContent
{
    public const string PackageId = "clankerworld-house-tools-v1";
    public const string CrudeWoodenAxe = "crude_wooden_axe";
    public const string CrudeWoodenPickaxe = "crude_wooden_pickaxe";

    private static readonly ContentVersion Version = ContentVersion.Parse("1.0.0");
    private static readonly string HouseId = HouseContent.House1x1().CanonicalId;
    private static readonly string Digest = "sha256:" + Convert.ToHexStringLower(SHA256.HashData(
        Encoding.UTF8.GetBytes(PackageId + ":1.0.0:house=" + HouseId +
            ":crude-wooden-axe:Make crude wooden axe:wood3:crude_wooden_axe1:24:house-tool,tool,woodcutting" +
            ":crude-wooden-pickaxe:Make crude wooden pickaxe:wood3:crude_wooden_pickaxe1:24:house-tool,tool,mining")));
    private static readonly RecipeDefinition[] Recipes =
    [
        new(Digest, "crude-wooden-axe", Version, "Make crude wooden axe",
            [new("wood", 3)], [new(CrudeWoodenAxe, 1)], 24, HouseId, ["house-tool", "tool", "woodcutting"]),
        new(Digest, "crude-wooden-pickaxe", Version, "Make crude wooden pickaxe",
            [new("wood", 3)], [new(CrudeWoodenPickaxe, 1)], 24, HouseId, ["house-tool", "tool", "mining"]),
    ];

    public static ContentPackageManifest Create()
    {
        var houseDefinitions = ContentDefinitionPayloadCodec.ApplyPackage(
            new DeclarativeWorldContentState([], []), HouseContent.Create());
        return StarterContent.BuildManifest(PackageId, Version, Digest, [], Recipes,
            [new(HouseContent.PackageId, new(Version, ContentVersion.Parse("2.0.0")))], houseDefinitions);
    }

    public static bool IsCrudeToolRecipe(RecipeDefinition recipe)
    {
        ArgumentNullException.ThrowIfNull(recipe);
        return Recipes.Any(builtIn => recipe.CanonicalId == builtIn.CanonicalId &&
            recipe.PayloadDigest == builtIn.PayloadDigest);
    }
}

/// <summary>Additive built-in content; never rewrites an existing starter package identity.</summary>
public static class HearthContent
{
    public const string PackageId = "clankerworld-settlement-v1";

    public static ContentPackageManifest Create()
    {
        var digest = "sha256:" + Convert.ToHexStringLower(SHA256.HashData(
            Encoding.UTF8.GetBytes("clankerworld-settlement-v1:1.0.0:stone-hearth:grain:stone-fiber-seed")));
        var version = ContentVersion.Parse("1.0.0");
        var hearth = new BuildingDefinition(digest, "stone-hearth", version, "Stone hearth", 1, 1, 2,
            [new("stone", 8), new("wood", 4)], ["cooking", "warmth"]);
        // The Weaving frame and its woven clothing are gone; the Tailor Shop
        // makes clothing now (TailorContent).
        RecipeDefinition[] recipes =
        [
            new(digest, "hearty-meal", version, "Hearty meal", [new("food", 2), new("wood", 1)], [new("food", 5)], 12, hearth.CanonicalId, ["food"]),
        ];
        return StarterContent.BuildManifest(PackageId, version, digest, [hearth], recipes,
            [new ContentDependency(StarterContent.PackageId, new ContentVersionRange(version, ContentVersion.Parse("2.0.0")))]);
    }
}
