using System.Security.Cryptography;
using System.Text;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private const string TownSalvagePrefix = "town_salvage:";

    private static string TownSalvageId(string townId, string lotId) => TownSystem.TownSalvageId(townId, lotId);
    private void AddTownSalvageCandidates(List<CognitionCandidate> candidates, string actor) => townSystem.AddTownSalvageCandidates(civicWorld, candidates, actor);
    private void SalvageTownStock(string actor, string candidate) => ApplyTownStockPlan(townSystem.PlanTownSalvage(civicWorld, actor, candidate));
}
