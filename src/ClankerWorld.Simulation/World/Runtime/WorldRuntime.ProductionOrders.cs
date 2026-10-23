using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Content;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private CognitionCandidate? ProductionOrderCandidateFor(OwnerQueuedInstruction instruction, AgentState person) =>
        OrderSystem.ProductionOrderCandidateFor(ProductionOrdersWorld, instruction, person);

    private PlacedBuilding? ProductionOrderBuilding(OwnerQueuedInstruction instruction, RecipeDefinition recipe) =>
        OrderSystem.ProductionOrderBuilding(ProductionOrdersWorld, instruction, recipe);

    private WorldProductionJob? BoundProductionJob(OwnerQueuedInstruction instruction) =>
        OrderSystem.BoundProductionJob(ProductionOrdersWorld, instruction);

    private static bool IsProductionOrderProject(OwnerQueuedInstruction instruction, AgentProject? project) =>
        instruction.Task is { Action: "produce_item", ProductionProjectStartedTick: { } started } order &&
        project is not null && project.StartedTick == started &&
        project.CandidateId == "build:recipe:" + order.TargetRecipeId &&
        (order.ProductionJobId is null || project.JobId == order.ProductionJobId);

    private void ExecuteProductionOrderStep(OwnerQueuedInstruction instruction, AgentState person) =>
        OrderSystem.ExecuteProductionOrderStep(ProductionOrdersWorld, instruction, person);

    private string ProductionOrderBlockedReason(OwnerQueuedInstruction instruction) =>
        OrderSystem.ProductionOrderBlockedReason(ProductionOrdersWorld, instruction);

    // Called before the inventory clock advances: pause and hold inputs before
    // expiry or a due completion can outrun an already urgent survival need.
    private void MaintainProductionOrdersBeforeTick() =>
        OrderSystem.MaintainProductionOrdersBeforeTick(ProductionOrdersWorld);

    private void PauseProductionOrderJob(OwnerQueuedInstruction instruction, WorldProductionJob job) =>
        OrderSystem.PauseProductionOrderJob(ProductionOrdersWorld, instruction, job);

    private bool IsActiveProductionOrderJob(string jobId) =>
        OrderSystem.IsActiveProductionOrderJob(ProductionOrdersWorld, jobId);

    private void CancelProductionForOrder(OwnerQueuedInstruction instruction) =>
        OrderSystem.CancelProductionForOrder(ProductionOrdersWorld, instruction);

    private void CreditProductionOrderJob(WorldProductionJob job, RecipeDefinition recipe) =>
        OrderSystem.CreditProductionOrderJob(ProductionOrdersWorld, job, recipe);

    private static string ProductionOrderReceipt(string jobId) =>
        DerivedId.Hash(jobId, "produce:job:");

    private static void ValidateProductionOrderBindings(WorldContentSimulationState simulation,
        DeclarativeWorldContentState content, IEnumerable<AgentState> physical,
        IEnumerable<OwnerQueuedInstruction> instructions)
    {
        var productionInstructions = instructions.Where(instruction => instruction.Task?.Action == "produce_item").ToArray();
        if (productionInstructions.Select(instruction => instruction.InstructionId).Distinct(StringComparer.Ordinal).Count() !=
            productionInstructions.Length)
            throw new InvalidDataException("Production order identities are not unique.");
        var orders = productionInstructions.ToDictionary(instruction => instruction.InstructionId, StringComparer.Ordinal);
        var people = physical.ToDictionary(person => person.AgentId, StringComparer.Ordinal);
        var jobs = simulation.ProductionJobs.ToDictionary(job => job.JobId, StringComparer.Ordinal);
        foreach (var job in simulation.ProductionJobs.Where(job => job.OrderInstructionId is not null))
        {
            if (!orders.TryGetValue(job.OrderInstructionId!, out var instruction) ||
                job.WorkerId != instruction.TargetAgentId || job.RecipeId != instruction.Task!.TargetRecipeId ||
                job.BuildingInstanceId != instruction.Task.ProductionBuildingId || job.StartedTick < instruction.SubmittedTick)
                throw new InvalidDataException("A production job does not belong to its recorded order.");
            if (job.State is WorldProductionJobState.Running or WorldProductionJobState.Paused &&
                (!IsActiveOrder(instruction.Task.Status) || instruction.Task.ProductionJobId != job.JobId ||
                 !people.TryGetValue(job.WorkerId, out var person) || !IsProductionOrderProject(instruction, person.Project) ||
                 person.Project!.WorkDone != ProjectWorkTicks || person.Project.Stage is not ("waiting" or "paused") ||
                 person.Project.RequiresFreshChoice))
                throw new InvalidDataException("Active ordered production has no matching worker project and order.");
        }
        foreach (var instruction in orders.Values)
        {
            var order = instruction.Task!;
            var recipe = WorldProductionOrderCatalog.Find(content, order.TargetRecipeId) ??
                throw new InvalidDataException("A production order references an unsupported recipe.");
            if (order.ProductionProjectStartedTick is { } started && started < instruction.SubmittedTick)
                throw new InvalidDataException("An ordered production project predates its instruction.");
            if (order.ProductionBuildingId is { } buildingId &&
                simulation.Buildings.FirstOrDefault(building => building.InstanceId == buildingId) is { } building &&
                building.DefinitionId != recipe.Recipe.WorkstationBuildingId)
                throw new InvalidDataException("An order is bound to a different recipe workstation.");
            if (order.ProductionJobId is { } jobId &&
                (!jobs.TryGetValue(jobId, out var bound) || bound.OrderInstructionId != instruction.InstructionId ||
                 bound.StartedTick < order.ProductionProjectStartedTick))
                throw new InvalidDataException("An order is bound to an unrelated production job.");
            var completed = simulation.ProductionJobs.Where(job => job.OrderInstructionId == instruction.InstructionId &&
                job.State == WorldProductionJobState.Completed).ToArray();
            var measuredUnits = Math.Min(1_000_000L, (long)completed.Length *
                (order.ProgressUnit == "production_batches" ? 1 : recipe.OutputQuantity));
            var latest = completed.OrderBy(job => job.CompletionTick).ThenBy(job => job.JobId, StringComparer.Ordinal).LastOrDefault();
            if (order.CompletedUnits != measuredUnits || order.CompletedUnits > 0 &&
                ProductionOrderReceipt(latest!.JobId) != order.LastEffectId)
                throw new InvalidDataException("Production order progress has no matching completed jobs.");
        }
    }
}
