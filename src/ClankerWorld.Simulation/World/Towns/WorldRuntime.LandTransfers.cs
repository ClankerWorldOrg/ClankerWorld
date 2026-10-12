using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private Dictionary<string, string?> LandTransferAdultHouseholds() => TownSystem.LandTransferAdultHouseholds(civicWorld);
    private IReadOnlyList<TownLandTransferParty> LandTransferParties(TownLandTransferRequest request) => TownSystem.LandTransferParties(civicWorld, request);
    private SocietyHousehold[] LandTransferKnownHouseholds(string actor, TownRuntimeState town) => TownSystem.LandTransferKnownHouseholds(civicWorld, actor, town);
    private string LandTransferSourceToken(TownRuntimeState town, string household) => townSystem.LandTransferSourceToken(civicWorld, town, household);
    private static string LandTransferActionToken(TownLandTransferRequest request) => TownSystem.LandTransferActionToken(request);
    private string LandTransferTerms(TownLandTransferRequest request) => TownSystem.LandTransferTerms(civicWorld, request);
    private void LandTransferEvent(string kind, TownRuntimeState town, TownLandTransferRequest request, string? actor, string status) => TownSystem.LandTransferEvent(civicWorld, kind, town, request, actor, status);
    private (TownCouncilState Council, TownLandRecords LandRecords) AdvanceTownLandTransfers(
        TownRuntimeState town, TownCouncilState council) => townSystem.AdvanceTownLandTransfers(civicWorld, town, council);
    private TownCouncilState PublishLandTransferResults(TownRuntimeState town, TownLandRecords before,
        TownLandRecords after, TownCouncilState council, string? paidRequestId = null, string? buyer = null) => TownSystem.PublishLandTransferResults(civicWorld, town, before, after, council, paidRequestId, buyer);
    private static string LandTransferFailureText(string? reason) => TownSystem.LandTransferFailureText(reason);
    private bool MayVisitLandTransfer(string actor, TownRuntimeState town) => TownSystem.MayVisitLandTransfer(civicWorld, actor, town);
    private void AddTownLandTransferCandidates(List<CognitionCandidate> candidates, string actor, TownRuntimeState town) => townSystem.AddTownLandTransferCandidates(civicWorld, candidates, actor, town);
}
