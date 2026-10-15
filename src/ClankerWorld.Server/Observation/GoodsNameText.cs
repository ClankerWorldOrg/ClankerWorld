using ClankerWorld.Simulation.Content;

namespace ClankerWorld.Server.Observation;

internal static class GoodsNameText
{
    public static string ForQuantity(string kind, int quantity, ContentRegistryState? content)
    {
        var named = content?.Packages.SelectMany(package => package.Manifest.Definitions.Select(definition =>
            (Id: definition.CanonicalId(package.Manifest.PackageDigest), definition.DisplayName)))
            .FirstOrDefault(definition => definition.Id == kind).DisplayName;
        if (named is not null) return named;
        return kind switch
        {
            "tree_seed" => quantity == 1 ? "tree seed" : "tree seeds",
            "stew" => "vegetable stew",
            "gold" => "refined gold",
            _ => kind.Contains('/') || kind.Contains(':') ? "goods" : kind.Replace('_', ' '),
        };
    }
}
