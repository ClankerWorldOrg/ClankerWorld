using ClankerWorld.Simulation.Kernel;

namespace ClankerWorld.Simulation.World;

internal sealed partial class ConstructionSystem
{
    private const int BuildingExpansionTicks = 20;

    internal ProductionStartResult StartExpansion(IExpansionAdmissionWorld world, string actor, string buildingId,
        OwnerQueuedInstruction? orderInstruction = null, ExpansionActionPlan? originalPlan = null)
    {
        var building = State.Buildings.SingleOrDefault(item => item.InstanceId == buildingId);
        if (building is null) return ProductionStartResult.Rejected(buildingId, "The building is not placed.");
        if (orderInstruction is not null &&
            (world.PendingInstructionFor(actor)?.InstructionId != orderInstruction.InstructionId ||
             world.ExpansionOrderBuilding(orderInstruction)?.InstanceId != buildingId))
            return ProductionStartResult.Rejected(buildingId, "The expansion order no longer matches this building.");
        if (!world.MayExpandBuilding(actor, building, out var failure)) return ProductionStartResult.Rejected(buildingId, failure);
        if (world.Agents[actor].Position != building.Position)
            return ProductionStartResult.Rejected(buildingId, "The worker must be at the building before expansion starts.");
        var plan = originalPlan ?? world.PlanExpansion(actor, building, orderInstruction);
        if (plan is null)
            return ProductionStartResult.Rejected(buildingId, "There is no legal space for this building's next footprint.");
        if (world.CheckExpansion(plan).Refusal is { } refusal)
            return ProductionStartResult.Rejected(buildingId, refusal.Explanation);
        var shape = (Position: plan.TargetPosition, Footprint: plan.TargetFootprint);
        var definition = world.Content.Buildings.Single(item => item.CanonicalId == building.DefinitionId);
        var owner = building.HouseholdId ?? building.TownId!;
        var costs = BuildingStorageRules.ExpansionCosts(definition, building, shape.Footprint);
        if (!world.HasExpansionMaterials(actor, building, costs))
            return ProductionStartResult.Rejected(buildingId, "Bring the expansion materials to the building or carry them to the site.");
        var jobId = world.ExpansionJobId(world.NextProductionJobSequence);
        var completion = world.WorldTick + world.SkilledBuildingWorkTicks(actor, BuildingExpansionTicks);
        var selectedInputs = plan.Inputs ?? world.PlanExpansionInputs(actor, building, costs);
        if (selectedInputs is null) return ProductionStartResult.Rejected(buildingId, "The expansion materials are no longer available.");
        var reservations = world.ReserveExpansionInputs(selectedInputs, actor, jobId, completion);
        var job = new BuildingExpansionJob(jobId, buildingId, actor, owner, building.Footprint?.Revision ?? 0,
            building.Position, shape.Position, shape.Footprint, world.WorldTick, completion, WorldProductionJobState.Running, reservations,
            DefinitionId: building.DefinitionId)
        { OrderInstructionId = orderInstruction?.InstructionId };
        Buildings.StartExpansion(job);
        world.AdvanceExpansionSequence();
        if (orderInstruction is not null) world.BindStartedExpansionOrder(orderInstruction, job);
        world.AppendEvent(new BuildingExpansionStartedEventPayload(actor, buildingId, jobId));
        return new ProductionStartResult(true, jobId, buildingId, null);
    }

    internal void ProcessExpansions(IExpansionCompletionWorld world, long targetTick)
    {
        foreach (var job in (State.Expansions ?? []).Where(item => item.State == WorldProductionJobState.Running).ToArray())
        {
            var building = State.Buildings.SingleOrDefault(item => item.InstanceId == job.BuildingInstanceId);
            string? failure = null;
            if (building is null || building.Position != job.ExpectedPosition ||
                (building.Footprint?.Revision ?? 0) != job.ExpectedRevision) failure = "The building changed before its expansion completed.";
            else if (!world.IsAdult(job.WorkerId) ||
                building.HouseholdId is { } houseOwner && (houseOwner != job.OwnerId || world.HouseholdFor(job.WorkerId) != houseOwner) ||
                building.HouseholdId is null && world.TownForResident(job.WorkerId) != building.TownId)
                failure = "The worker no longer has permission to expand this building.";
            else if (!world.CanFitExpansion(building, job.TargetPosition, job.TargetFootprint, out var fitFailure, job.JobId))
                failure = fitFailure;
            else if (job.InputReservationIds.Any(id => world.Inventory.GetReservation(id) is not
            { State: InventoryReservationState.Reserved, ExpiryTick: var expiry } || expiry < targetTick ||
                world.Inventory.GetLot(world.Inventory.GetReservation(id).LotId) is not
                { ConditionBasisPoints: > 0, FreshnessBasisPoints: > 0 }))
                failure = "The expansion materials are no longer usable.";
            if (failure is null && job.CompletionTick > targetTick) continue;
            if (failure is null) world.CompleteExpansionInputs(job.InputReservationIds);
            else world.CancelExpansionInputs(job.InputReservationIds);
            if (failure is null)
            {
                var expanded = building! with { Position = job.TargetPosition, Footprint = job.TargetFootprint };
                Buildings.ApplyExpandedFootprint(expanded);
                world.RecordExpandedWork(expanded, job);
            }
            Buildings.FinishExpansion(job.JobId, failure);
            if (failure is null) world.CreditExpansionOrderJob(job);
            world.AppendEvent(failure is null ? new BuildingExpandedEventPayload(job.WorkerId, job.BuildingInstanceId, job.JobId) : new BuildingExpansionCancelledEventPayload(job.WorkerId, job.BuildingInstanceId, job.JobId, failure));
        }
    }
}
