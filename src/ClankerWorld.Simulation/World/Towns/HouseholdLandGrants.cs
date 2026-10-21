using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Content;

namespace ClankerWorld.Simulation.World;

public static class HouseholdLandGrantRules
{
    private const string GrantPrefix = "council:";
    private static string Digest(string text) => DerivedId.HashBase64Url(text);
    public static string GrantSource(string requestId) => GrantPrefix + Digest(requestId);
    public static string RightId(string requestId) => "household-use:grant:" + Digest(requestId);
    public static string RequestKey(HouseholdLandUseRequest request) => "land_use:" + Digest(
        request.TownId + "|" + request.HouseholdId + "|" + request.AgreedEndTick?.ToString(CultureInfo.InvariantCulture) +
        "|" + TownLandClaimRules.DescribeTiles(request.Tiles));

    public static bool IsAvailable(HouseholdLandUseRequest request, IReadOnlyList<HouseholdLandUseRight> rights,
        IReadOnlyList<HouseholdLandUseRequest> requests) =>
        request.HearingResolutions.Count == 0 && !rights.Any(right => right.Tiles.Any(request.Tiles.Contains)) &&
        !requests.Any(other => other.Status == "pending" && other.HouseholdId != request.HouseholdId &&
            other.Tiles.Any(tile => request.Tiles.Contains(tile) && !other.HearingResolutions.Any(resolution => resolution.Tiles.Contains(tile))));

    public static string? RefusalReason(HouseholdLandUseRequest request, IReadOnlyList<string> adults,
        TownProposal? proposal, long tick) =>
        adults.Count == 0 ? "The household has no living adult signatory." :
        request.AgreedEndTick is { } end && end <= tick ? "The requested end date has arrived." :
        request.Consents.Any(c => !c.Accepted && adults.Contains(c.AgentId, StringComparer.Ordinal)) ? "A current adult in the household declined." :
        proposal is { Status: "cancelled" } && request.HearingResolutions.Count == 0 ? "The Council changed before it decided; the household may ask again." :
        proposal is { Status: "rejected" or "withdrawn" } ? "The Council proposal did not pass." : null;

    public static void Validate(long tick, IReadOnlyList<TownRuntimeState> towns,
        IReadOnlyList<HouseholdLandUseRight> rights, IReadOnlyList<HouseholdLandUseRequest> requests,
        IReadOnlySet<string> knownAgents)
    {
        IReadOnlyList<HouseholdLandUseRight> receiptRights = rights;
        foreach (var town in towns)
        {
            if (!TownLandHearingValidation.HasRecords(town.LandRecords))
                throw new InvalidDataException("Saved Town hearing history must be present.");
            receiptRights = TownLandHearingRules.OriginalGrantRights(town.LandRecords, receiptRights);
        }
        foreach (var request in requests)
        {
            TownSystem.ValidateLocalLandRequest(request);
            var council = towns.Single(t => t.Id == request.TownId).Council;
            var proposal = council?.Proposals.SingleOrDefault(p => p.Id == request.CouncilProposalId);
            if (request.SettledTick > tick ||
                request.Consents.Any(c => !knownAgents.Contains(c.AgentId) || c.Tick > tick || council is null ||
                    !council.Knowledge.Any(k => k.AgentId == c.AgentId && k.LearnedTick <= c.Tick &&
                        council.Notices.Any(n => n.Id == k.NoticeId && n.Kind == "land_use" && n.SubjectId == request.Id))) ||
                request.CouncilProposalId is not null && (proposal is null || proposal.Kind != "land_use" ||
                    proposal.SubjectId != request.Id || proposal.RequestKey != RequestKey(request) ||
                    proposal.AuthorId != request.RequestedByAgentId || proposal.OpenedTick < request.RequestedTick))
                throw new InvalidDataException("A saved household land request has invalid approval or consent history.");
            var grants = receiptRights.Where(right => right.GrantSource == GrantSource(request.Id)).ToArray();
            if (request.Status == "granted")
            {
                if (proposal is not { Status: "passed" } || proposal.SettledTick > request.SettledTick ||
                    request.GrantAdults.Any(id => !knownAgents.Contains(id)) ||
                    !TownLandRightsRules.OrderTiles(grants.SelectMany(right => right.Tiles)).SequenceEqual(request.Tiles) ||
                    grants.Any(right => right.TownId != request.TownId || right.GrantedTick != request.SettledTick ||
                        right.AgreedEndTick != request.AgreedEndTick))
                    throw new InvalidDataException("A household land grant must retain its Council approval, adult acceptance and recorded terms.");
            }
            else if (grants.Length != 0)
                throw new InvalidDataException("An unfinished or refused land request cannot supply a use right.");
        }
        TownLandHearingValidation.ValidateRequestResolutions(tick, towns.Select(town => town.LandRecords).ToArray(), requests);
        if (receiptRights.Any(right => right.GrantSource.StartsWith(GrantPrefix, StringComparison.Ordinal) &&
                !requests.Any(request => request.Status == "granted" && GrantSource(request.Id) == right.GrantSource)) ||
            towns.Any(town => town.Council?.Proposals.Any(proposal => proposal.Kind == "land_use" &&
                !requests.Any(request => request.TownId == town.Id && request.CouncilProposalId == proposal.Id &&
                    request.Id == proposal.SubjectId)) == true))
            throw new InvalidDataException("A saved land approval or use right is missing its household request.");
    }
}

