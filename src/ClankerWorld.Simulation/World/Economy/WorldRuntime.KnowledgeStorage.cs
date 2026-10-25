using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Kernel;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{

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
    private KnowledgeStorageActionPlan? PlanKnowledgeStorage(string actor, AgentKnowledgeArtifact artifact, bool collect)
    {
        var lot = collect ? KnowledgeArtifactForCollection(actor, artifact) : HeldKnowledgeArtifactLot(actor, artifact);
        if (lot is null) return null;
        var household = HouseholdFor(actor);
        var house = household is null ? null : HouseForHousehold(household);
        if (!collect && house is null) return null;
        var target = collect ? HouseholdStockPosition(lot) : house!.Position;
        var range = collect ? KnowledgeCollectionRange(actor, lot) : 0;
        return new(actor, artifact, collect, lot.OwnerId, household, collect ? null : house!.InstanceId,
            lot.CarrierId, lot.StorageBuildingId, lot.GroundPosition, target, range,
            Reach(actor, target, ReachPurpose.KnowledgeStorage, range, 0, SwimmingPolicy.WhenCapable));
    }

    private ActionCheck CheckKnowledgeStorage(KnowledgeStorageActionPlan plan, ActorCapabilityPolicy? policy = null)
    {
        var capability = ActorCapabilities.Check(CapabilitiesFor(plan.Actor), plan.CandidateId,
            policy ?? KnowledgeStorageCapabilities);
        if (!capability.Allowed) return capability;
        if (knowledge.Artifacts.SingleOrDefault(item => item.Id == plan.Artifact.Id) is not { } artifact ||
            TownHearingProcedure.Digest(artifact) != TownHearingProcedure.Digest(plan.Artifact) || HouseholdFor(plan.Actor) != plan.HouseholdId)
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected artifact or household has changed");
        var lot = plan.Collect ? KnowledgeArtifactForCollection(plan.Actor, artifact) : HeldKnowledgeArtifactLot(plan.Actor, artifact);
        if (lot is null || lot.OwnerId != plan.OwnerId || lot.CarrierId != plan.CarrierId || lot.StorageBuildingId != plan.StorageBuildingId ||
            lot.GroundPosition != plan.GroundPosition)
            return ActionCheck.Refuse(ActionRefusalReason.StockUnavailable, "the selected artifact's custody or location has changed");
        if (plan.Collect)
        {
            if (FreeCarryCapacity(plan.Actor) < 1) return ActionCheck.Refuse(ActionRefusalReason.Capacity, "there is no room for the selected artifact");
            if (HouseholdStockPosition(lot) != plan.Target || KnowledgeCollectionRange(plan.Actor, lot) != plan.Range)
                return ActionCheck.Refuse(ActionRefusalReason.StockUnavailable, "the selected artifact's pickup has changed");
        }
        else if (plan.HouseholdId is null || HouseForHousehold(plan.HouseholdId) is not { } house || house.InstanceId != plan.HouseId ||
            house.Position != plan.Target || DestinationRoom(house.InstanceId) < 1)
            return ActionCheck.Refuse(ActionRefusalReason.Capacity, "the selected House is unavailable or has no room");
        return IsWithinInteractionRange(agents[plan.Actor].Position, plan.Target, plan.Range) ||
            Reach(plan.Actor, plan.Target, ReachPurpose.KnowledgeStorage, plan.Range, 0, SwimmingPolicy.WhenCapable).Count > 0
            ? new() : ActionCheck.Refuse(ActionRefusalReason.OutOfReach, "the selected knowledge storage trip is out of reach");
    }
    private bool NeedsKnowledgePaper(string? householdId)
    {
        if (householdId is null || knowledge.Artifacts.Count >= AgentKnowledgeRules.MaximumArtifactsInWorld)
            return false;

        var writers = society.Checkpoint.Agents.Where(person => person.HouseholdId == householdId &&
                IsAdult(person.Id) && knowledge.Facts.Any(fact => fact.OwnerId == person.Id) &&
                knowledge.Artifacts.Count(artifact => artifact.CreatorId == person.Id) <
                    AgentKnowledgeRules.MaximumArtifactsPerCreator)
            .Select(person => person.Id).ToHashSet(StringComparer.Ordinal);
        if (writers.Count == 0)
            return false;

        // Two sheets per eligible writer permit a book. The shared same-job
        // rule counts writing commitments without making them available to pickups.
        var available = FindGoods(new(GoodsUse.Holdings, null, new(writers.Append(householdId).ToArray()),
            GoodsKinds.One(KnowledgeContent.Paper), DemandCoverage: GoodsDemandCoverage.KnowledgeWriting)).CoveredTotal;
        return available < writers.Count * 2;
    }
}
