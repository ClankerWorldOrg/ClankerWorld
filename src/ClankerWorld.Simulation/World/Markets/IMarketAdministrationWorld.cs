using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

internal interface IMarketAdministrationWorld
{
    SocietyCheckpoint Society { get; }
    IReadOnlyList<TownRuntimeState> Towns { get; }
    IReadOnlyList<PlacedBuilding> Buildings { get; }
    IMarketStallWorld Stalls { get; }
    long WorldTick { get; }
    void PlaceStarterStalls(IReadOnlyList<PlacedBuilding> buildings);
    void RecordConstructedMarkets(TownRuntimeState town, IReadOnlyList<TownMarketState> markets);
    void RecordRemovedMarketBuildings(TownRuntimeState town, IReadOnlyList<TownMarketState> markets);
    void AssignBuildingToTown(PlacedBuilding building, BuildingDefinition definition);
    void MaintainTownProjects();
    void RetireMarketOffers(IReadOnlySet<string> offers, IReadOnlySet<string> claims);
    void AppendEvent(WorldEventPayload payload, GridPoint? position = null);
}
