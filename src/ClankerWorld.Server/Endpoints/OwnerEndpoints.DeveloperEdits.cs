using ClankerWorld.Simulation.World;
using ClankerWorld.Server.Pairing;
using ClankerWorld.Server.Saves;

namespace ClankerWorld.Server.Endpoints;

internal static partial class OwnerEndpoints
{
    private static void MapDeveloperEdits(WebApplication app)
    {
        app.MapPost("/api/v1/owner/developer-edit", (
            OwnerSignedHttpRequest<OwnerDeveloperEditAction> request,
            IServiceProvider services) =>
        {
            var action = request.Action;
            var runtime = services.GetRequiredService<WorldRuntime>();
            var result = services.GetRequiredService<WorldStateFile>().ApplyDeveloperEdit(runtime,
                new(action.WorldId, action.ExpectedEventId, action.AgentId, action.Operation, action.Value, action.Amount, action.OtherAgentId));
            if (result.Failure is { } failure) return Results.Conflict(new { error = failure });
            return Results.Ok(OwnerControlReceipt.From("developer_edit", result.Applied, runtime.ExportState()));

        }).ForWorld<OwnerDeveloperEditAction>(action => action.WorldId,
            OwnerHttpBinding.DeveloperEditPayload,
            (_, _) => Results.Conflict(new { error = "The active world changed. Refresh before editing." }),
            validate: action => action.ExpectedEventId < 0 ||
                new[] { action.WorldId, action.AgentId, action.Operation, action.Value }
                    .Any(value => string.IsNullOrWhiteSpace(value) || value.Length > 512 || value.Any(char.IsControl)) ||
                action.OtherAgentId is { } other && (other.Length > 512 || other.Any(char.IsControl))
                ? Results.ValidationProblem(new Dictionary<string, string[]> { ["action"] = ["Choose an agent and a valid developer edit."] }) : null,
            missingAction: () => Results.ValidationProblem(new Dictionary<string, string[]> { ["action"] = ["Choose an agent and a valid developer edit."] }));
    }
}
