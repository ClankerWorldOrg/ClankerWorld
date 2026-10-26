using ClankerWorld.Simulation.World;
using ClankerWorld.Simulation.Content;

namespace ClankerWorld.Contracts;

public sealed partial record OwnerContentPackageReceipt
{
    public static OwnerContentPackageReceipt From(
        string operation,
        ContentPackageRecord record) => new(
            operation,
            true,
            record.Manifest.PackageId,
            record.Manifest.Version.ToString(),
            record.Manifest.PackageDigest,
            record.Lifecycle.ToString().ToLowerInvariant(),
            record.LockDigest,
            record.ValidationTick,
            record.StagedTick,
            record.ActivationTick,
            null,
            record.ManifestDigest);
}

public sealed partial record OwnerControlReceipt
{
    public static OwnerControlReceipt From(string operation, bool changed, OwnerWorldSnapshot snapshot)
    {
        var authoring = snapshot.Authoring;
        return new OwnerControlReceipt(
            operation,
            changed,
            authoring?.IsPaused ?? snapshot.Cognition?.IsPaused ?? false,
            authoring?.RunEpoch ?? 0,
            authoring?.Revision ?? snapshot.WorldTick,
            snapshot.LatestEventId);
    }

    // Control acknowledgements must not depend on the optional world-view
    // projection. A projection defect must never turn an accepted pause into
    // an HTTP 500 that leaves the client unsure whether the world stopped.
    public static OwnerControlReceipt From(string operation, bool changed, WorldRuntimeState state)
    {
        var revision = state.EventHistoryFloor + state.Events.Count;
        return new OwnerControlReceipt(operation, changed, state.Society.Society.IsPaused,
            state.Society.Society.RunEpoch, revision, revision);
    }
}

public sealed partial record CatalogWorld
{
    internal CatalogWorld StampedByThisBuild() =>
        this with { GameVersion = BuildInformation.Version, SourceRevision = BuildInformation.SourceRevision };
}

/// <summary>
/// A small picture of a world's land and water for the Load World list: the
/// commonest terrain in each block of tiles, at most <see cref="MaximumWidth"/>
/// pixels wide and packed like the world's own terrain. The catalog keeps it,
/// so the list can show it without reading the world's save again.
/// </summary>
public sealed partial record WorldThumbnail
{
    public const int MaximumWidth = 96;

    public static WorldThumbnail From(WorldMap map)
    {
        ArgumentNullException.ThrowIfNull(map);
        var width = Math.Min(MaximumWidth, map.Width);
        var height = Math.Clamp((int)Math.Round(width * (double)map.Height / map.Width), 1, map.Height);
        var terrain = new byte[checked(map.Width * map.Height)];
        foreach (var tile in map.Tiles)
            terrain[tile.Position.Y * map.Width + tile.Position.X] = TerrainCodes.Encode(tile.Terrain.ToString());
        var pixels = new byte[checked(width * height)];
        var counts = new int[256];
        for (var py = 0; py < height; py++)
        {
            var top = py * map.Height / height;
            var bottom = Math.Max(top + 1, (py + 1) * map.Height / height);
            for (var px = 0; px < width; px++)
            {
                var left = px * map.Width / width;
                var right = Math.Max(left + 1, (px + 1) * map.Width / width);
                Array.Clear(counts);
                for (var y = top; y < bottom; y++)
                    for (var x = left; x < right; x++)
                        counts[terrain[y * map.Width + x]]++;
                var commonest = 0;
                for (var kind = 1; kind < counts.Length; kind++)
                    if (counts[kind] > counts[commonest]) commonest = kind;
                pixels[py * width + px] = (byte)commonest;
            }
        }
        return new WorldThumbnail(width, height, TerrainCodes.TerrainEncoding, Convert.ToBase64String(pixels));
    }
}
