using ExpansionOrderPlan = ClankerWorld.Simulation.World.OrderSystem.ExpansionOrderPlan;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{

    private static bool ExpansionJobMatchesOrder(BuildingExpansionJob job, OwnerQueuedInstruction instruction) =>
        instruction.Task is { Action: "expand_building", ExpansionBinding: { } binding } &&
        job.OrderInstructionId == instruction.InstructionId && job.WorkerId == instruction.TargetAgentId &&
        job.BuildingInstanceId == binding.BuildingInstanceId && job.DefinitionId == binding.DefinitionId &&
        job.OwnerId == binding.OwnerId && job.ExpectedPosition == binding.ExpectedPosition &&
        job.ExpectedRevision == binding.ExpectedRevision && job.TargetPosition == binding.TargetPosition &&
        job.TargetFootprint == binding.TargetFootprint && job.StartedTick >= instruction.SubmittedTick;

    private static string ExpansionOrderReceipt(string jobId) =>
        DerivedId.Hash(jobId, "expand:job:");

    private static void ValidateExpansionOrderBindings(WorldContentSimulationState? simulation,
        DeclarativeWorldContentState content, SocietyCheckpoint society, IReadOnlyList<TownRuntimeState>? savedTowns,
        IEnumerable<OwnerQueuedInstruction> instructions, long worldTick)
    {
        var expansionInstructions = instructions.Where(item => item.Task?.Action == "expand_building").ToArray();
        if (expansionInstructions.Select(item => item.InstructionId).Distinct(StringComparer.Ordinal).Count() != expansionInstructions.Length)
            throw new InvalidDataException("Expansion order identities are not unique.");
        var orders = expansionInstructions.ToDictionary(item => item.InstructionId, StringComparer.Ordinal);
        var jobs = simulation?.BuildingExpansions ?? [];
        foreach (var job in jobs.Where(item => item.OrderInstructionId is not null))
        {
            if (!orders.TryGetValue(job.OrderInstructionId!, out var instruction) || !ExpansionJobMatchesOrder(job, instruction) ||
                job.State is WorldProductionJobState.Running or WorldProductionJobState.Paused &&
                    (!IsActiveOrder(instruction.Task!.Status) || instruction.Task.Status == "queued" ||
                     instruction.Task.ExpansionBinding!.JobId != job.JobId) ||
                job.State == WorldProductionJobState.Completed && job.CompletionTick > worldTick)
                throw new InvalidDataException("An expansion job does not belong to its recorded owner order.");
        }
        foreach (var instruction in orders.Values)
        {
            var order = instruction.Task!;
            if (order.ExpansionBinding is { } binding)
            {
                var entry = WorldBuildingOrderCatalog.Find(content, binding.DefinitionId);
                if (simulation is null || !ValidExpansionBindingId(binding.BuildingInstanceId) || !ValidExpansionBindingId(binding.OwnerId) ||
                    entry is null || entry.BuildingKind != order.TargetBuildingKind ||
                    !(order.TargetBuildingKind is "house" or "animal-yard" ? society.Households.Any(home => home.Id == binding.OwnerId) :
                        savedTowns?.Any(town => town.Id == binding.OwnerId) == true) ||
                    !ValidExpansionBindingPosition(binding.ExpectedPosition) || !ValidExpansionBindingPosition(binding.TargetPosition) ||
                    order.TargetPosition is { } requested && requested != binding.ExpectedPosition ||
                    binding.TargetFootprint is null || !BuildingStorageRules.IsSupported(entry.Definition, binding.TargetFootprint) ||
                    binding.ExpectedRevision != binding.TargetFootprint.Revision - 1 ||
                    !PlausibleExpansionAnchors(binding) ||
                    binding.JobId is { } id && (!ValidExpansionBindingId(id) ||
                        !jobs.Any(job => job.JobId == id && ExpansionJobMatchesOrder(job, instruction))))
                    throw new InvalidDataException("An owner expansion binding is malformed or references unrelated work.");
            }
            var completed = jobs.Where(job => job.OrderInstructionId == instruction.InstructionId &&
                job.State == WorldProductionJobState.Completed).ToArray();
            if (completed.Length > 1 || order.CompletedUnits != completed.Length ||
                (completed.Length == 0 ? order.LastEffectId is not null :
                    order.LastEffectId != ExpansionOrderReceipt(completed[0].JobId) ||
                    order.ExpansionBinding?.JobId != completed[0].JobId))
                throw new InvalidDataException("Expansion order progress has no matching completed expansion.");
        }
    }

    private static bool ValidExpansionBindingId(string value) => !string.IsNullOrWhiteSpace(value) &&
        value.Length <= 512 && value == value.Trim() && !value.Any(char.IsControl);

    private static bool ValidExpansionBindingPosition(GridPoint position) =>
        position.X is >= -10_000_000 and <= 10_000_000 && position.Y is >= -10_000_000 and <= 10_000_000;

    private static bool PlausibleExpansionAnchors(OwnerBuildingExpansionBinding binding)
    {
        var dx = binding.ExpectedPosition.X - binding.TargetPosition.X;
        var dy = binding.ExpectedPosition.Y - binding.TargetPosition.Y;
        // Yards add two rows or columns; House/Warehouse stages add one.
        var shift = (binding.TargetFootprint.Width, binding.TargetFootprint.Height) is (2, 4) or (4, 2) ? 2 : 1;
        return dx >= 0 && dy >= 0 && dx <= shift && dy <= shift && (dx == 0 || dy == 0) &&
            (binding.TargetFootprint.Width > 1 || dx == 0) && (binding.TargetFootprint.Height > 1 || dy == 0) &&
            (binding.TargetFootprint.Width != 2 || binding.TargetFootprint.Height != 3 || dx == 0) &&
            (binding.TargetFootprint.Width != 3 || binding.TargetFootprint.Height != 2 || dy == 0);
    }
}
