using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Kernel;
using System.Text.Json;

namespace ClankerWorld.Simulation.World;

internal sealed partial class ProductionSystem
{
    internal static string ToolMakingRequestId(string actor, string building, string recipe, long tick) =>
        DerivedId.Hash(JsonSerializer.Serialize(new[] { actor, building, recipe, tick.ToString(System.Globalization.CultureInfo.InvariantCulture) }), "tool-making-request:");

    internal static bool IsToolRequestRecipeId(string? id) => !string.IsNullOrWhiteSpace(id) && id == id.Trim() && !id.Any(char.IsControl);

    internal void CheckMaintainedRequests()
    {
        if (SystemInvariantChecks.Enabled) ValidateLocalRequests(State.Requests, enforceRetention: true);
    }

    internal static void ValidateLocalRequests([System.Diagnostics.CodeAnalysis.NotNull] IReadOnlyList<ToolMakingRequestState>? requests, bool enforceRetention)
    {
        if (requests is null || requests.Any(request => request is null) || enforceRetention && (requests.Count >
                ToolMakingRequestRules.MaximumActiveRequests + ToolMakingRequestRules.MaximumTerminalRequests ||
            requests.Count(request => !ToolMakingRequestRules.IsTerminal(request.Status)) > ToolMakingRequestRules.MaximumActiveRequests ||
            requests.Count(request => ToolMakingRequestRules.IsTerminal(request.Status)) > ToolMakingRequestRules.MaximumTerminalRequests) ||
            requests.Select(request => request.Id).Distinct(StringComparer.Ordinal).Count() != requests.Count ||
            requests.Where(request => !ToolMakingRequestRules.IsTerminal(request.Status)).GroupBy(request => request.RequesterId)
                .Any(group => group.Count() > 1) || requests.Where(request => request.JobId is not null).GroupBy(request => request.JobId)
                .Any(group => group.Count() > 1) || requests.Where(request => request.OfferId is not null).GroupBy(request => request.OfferId)
                .Any(group => group.Count() > 1))
            throw new InvalidDataException("Tool requests are missing, unbounded or duplicated.");
        foreach (var request in requests)
        {
            var accepted = request.AcceptedTick is not null;
            if (!Enum.IsDefined(request.Status) || !BoundedText.IsValid(request.Id, 128, requireTrimmed: true) ||
                !request.Id.StartsWith("tool-making-request:", StringComparison.Ordinal) ||
                request.Id != ToolMakingRequestId(request.RequesterId, request.BuildingInstanceId, request.RecipeId, request.RequestedTick) ||
                !BoundedText.IsValid(request.BuildingInstanceId, 256, requireTrimmed: true) || !IsToolRequestRecipeId(request.RecipeId) ||
                ToolProgressionRules.Find(request.ItemKind) is null || request.RequestedTick < 0 || request.LastTransitionTick < request.RequestedTick ||
                request.AcceptedTick is { } acceptedTick && (acceptedTick < request.RequestedTick || acceptedTick > request.LastTransitionTick) ||
                request.Blocker is not null && !BoundedText.IsValid(request.Blocker, 160, requireTrimmed: true) || accepted != (request.WorkerId is not null) ||
                request.Status is ToolMakingRequestStatus.Requested or ToolMakingRequestStatus.Refused && (accepted || request.JobId is not null || request.OfferId is not null) ||
                request.Status is ToolMakingRequestStatus.Accepted or ToolMakingRequestStatus.Ready or ToolMakingRequestStatus.Offered or ToolMakingRequestStatus.Fulfilled && !accepted ||
                request.JobId is not null && !accepted || request.JobId is null && request.Status is ToolMakingRequestStatus.Ready or ToolMakingRequestStatus.Offered or ToolMakingRequestStatus.Fulfilled)
                throw new InvalidDataException("A tool request has invalid identity, authority, status or timing.");
        }
    }
}
