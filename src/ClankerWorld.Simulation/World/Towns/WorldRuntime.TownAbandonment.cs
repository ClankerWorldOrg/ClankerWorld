using System.Security.Cryptography;
using System.Text;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private const string TownSalvagePrefix = "town_salvage:";

    private bool MayResettleTown(string actor, TownRuntimeState town) => TownSystem.MayResettleTown(civicWorld, actor, town);
    private string ResettlementText(string actor, TownRuntimeState town) => townSystem.ResettlementText(civicWorld, actor, town);
    private void ResettleTown(string actor, TownRuntimeState town) => townSystem.ResettleTown(civicWorld, actor, town);
    private static string TownSalvageId(string townId, string lotId) => TownSystem.TownSalvageId(townId, lotId);
    private IEnumerable<(TownRuntimeState Town, InventoryLot Lot, int Quantity)> AbandonedStockForPickup(string actor) => townSystem.AbandonedStockForPickup(civicWorld, actor);
    private void AddTownSalvageCandidates(List<CognitionCandidate> candidates, string actor) => townSystem.AddTownSalvageCandidates(civicWorld, candidates, actor);
    private void SalvageTownStock(string actor, string candidate) => townSystem.SalvageTownStock(civicWorld, actor, candidate);
}
