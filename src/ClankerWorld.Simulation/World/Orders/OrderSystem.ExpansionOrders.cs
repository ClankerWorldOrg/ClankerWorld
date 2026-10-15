using System.Collections.ObjectModel;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

internal sealed partial class OrderSystem
{
    internal static PlacedBuilding? ExpansionOrderBuilding(IExpansionOrdersWorld world, OwnerQueuedInstruction instruction)
    {
        if (instruction.Task is not { Action: "expand_building", ExpansionBinding: { } binding } order ||
            !world.IsAdult(instruction.TargetAgentId)) return null;
        var building = world.Simulation.Buildings.SingleOrDefault(item => item.InstanceId == binding.BuildingInstanceId);
        if (building is null || building.DefinitionId != binding.DefinitionId || building.Position != binding.ExpectedPosition ||
            (building.Footprint?.Revision ?? 0) != binding.ExpectedRevision || world.ExpansionOwner(building) != binding.OwnerId ||
            order.TargetPosition is { } requested && requested != binding.ExpectedPosition ||
            WorldBuildingOrderCatalog.Find(world.Content, building.DefinitionId)?.BuildingKind != order.TargetBuildingKind)
            return null;
        return order.TargetBuildingKind is "house" or "animal-yard"
            ? world.HouseholdFor(instruction.TargetAgentId) == building.HouseholdId ? building : null
            : world.TownForResident(instruction.TargetAgentId) == building.TownId ? building : null;
    }

    internal static BuildingExpansionJob? BoundExpansionJob(IExpansionOrdersWorld world, OwnerQueuedInstruction instruction) =>
        instruction.Task?.ExpansionBinding?.JobId is { } id
            ? (world.Simulation.BuildingExpansions ?? []).SingleOrDefault(job => job.JobId == id &&
                world.ExpansionJobMatchesOrder(job, instruction)) : null;

    internal static (GridPoint Position, BuildingFootprintRevision Footprint) ExpansionShapeForOrder(IExpansionOrdersWorld world,
        PlacedBuilding building, OwnerQueuedInstruction? instruction) =>
        world.ExpansionShapes(building).FirstOrDefault(shape =>
            (instruction?.Task?.ExpansionBinding is not { } binding ||
             shape.Position == binding.TargetPosition && shape.Footprint == binding.TargetFootprint) &&
            world.CanFitExpansion(building, shape.Position, shape.Footprint, out _));

    internal static ExpansionOrderPlan? ExpansionPlanFor(IExpansionOrdersWorld world, OwnerQueuedInstruction instruction, AgentState person)
    {
        var actor = instruction.TargetAgentId;
        var order = instruction.Task!;
        if (!world.IsAdult(actor) || !world.ReadyForBriefInteraction(actor)) return null;
        var candidates = order.ExpansionBinding is null
            ? world.Simulation.Buildings.OrderBy(building => building.InstanceId, StringComparer.Ordinal)
            : world.Simulation.Buildings.Where(building => ExpansionOrderBuilding(world, instruction)?.InstanceId == building.InstanceId);
        foreach (var building in candidates)
        {
            if (WorldBuildingOrderCatalog.Find(world.Content, building.DefinitionId)?.BuildingKind != order.TargetBuildingKind ||
                order.TargetPosition is { } position && position != building.Position ||
                !world.MayExpandBuilding(actor, building, out _) ||
                person.Position != building.Position && world.Reach(actor, building.Position, ReachPurpose.BuildingExpansion, 0, 0, SwimmingPolicy.WhenCapable).Count == 0)
                continue;
            var shape = ExpansionShapeForOrder(world, building, instruction);
            if (shape.Footprint is null) continue;
            var definition = world.Content.Buildings.Single(item => item.CanonicalId == building.DefinitionId);
            var costs = BuildingStorageRules.ExpansionCosts(definition, building, shape.Footprint);
            if (!world.CanAcquireProjectInputs(costs, world.HouseholdFor(actor), actor) ||
                !world.CanAcquireExpansionMaterials(actor, building, costs)) continue;
            return new(building, shape.Position, shape.Footprint);
        }
        return null;
    }

