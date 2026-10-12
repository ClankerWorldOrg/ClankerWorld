namespace ClankerWorld.Simulation.World;

/// <summary>
/// A coordinate in the first world's bounded, logical ground grid.
/// </summary>
public readonly record struct GridPoint(int X, int Y);

public enum TerrainKind
{
    Meadow,
    Water,
    Mountain,
    River,
    Lake,
    Ocean,
    Peak,
    Sand,
    Forest,
    Snow,
}

// These are independent facts; TerrainKind remains the stable v1 manifest/save
// compatibility projection and the fallback for legacy maps and fixtures.
// Values 0-4 were shipped in map-layers-v1. Keep them stable for private saves.
public enum SurfaceKind : byte
{
    Grass,
    Sand,
    Rock,
    Snow,
    Water,
    ForestFloor,
    DryScrub,
    FertileSoil,
}

// Values 0-4 were shipped in map-layers-v1. Keep them stable for private saves.
public enum VegetationCover : byte { None, Grass, Forest, Scrub, Tundra, Cactus }

public enum ResourceState
{
    Available,
    Depleted,
}

/// <summary>The direction a bridge deck carries travelers across its river.</summary>
public enum BridgeAxis : byte
{
    NorthSouth,
    EastWest,
}

public sealed record TerrainTile(GridPoint Position, TerrainKind Terrain);

public sealed record MapObject(string Id, string Kind, GridPoint Position);

public sealed record MapResource(
    string Id,
    string Kind,
    GridPoint Position,
    bool IsRenewable,
    string? TreeKind = null,
    string? NaturalObjectKind = null);
