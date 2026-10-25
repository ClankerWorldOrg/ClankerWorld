using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Kernel;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private IInventoryExecutionWorld InventoryExecutionWorld => new InventoryExecutionAdapter(this);
    private sealed class InventoryExecutionAdapter(WorldRuntime runtime) : GoodsRuntimeAdapter(runtime), IInventoryExecutionWorld
    {
        public int ReservedShopStorageSpace(string building, InventoryCheckpoint inventory) => Runtime.marketSystem.ReservedShopStorageSpace(Runtime.MarketShopWorld, building, inventory);
        public int ReservedShopCarrySpace(string actor, InventoryCheckpoint inventory) => Runtime.marketSystem.ReservedShopCarrySpace(Runtime.MarketShopWorld, actor, inventory);
        public int HorseCargoCapacity(string actor) => Runtime.HorseCargoCapacity(actor);
    }

    private IInventoryCommunityWorld InventoryCommunityWorld => new InventoryCommunityAdapter(this);
    private sealed class InventoryCommunityAdapter(WorldRuntime runtime) : GoodsRuntimeAdapter(runtime), IInventoryCommunityWorld
    {
    }

    private static IInventoryConstructionWorld InventoryConstructionWorld => new InventoryConstructionAdapter();
    private sealed class InventoryConstructionAdapter : IInventoryConstructionWorld
    {
        public InventoryCheckpoint ReserveExpansionInputs(InventoryCheckpoint inventory, IReadOnlyList<ExpansionMaterialInput> inputs, string actor, string jobId, long completion, out IReadOnlyList<string> reservationIds) => WorldRuntime.ReserveExpansionInputs(inventory, inputs, actor, jobId, completion, out reservationIds);
        public InventoryCheckpoint ConsumeConstructionInputs(InventoryCheckpoint inventory, IReadOnlyList<ConstructionMaterialInput> inputs, string purpose, string owner) => WorldRuntime.ConsumeConstructionInputs(inventory, inputs, purpose, owner);
    }

    private IInventoryEconomyWorld InventoryEconomyWorld => new InventoryEconomyAdapter(this);
    private sealed class InventoryEconomyAdapter(WorldRuntime runtime) : GoodsRuntimeAdapter(runtime), IInventoryEconomyWorld
    {
        public long nextEventId => Runtime.nextEventId;
        public string OrnamentUnitId(long tick, long eventId, string actor, string lotId) => WorldRuntime.OrnamentUnitId(tick, eventId, actor, lotId);
        public AgentStore agentStore => Runtime.agentStore;
        public InventoryGroundPosition ExpansionGroundPosition(PlacedBuilding building) => WorldRuntime.ExpansionGroundPosition(building);
        public InventoryReservation? OrchardReservationForFood(InventoryCheckpoint inventory, string actor, InventoryLot lot) => Runtime.OrchardReservationForFood(inventory, actor, lot);
        public bool NeedsUrgentFood(AgentState person) => WorldRuntime.NeedsUrgentFood(person);
        public string EquipmentRepairReservationId(long tick, string actor, string material) => WorldRuntime.EquipmentRepairReservationId(tick, actor, material);
        public string EquipmentTransferId(long tick, string actor, string lotId) => WorldRuntime.EquipmentTransferId(tick, actor, lotId);
        public string EquippedUnitId(long tick, string actor, string lotId) => WorldRuntime.EquippedUnitId(tick, actor, lotId);
        public IReadOnlyDictionary<string, AgentState> agents => Runtime.agents;
    }

    private IInventoryFamilyWorld InventoryFamilyWorld => new InventoryFamilyAdapter(this);
    private sealed class InventoryFamilyAdapter(WorldRuntime runtime) : GoodsRuntimeAdapter(runtime), IInventoryFamilyWorld
    {
        public string MedicalDosePurpose(string caregiver, string patient, string kind, string owner, long startedTick) => FamilySystem.MedicalDosePurpose(caregiver, patient, kind, owner, startedTick);
        public InventoryCheckpoint MarkBuildingMaterialDelivery(InventoryCheckpoint inventory, string lotId, string? deliveryBuildingId) => WorldRuntime.MarkBuildingMaterialDelivery(inventory, lotId, deliveryBuildingId);
    }

    private IInventoryKnowledgeWorld InventoryKnowledgeWorld => new InventoryKnowledgeAdapter(this);
    private sealed class InventoryKnowledgeAdapter(WorldRuntime runtime) : GoodsRuntimeAdapter(runtime), IInventoryKnowledgeWorld
    {
        public string WritingMaterialTransferId(long tick, string actor, string kind) => WorldRuntime.WritingMaterialTransferId(tick, actor, kind);
    }

    private IInventoryMarketsWorld InventoryMarketsWorld => new InventoryMarketsAdapter(this);
    private sealed class InventoryMarketsAdapter(WorldRuntime runtime) : GoodsRuntimeAdapter(runtime), IInventoryMarketsWorld
    {
        public IReadOnlyList<TownRuntimeState> towns => Runtime.towns;
    }

    private static IInventoryProductionWorld InventoryProductionWorld => new InventoryProductionAdapter();
    private sealed class InventoryProductionAdapter : IInventoryProductionWorld
    {
        public InventoryCheckpoint ReserveRecipeInputs(InventoryCheckpoint inventory, RecipeProjectActionPlan plan, string purpose, long expiryTick, out IReadOnlyList<string> reservationIds) => WorldRuntime.ReserveRecipeInputs(inventory, plan, purpose, expiryTick, out reservationIds);
    }

    private IInventoryRuntimeWorld InventoryRuntimeWorld => new InventoryRuntimeAdapter(this);
    private sealed class InventoryRuntimeAdapter(WorldRuntime runtime) : GoodsRuntimeAdapter(runtime), IInventoryRuntimeWorld
    {
        public string ProductionOutputLotId(string jobId, int outputIndex) => WorldRuntime.ProductionOutputLotId(jobId, outputIndex);
        public string ProductionVesselUnitId(string outputLotId, int unitIndex) => WorldRuntime.ProductionVesselUnitId(outputLotId, unitIndex);
        public InventoryCheckpoint ApplyToolWorkToInventory(InventoryCheckpoint inventory, string actor, long tick, IReadOnlyList<ToolWorkPlan> plans) => WorldRuntime.ApplyToolWorkToInventory(inventory, actor, tick, plans);
        public HashSet<string> PerishableKinds => WorldRuntime.PerishableKinds;
        public IReadOnlyDictionary<string, int> FoodSpoilageRates => WorldRuntime.FoodSpoilageRates;
        public string OrchardReplantingReservationId(string lotId) => WorldRuntime.OrchardReplantingReservationId(lotId);
    }

    private IInventoryTownsWorld InventoryTownsWorld => new InventoryTownsAdapter(this);
    private sealed class InventoryTownsAdapter(WorldRuntime runtime) : GoodsRuntimeAdapter(runtime), IInventoryTownsWorld
    {
        public InventoryCheckpoint ReserveTownProjectDelivery(InventoryCheckpoint inventory, string townId, TownProject project, TownProjectDelivery delivery) => WorldRuntime.ReserveTownProjectDelivery(inventory, townId, project, delivery);
    }

    private IInventoryTransportWorld InventoryTransportWorld => new InventoryTransportAdapter(this);
    private sealed class InventoryTransportAdapter(WorldRuntime runtime) : GoodsRuntimeAdapter(runtime), IInventoryTransportWorld
    {
        public string CartBuildMaterialTransferId(long tick, string actor) => WorldRuntime.CartBuildMaterialTransferId(tick, actor);
        public string CartRepairReservationId(long tick, string actor, string material) => WorldRuntime.CartRepairReservationId(tick, actor, material);
    }

    private IInventoryWorldWorld InventoryWorldWorld => new InventoryWorldAdapter(this);
    private sealed class InventoryWorldAdapter(WorldRuntime runtime) : GoodsRuntimeAdapter(runtime), IInventoryWorldWorld
    {
        public string UsedToolLotId(string lotId, long tick, string actor) => WorldRuntime.UsedToolLotId(lotId, tick, actor);
        public string MaterialLotId(long tick, string actor) => WorldRuntime.MaterialLotId(tick, actor);
        public InventoryCheckpoint MarkBuildingMaterialDelivery(InventoryCheckpoint inventory, string lotId, string? deliveryBuildingId) => WorldRuntime.MarkBuildingMaterialDelivery(inventory, lotId, deliveryBuildingId);
        public string TreeSeedLotId(long tick, string actor) => WorldRuntime.TreeSeedLotId(tick, actor);
        public InventoryCheckpoint ApplyToolWorkToInventory(InventoryCheckpoint inventory, string actor, long tick, IReadOnlyList<ToolWorkPlan> plans) => WorldRuntime.ApplyToolWorkToInventory(inventory, actor, tick, plans);
        public string RepairedToolLotId(string lotId, long tick, string actor) => WorldRuntime.RepairedToolLotId(lotId, tick, actor);
        public string ToolRepairReservationId(long tick, string actor, string repairedId, string material) => WorldRuntime.ToolRepairReservationId(tick, actor, repairedId, material);
        public string ToolRepairGiftId(long tick, string actor, string repairedId, string material) => WorldRuntime.ToolRepairGiftId(tick, actor, repairedId, material);
    }
}

