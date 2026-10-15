namespace ClankerWorld.Simulation.World;

internal sealed partial class ConstructionSystem(WorldConstructionState state)
{
    private readonly Dictionary<string, (long StartedTick, ConstructionActionPlan Plan)> retainedPlans = new(StringComparer.Ordinal);
    internal BuildingStore Buildings { get; } = new(state);
    internal WorldConstructionState State => Buildings.State;

    internal void RememberPlan(string actor, long startedTick, ConstructionActionPlan plan) => retainedPlans[actor] = (startedTick, plan);
    internal void ForgetPlan(string actor) => retainedPlans.Remove(actor);
    internal ConstructionActionPlan? RetainedPlan(string actor, AgentProject project) =>
        retainedPlans.TryGetValue(actor, out var retained) && retained.StartedTick == project.StartedTick &&
        retained.Plan.CandidateId == project.CandidateId && retained.Plan.InstructionId == project.OrderInstructionId ? retained.Plan : null;

    internal void CopyPlansFrom(ConstructionSystem source, Func<string, long, ConstructionActionPlan, bool> retain)
    {
        var selected = source.retainedPlans.Where(item => retain(item.Key, item.Value.StartedTick, item.Value.Plan)).ToArray();
        retainedPlans.Clear();
        foreach (var (actor, plan) in selected) retainedPlans.Add(actor, plan);
    }
}
