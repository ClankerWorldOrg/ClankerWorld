using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Kernel;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    // Initial small-load and short-trip limits, independent of adult equipment upgrades.

    private PlacedBuilding? ChildHelpingHouse(string actor, AgentState state) =>
        FamilySystem.ChildHelpingHouse(FamilyChildWorld, actor, state);

    private int ChildHelpingRoom(string actor) =>
        FamilySystem.ChildHelpingRoom(FamilyChildWorld, actor);

    private bool ChildHelpingReachable(string actor, GridPoint source, int range, PlacedBuilding house) =>
        FamilySystem.ChildHelpingReachable(FamilyChildWorld, actor, source, range, house);

    private static bool ChildHelpingItem(string kind) =>
        FamilySystem.ChildHelpingItem(kind);

    private InventoryLot? ChildHelpingDelivery(string actor, PlacedBuilding house) =>
        FamilySystem.ChildHelpingDelivery(FamilyChildWorld, actor, house);

    private InventoryLot? ChildHelpingStock(string actor, PlacedBuilding house) =>
        FamilySystem.ChildHelpingStock(FamilyChildWorld, actor, house);

    private static GoodsRequest ChildHelpingGoodsRequest(string actor, string owner) =>
        FamilySystem.ChildHelpingGoodsRequest(actor, owner);

    private static bool ChildGoodsCanMove(GoodsMatch goods, IReadOnlyList<InventoryHold> access) =>
        FamilySystem.ChildGoodsCanMove(goods, access);

    private bool ChildHelpingSourceAvailable(string actor, PlacedBuilding house, MapResource source, string kind) =>
        FamilySystem.ChildHelpingSourceAvailable(FamilyChildWorld, actor, house, source, kind);

    private MapResource? ChildHelpingSource(string actor, PlacedBuilding house, string kind) =>
        FamilySystem.ChildHelpingSource(FamilyChildWorld, actor, house, kind);

    private void AddChildHelpingCandidates(List<CognitionCandidate> candidates, string actor, AgentState state) =>
        FamilySystem.AddChildHelpingCandidates(FamilyChildWorld, candidates, actor, state);

    private void ApplyChildHelpingPlan(ChildActionPlan plan) =>
        FamilySystem.ApplyChildHelpingPlan(FamilyChildWorld, plan);
}