internal interface IInventoryCommunityWorld
{
    long WorldTick { get; }
}

internal interface IInventoryConstructionWorld
{
    InventoryCheckpoint ReserveExpansionInputs(InventoryCheckpoint inventory, IReadOnlyList<ExpansionMaterialInput> inputs, string actor, string jobId, long completion, out IReadOnlyList<string> reservationIds);
    InventoryCheckpoint ConsumeConstructionInputs(InventoryCheckpoint inventory, IReadOnlyList<ConstructionMaterialInput> inputs, string purpose, string owner);
}

internal interface IInventoryEconomyWorld
{
    long WorldTick { get; }
    long nextEventId { get; }
    string OrnamentUnitId(long tick, long eventId, string actor, string lotId);
    AgentStore agentStore { get; }
    InventoryGroundPosition ExpansionGroundPosition(PlacedBuilding building);
    InventoryReservation? OrchardReservationForFood(InventoryCheckpoint inventory, string actor, InventoryLot lot);
    bool NeedsUrgentFood(AgentState person);
    IEnumerable<InventoryLot> PreferredFood(string owner, string? actor = null);
    string EquipmentRepairReservationId(long tick, string actor, string material);
    string EquipmentTransferId(long tick, string actor, string lotId);
    string EquippedUnitId(long tick, string actor, string lotId);
    IReadOnlyDictionary<string, AgentState> agents { get; }
}

