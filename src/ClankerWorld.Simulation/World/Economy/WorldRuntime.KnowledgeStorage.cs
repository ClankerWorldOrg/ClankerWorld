using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Kernel;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private const string KnowledgeCollectPrefix = "knowledge_collect:";
    private const string KnowledgeStorePrefix = "knowledge_store:";

    private static int KnowledgeCollectionRange(string actor, InventoryLot lot) => lot.OwnerId == actor
        ? lot.GroundPosition is null ? 1 : ResourceInteractionRange
        : HouseholdStockInteractionRange(lot);

    private InventoryLot? KnowledgeArtifactForCollection(string actor, AgentKnowledgeArtifact artifact)
    {
        var inventory = society.Checkpoint.Inventory;
        var lot = inventory.Lots.FirstOrDefault(item => item.Id == artifact.LotId && item.ItemKind == artifact.Kind &&
            item.Quantity == 1 && item.CarrierId is null && item.ContainerLotId is null &&
            item.DeliveryBuildingId is null && AvailableLotQuantity(item) == 1);
        if (lot is null || PersonalEquipmentRules.IsCarried(lot, actor))
            return null;
        if (lot.OwnerId == actor)
            return PersonalGoodsAwaitingCollection(actor).Any(item => item.Id == lot.Id) ? lot : null;
        var householdId = society.Checkpoint.GetAgent(actor).HouseholdId;
        return householdId is not null && lot.OwnerId == householdId &&
            (lot.StorageBuildingId is null || worldSimulation.Buildings.Any(building =>
                building.InstanceId == lot.StorageBuildingId && building.HouseholdId == householdId)) ? lot : null;
    }

    private InventoryLot? HeldKnowledgeArtifactLot(string actor, AgentKnowledgeArtifact artifact) =>
        HeldKnowledgeArtifacts(actor).Any(item => item.Id == artifact.Id)
            ? RecheckGoods(new(GoodsUse.CarriedInputs, actor, GoodsOwners.One(actor), GoodsKinds.One(artifact.Kind)), artifact.LotId)?.Lot : null;

    private void AddKnowledgeStorageCandidates(List<CognitionCandidate> candidates,
        string actor, AgentState person)
    {
        if (!IsAdult(actor)) return;
        if (FreeCarryCapacity(actor) > 0)
        {
            foreach (var artifact in knowledge.Artifacts.OrderBy(item => item.Id, StringComparer.Ordinal))
            {
                if (KnowledgeArtifactForCollection(actor, artifact) is not { } lot)
                    continue;
                var position = HouseholdStockPosition(lot);
                var range = KnowledgeCollectionRange(actor, lot);
                var plan = PlanKnowledgeStorage(actor, artifact, true);
                if (plan is null || !CheckActionPlan(plan).Allowed) continue;
                // Explicit access stays available without an automatic
                // collect/store cycle when the owner has no current use.
                candidates.Add(new(plan.CandidateId,
                    $"Collect {artifact.Title} to carry, read, copy or trade it.", 190)
                { ActionPlan = plan });
            }
        }
        if (society.Checkpoint.GetAgent(actor).HouseholdId is not { } householdId ||
            HouseForHousehold(householdId) is not { } house ||
            DestinationRoom(house.InstanceId) < 1 ||
            person.Position != house.Position && Reach(actor, house.Position, ReachPurpose.KnowledgeStorage, 0, 0, SwimmingPolicy.WhenCapable).Count == 0)
            return;
        foreach (var artifact in HeldKnowledgeArtifacts(actor))
        {
            var plan = PlanKnowledgeStorage(actor, artifact, false);
            if (plan is not null && CheckActionPlan(plan).Allowed) candidates.Add(new(plan.CandidateId,
                $"Store {artifact.Title} in your House while keeping personal ownership.", 190)
            { ActionPlan = plan });
        }
    }

    private bool ApplyCheckedKnowledgeStorage(KnowledgeStorageActionPlan plan, AgentState person)
    {
        var actor = plan.Actor;
        var collect = plan.Collect;
        var artifact = plan.Artifact;
        if (collect)
        {
            if (FreeCarryCapacity(actor) < 1 || KnowledgeArtifactForCollection(actor, artifact) is not { } lot)
                return true;
            var position = HouseholdStockPosition(lot);
            var range = KnowledgeCollectionRange(actor, lot);
            if (!IsWithinInteractionRange(person.Position, position, range))
            {
                MoveToward(actor, person, position, "knowledge_collect", range);
                return true;
            }
            RunInventoryCommand(InventorySystem.CollectWrittenKnowledge(InventoryEconomyWorld, lot, actor, artifact));
            AppendEvent(new AgentKnowledgeArtifactCollectedEventPayload(actor, artifact.Id));
            return true;
        }
        if (!HeldKnowledgeArtifacts(actor).Any(item => item.Id == artifact.Id) ||
            society.Checkpoint.GetAgent(actor).HouseholdId is not { } householdId ||
            HouseForHousehold(householdId) is not { } house ||
            DestinationRoom(house.InstanceId) < 1)
            return true;
        if (person.Position != house.Position)
        {
            MoveToward(actor, person, house.Position, "knowledge_store", 0);
            return true;
        }
        RunInventoryCommand(InventorySystem.StoreWrittenKnowledge(InventoryEconomyWorld, actor, artifact, house));
        AppendEvent(new AgentKnowledgeArtifactStoredEventPayload(actor, artifact.Id, house.InstanceId));
        return true;
    }
}