public sealed partial class WorldRuntime
{
    private TownCouncilState OpenLandUseProposal(TownRuntimeState town, TownCouncilState state,
        ref HouseholdLandUseRequest request) => townSystem.OpenLandUseProposal(civicWorld, town, state, ref request);
    private TownCouncilState ResolveHouseholdLandRequests(TownRuntimeState town, TownCouncilState state) => townSystem.ResolveHouseholdLandRequests(civicWorld, town, state);
    /// <summary>Land other households hold or have asked for; a Town building (null household) avoids all of it.</summary>
    private HashSet<GridPoint> HouseholdLandHeldByOthers(string? household) => townSystem.HouseholdLandHeldByOthers(civicWorld, household);
    private bool IncludesForeignFieldWithoutUseRight(string household, IReadOnlyList<GridPoint> tiles) => townSystem.IncludesForeignFieldWithoutUseRight(civicWorld, household, tiles);
    private HashSet<GridPoint> BuildingFootprintTiles(Func<PlacedBuilding, bool> include) => TownSystem.BuildingFootprintTiles(civicWorld, include);
    /// <summary>Tiles a running or paused expansion is building on, for the buildings <paramref name="include"/> selects.</summary>
    private HashSet<GridPoint> ExpansionWorkTiles(Func<PlacedBuilding?, bool> include) => TownSystem.ExpansionWorkTiles(civicWorld, include);
    /// <summary>Free Town-titled land nearest first: no use right, pending request, building, expansion, road, field or Town project site.</summary>
    private GridPoint[] RequestableLandNear(TownRuntimeState town, GridPoint from, int count) => townSystem.RequestableLandNear(civicWorld, town, from, count);
    private string LandUseTerms(HouseholdLandUseRequest request) => TownSystem.LandUseTerms(civicWorld, request);
    private void AddHouseholdLandCandidates(List<CognitionCandidate> candidates, string actor, TownRuntimeState town) => townSystem.AddHouseholdLandCandidates(civicWorld, candidates, actor, town);
    private GridPoint[]? ExpansionLandRequestTiles(string actor, PlacedBuilding building) => townSystem.ExpansionLandRequestTiles(civicWorld, actor, building);
    private TownCouncilState SubmitExpansionLandRequest(TownRuntimeState town, TownCouncilState state, string actor, string buildingId) => townSystem.SubmitExpansionLandRequest(civicWorld, town, state, actor, buildingId);
    private TownCouncilState DecideHouseholdLandUse(TownCouncilState state, string actor, string requestId, string action) => townSystem.DecideHouseholdLandUse(civicWorld, state, actor, requestId, action);
    private TownCouncilState SubmitPersonalLandUseRequest(TownRuntimeState town, TownCouncilState state,
        string actor, IReadOnlyList<CognitionLandTile>? landTiles) => townSystem.SubmitPersonalLandUseRequest(civicWorld, town, state, actor, landTiles);
}
