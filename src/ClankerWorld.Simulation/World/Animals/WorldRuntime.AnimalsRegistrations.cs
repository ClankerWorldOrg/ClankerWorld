namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private static ActionRegistration RegisterAnimalActions() => new(ActionPhase.Ordinary, "ApplyAnimalCandidate", [], [], static (runtime, action) =>
    {
        return runtime.ApplyAnimalCandidate(action.InhabitantId, action.CandidateId);
    });
    private static ChoiceRegistration RegisterAnimalChoices() => new(ChoiceGroup.Prelude, "animals", static (runtime, context) => runtime.AddAnimalCandidates(context.Candidates, context.InhabitantId), static (runtime, context) => runtime.AdultResident(context.InhabitantId));
}
