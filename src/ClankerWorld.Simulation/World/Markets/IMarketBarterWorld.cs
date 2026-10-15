using System.Collections.ObjectModel;
using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

internal interface IMarketBarterWorld
{
    AgentStore PhysicalAgents { get; }
    SocietyCheckpoint Society { get; }
    WorldKnowledgeState Knowledge { get; }
    DeclarativeWorldContentState Content { get; }
    long WorldTick { get; }
    GridPoint SharedStoragePosition { get; }
    int ResourceInteractionRange { get; }
    bool NeedsUrgentWarmth(AgentState person);
    int AvailableLotQuantity(InventoryLot lot);
    int HorseCargoCapacity(string actor);
    int ReservedShopCarrySpace(string actor);
    bool MedicalSupplyWanted(string actor, string kind);
    bool WantsOrnamentInput(string actor, string kind);
    bool HasUnknownArtifactContents(string actor, AgentKnowledgeArtifact artifact);
    string BuildingConstructionOwner(string actor, BuildingDefinition building);
    string PersonalProductionOwner(string actor);
    bool HasAvailableQuantities(IReadOnlyList<ContentQuantity> quantities, string? owner = null);
    IEnumerable<GoodsMatch> SpendingGoods(string actor, GoodsUse use, IReadOnlyList<string> owners, string? kind = null, string? building = null, InventoryCheckpoint? inventory = null);
    bool IsAdult(string actor);
    ActorCapabilityState? CapabilitiesFor(string actor);
    ReadOnlyCollection<GridPoint> Reach(string actor, GridPoint target, ReachPurpose purpose, int range, int load, SwimmingPolicy swimming);
    bool IsWithinInteractionRange(GridPoint origin, GridPoint destination, int range);
    void MoveToward(string actor, AgentState person, GridPoint destination, string reason, int range);
    string DirectBarterOfferId(long tick, string actor, string other);
    void CreateBarterOffer(DirectBarterProposal proposal);
    void AcceptBarterOffer(string id, int revision, string actor);
    void CancelBarterOffer(string id, int revision, string actor);
    void RetireBarterOffers(IReadOnlySet<string> retired);
    void ReadTradedKnowledge(string first, string second, string firstLot, string secondLot);
    void IncreaseTrust(string actor, string other, int amount, string reason);
    string TrustMemoryId(string actor, string other);
    void RecordBarterMemory(string id, string actor, string other);
    void AppendEvent(string kind, string detail);
}
