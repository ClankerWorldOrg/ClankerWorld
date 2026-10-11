using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using ClankerWorld.Simulation.Harness;

namespace ClankerWorld.Simulation.Playtest;

public sealed partial class PrivateWorldRuntime
{
    private IEnumerable<GridPoint> TownFoundingBuildingTiles(PlacedBuilding building) => CivicSystem.TownFoundingBuildingTiles(civicWorld, building);
    private TownRuntimeState? TownFoundingLayout(string actor) => civicSystem.TownFoundingLayout(civicWorld, actor);
    private void FoundTown(string actor, string candidate) => civicSystem.FoundTown(civicWorld, actor, candidate);
}
