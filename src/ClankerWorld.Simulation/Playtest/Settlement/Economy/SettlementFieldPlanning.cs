using System.Globalization;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Harness;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.Playtest;

public sealed partial class PrivateWorldRuntime
{
    private static string FarmCandidate(FarmWorkKind kind, GridPoint point, string? crop = null) =>
        FarmingSystem.FarmCandidate(kind, point, crop);

    private void AddFieldCandidates(List<CognitionCandidate> candidates, string actor, PlaytestInhabitantState state) =>
        farmingSystem.AddFieldCandidates(farmingWorld, candidates, actor, state);

    private IEnumerable<GridPoint> NearbyFarmTiles(GridPoint origin) =>
        FarmingSystem.NearbyFarmTiles(farmingWorld, origin);

    private bool HasOtherInhabitantClaimedPlanting(FarmFieldState field, string actor) =>
        FarmingSystem.HasOtherInhabitantClaimedPlanting(farmingWorld, field, actor);

    private bool CanReachField(string actor, GridPoint from, GridPoint destination) =>
        FarmingSystem.CanReachField(farmingWorld, actor, from, destination);

    private bool FarmNeedsFood(string householdId) =>
        FarmingSystem.FarmNeedsFood(farmingWorld, householdId);

    private (int MissingMeals, int FarmingHouseholds) FarmFoodDemand(string householdId) =>
        FarmingSystem.FarmFoodDemand(farmingWorld, householdId);

    private bool WantsFieldPlantingStock(string actor, string itemKind) =>
        farmingSystem.WantsFieldPlantingStock(farmingWorld, actor, itemKind);

    private IEnumerable<string> PlantableCrops(string actor, FarmFieldState field) =>
        farmingSystem.PlantableCrops(farmingWorld, actor, field);

    private InventoryLot? PlantingStock(string actor, FarmFieldState field, string crop, bool requirePickupRoute = false) =>
        FarmingSystem.PlantingStock(farmingWorld, actor, field, crop, requirePickupRoute);

    private void ApplyFieldCandidate(string actor, PlaytestInhabitantState state, string candidate, string? orderInstructionId = null) =>
        farmingSystem.ApplyFieldCandidate(farmingWorld, actor, state, candidate, orderInstructionId);

}