    internal static CognitionCandidate? ExpansionOrderCandidateFor(IExpansionOrdersWorld world, OwnerQueuedInstruction instruction, AgentState person)
    {
        if (!world.ReadyForBriefInteraction(instruction.TargetAgentId)) return null;
        if (BoundExpansionJob(world, instruction) is { State: WorldProductionJobState.Running or WorldProductionJobState.Paused } job)
            return ExpansionOrderBuilding(world, instruction) is { } building &&
                world.CanFitExpansion(building, job.TargetPosition, job.TargetFootprint, out _, job.JobId) &&
                ExpansionOrderInputsAvailable(world, job, building) &&
                (person.Position == building.Position || world.Reach(person.AgentId, building.Position, ReachPurpose.BuildingExpansion, 0, 0, SwimmingPolicy.WhenCapable).Count > 0)
                ? new("expand_building", "Continue the ordered expansion using its committed materials.", 0, job.BuildingInstanceId) : null;
        return ExpansionPlanFor(world, instruction, person) is { } plan
            ? new("expand_building", "Bring the materials and complete the building's next permitted expansion.", 0, plan.Building.InstanceId) : null;
    }

    internal static void ExecuteExpansionOrderStep(IExpansionOrdersWorld world, OwnerQueuedInstruction instruction, AgentState person)
    {
        instruction = world.Orders.Instructions[instruction.IdempotencyKey];
        var actor = instruction.TargetAgentId;
        if (world.NeedsUrgentFood(person) || world.NeedsUrgentWarmth(person))
        {
            PauseExpansionForOrder(world, instruction);
            world.SetOrderStatus(instruction, "interrupted", "Food or warmth needs come first.");
            return;
        }
        if (BoundExpansionJob(world, instruction) is { State: WorldProductionJobState.Running or WorldProductionJobState.Paused } job)
        {
            if (ExpansionOrderCandidateFor(world, instruction, person) is null || ExpansionOrderBuilding(world, instruction) is not { } building)
            {
                PauseExpansionForOrder(world, instruction);
                world.SetOrderStatus(instruction, "blocked", ExpansionOrderBlockedReason(world, instruction));
                return;
            }
            if (person.Position != building.Position)
            {
                PauseExpansionForOrder(world, instruction);
                world.MoveToward(actor, person, building.Position, "building_expansion", 0);
                return;
            }
            if (job.State == WorldProductionJobState.Paused)
            {
                var deadline = checked(world.WorldTick + job.CompletionTick - job.PausedAtTick!.Value);
                world.ExtendOrderReservations(job.InputReservationIds, deadline);
                world.ResumeExpansion(job.JobId, deadline);
                world.AppendEvent("expansion_order_resumed", $"{instruction.InstructionId}:{job.JobId}");
            }
            return;
        }
        if (ExpansionPlanFor(world, instruction, person) is not { } plan)
        {
            world.SetOrderStatus(instruction, "blocked", ExpansionOrderBlockedReason(world, instruction));
            return;
        }
        if (instruction.Task!.ExpansionBinding is null)
        {
            instruction = OrderStore.PrepareTaskBinding(instruction, OrderStore.PrepareExpansionBinding(instruction.Task, OrderStore.CreateOwnerBuildingExpansionBinding(plan.Building.InstanceId, plan.Building.DefinitionId, world.ExpansionOwner(plan.Building), plan.Building.Position, plan.Building.Footprint?.Revision ?? 0, plan.Position, plan.Footprint)));
            world.Orders.RecordExpansionOrderStep(instruction.IdempotencyKey, instruction);
            world.MarkCheckpointCurrent();
        }
        world.ApplyBuildingExpansionCandidate(actor, person, plan.Building.InstanceId, instruction);
    }

    internal static void BindStartedExpansionOrder(IExpansionOrdersWorld world, OwnerQueuedInstruction instruction, BuildingExpansionJob job)
    {
        var current = world.Orders.Instructions[instruction.IdempotencyKey];
        world.Orders.RecordStartedExpansionOrder(instruction.IdempotencyKey, OrderStore.PrepareTaskBinding(current, OrderStore.PrepareExpansionBinding(current.Task!, OrderStore.PrepareExpansionJob(current.Task!.ExpansionBinding!, job.JobId))));
    }

