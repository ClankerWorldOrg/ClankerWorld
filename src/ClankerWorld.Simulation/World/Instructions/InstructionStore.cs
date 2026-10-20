namespace ClankerWorld.Simulation.World;

/// <summary>Owns instruction replacement and the derived, unsaved active queues.</summary>
internal sealed class InstructionStore(IEqualityComparer<string> comparer)
{
    private readonly Dictionary<string, OwnerQueuedInstruction> records = new(comparer);
    private readonly Dictionary<string, SortedDictionary<(bool Queued, long Sequence), string>> activeByAgent = new(comparer);

    public Dictionary<string, OwnerQueuedInstruction>.ValueCollection Values => records.Values;
    public int Count => records.Count;

    public OwnerQueuedInstruction this[string key]
    {
        get => records[key];
        set
        {
            if (records.TryGetValue(key, out var previous)) Unindex(previous);
            records[key] = value;
            Index(value);
        }
    }

    public bool TryGetValue(string key, out OwnerQueuedInstruction instruction) =>
        records.TryGetValue(key, out instruction!);

    public void Add(string key, OwnerQueuedInstruction instruction)
    {
        records.Add(key, instruction);
        Index(instruction);
    }

    public bool TryAdd(string key, OwnerQueuedInstruction instruction)
    {
        if (!records.TryAdd(key, instruction)) return false;
        Index(instruction);
        return true;
    }

    public void Clear()
    {
        records.Clear();
        activeByAgent.Clear();
    }

    public OwnerQueuedInstruction? FirstActiveFor(string agentId) =>
        activeByAgent.TryGetValue(agentId, out var active) && active.Count != 0
            ? records[active.First().Value]
            : null;

    public OwnerQueuedInstruction[] ActiveFor(string agentId) =>
        activeByAgent.TryGetValue(agentId, out var active)
            ? active.Values.Select(key => records[key]).ToArray()
            : [];

    public OwnerQueuedInstruction[] PruneClosedOrders()
    {
        var retired = records.Values.Where(item => item.Kind == OwnerInstructionKind.Order && item.Task?.IsTerminal == true)
            .GroupBy(item => item.TargetAgentId, comparer)
            .SelectMany(group => group.OrderByDescending(item => item.SubmissionSequence)
                .Skip(OwnerQueuedInstruction.RecentClosedOrderLimitPerAgent))
            .ToArray();
        foreach (var instruction in retired) records.Remove(instruction.IdempotencyKey);
        return retired;
    }

    private void Index(OwnerQueuedInstruction instruction)
    {
        if (instruction.Kind != OwnerInstructionKind.Order || instruction.Task is not { IsTerminal: false }) return;
        if (!activeByAgent.TryGetValue(instruction.TargetAgentId, out var active))
            activeByAgent.Add(instruction.TargetAgentId, active = []);
        active.Add((instruction.Task!.LifecycleStatus == OrderStatus.Queued, instruction.SubmissionSequence), instruction.IdempotencyKey);
    }

    private void Unindex(OwnerQueuedInstruction instruction)
    {
        if (instruction.Kind != OwnerInstructionKind.Order || instruction.Task is not { IsTerminal: false }) return;
        if (!activeByAgent.TryGetValue(instruction.TargetAgentId, out var active)) return;
        active.Remove((instruction.Task!.LifecycleStatus == OrderStatus.Queued, instruction.SubmissionSequence));
        if (active.Count == 0) activeByAgent.Remove(instruction.TargetAgentId);
    }
}
