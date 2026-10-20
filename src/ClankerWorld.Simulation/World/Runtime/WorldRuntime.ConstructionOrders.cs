using System.Security.Cryptography;
using System.Text;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

/// <summary>Recent owner construction evidence survives removal and reassignment until its order retires.</summary>
public sealed record WorldConstructionReceipt(
    Identity InstructionId, string ActorId, string BuildingInstanceId, string DefinitionId,
    string OwnerId, string MaterialOwnerId, GridPoint Position, long StartedTick,
    long CompletedTick, IReadOnlyList<Identity> InputReservationIds);

public sealed partial class WorldRuntime
{
    private void RetireClosedOrderProjectReferences(HashSet<string> retired)
    {
        foreach (var person in agents.Values.Where(person => person.Project?.OrderInstructionId is { } id && retired.Contains(id)).ToArray())
            agentStore.RecordProject(person.AgentId, person, person.Project! with { OrderInstructionId = null });
    }

    private static string ConstructionInstanceId(string instructionId) =>
        DerivedId.Hash(instructionId, "ordered-building-");

    private bool BuildingIdentityIsReserved(string instanceId, string? constructionInstructionId = null) =>
        instanceId.StartsWith("ordered-building-", StringComparison.Ordinal) &&
            (constructionInstructionId is null || ConstructionInstanceId(constructionInstructionId) != instanceId) ||
        (worldSimulation.ConstructionReceipts ?? []).Any(receipt => receipt.BuildingInstanceId == instanceId) ||
        instructionsByIdempotency.Values.Any(instruction => instruction.Task is { } order &&
            (order.ExpansionBinding?.BuildingInstanceId == instanceId ||
             order.ShelterBinding is { } shelter && shelter.BuildingInstanceId == instanceId &&
                 shelter.BuildingPlacedTick == WorldTick ||
             order.ConstructionInstanceId == instanceId && instruction.InstructionId != constructionInstructionId));

    private static string ConstructionOrderReceipt(string instanceId) =>
        DerivedId.Hash(instanceId, "construction:building:");

    private static bool IsConstructionOrderProject(OwnerQueuedInstruction instruction, AgentProject? project) =>
        instruction.Task is
        {
            Action: "construct_building", ConstructionPosition: { } position,
            ConstructionStartedTick: { } started
        } order &&
        project is not null && project.OrderInstructionId == instruction.InstructionId &&
        project.StartedTick == started && project.JobId is null &&
        project.CandidateId == TownConstructionCandidateIds.Building(order.TargetDefinitionId!, position);

    private bool TryConstructionOrderSite(OwnerQueuedInstruction instruction, AgentState person,
        out BuildingDefinition? definition, out string? household, out GridPoint position, out string failure)
    {
        var order = instruction.Task!;
        var actor = instruction.TargetAgentId;
        var catalog = WorldBuildingOrderCatalog.Find(worldContent, order.TargetDefinitionId);
        definition = catalog?.Definition;
        household = society.Checkpoint.GetAgent(actor).HouseholdId;
        position = default;
        failure = "The requested building design is no longer active.";
        if (definition is null || !WorldBuildingOrderCatalog.Supports("construct_building", catalog!.BuildingKind)) return false;
        failure = "Only an adult household member can construct this building.";
        if (!IsAdult(actor) || household is null) return false;
        failure = "The household this building was ordered for has changed.";
        if (order.ConstructionOwnerId is { } owner && owner != household) return false;
        failure = "Another project is already in progress; finish or stop it before starting this construction.";
        if (person.Project is { Stage: not ("completed" or "cancelled") } project &&
            !IsConstructionOrderProject(instruction, project) && !OrdinaryProjectCanYieldToOrder(project)) return false;
        var kind = HouseholdBuildingKind(definition);
        failure = "The Town already has enough shops of this kind for its residents.";
        if (!(IsConstructionOrderProject(instruction, person.Project) &&
              person.Project is { Stage: not ("completed" or "cancelled") }) &&
            !TownHasRoomForSharedShop(actor, kind)) return false;
        failure = "This household already holds the requested building.";
        if (HouseholdBuildingWithTag(household, kind!) is not null) return false;
        failure = "Only a household holding a Farmhouse can build a Silo.";
        if (kind == "silo" && FarmhouseForHousehold(household) is null) return false;
        failure = "The household needs a completed House before it can build an animal yard.";
        if (kind == AnimalContent.YardTag && HouseForHousehold(household) is null) return false;
        failure = "This building has no reachable inputs for its work.";
        if (!WorkBuildingHasInputs(actor, household, definition)) return false;
        failure = "Another household member is already planning this building.";
        var householdId = household;
        if (agents.Values.Any(other => other.AgentId != actor &&
                other.Project is { Stage: not ("completed" or "cancelled") } otherProject &&
                society.Checkpoint.GetAgent(other.AgentId).HouseholdId == householdId &&
                TownConstructionCandidateIds.TryParse(otherProject.CandidateId, out var selection) && selection.IsBuilding &&
                worldContent.Buildings.Any(item => item.CanonicalId == selection.DefinitionId && HouseholdBuildingKind(item) == kind)))
            return false;
        var target = order.ConstructionPosition ?? order.TargetPosition;
        var layout = CreateTownLayoutContext(actor, target, definition);
        failure = target is null ? "No site where this building is allowed is free right now."
            : "The requested site isn't allowed for this building or can't be reached.";
        if (target is { } requested)
        {
            if (!TownLayoutService.TryEvaluateConstructionSite(layout, definition, requested, out _)) return false;
            position = requested;
        }
        else
        {
            var choices = TownLayoutService.RankConstructionSites(layout, definition);
            if (choices.Count == 0) return false;
            position = choices[0].Position;
        }
        failure = string.Empty;
        return true;
    }

