using System.Security.Cryptography;
using System.Text;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.Playtest;

public sealed partial class PrivateWorldRuntime
{
    private const string TownSalvagePrefix = "town_salvage:";

    private bool MayResettleTown(string actor, TownRuntimeState town) => CivicSystem.MayResettleTown(civicWorld, actor, town);
    private string ResettlementText(string actor, TownRuntimeState town) => civicSystem.ResettlementText(civicWorld, actor, town);
    private void ResettleTown(string actor, TownRuntimeState town) => civicSystem.ResettleTown(civicWorld, actor, town);
    private static string TownSalvageId(string townId, string lotId) => CivicSystem.TownSalvageId(townId, lotId);
    private IEnumerable<(TownRuntimeState Town, InventoryLot Lot, int Quantity)> AbandonedStockForPickup(string actor) => civicSystem.AbandonedStockForPickup(civicWorld, actor);
    private void AddTownSalvageCandidates(List<CognitionCandidate> candidates, string actor) => civicSystem.AddTownSalvageCandidates(civicWorld, candidates, actor);
    private void SalvageTownStock(string actor, string candidate) => civicSystem.SalvageTownStock(civicWorld, actor, candidate);
}
