using ClankerWorld.GodotClient.UI;
using Godot;

namespace ClankerWorld.GodotClient;

public partial class Main
{
    /// <summary>
    /// People inside a building are hidden from the map and the building shows
    /// how many (agreed October 1); people outside it, or in an open yard,
    /// stay on the map, and the badge goes when everyone has left.
    /// </summary>
    private void VerifyPeopleInside()
    {
        if (renderedMapSnapshot is not { } map) return;
        var (zoomBefore, centerBefore) = (cameraZoom, cameraCenterTiles);
        var (buildingBefore, detailsBefore) = (selectedBuildingId, buildingDetailsRequested);
        var cardBefore = buildingCardSnapshot;
        cameraZoom = maximumCameraZoom;
        cameraCenterTiles = new Vector2(12, 11);
        OwnerWorldAgent Person(string id, string name, int x, int y) => new(id, name, "active", new(x, y), 8_000, [], [],
            new OwnerWorldRoute("idle", null, null, [], string.Empty), new OwnerWorldSpatialKnowledge(new(x, y), [new(x, y)], [new(x, y)]), false);
        var house = new OwnerWorldPlacedBuilding("inside-house", "test/house", new(10, 10), 0, "House", ["house"], 1, 1, Entrance: new(10, 11));
        var yard = new OwnerWorldPlacedBuilding("inside-yard", "test/yard", new(13, 10), 0, "Animal yard", ["animal-yard"], 2, 2, Entrance: new(13, 12));
        var busy = map with
        {
            PlacedBuildings = [house, yard],
            Animals = [],
            Agents = [Person("inside:ada", "Ada", 10, 10), Person("inside:ben", "Ben", 10, 10),
                Person("inside:cy", "Cy", 10, 11), Person("inside:dee", "Dee", 13, 10)],
        };
        try
        {
            RenderMap(busy);
            if (agentVisuals["inside:ada"].Visible || agentVisuals["inside:ben"].Visible ||
                !agentVisuals["inside:cy"].Visible || !agentVisuals["inside:dee"].Visible)
                throw new InvalidOperationException("People inside a House must be hidden, and people outside it or in an open yard shown.");
            if (!occupancyBadges.TryGetValue("inside-house", out var badge) || badge.Count != 2 || occupancyBadges.ContainsKey("inside-yard") ||
                badge.TooltipText != "2 people inside: Ada, Ben" || !badge.Visible)
                throw new InvalidOperationException("A House with people inside must show how many, and who on hover; an open yard shows none.");
            var corner = new Vector2((house.Position.X + 1) * (currentTileSize + TileGap), house.Position.Y * (currentTileSize + TileGap));
            if (badge.Position.X + badge.Size.X > corner.X || badge.Position.Y < corner.Y || badge.Position.X < corner.X - currentTileSize)
                throw new InvalidOperationException("The badge must sit in the building's top-right corner.");

            RenderMap(busy with { Agents = [Person("inside:ada", "Ada", 10, 11), Person("inside:ben", "Ben", 11, 11)] });
            if (occupancyBadges.ContainsKey("inside-house") || !agentVisuals["inside:ada"].Visible || !agentVisuals["inside:ben"].Visible)
                throw new InvalidOperationException("People who leave must show again, and the empty House lose its badge.");

            var smith = house with
            {
                InstanceId = "inside-smith",
                DefinitionId = "test/blacksmith",
                DisplayName = "Blacksmith",
                Tags = ["blacksmith"],
                Width = 2,
                Height = 2,
                Entrance = new(10, 12)
            };
            var split = busy with
            {
                PlacedBuildings = [smith],
                Agents = [Person("inside:eli", "Eli", 10, 10), Person("inside:fern", "Fern", 11, 10)]
            };
            RenderMap(split);
            selectedBuildingId = smith.InstanceId;
            buildingDetailsRequested = true;
            RenderBuildingCard(split);
            if (agentVisuals["inside:eli"].Visible || !agentVisuals["inside:fern"].Visible ||
                !occupancyBadges.TryGetValue(smith.InstanceId, out var smithBadge) || smithBadge.Count != 1 ||
                smithBadge.TooltipText != "1 person inside: Eli" || buildingPeopleSummary.Text != "1 inside" ||
                buildingPeopleText.Text != "Inside: Eli")
                throw new InvalidOperationException("The map badge and building card must name only the person under the roof, not the person in its open yard.");
        }
        finally
        {
            (cameraZoom, cameraCenterTiles) = (zoomBefore, centerBefore);
            (selectedBuildingId, buildingDetailsRequested) = (buildingBefore, detailsBefore);
            RenderMap(map);
            buildingCardSnapshot = cardBefore;
            if (buildingBefore is not null && cardBefore is not null)
                RenderBuildingCard(cardBefore);
            else
                ClearBuildingSelection();
        }
    }
}