    private CognitionCandidate? ConstructionOrderCandidateFor(OwnerQueuedInstruction instruction, AgentState person) =>
        ReadyForBriefInteraction(instruction.TargetAgentId) &&
        TryConstructionOrderSite(instruction, person, out _, out _, out var position, out _)
            ? new("construct_building", "Prepare and complete the requested household building at its selected site.",
                0, $"build-site:{position.X},{position.Y}") : null;

    private string ConstructionOrderBlockedReason(OwnerQueuedInstruction instruction, AgentState person)
    {
        if (!TryConstructionOrderSite(instruction, person, out _, out _, out _, out var failure)) return failure;
        return IsConstructionOrderProject(instruction, person.Project) && person.Project?.Blocker is { } blocker
            ? blocker : "The ordered construction is waiting for usable materials or access to its site.";
    }

    private void ExecuteConstructionOrderStep(OwnerQueuedInstruction instruction, AgentState person)
    {
        instruction = instructionsByIdempotency[instruction.IdempotencyKey];
        var actor = instruction.TargetAgentId;
        if (NeedsUrgentFood(person) || NeedsUrgentWarmth(person))
        {
            SetOrderStatus(instruction, OrderStatus.Interrupted, "Food or warmth needs come first.");
            return;
        }
        if (!TryConstructionOrderSite(instruction, person, out var definition, out var household, out var position, out var failure))
        {
            SetOrderStatus(instruction, OrderStatus.Blocked, failure);
            return;
        }
        if (!IsConstructionOrderProject(instruction, person.Project) || person.Project!.Stage == "cancelled")
        {
            var order = instruction.Task!;
            var started = order.ConstructionStartedTick ?? WorldTick;
            order = order.UpdateDefinition(definition => definition switch
{
    ConstructBuildingOrderDefinition task => task with { ConstructionOwnerId = household, ConstructionPosition = position, ConstructionStartedTick = started, ConstructionInstanceId = ConstructionInstanceId(instruction.InstructionId) },
    _ => throw new InvalidOperationException("This order kind does not own the requested fields."),
});
            instruction = instruction with { Task = order };
            instructionsByIdempotency[instruction.IdempotencyKey] = instruction;
            var project = new AgentProject(TownConstructionCandidateIds.Building(definition!.CanonicalId, position),
                definition.DisplayName, started, "acquiring", LastTransitionTick: WorldTick,
                OrderInstructionId: instruction.InstructionId);
            agentStore.RecordProject(actor, person, project);
            checkpointSchemaVersion = StateSchemaVersion;
            AppendEvent(new ProjectChosenEventPayload(actor, project.CandidateId));
        }
        if (agents[actor].Project is { Stage: "paused" } paused)
            SetProject(actor, paused with { Stage = "acquiring", Blocker = null, RequiresFreshChoice = false });
        ContinueProject(actor, agents[actor]);
        if (agents[actor].Project is { Stage: "blocked" or "cancelled" } blocked)
            SetOrderStatus(instruction, OrderStatus.Blocked, blocked.Blocker ?? "The ordered construction cannot continue yet.");
    }

