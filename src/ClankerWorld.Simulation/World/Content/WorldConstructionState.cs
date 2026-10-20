namespace ClankerWorld.Simulation.World;

internal sealed record WorldConstructionState(
    IReadOnlyList<PlacedBuilding> Buildings,
    IReadOnlyList<BuildingExpansionJob>? Expansions,
    IReadOnlyList<HouseGuestInvitation>? Guests,
    IReadOnlyList<WorldConstructionReceipt>? Receipts)
{
    internal static WorldConstructionState From(WorldContentSimulationState snapshot) =>
        new(snapshot.Buildings, snapshot.BuildingExpansions, snapshot.GuestInvitations, snapshot.ConstructionReceipts);
}
