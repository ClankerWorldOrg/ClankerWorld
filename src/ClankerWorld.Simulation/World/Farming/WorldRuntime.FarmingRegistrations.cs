namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private static ActionRegistration RegisterFieldActions() => new(ActionPhase.Ordinary, "farm:", ["farm:"], [], Definition: FieldDefinition);
    private static ChoiceRegistration RegisterFieldChoices() => new(ChoiceGroup.UnhurriedAdult, "AddFieldCandidates", static (runtime, context) => runtime.farmingSystem.AddFieldCandidates(runtime.farmingWorld, context.Candidates, context.AgentId, context.State), null);
    private static ChoiceRegistration RegisterTreePlantingChoices() => new(ChoiceGroup.UnhurriedAdult, "AddTreePlantingCandidates", static (runtime, context) => FarmingSystem.AddTreePlantingCandidates(runtime.farmingWorld, context.Candidates, context.AgentId, context.State), null);
}
