namespace ClankerWorld.Simulation.Playtest;

public sealed partial class PrivateWorldRuntime
{
    private static ActionRegistration RegisterFieldActions() => new(ActionPhase.Ordinary, "farm:", ["farm:"], [], static (runtime, action) =>
    {
        runtime.ApplyFieldCandidate(action.InhabitantId, action.State, action.CandidateId);
        return true;
    });
    private static ChoiceRegistration RegisterFieldChoices() => new(ChoiceGroup.UnhurriedAdult, "AddFieldCandidates", static (runtime, context) => runtime.AddFieldCandidates(context.Candidates, context.InhabitantId, context.State), null);
    private static ChoiceRegistration RegisterForestryChoices() => new(ChoiceGroup.UnhurriedAdult, "AddForestryCandidates", static (runtime, context) => runtime.AddForestryCandidates(context.Candidates, context.InhabitantId, context.State), null);
}
