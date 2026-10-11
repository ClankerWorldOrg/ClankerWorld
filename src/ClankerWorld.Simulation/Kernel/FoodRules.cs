namespace ClankerWorld.Simulation.Kernel;

/// <summary>One food catalog, with ingredients kept distinct from edible servings.</summary>
public static class FoodRules
{
    public static IReadOnlyList<string> PreparedKinds { get; } = Array.AsReadOnly<string>([
        "simple_meal", "porridge", "berry_porridge", "fruit_porridge", "bread", "stew",
        "restaurant_meal", "cooked_eggs", "milk_porridge", "rich_meal",
    ]);
    public static IReadOnlyList<string> EdibleKinds { get; } = Array.AsReadOnly<string>([
        .. PreparedKinds, "food", "fruit", "berries", "wild_greens", "cultivated_greens", "milk",
    ]);
    public static IReadOnlyList<string> StorageKinds { get; } = Array.AsReadOnly<string>([
        .. EdibleKinds, "grain", "flour", "potato", "potatoes", "greens", "eggs",
    ]);

    public static bool IsEdible(string kind) => EdibleKinds.Contains(kind, StringComparer.Ordinal);
    public static bool IsFood(string kind) => StorageKinds.Contains(kind, StringComparer.Ordinal);
}
