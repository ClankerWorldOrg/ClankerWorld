using ClankerWorld.Simulation.Cognition;

namespace ClankerWorld.Simulation.Playtest;

public sealed partial class PrivateWorldRuntime
{
    private void AddTownGovernmentCandidates(List<CognitionCandidate> candidates, string actor, TownRuntimeState town) => CivicSystem.AddTownGovernmentCandidates(civicWorld, candidates, actor, town);
    private static string NonLandExtensionToken(TownOffice office) => CivicSystem.NonLandExtensionToken(office);
    private static TownOffice NonLandExtensionOffice(TownGovernmentState government, string token) => CivicSystem.NonLandExtensionOffice(government, token);
    private (TownGovernanceState Council, TownGovernmentState Government) ApplyTownGovernmentAction(TownRuntimeState town, string actor, string action, string subject, string choice, TownGovernanceState council, TownGovernmentState government) => CivicSystem.ApplyTownGovernmentAction(civicWorld, town, actor, action, subject, choice, council, government);
}
