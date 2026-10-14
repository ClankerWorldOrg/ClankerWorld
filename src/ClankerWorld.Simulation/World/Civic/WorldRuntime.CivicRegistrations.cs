namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private static ActionRegistration RegisterFoodPolicyActions() => new(ActionPhase.Ordinary, "food_policy_", ["food_policy_"], [], Definition: FoodPolicyDefinition);
    private static ActionRegistration RegisterCivicPersonalActions() => new(ActionPhase.Ordinary, "civic|", ["civic|"], [], static (runtime, action) =>
    {
        // Civic choices execute only through the personal admission path above.
        return true;
    });
    private static ChoiceRegistration RegisterFoodPolicyChoices() => new(ChoiceGroup.UnhurriedAdult, "AddFoodPolicyCandidates", static (runtime, context) => runtime.AddFoodPolicyCandidates(context.Candidates, context.AgentId), null);
    private static ChoiceRegistration RegisterTownCivicChoices() => new(ChoiceGroup.UnhurriedAdult, "AddTownCivicCandidates", static (runtime, context) => runtime.AddTownCivicCandidates(context.Candidates, context.AgentId, context.BuildingsWithoutSites!), null);
}
