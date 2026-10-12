namespace ClankerWorld.Simulation.World;

internal sealed partial class EnvironmentSystem(WorldSystemsState state)
{
    internal WorldSystemsState State { get; private set; } = state;

    internal void ReplaceEcologyResource(string id, EcologyResource replacement) => State = State with
    {
        Ecology = State.Ecology with
        {
            Resources = State.Ecology.Resources.Select(resource => resource.Id == id ? replacement : resource).ToArray(),
        },
    };

    internal void PlantTree(MapResource tree, long day, ChunkManifest chunk)
    {
        var origin = chunk.Coordinate.Origin(chunk.ChunkSize);
        State = State with
        {
            Ecology = State.Ecology with
            {
                Resources = State.Ecology.Resources.Append(TreeGrowthRules.PlantedSapling(tree, day))
                    .OrderBy(resource => resource.Id, StringComparer.Ordinal).ToArray(),
            },
            Chunks = State.Chunks.Select(item => item.Coordinate != chunk.Coordinate
                ? item
                : ChunkManifestCodec.WithDigest(item with
                {
                    Resources = item.Resources.Append(new ChunkResourceMetadata(tree.Id, tree.Kind,
                            new GridPoint(tree.Position.X - origin.X, tree.Position.Y - origin.Y), true))
                        .OrderBy(resource => resource.ResourceId, StringComparer.Ordinal).ToArray(),
                })).ToArray(),
        };
    }

    internal void AddStartingResources(WorldMap map, IReadOnlyList<MapResource> additions,
        IReadOnlySet<ChunkCoordinate> changedChunks, long worldTick) => State = State with
        {
            Ecology = State.Ecology with
            {
                Resources = State.Ecology.Resources.Concat(additions.Select(resource => new EcologyResource(
                    resource.Id, resource.Kind, resource.Position, resource.IsRenewable, 16, 16,
                    resource.IsRenewable ? 4 : 0, resource.IsRenewable ? 1 : 0, SeasonKind.Spring,
                    WorldCalendarRules.FromTick(worldTick, State.Config).DayIndex + 1, EcologyResourceState.Available)))
                .OrderBy(resource => resource.Id, StringComparer.Ordinal).ToArray(),
            },
            Chunks = State.Chunks.Select(chunk =>
            {
                if (!changedChunks.Contains(chunk.Coordinate)) return chunk;
                var origin = chunk.Coordinate.Origin(chunk.ChunkSize);
                return ChunkManifestCodec.WithDigest(chunk with
                {
                    Resources = map.Resources.Where(resource =>
                            resource.Position.X >= origin.X && resource.Position.X < origin.X + chunk.Width &&
                            resource.Position.Y >= origin.Y && resource.Position.Y < origin.Y + chunk.Height)
                        .Select(resource => new ChunkResourceMetadata(
                            resource.Id, resource.Kind,
                            new GridPoint(resource.Position.X - origin.X, resource.Position.Y - origin.Y),
                            resource.IsRenewable)).ToArray(),
                });
            }).ToArray(),
        };
}
