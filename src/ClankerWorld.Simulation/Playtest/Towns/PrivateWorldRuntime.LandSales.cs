using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Content;

namespace ClankerWorld.Simulation.Playtest;

public sealed partial class PrivateWorldRuntime
{
    private static bool LandSaleKind(string kind) => CivicSystem.LandSaleKind(kind);
    private InventoryLot[] LandSaleCarriedLots(string actor, TownLandSalePrice price) => CivicSystem.LandSaleCarriedLots(civicWorld, actor, price);
    private int LandSaleCarriedQuantity(string actor, TownLandSalePrice price) => CivicSystem.LandSaleCarriedQuantity(civicWorld, actor, price);
    private InventoryLot? LandSaleStock(string actor, TownRuntimeState town, TownLandSalePrice price) => civicSystem.LandSaleStock(civicWorld, actor, town, price);
    private int LandSalePickupCapacity(string actor, TownRuntimeState town, InventoryLot stock) => CivicSystem.LandSalePickupCapacity(civicWorld, actor, town, stock);
    private void AddTownLandSaleCandidates(List<CognitionCandidate> candidates, string actor, TownRuntimeState town,
        TownLandTransferRequest request, IReadOnlyList<TownLandTransferParty> parties, string token) => civicSystem.AddTownLandSaleCandidates(civicWorld, candidates, actor, town, request, parties, token);
    /// <summary>Only the chosen physical trip continues locally; arrival needs a fresh personal choice.</summary>
    private void ContinueTownLandSaleWalk(string actor, string candidate) => civicSystem.ContinueTownLandSaleWalk(civicWorld, actor, candidate);
    private void CollectLandSalePayment(string actor, TownRuntimeState town, TownLandTransferRequest request) => civicSystem.CollectLandSalePayment(civicWorld, actor, town, request);
    private (TownGovernanceState Council, TownLandHearingState LandHearings) SettleLandSale(TownRuntimeState town,
        TownGovernanceState council, TownLandTransferRequest request, string buyer) => civicSystem.SettleLandSale(civicWorld, town, council, request, buyer);
}