internal interface IInventoryExecutionWorld
{
    IReadOnlyDictionary<string, AgentState> Agents { get; }
    IReadOnlyList<TownRuntimeState> Towns { get; }
    WorldContentSimulationState Simulation { get; }
    DeclarativeWorldContentState Content { get; }
    int ReservedShopStorageSpace(string building, InventoryCheckpoint inventory);
    int ReservedShopCarrySpace(string actor, InventoryCheckpoint inventory);
    int HorseCargoCapacity(string actor);
}

internal interface IInventoryFamilyWorld
{
    string MedicalDosePurpose(string caregiver, string patient, string kind, string owner, long startedTick);
    long WorldTick { get; }
    InventoryCheckpoint MarkBuildingMaterialDelivery(InventoryCheckpoint inventory, string lotId, string? deliveryBuildingId);
    string HouseholdFor(string actor);
}

internal interface IInventoryKnowledgeWorld
{
    string WritingMaterialTransferId(long tick, string actor, string kind);
    long WorldTick { get; }
}

internal interface IInventoryMarketsWorld
{
    IReadOnlyList<TownRuntimeState> towns { get; }
    long WorldTick { get; }
}

internal interface IInventoryProductionWorld
{
    InventoryCheckpoint ReserveRecipeInputs(InventoryCheckpoint inventory, RecipeProjectActionPlan plan, string purpose, long expiryTick, out IReadOnlyList<string> reservationIds);
}

internal interface IInventoryRuntimeWorld
{
    string ProductionOutputLotId(string jobId, int outputIndex);
    string ProductionVesselUnitId(string outputLotId, int unitIndex);
    InventoryCheckpoint ApplyToolWorkToInventory(InventoryCheckpoint inventory, string actor, long tick, IReadOnlyList<ToolWorkPlan> plans);
    long WorldTick { get; }
    HashSet<string> PerishableKinds { get; }
    IReadOnlyDictionary<string, int> FoodSpoilageRates { get; }
    string OrchardReplantingReservationId(string lotId);
}

internal interface IInventoryTownsWorld
{
    InventoryCheckpoint ReserveTownProjectDelivery(InventoryCheckpoint inventory, string townId, TownProject project, TownProjectDelivery delivery);
    long WorldTick { get; }
}

internal interface IInventoryTransportWorld
{
    long WorldTick { get; }
    string CartBuildMaterialTransferId(long tick, string actor);
    string CartRepairReservationId(long tick, string actor, string material);
}

internal interface IInventoryWorldWorld
{
    string UsedToolLotId(string lotId, long tick, string actor);
    long WorldTick { get; }
    string MaterialLotId(long tick, string actor);
    InventoryCheckpoint MarkBuildingMaterialDelivery(InventoryCheckpoint inventory, string lotId, string? deliveryBuildingId);
    string TreeSeedLotId(long tick, string actor);
    InventoryCheckpoint ApplyToolWorkToInventory(InventoryCheckpoint inventory, string actor, long tick, IReadOnlyList<ToolWorkPlan> plans);
    string RepairedToolLotId(string lotId, long tick, string actor);
    string ToolRepairReservationId(long tick, string actor, string repairedId, string material);
    string ToolRepairGiftId(long tick, string actor, string repairedId, string material);
}
