namespace ClankerWorld.Simulation.World;

/// <summary>A paused-world command retained in its Developer edit event for exact retries and replay.</summary>
public sealed record WorldMapEdit(string WorldId, long ExpectedEventId, long RunEpoch, string BatchId,
    IReadOnlyList<WorldMapEditOperation> Operations);

public sealed record WorldMapEditOperation(string Kind, string? Id, string? Value, string? SecondaryValue,
    int? X, int? Y, bool? IsRenewable);

/// <summary>The same definitions supply the owner's choices and validate their execution.</summary>
public sealed record WorldMapEditChoice(string Operation, string Value, string DisplayName,
    TerrainKind? Terrain = null, string? ResourceKind = null, string? NaturalObjectKind = null,
    bool? IsRenewable = null, string? ObjectKind = null);

public static class WorldMapEditRules
{
    public const int MaximumOperations = 64;

    // Native starting-site references must never be claimed by an added object.
    public static bool IsReservedObjectId(string id) => id is "cooking" or "founder-scout" or "shelter" or
        "storage" or "bedroll" or "second-shelter" or "workshop" or "path";

    public static IReadOnlyList<WorldMapEditChoice> Choices { get; } = Array.AsReadOnly(new WorldMapEditChoice[]
    {
        new("set_terrain", "meadow", "Meadow", TerrainKind.Meadow),
        new("set_terrain", "mountain", "Mountain", TerrainKind.Mountain),
        new("set_terrain", "peak", "Peak", TerrainKind.Peak),
        new("set_terrain", "river", "River", TerrainKind.River),
        new("set_terrain", "lake", "Lake", TerrainKind.Lake),
        new("set_terrain", "ocean", "Ocean", TerrainKind.Ocean),
        new("set_terrain", "sand", "Sand", TerrainKind.Sand),
        new("set_terrain", "forest", "Forest", TerrainKind.Forest),
        new("set_terrain", "snow", "Snow", TerrainKind.Snow),
        new("place_resource", "food", "Berry bush", ResourceKind: "food", NaturalObjectKind: "berry_bush", IsRenewable: true),
        new("place_resource", "construction", "Timber", ResourceKind: "construction", IsRenewable: true),
        new("place_resource", "wood", "Fallen wood", ResourceKind: "wood", NaturalObjectKind: "fallen_wood", IsRenewable: false),
        new("place_resource", "stone", "Stone outcrop", ResourceKind: "stone", NaturalObjectKind: "stone_outcrop", IsRenewable: false),
        new("place_resource", "fiber", "Fiber plant", ResourceKind: "fiber", NaturalObjectKind: "fiber_plant", IsRenewable: true),
        new("place_resource", "clay", "Clay bank", ResourceKind: "clay", NaturalObjectKind: "clay_bank", IsRenewable: false),
        new("place_object", "shelter", "Camp shelter", ObjectKind: "shelter"),
        new("place_object", "storage", "Camp storage", ObjectKind: "storage"),
        new("place_object", "cooking", "Camp cooking fire", ObjectKind: "cooking"),
    });
}
