using ClankerWorld.Server.Worlds;

namespace ClankerWorld.Tests;

internal static class WorldSelectionTestExtensions
{
    public static CatalogWorld SelectApprovedModels(this WorldSelectionCoordinator selection, string id) =>
        selection.Select(id, selection.PreviewModels(id).Approval);
}