    private void PauseConstructionForOrder(OwnerQueuedInstruction instruction)
    {
        if (agents.TryGetValue(instruction.TargetAgentId, out var person) &&
            IsConstructionOrderProject(instruction, person.Project) &&
            person.Project is { Stage: not ("completed" or "cancelled" or "paused") } project)
            SetProject(person.AgentId, project with { Stage = "paused", Blocker = "Food or warmth needs come first." });
    }

    private void CancelConstructionForOrder(OwnerQueuedInstruction instruction)
    {
        if (agents.TryGetValue(instruction.TargetAgentId, out var person) &&
            IsConstructionOrderProject(instruction, person.Project) &&
            person.Project is { Stage: not ("completed" or "cancelled") } project)
            SetProject(person.AgentId, project with { Stage = "cancelled", Blocker = "The construction order stopped." });
    }

    private void MaintainConstructionOrdersBeforeTick()
    {
        foreach (var instruction in instructionsByIdempotency.Values.Where(item =>
                     item.Task is { Action: "construct_building", ConstructionOwnerId: not null } order &&
                     IsActiveOrder(order.Status)).ToArray())
        {
            if (!agents.TryGetValue(instruction.TargetAgentId, out var person) ||
                !IsAdult(instruction.TargetAgentId) ||
                society.Checkpoint.GetAgent(instruction.TargetAgentId).HouseholdId != instruction.Task!.ConstructionOwnerId)
            {
                CancelConstructionForOrder(instruction);
                SetOrderStatus(instruction, OrderStatus.Blocked, "The worker or household for this building is no longer available.");
            }
            else if (NeedsUrgentFood(person) || NeedsUrgentWarmth(person))
                SetOrderStatus(instruction, OrderStatus.Interrupted, "Food or warmth needs come first.");
        }
    }

    private OwnerQueuedInstruction? ConstructionInstructionForProject(string actor, AgentProject? project) =>
        project?.OrderInstructionId is { } id
            ? instructionsByIdempotency.Values.FirstOrDefault(instruction => instruction.InstructionId == id &&
                instruction.TargetAgentId == actor && instruction.Task is { } order && IsActiveOrder(order.Status) &&
                IsConstructionOrderProject(instruction, project)) : null;

    private void RecordConstructionOrderCompletion(OwnerQueuedInstruction instruction, string materialOwner)
    {
        var order = instruction.Task!;
        var purpose = "building:" + order.ConstructionInstanceId;
        var receipt = new WorldConstructionReceipt(Identity.Parse(instruction.InstructionId), instruction.TargetAgentId,
            order.ConstructionInstanceId!, order.TargetDefinitionId!, order.ConstructionOwnerId!, materialOwner,
            order.ConstructionPosition!.Value, order.ConstructionStartedTick!.Value, WorldTick,
            society.Checkpoint.Inventory.Reservations.Where(item => item.Purpose == purpose)
                .Select(item => item.Id).Order().ToArray());
        constructionSystem.Buildings.RecordConstructionReceipt(receipt);
        CreditOrderEffect(instruction, Identity.Parse(ConstructionOrderReceipt(receipt.BuildingInstanceId)), 1);
    }

