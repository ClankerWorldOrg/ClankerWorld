using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private IEnumerable<GridPoint> TownFoundingBuildingTiles(PlacedBuilding building) => TownSystem.TownFoundingBuildingTiles(civicWorld, building);
    private TownRuntimeState? TownFoundingLayout(string actor) => townSystem.TownFoundingLayout(civicWorld, actor);
    private void FoundTown(string actor, string candidate) => townSystem.FoundTown(civicWorld, actor, candidate);
}
