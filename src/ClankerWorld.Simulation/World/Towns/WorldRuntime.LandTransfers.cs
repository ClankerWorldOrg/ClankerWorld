using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private Dictionary<string, string?> LandTransferAdultHouseholds() => CivicSystem.LandTransferAdultHouseholds(civicWorld);
    private IReadOnlyList<TownLandTransferParty> LandTransferParties(TownLandTransferRequest request) => CivicSystem.LandTransferParties(civicWorld, request);
    private SocietyHousehold[] LandTransferKnownHouseholds(string actor, TownRuntimeState town) => CivicSystem.LandTransferKnownHouseholds(civicWorld, actor, town);
    private string LandTransferSourceToken(TownRuntimeState town, string household) => civicSystem.LandTransferSourceToken(civicWorld, town, household);
    private static string LandTransferActionToken(TownLandTransferRequest request) => CivicSystem.LandTransferActionToken(request);
    private string LandTransferTerms(TownLandTransferRequest request) => CivicSystem.LandTransferTerms(civicWorld, request);
    private void LandTransferEvent(string kind, TownRuntimeState town, TownLandTransferRequest request, string? actor, string status) => CivicSystem.LandTransferEvent(civicWorld, kind, town, request, actor, status);
    private (TownGovernanceState Council, TownLandHearingState LandHearings) AdvanceTownLandTransfers(
        TownRuntimeState town, TownGovernanceState council) => civicSystem.AdvanceTownLandTransfers(civicWorld, town, council);
    private TownGovernanceState PublishLandTransferResults(TownRuntimeState town, TownLandHearingState before,
        TownLandHearingState after, TownGovernanceState council, string? paidRequestId = null, string? buyer = null) => CivicSystem.PublishLandTransferResults(civicWorld, town, before, after, council, paidRequestId, buyer);
    private static string LandTransferFailureText(string? reason) => CivicSystem.LandTransferFailureText(reason);
    private bool MayVisitLandTransfer(string actor, TownRuntimeState town) => CivicSystem.MayVisitLandTransfer(civicWorld, actor, town);
    private void AddTownLandTransferCandidates(List<CognitionCandidate> candidates, string actor, TownRuntimeState town) => civicSystem.AddTownLandTransferCandidates(civicWorld, candidates, actor, town);
}
