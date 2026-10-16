namespace ClankerWorld.Simulation.World;

internal sealed partial class TownSystem
{
    internal static NonLandWorkActionPlan? PlanNonLandWork(ITownWorld world, string actor, TownRuntimeState town,
        TownRestorativeAgreement agreement, TownRemedyTerm term)
    {
        var remaining = NonLandRemaining(town.NonLandHearings, agreement, term);
        if (remaining <= 0 || !NonLandWorkAllowed(world, actor) || !NonLandRemedyFeasible(world, town, term with { Quantity = remaining })) return null;
        var choice = new NonLandWorkChoice(town.Id, agreement.Id, term.Id);
        if (term.Kind == "return_goods")
            return NonLandReturnLot(world, term with { Quantity = remaining }) is { } lot ?
                new(actor, choice, agreement, term, remaining, NonLandWorkPhase.ReturnGoods, lot.Id, lot.ItemKind, remaining, null,
                    world.Agents[term.BeneficiaryId!].Position, [], null, null) : null;
        if (term.Kind == "public_service_goods")
        {
            var warehouse = world.WarehouseForResident(actor);
            if (warehouse is null || world.PersonalWarehouseSurplus(actor, term.ItemKind) is not { } surplus) return null;
            var travel = world.Agents[actor].Position != warehouse.Position;
            var quantity = Math.Min(remaining, Math.Min(WorldRuntime.CivicSharedRules.WarehouseLoadQuantity,
                Math.Min(world.DestinationRoom(warehouse.InstanceId), surplus.Quantity)));
            return new(actor, choice, agreement, term, remaining, travel ? NonLandWorkPhase.ServiceTravel : NonLandWorkPhase.ServiceDelivery,
                surplus.Lot.Id, surplus.Lot.ItemKind, quantity, warehouse.InstanceId, warehouse.Position,
                world.ReachNonLandService(actor, warehouse.Position), null, null);
        }
        var person = world.Agents[actor];
        if (person.Equipment?.Repair is { } progress && progress.LotId == term.TargetId)
            return new(actor, choice, agreement, term, remaining, NonLandWorkPhase.ContinueRepair, term.TargetId, term.ItemKind, 1,
                null, person.Position, [], null, progress);
        return world.PlanNonLandRepair(actor, term.TargetId!) is { } repair ?
            new(actor, choice, agreement, term, remaining, NonLandWorkPhase.StartRepair, term.TargetId, term.ItemKind, 1, null,
                null, [], repair, null) : null;
    }

