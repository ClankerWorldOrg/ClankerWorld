namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
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

    private ActionCheck CheckKnowledgeStorage(KnowledgeStorageActionPlan plan)
    {
        var capability = ActorCapabilities.Check(CapabilitiesFor(plan.Actor), plan.CandidateId,
            new(ActionLifeStages.Adults, AllowUrgentFood: true, AllowUrgentWarmth: false, AllowActiveOrder: true));
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
}
