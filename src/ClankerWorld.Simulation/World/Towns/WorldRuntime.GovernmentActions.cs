using ClankerWorld.Simulation.Cognition;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private void AddTownGovernmentCandidates(List<CognitionCandidate> candidates, string actor, TownRuntimeState town) => TownSystem.AddTownGovernmentCandidates(civicWorld, candidates, actor, town);
    private static string NonLandExtensionToken(TownOffice office) => TownSystem.NonLandExtensionToken(office);
    private static TownOffice NonLandExtensionOffice(TownGovernmentState government, string token) => TownSystem.NonLandExtensionOffice(government, token);
    private (TownCouncilState Council, TownGovernmentState Government) ApplyTownGovernmentAction(TownRuntimeState town, string actor, string action, string subject, string choice, TownCouncilState council, TownGovernmentState government) => TownSystem.ApplyTownGovernmentAction(civicWorld, town, actor, action, subject, choice, council, government);
}
