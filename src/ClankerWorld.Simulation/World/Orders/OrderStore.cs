namespace ClankerWorld.Simulation.World;

internal sealed partial class OrderStore
{
    private readonly Dictionary<string, OwnerQueuedInstruction> instructions = new(StringComparer.Ordinal);
    private readonly Dictionary<string, OwnerInstructionReceipt> receipts = new(StringComparer.Ordinal);
    private readonly Dictionary<string, OwnerOrderCancellation> cancellations = new(StringComparer.Ordinal);
    private readonly HashSet<string> completed = new(StringComparer.Ordinal);
    internal IReadOnlyDictionary<string, OwnerQueuedInstruction> Instructions => instructions;
    internal IReadOnlyDictionary<string, OwnerInstructionReceipt> Receipts => receipts;
    internal IReadOnlyDictionary<string, OwnerOrderCancellation> Cancellations => cancellations;
    internal IReadOnlySet<string> Completed => completed;
    internal long NextSequence { get; private set; } = 1;

    internal long BeginSubmissionSequence() => NextSequence++;
    internal void QueueInstruction(OwnerQueuedInstruction instruction)
    {
        if (SystemInvariantChecks.Enabled) ValidateLocalInstruction(instruction);
        instructions.Add(instruction.IdempotencyKey, instruction);
    }
    internal void RecordSubmissionReceipt(string key, OwnerInstructionReceipt receipt)
    {
        if (SystemInvariantChecks.Enabled && (key != receipt.IdempotencyKey || string.IsNullOrWhiteSpace(receipt.InstructionId) || receipt.SubmittedTick < 0 || receipt.RunEpoch < 0 || receipt.Revision < 1))
            throw new InvalidDataException("Owner instruction receipt has invalid local identity.");
        receipts.Add(key, receipt);
    }
    internal void RecordCancellationReceipt(string key, OwnerOrderCancellation receipt)
    {
        if (SystemInvariantChecks.Enabled && (key != receipt.IdempotencyKey || string.IsNullOrWhiteSpace(receipt.OrderId) || receipt.Receipt.WorldTick < 0))
            throw new InvalidDataException("Owner cancellation receipt has invalid local identity.");
        cancellations.Add(key, receipt);
    }
    internal bool CompleteInstruction(string id) => completed.Add(id);

    private void Commit(string key, OwnerQueuedInstruction instruction)
    {
        if (SystemInvariantChecks.Enabled)
        {
            ValidateLocalInstruction(instruction);
            if (key != instruction.IdempotencyKey) throw new InvalidDataException("Owner instruction binding uses a different idempotency key.");
        }
        instructions[key] = instruction;
    }

    internal void Restore(IEnumerable<OwnerQueuedInstruction> savedInstructions, IEnumerable<string> savedCompleted, IEnumerable<OwnerOrderCancellation> savedCancellations)
    {
        var restoreInstructions = savedInstructions.ToArray();
        if (SystemInvariantChecks.Enabled)
            foreach (var instruction in restoreInstructions) ValidateLocalInstruction(instruction);
        var restoreCompleted = savedCompleted.ToArray();
        var restoreCancellations = savedCancellations.ToArray();
        savedInstructions = restoreInstructions;
        savedCompleted = restoreCompleted;
        savedCancellations = restoreCancellations;
        instructions.Clear();
        receipts.Clear();
        cancellations.Clear();
        completed.Clear();
        foreach (var instruction in savedInstructions)
        {
            if (!instructions.TryAdd(instruction.IdempotencyKey, instruction))
            {
                throw new InvalidDataException("The world instruction idempotency keys are duplicated.");
            }

            receipts.Add(
                instruction.IdempotencyKey,
                OrderStore.CreateOwnerInstructionReceipt(instruction.InstructionId, instruction.IdempotencyKey, instruction.SubmittedTick, instruction.RunEpoch, instruction.SubmissionSequence));
        }

        foreach (var completedInstructionId in savedCompleted)
        {
            completed.Add(completedInstructionId);
        }

        foreach (var cancellation in savedCancellations)
        {
            if (!cancellations.TryAdd(cancellation.IdempotencyKey, cancellation))
            {
                throw new InvalidDataException("The world order cancellation idempotency keys are duplicated.");
            }
        }

        NextSequence = instructions.Count == 0
            ? 1
            : checked(instructions.Values.Max(item => item.SubmissionSequence) + 1);

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
        if (instruction.Task is { } task && (string.IsNullOrWhiteSpace(task.Action) || string.IsNullOrWhiteSpace(task.Status) ||
            task.RequestedUnits < 0 || task.CompletedUnits is < 0 or > 1_000_000))
            throw new InvalidDataException("Owner order has invalid local progress.");
    }
}