    private static void ValidateConstructionOrderBindings(WorldContentSimulationState? simulation,
        DeclarativeWorldContentState content, SocietyCheckpoint society,
        IEnumerable<AgentState> agents, IEnumerable<OwnerQueuedInstruction> instructions, long worldTick)
    {
        var savedInstructions = instructions.ToArray();
        if (savedInstructions.Any(item => item is null || !IdentityRules.Reference(item.InstructionId, int.MaxValue, requireTrimmed: true)) ||
            savedInstructions.Select(item => item.InstructionId).Distinct(StringComparer.Ordinal).Count() != savedInstructions.Length)
            throw new InvalidDataException("Construction orders require unique instruction IDs.");
        var instructionById = savedInstructions.ToDictionary(item => item.InstructionId, StringComparer.Ordinal);
        var receipts = simulation?.ConstructionReceipts ?? [];
        BuildingStore.ValidateReceipts(receipts);
        var householdIds = society.Households.Select(item => item.Id).ToHashSet(StringComparer.Ordinal);
        foreach (var receipt in receipts)
        {
            if (!instructionById.TryGetValue(receipt.InstructionId, out var instruction) ||
                instruction.Task is not { Action: "construct_building", CompletedUnits: 1, Status: "finished" } order ||
                receipt.ActorId != instruction.TargetAgentId || receipt.DefinitionId != order.TargetDefinitionId ||
                receipt.BuildingInstanceId != ConstructionInstanceId(instruction.InstructionId) ||
                receipt.BuildingInstanceId != order.ConstructionInstanceId || receipt.OwnerId != order.ConstructionOwnerId ||
                receipt.Position != order.ConstructionPosition || receipt.StartedTick != order.ConstructionStartedTick ||
                receipt.StartedTick < instruction.SubmittedTick || receipt.CompletedTick > worldTick ||
                !householdIds.Contains(receipt.OwnerId) ||
                receipt.MaterialOwnerId != receipt.OwnerId && receipt.MaterialOwnerId != receipt.ActorId ||
                WorldBuildingOrderCatalog.Find(content, receipt.DefinitionId) is not { } catalog ||
                !WorldBuildingOrderCatalog.Supports("construct_building", catalog.BuildingKind) ||
                receipt.MaterialOwnerId == receipt.ActorId && !catalog.Definition.Tags.Contains("house", StringComparer.Ordinal))
                throw new InvalidDataException("A construction receipt does not match its completed owner order.");
            var purpose = "building:" + receipt.BuildingInstanceId;
            var definition = catalog.Definition;
            var paid = society.Inventory.Reservations.Where(item => item.Purpose == purpose).ToArray();
            if (!paid.Select(item => item.Id).ToHashSet().SetEquals(receipt.InputReservationIds) ||
                paid.Any(item => item.State != InventoryReservationState.Completed || item.OwnerId != receipt.MaterialOwnerId ||
                    item.ExpiryTick != receipt.CompletedTick) ||
                definition.BuildCosts.Select((cost, index) => (cost, index)).Any(entry =>
                    paid.Where(item => item.Id == ProductionInputReservationId(purpose, entry.index, item.LotId))
                        .Sum(item => (long)item.Quantity) != entry.cost.Amount) ||
                paid.Any(item => !definition.BuildCosts.Select((_, index) => index).Any(index =>
                    item.Id == ProductionInputReservationId(purpose, index, item.LotId))))
                throw new InvalidDataException("A construction receipt lacks its actual completed material payments.");
            if (simulation!.Buildings.FirstOrDefault(item => item.InstanceId == receipt.BuildingInstanceId) is { } placed &&
                (placed.DefinitionId != receipt.DefinitionId || placed.PlacedTick != receipt.CompletedTick))
                throw new InvalidDataException("A constructed building differs from its completion receipt.");
        }
        foreach (var instruction in savedInstructions.Where(item => item.Task?.Action == "construct_building"))
        {
            var order = instruction.Task!;
            if (WorldBuildingOrderCatalog.Find(content, order.TargetDefinitionId) is null ||
                order.ConstructionOwnerId is { } owner && !householdIds.Contains(owner) ||
                order.ConstructionInstanceId is { } instance && instance != ConstructionInstanceId(instruction.InstructionId))
                throw new InvalidDataException("A construction order has an invalid definition or household binding.");
            var receipt = receipts.FirstOrDefault(item => item.InstructionId == instruction.InstructionId);
            if (order.CompletedUnits != (receipt is null ? 0 : 1) ||
                order.LastEffectId != (receipt is null ? null : ConstructionOrderReceipt(receipt.BuildingInstanceId)))
                throw new InvalidDataException("Construction progress requires an actual paid completion receipt.");
        }
        foreach (var person in agents.Where(item => item.Project?.OrderInstructionId is not null))
        {
            var project = person.Project!;
            if (!instructionById.TryGetValue(project.OrderInstructionId!, out var instruction) ||
                instruction.TargetAgentId != person.AgentId || !IsConstructionOrderProject(instruction, project) ||
                project.Stage == "completed" && (project.WorkDone != ProjectWorkTicks ||
                    !receipts.Any(item => item.InstructionId == instruction.InstructionId)) ||
                project.Stage is not ("completed" or "cancelled") && !IsActiveOrder(instruction.Task!.Status))
                throw new InvalidDataException("An ordered construction project does not match its saved order.");
        }
    }

    private static bool ValidConstructionIdentity(string? value) => BuildingStore.ValidConstructionIdentity(value);
}