    internal static bool ExpansionOrderInputsAvailable(IExpansionOrdersWorld world, BuildingExpansionJob job, PlacedBuilding building)
    {
        var inventory = world.Society.Inventory;
        var site = world.ExpansionGroundPosition(building);
        return job.InputReservationIds.All(id =>
        {
            var reservation = inventory.Reservations.SingleOrDefault(item => item.Id == id);
            var lot = reservation is null ? null : inventory.Lots.SingleOrDefault(item => item.Id == reservation.LotId);
            return reservation is { State: InventoryReservationState.Reserved } &&
                lot is { ConditionBasisPoints: > 0, FreshnessBasisPoints: > 0 } && lot.Quantity >= reservation.Quantity &&
                reservation.OwnerId == lot.OwnerId && lot.DeliveryBuildingId is null &&
                (lot.OwnerId == job.OwnerId && (lot.StorageBuildingId == building.InstanceId || lot.GroundPosition == site) ||
                 lot.OwnerId == job.WorkerId && PersonalEquipmentRules.IsCarried(lot, job.WorkerId));
        });
    }

    internal static void MaintainExpansionOrdersBeforeTick(IExpansionOrdersWorld world)
    {
        foreach (var instruction in world.Orders.Instructions.Values.Where(item =>
                     item.Task is { Action: "expand_building" } order && world.IsActiveOrder(order.Status)).ToArray())
        {
            if (BoundExpansionJob(world, instruction) is not { State: WorldProductionJobState.Running or WorldProductionJobState.Paused } job) continue;
            if (!world.Agents.TryGetValue(instruction.TargetAgentId, out var person) ||
                ExpansionOrderBuilding(world, instruction) is not { } building || !ExpansionOrderInputsAvailable(world, job, building))
            {
                CancelExpansionForOrder(world, instruction);
                world.SetOrderStatus(instruction, "blocked", "The selected building, worker or committed materials are no longer available.");
                continue;
            }
            if (world.NeedsUrgentFood(person) || world.NeedsUrgentWarmth(person))
            {
                PauseExpansionForOrder(world, instruction);
                world.SetOrderStatus(instruction, "interrupted", "Food or warmth needs come first.");
            }
            else if (person.Position != building.Position || world.IsConversationBusy(person.AgentId) ||
                     instruction.Task!.WaitForDecisionAfterFailure)
                PauseExpansionForOrder(world, instruction);
        }
    }

    internal static void PauseExpansionForOrder(IExpansionOrdersWorld world, OwnerQueuedInstruction instruction)
    {
        if (BoundExpansionJob(world, instruction) is not { State: WorldProductionJobState.Running } job) return;
        world.HoldOrderReservations(job.InputReservationIds);
        world.PauseExpansion(job.JobId, world.WorldTick);
        world.AppendEvent("expansion_order_paused", $"{instruction.InstructionId}:{job.JobId}");
    }

    internal static void CancelExpansionForOrder(IExpansionOrdersWorld world, OwnerQueuedInstruction instruction)
    {
        if (BoundExpansionJob(world, instruction) is not { State: WorldProductionJobState.Running or WorldProductionJobState.Paused } job) return;
        world.ReleaseExpansionOrderInputs(job);
        world.CancelExpansions(new HashSet<string>(StringComparer.Ordinal) { job.JobId }, "The expansion order stopped.");
        world.AppendEvent("building_expansion_cancelled", $"{job.BuildingInstanceId}:{job.JobId}:The expansion order stopped.");
    }

    internal static void CreditExpansionOrderJob(IExpansionOrdersWorld world, BuildingExpansionJob job)
    {
        if (job.OrderInstructionId is not { } id) return;
        var instruction = world.Orders.Instructions.Values.SingleOrDefault(item => item.InstructionId == id);
        if (instruction is null || BoundExpansionJob(world, instruction) is not { State: WorldProductionJobState.Completed } completed ||
            completed.JobId != job.JobId) return;
        world.CreditOrderEffect(instruction, world.ExpansionOrderReceipt(job.JobId), 1);
    }

    internal static string ExpansionOrderBlockedReason(IExpansionOrdersWorld world, OwnerQueuedInstruction instruction)
    {
        if (!world.IsAdult(instruction.TargetAgentId)) return "An adult resident is needed to expand a building.";
        if (instruction.Task!.ExpansionBinding is not null && ExpansionOrderBuilding(world, instruction) is null)
            return "The selected building moved, changed owner or changed size; this order keeps its original building and expansion.";
        if (instruction.Task.ExpansionBinding is { } binding && ExpansionOrderBuilding(world, instruction) is { } building &&
            !world.CanFitExpansion(building, binding.TargetPosition, binding.TargetFootprint, out var failure, binding.JobId))
            return failure;
        return "The building needs more space, a permitted expansion site, reachable materials and a worker who may build there before this order can continue.";
    }
}
