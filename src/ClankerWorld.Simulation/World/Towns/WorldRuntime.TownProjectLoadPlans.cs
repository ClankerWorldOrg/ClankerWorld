using ClankerWorld.Simulation.Kernel;
namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private int TownProjectLoadQuantity(string actor, TownRuntimeState town, TownProject project, InventoryLot lot) =>
        Math.Min(TownProjectMissing(town, project, lot.ItemKind), Math.Min(AvailableLotQuantity(lot),
            lot.CarrierId == actor ? lot.Quantity : Math.Min(WarehouseLoadQuantity,
                PickupCarryCapacity(actor, lot, TownProjectRules.WorkSite(project.Plan)))));

    private TownProjectLoadActionPlan PlanTownProjectLoad(string actor, TownProjectChoice choice)
    {
        var destination = TownProjectRules.WorkSite(choice.Project.Plan);
        var deliver = choice.Kind == "deliver";
        var carried = choice.Lot!.CarrierId == actor;
        var target = deliver || carried ? destination : HouseholdStockPosition(choice.Lot);
        var range = deliver || carried ? 0 : HouseholdStockInteractionRange(choice.Lot);
        return new(actor, new(deliver ? TownProjectLoadKind.Deliver : TownProjectLoadKind.Supply,
            choice.Project.Id, choice.Lot.Id, choice.Quantity, choice.Delivery?.Id), choice.Town.Id,
            choice.Project.ProposalId, choice.Project.Plan, choice.ItemKind, destination, target, range,
            choice.Lot.CarrierId, choice.Lot.StorageBuildingId, choice.Lot.GroundPosition, choice.Delivery,
            Reach(actor, target, ReachPurpose.TownProjectLoad, range, 0, SwimmingPolicy.WhenCapable),
            ReachLeg(actor, carried ? agents[actor].Position : target, destination,
                ReachPurpose.TownProjectLoad, 0, 0, SwimmingPolicy.WhenCapable));
    }

    private ActionCheck CheckTownProjectLoad(TownProjectLoadActionPlan plan, ActorCapabilityPolicy? policy = null)
    {
        var capability = ActorCapabilities.Check(CapabilitiesFor(plan.Actor), plan.CandidateId,
            policy ?? TownProjectPhysicalCapabilities);
        if (!capability.Allowed) return capability;
        if (!ReadyForTownProject(plan.Actor) || TownForResident(plan.Actor) != plan.TownId ||
            towns.SingleOrDefault(town => town.Id == plan.TownId) is not { Council: not null } town ||
            town.Projects.SingleOrDefault(project => project.Id == plan.Choice.ProjectId) is not { Stage: "supplying" or "working" } project ||
            project.ProposalId != plan.ProposalId || TownHearingProcedure.Digest(project.Plan) != TownHearingProcedure.Digest(plan.Project) ||
            !TownSystem.CivicHistory(town).Knows(plan.Actor, project.ProposalId) || TownProjectRules.WorkSite(project.Plan) != plan.Destination ||
            TownProjectSiteFailure(town, project.Plan, project.Id, plan.Actor) is not null)
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected project scope, knowledge or carrying ability has changed");
        InventoryLot? lot;
        if (plan.Choice.Kind == TownProjectLoadKind.Deliver)
        {
            if (project.Deliveries.SingleOrDefault(delivery => delivery.Id == plan.Choice.DeliveryId) is not { } delivery ||
                delivery != plan.Delivery || delivery.ContributorId != plan.Actor || delivery.LotId != plan.Choice.StockId ||
                delivery.Quantity != plan.Choice.Quantity || delivery.ItemKind != plan.ItemKind || !TownProjectIncomingIsLive(town.Id, delivery))
                return ActionCheck.Refuse(ActionRefusalReason.StockUnavailable, "the selected incoming project load is no longer in their custody");
            lot = RecheckGoods(new(GoodsUse.CarriedInputs, plan.Actor, GoodsOwners.One(plan.TownId), GoodsKinds.One(plan.ItemKind)), delivery.LotId)?.Lot;
            if (lot is null) return ActionCheck.Refuse(ActionRefusalReason.StockUnavailable, "the selected project load is unavailable");
        }
        else
        {
            lot = TownProjectMaterialSources(town, project, plan.Actor, plan.ItemKind).SingleOrDefault(item => item.Id == plan.Choice.StockId);
            if (lot is null || lot.CarrierId != plan.CarrierId || lot.StorageBuildingId != plan.StorageBuildingId ||
                lot.GroundPosition != plan.GroundPosition || TownProjectLoadQuantity(plan.Actor, town, project, lot) < plan.Choice.Quantity)
                return ActionCheck.Refuse(ActionRefusalReason.StockUnavailable, "the selected Town goods or exact required load are unavailable");
            if (lot.CarrierId != plan.Actor && (HouseholdStockPosition(lot) != plan.Target || HouseholdStockInteractionRange(lot) != plan.InteractionRange))
                return ActionCheck.Refuse(ActionRefusalReason.StockUnavailable, "the selected pickup location has changed");
        }
        var origin = lot.CarrierId == plan.Actor ? agents[plan.Actor].Position : plan.Target;
        if (!IsWithinInteractionRange(origin, plan.Destination, 0) && ReachLeg(plan.Actor, origin, plan.Destination,
                ReachPurpose.TownProjectLoad, 0, 0, SwimmingPolicy.WhenCapable).Count == 0)
            return ActionCheck.Refuse(ActionRefusalReason.OutOfReach, "the selected project's delivery leg is out of reach");
        return IsWithinInteractionRange(agents[plan.Actor].Position, plan.Target, plan.InteractionRange) ||
            Reach(plan.Actor, plan.Target, ReachPurpose.TownProjectLoad, plan.InteractionRange, 0, SwimmingPolicy.WhenCapable).Count > 0
            ? new() : ActionCheck.Refuse(ActionRefusalReason.OutOfReach, "the selected project load is out of reach");
    }

    private void ApplyCheckedTownProjectLoad(TownProjectLoadActionPlan plan)
    {
        var town = towns.Single(item => item.Id == plan.TownId);
        var project = town.Projects.Single(item => item.Id == plan.Choice.ProjectId);
        var lot = plan.Choice.Kind == TownProjectLoadKind.Deliver
            ? RecheckGoods(new(GoodsUse.CarriedInputs, plan.Actor, GoodsOwners.One(plan.TownId), GoodsKinds.One(plan.ItemKind)), plan.Choice.StockId)!.Lot
            : TownProjectMaterialSources(town, project, plan.Actor, plan.ItemKind).Single(item => item.Id == plan.Choice.StockId);
        var choice = new TownProjectChoice(plan.CandidateId, plan.Choice.Kind == TownProjectLoadKind.Deliver ? "deliver" : "supply",
            town, project, plan.ItemKind, lot, plan.Choice.Quantity, plan.Delivery);
        if (plan.Choice.Kind == TownProjectLoadKind.Deliver) DeliverTownProjectLoad(plan.Actor, agents[plan.Actor], choice);
        else PickUpTownProjectLoad(plan.Actor, agents[plan.Actor], choice);
    }
}
