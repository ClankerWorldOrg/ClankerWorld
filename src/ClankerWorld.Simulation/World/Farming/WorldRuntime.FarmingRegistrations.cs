namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private static ActionRegistration RegisterFieldActions() => new(ActionPhase.Ordinary, "farm:", ["farm:"], [], static (runtime, action) =>
    {
        runtime.ApplyFieldActionPlan(action.Plan as FieldActionPlan ?? (FieldChoice.Decode(action.CandidateId) is { } choice
            ? runtime.farmingSystem.PlanFieldAction(runtime.farmingWorld, action.AgentId, choice) : null));
        return true;
    });
    private static ChoiceRegistration RegisterFieldChoices() => new(ChoiceGroup.UnhurriedAdult, "AddFieldCandidates", static (runtime, context) => runtime.AddFieldCandidates(context.Candidates, context.AgentId, context.State), null);
    private static ChoiceRegistration RegisterTreePlantingChoices() => new(ChoiceGroup.UnhurriedAdult, "AddTreePlantingCandidates", static (runtime, context) => runtime.AddTreePlantingCandidates(context.Candidates, context.AgentId, context.State), null);
}
