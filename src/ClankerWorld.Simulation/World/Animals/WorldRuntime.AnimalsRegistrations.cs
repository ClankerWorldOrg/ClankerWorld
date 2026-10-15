namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private static ActionRegistration RegisterAnimalActions() => new(ActionPhase.Ordinary, "ApplyAnimalCandidate", [], [], static (runtime, action) =>
    {
        return runtime.ApplyAnimalCandidate(action.AgentId, action.CandidateId, action.Plan);
    });
    private static ChoiceRegistration RegisterAnimalChoices() => new(ChoiceGroup.Prelude, "animals", static (runtime, context) => runtime.AddAnimalCandidates(context.Candidates, context.AgentId), static (runtime, context) => runtime.IsAdult(context.AgentId));
}