    internal static ActionCheck CheckNonLandWork(ITownWorld world, NonLandWorkActionPlan plan)
    {
        var capability = ActorCapabilities.Check(world.CapabilitiesFor(plan.Actor), plan.CandidateId,
            new(ActionLifeStages.Adults, AllowUrgentFood: false, AllowUrgentWarmth: false, AllowActiveOrder: true));
        if (!capability.Allowed) return capability;
        if (world.Towns.SingleOrDefault(town => town.Id == plan.Choice.TownId) is not { } town ||
            town.NonLandHearings.Agreements.SingleOrDefault(item => item.Id == plan.Agreement.Id) is not { Status: "pending" or "overdue" } agreement ||
            TownRemedyRules.IsSuperseded(town.NonLandHearings, agreement.Id) ||
            TownHearingProcedure.Digest(agreement with { Status = plan.Agreement.Status }) != TownHearingProcedure.Digest(plan.Agreement) ||
            !agreement.Terms.Contains(plan.Term) || plan.Term.ContributorId != plan.Actor ||
            NonLandRemaining(town.NonLandHearings, agreement, plan.Term) != plan.Remaining || !NonLandWorkAllowed(world, plan.Actor))
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected agreement, remaining contribution or personal work ability has changed");
        if (plan.Phase == NonLandWorkPhase.ReturnGoods)
            return NonLandReturnIsCurrent(world, plan.Term with { Quantity = plan.Quantity }, plan.StockId)
                ? new() : ActionCheck.Refuse(ActionRefusalReason.StockUnavailable, "the selected return goods or recipient are unavailable");
        if (plan.Phase is NonLandWorkPhase.ServiceTravel or NonLandWorkPhase.ServiceDelivery)
        {
            if (world.WarehouseForResident(plan.Actor) is not { } warehouse || warehouse.InstanceId != plan.WarehouseId || warehouse.Position != plan.Destination ||
                warehouse.TownId != town.Id || plan.Term.BeneficiaryId != town.Id || plan.Term.TargetId is { } target && target != warehouse.InstanceId ||
                world.PersonalWarehouseSurplus(plan.Actor, plan.ItemKind, plan.StockId) is not { } selected || selected.Lot.Id != plan.StockId || selected.Quantity < plan.Quantity)
                return ActionCheck.Refuse(ActionRefusalReason.StockUnavailable, "the selected service load or resident Warehouse is unavailable");
            if (plan.Phase == NonLandWorkPhase.ServiceDelivery)
                return world.Agents[plan.Actor].Position == plan.Destination && plan.Quantity > 0 && world.DestinationRoom(warehouse.InstanceId) >= plan.Quantity
                    ? new() : ActionCheck.Refuse(ActionRefusalReason.Capacity, "the selected service load cannot be delivered there now");
            return world.Agents[plan.Actor].Position != plan.Destination && world.ReachNonLandService(plan.Actor, warehouse.Position).Count > 0 &&
                world.DestinationRoom(warehouse.InstanceId) >= plan.Remaining &&
                world.PersonalWarehouseAvailableQuantity(plan.Actor, plan.ItemKind!) - WorldRuntime.CivicSharedRules.WarehouseLoadQuantity >= plan.Remaining ? new() :
                ActionCheck.Refuse(ActionRefusalReason.OutOfReach, "the selected service trip cannot reach its destination now");
        }
        if (plan.Phase == NonLandWorkPhase.StartRepair)
            return plan.Repair is { } repair ? world.CheckNativeActionPlan(repair) :
                ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected native repair preparation is unavailable");
        var current = world.Agents[plan.Actor].Equipment?.Repair;
        return current is not null && plan.RepairProgress is { } original &&
            TownHearingProcedure.Digest(current with { WorkDone = original.WorkDone }) == TownHearingProcedure.Digest(original) &&
            world.NonLandRepairProgressIsCurrent(plan.Actor, original) ? new() :
            ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected repair or its reserved materials are unavailable");
    }

    internal static NonLandReportRelayActionPlan? PlanNonLandReportRelay(ITownWorld world, string actor, TownRuntimeState town,
        TownConductRecord record, string recipient)
    {
        var source = town.NonLandHearings.Acquisitions.FirstOrDefault(item => item.ConductId == record.Id && item.AgentId == actor);
        var parent = town.NonLandHearings.Acquisitions.Where(item => item.ConductId == record.Id && item.AgentId == actor)
            .OrderBy(item => item.Tick).ThenBy(item => item.Id, StringComparer.Ordinal).FirstOrDefault();
        return source is null || parent is null ? null : new(actor, new(town.Id, record.Id, recipient), record, parent, source.Text);
    }

    internal static ActionCheck CheckNonLandReportRelay(ITownWorld world, NonLandReportRelayActionPlan plan)
    {
        var capability = ActorCapabilities.Check(world.CapabilitiesFor(plan.Actor), plan.CandidateId,
            new(ActionLifeStages.NonInfants, AllowBoat: true, AllowUrgentFood: true, AllowUrgentWarmth: true, AllowActiveOrder: true));
        if (!capability.Allowed) return capability;
        return world.Towns.SingleOrDefault(town => town.Id == plan.Choice.TownId) is { } town &&
            town.NonLandHearings.ConductRecords.Any(record => TownHearingProcedure.Digest(record) == TownHearingProcedure.Digest(plan.Conduct)) &&
            town.NonLandHearings.Acquisitions.Contains(plan.Source) && town.NonLandHearings.Acquisitions.Any(item => item.ConductId == plan.Conduct.Id &&
                item.AgentId == plan.Actor && item.Text == plan.Statement) && !town.NonLandHearings.Acquisitions.Any(item => item.ConductId == plan.Conduct.Id && item.AgentId == plan.Choice.Recipient) &&
            world.Agents.TryGetValue(plan.Choice.Recipient, out var recipient) && recipient.AgentId != plan.Actor &&
            world.IsWithinInteractionRange(world.Agents[plan.Actor].Position, recipient.Position, WorldRuntime.CivicSharedRules.ResourceInteractionRange)
                ? new() : ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected known source or nearby listener has changed");
    }
}
