namespace ClankerWorld.Simulation.World;

internal sealed partial class OrderStore
{
    private readonly InstructionStore instructions = new(StringComparer.Ordinal);
    private readonly Dictionary<string, RecordedInstructionReceipt> receipts = new(StringComparer.Ordinal);
    private readonly Dictionary<string, OwnerOrderCancellation> cancellations = new(StringComparer.Ordinal);
    private readonly HashSet<string> completedSuggestions = new(StringComparer.Ordinal);
    internal IReadOnlyDictionary<string, OwnerQueuedInstruction> Instructions => instructions;
    internal IReadOnlyDictionary<string, RecordedInstructionReceipt> Receipts => receipts;
    internal IReadOnlyDictionary<string, OwnerOrderCancellation> Cancellations => cancellations;
    internal IReadOnlySet<string> CompletedSuggestions => completedSuggestions;
    internal long NextSequence { get; private set; } = 1;

    internal long BeginSubmissionSequence() => NextSequence++;
    internal OwnerQueuedInstruction? FirstActiveFor(string agentId) => instructions.FirstActiveFor(agentId);

    internal void QueueInstruction(OwnerQueuedInstruction instruction)
    {
        if (SystemInvariantChecks.Enabled) ValidateLocalInstruction(instruction);
        instructions.Add(instruction.IdempotencyKey, instruction);
    }

    internal void RecordSubmissionReceipt(string key, RecordedInstructionReceipt recorded)
    {
        var receipt = recorded.Receipt;
        if (SystemInvariantChecks.Enabled && (key != receipt.IdempotencyKey || receipt.InstructionId is null || receipt.SubmittedTick < 0 || receipt.RunEpoch < 0 || receipt.Revision < 1))
            throw new InvalidDataException("Owner instruction receipt has invalid local identity.");
        receipts.Add(key, recorded);
    }

    internal void RecordCancellationReceipt(string key, OwnerOrderCancellation receipt)
    {
        if (SystemInvariantChecks.Enabled && (key != receipt.IdempotencyKey || string.IsNullOrWhiteSpace(receipt.OrderId) || receipt.Receipt.WorldTick < 0))
            throw new InvalidDataException("Owner cancellation receipt has invalid local identity.");
        cancellations.Add(key, receipt);
    }

    internal bool CompleteInstruction(string id) => completedSuggestions.Add(id);

    private void Commit(string key, OwnerQueuedInstruction instruction)
    {
        if (SystemInvariantChecks.Enabled)
        {
            ValidateLocalInstruction(instruction);
            if (key != instruction.IdempotencyKey) throw new InvalidDataException("Owner instruction binding uses a different idempotency key.");
        }
        instructions[key] = instruction;
    }

    internal OwnerQueuedInstruction[] PruneClosedOrders()
    {
        var retiredInstructions = instructions.PruneClosedOrders();
        if (retiredInstructions.Length != 0)
        {
            var retired = retiredInstructions.Select(item => item.InstructionId).ToHashSet(StringComparer.Ordinal);
            foreach (var key in cancellations.Where(item => retired.Contains(item.Value.OrderId)).Select(item => item.Key).ToArray())
                cancellations.Remove(key);
        }
        return retiredInstructions;
    }

    internal void PruneCancellationRetries()
    {
        var expiredRetries = cancellations.Values.GroupBy(item => item.TargetAgentId, StringComparer.Ordinal)
            .SelectMany(group => group.OrderByDescending(item => item.Receipt.WorldTick)
                .ThenByDescending(item => item.Receipt.LatestEventId)
                .ThenByDescending(item => item.IdempotencyKey, StringComparer.Ordinal)
                .Skip(OwnerQueuedInstruction.RecentClosedOrderLimitPerAgent))
            .Select(item => item.IdempotencyKey).ToArray();
        foreach (var key in expiredRetries) cancellations.Remove(key);
    }

    internal void Restore(IEnumerable<OwnerQueuedInstruction> savedInstructions, IEnumerable<string> savedCompleted,
        IEnumerable<OwnerOrderCancellation> savedCancellations, IEnumerable<RecordedInstructionReceipt> savedReceipts)
    {
        var restoreInstructions = savedInstructions.ToArray();
        if (SystemInvariantChecks.Enabled)
            foreach (var instruction in restoreInstructions) ValidateLocalInstruction(instruction);
        var restoreCompleted = savedCompleted.ToArray();
        var restoreCancellations = savedCancellations.ToArray();
        var restoreReceipts = savedReceipts.ToArray();
        instructions.Clear();
        receipts.Clear();
        cancellations.Clear();
        completedSuggestions.Clear();
        foreach (var instruction in restoreInstructions)
            if (!instructions.TryAdd(instruction.IdempotencyKey, instruction))
                throw new InvalidDataException("The world instruction idempotency keys are duplicated.");
        foreach (var recorded in restoreReceipts)
            receipts.Add(recorded.Receipt.IdempotencyKey, recorded);
        var suggestionIds = instructions.Values.Where(item => item.Kind == OwnerInstructionKind.Suggestion)
            .Select(item => item.InstructionId).ToHashSet(StringComparer.Ordinal);
        foreach (var id in restoreCompleted)
            if (suggestionIds.Contains(id)) completedSuggestions.Add(id);
        foreach (var cancellation in restoreCancellations)
            if (!cancellations.TryAdd(cancellation.IdempotencyKey, cancellation))
                throw new InvalidDataException("The world order cancellation idempotency keys are duplicated.");
        NextSequence = receipts.Count == 0 ? 1 : checked(receipts.Values.Max(item => item.Receipt.Revision) + 1);
    }

    internal static void ValidateLocalInstruction(OwnerQueuedInstruction instruction)
    {
        if (string.IsNullOrWhiteSpace(instruction.InstructionId) || string.IsNullOrWhiteSpace(instruction.IdempotencyKey) ||
            string.IsNullOrWhiteSpace(instruction.IssuerId) || string.IsNullOrWhiteSpace(instruction.TargetAgentId) ||
            string.IsNullOrWhiteSpace(instruction.Text) || instruction.Text.Length > OwnerQueuedInstruction.MaximumTextLength ||
            instruction.SubmissionSequence < 1 || instruction.SubmittedTick < 0 || instruction.RunEpoch < 0 ||
            instruction.ObservedTick is { } observed && observed < instruction.SubmittedTick ||
            instruction.GuidancePromptedTick is { } prompted && prompted < instruction.SubmittedTick)
            throw new InvalidDataException("Owner instruction has invalid local identity or lifecycle state.");
        if (instruction.Task is { } task && (task.Definition is null || task.RequestedUnits < 0 || task.CompletedUnits is < 0 or > 1_000_000))
            throw new InvalidDataException("Owner order has invalid local progress.");
    }
}
