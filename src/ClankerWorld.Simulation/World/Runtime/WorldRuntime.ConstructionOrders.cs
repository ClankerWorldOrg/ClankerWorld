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
        OrderSystem.BuildingIdentityIsReserved(ConstructionOrdersWorld, instanceId, constructionInstructionId);

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
        out BuildingDefinition? definition, out string? household, out GridPoint position, out string failure) =>
        OrderSystem.TryConstructionOrderSite(ConstructionOrdersWorld, instruction, person, out definition, out household, out position, out failure);

    private CognitionCandidate? ConstructionOrderCandidateFor(OwnerQueuedInstruction instruction, AgentState person) =>
        OrderSystem.ConstructionOrderCandidateFor(ConstructionOrdersWorld, instruction, person);

    private string ConstructionOrderBlockedReason(OwnerQueuedInstruction instruction, AgentState person) =>
        OrderSystem.ConstructionOrderBlockedReason(ConstructionOrdersWorld, instruction, person);

    private void ExecuteConstructionOrderStep(OwnerQueuedInstruction instruction, AgentState person) =>
        OrderSystem.ExecuteConstructionOrderStep(ConstructionOrdersWorld, instruction, person);

    private void PauseConstructionForOrder(OwnerQueuedInstruction instruction) =>
        OrderSystem.PauseConstructionForOrder(ConstructionOrdersWorld, instruction);

    private void CancelConstructionForOrder(OwnerQueuedInstruction instruction) =>
        OrderSystem.CancelConstructionForOrder(ConstructionOrdersWorld, instruction);

    private void MaintainConstructionOrdersBeforeTick() =>
        OrderSystem.MaintainConstructionOrdersBeforeTick(ConstructionOrdersWorld);

    private OwnerQueuedInstruction? ConstructionInstructionForProject(string actor, AgentProject? project) =>
        OrderSystem.ConstructionInstructionForProject(ConstructionOrdersWorld, actor, project);

    private void RecordConstructionOrderCompletion(OwnerQueuedInstruction instruction, string materialOwner) =>
        OrderSystem.RecordConstructionOrderCompletion(ConstructionOrdersWorld, instruction, materialOwner);

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
