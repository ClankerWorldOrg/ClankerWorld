namespace ClankerWorld.Simulation.Playtest;

public sealed partial class PrivateWorldRuntime
{
    private static ActionRegistration RegisterCouncilActions() => new(ActionPhase.Ordinary, "council_", ["council_"], [], static (runtime, action) =>
    {
        runtime.ApplyCouncilCandidate(action.InhabitantId, action.CandidateId);
        return true;
    });
    private static ActionRegistration RegisterCivicPersonalActions() => new(ActionPhase.Ordinary, "civic|", ["civic|"], [], static (runtime, action) =>
    {
        // Civic choices execute only through the personal admission path above.
        return true;
    });
    private static ChoiceRegistration RegisterCouncilChoices() => new(ChoiceGroup.UnhurriedAdult, "AddCouncilCandidates", static (runtime, context) => runtime.AddCouncilCandidates(context.Candidates, context.InhabitantId), null);
    private static ChoiceRegistration RegisterTownCivicChoices() => new(ChoiceGroup.UnhurriedAdult, "AddTownCivicCandidates", static (runtime, context) => runtime.AddTownCivicCandidates(context.Candidates, context.InhabitantId, context.BuildingsWithoutSites!), null);
}
