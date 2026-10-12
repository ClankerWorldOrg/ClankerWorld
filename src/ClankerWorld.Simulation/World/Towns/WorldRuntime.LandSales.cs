using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Content;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private static bool LandSaleKind(string kind) => TownSystem.LandSaleKind(kind);
    private InventoryLot[] LandSaleCarriedLots(string actor, TownLandSalePrice price) => TownSystem.LandSaleCarriedLots(civicWorld, actor, price);
    private int LandSaleCarriedQuantity(string actor, TownLandSalePrice price) => TownSystem.LandSaleCarriedQuantity(civicWorld, actor, price);
    private InventoryLot? LandSaleStock(string actor, TownRuntimeState town, TownLandSalePrice price) => townSystem.LandSaleStock(civicWorld, actor, town, price);
    private int LandSalePickupCapacity(string actor, TownRuntimeState town, InventoryLot stock) => TownSystem.LandSalePickupCapacity(civicWorld, actor, town, stock);
    private void AddTownLandSaleCandidates(List<CognitionCandidate> candidates, string actor, TownRuntimeState town,
        TownLandTransferRequest request, IReadOnlyList<TownLandTransferParty> parties, string token) => townSystem.AddTownLandSaleCandidates(civicWorld, candidates, actor, town, request, parties, token);
    /// <summary>Only the chosen physical trip continues locally; arrival needs a fresh personal choice.</summary>
    private void ContinueTownLandSaleWalk(string actor, string candidate) => townSystem.ContinueTownLandSaleWalk(civicWorld, actor, candidate);
    private void CollectLandSalePayment(string actor, TownRuntimeState town, TownLandTransferRequest request) => townSystem.CollectLandSalePayment(civicWorld, actor, town, request);
    private (TownCouncilState Council, TownLandRecords LandRecords) SettleLandSale(TownRuntimeState town,
        TownCouncilState council, TownLandTransferRequest request, string buyer) => townSystem.SettleLandSale(civicWorld, town, council, request, buyer);
}
