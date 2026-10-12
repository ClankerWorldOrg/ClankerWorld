using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.World;
using ClankerWorld.Server.Pairing;
using ClankerWorld.Server.Providers;
using ClankerWorld.Server.Saves;

namespace ClankerWorld.Server.Endpoints;

internal static partial class OwnerEndpoints
{
    private static void MapInstructions(WebApplication app)
    {
        app.MapPost("/api/v1/owner/instructions", (
            OwnerSignedHttpRequest<OwnerInstructionAction> request,
            OwnerRequestAuthorizer authorizer,
            IServiceProvider services) =>
        {
            if (request?.Action is null || !TryParseInstructionKind(request.Action.Kind, out var kind))
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["action.kind"] = ["Instruction kind must be suggestion or order."],
                });
            }

            string payload;
            try
            {
                payload = OwnerHttpBinding.InstructionPayload(request.Action);
            }
            catch (ArgumentException exception)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["action"] = [exception.Message],
                });
            }

            var authorization = authorizer.Authorize(request, "POST", "/api/v1/owner/instructions", payload);
            if (!authorization.IsSuccess)
            {
                return OwnerFailures.ToHttpResult(authorization.Failure);
            }


            var worldRuntime = services.GetRequiredService<WorldRuntime>();
            var worldStateFile = services.GetRequiredService<WorldStateFile>();
            try
            {
                // Selection and load use this same gate. Keep the world check,
                // mutation and durable save together so selection cannot retarget a retry.
                lock (services.GetRequiredService<ProviderConfigurationStore>().WorldMutationGate)
                {
                    if (request.Action.WorldId != worldRuntime.Society.WorldId)
                        return Results.Conflict(new OwnerControlFailure("world_mismatch",
                            "Return to the world where this instruction was sent before retrying."));
                    var receipt = worldRuntime.SubmitInstruction(new OwnerInstructionRequest(
                        request.Action.IdempotencyKey,
                        $"owner-device:{authorization.Value!.DeviceId}",
                        request.Action.TargetAgentId,
                        kind,
                    request.Action.Text,
                    request.Action.Queue));
                    worldStateFile.Save(worldRuntime);
                    return Results.Ok(receipt);
                }
            }
            catch (ArgumentException exception)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["action"] = [exception.Message],
                });
            }
            catch (InvalidOperationException exception)
            {
                return Results.Conflict(new OwnerControlFailure("idempotency_conflict", exception.Message));
            }


        });

        app.MapPost("/api/v1/owner/orders/cancel", (
            OwnerSignedHttpRequest<OwnerOrderCancelAction> request,
            OwnerRequestAuthorizer authorizer,
            IServiceProvider services) =>
        {
            if (request?.Action is null)
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["action"] = ["An order cancellation is required."],
                });

            string payload;
            try
            {
                payload = OwnerHttpBinding.OrderCancelPayload(request.Action);
            }
            catch (ArgumentException exception)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["action"] = [exception.Message],
                });
            }

            var authorization = authorizer.Authorize(request, "POST", "/api/v1/owner/orders/cancel", payload);
            if (!authorization.IsSuccess)
                return OwnerFailures.ToHttpResult(authorization.Failure);

            var worldRuntime = services.GetRequiredService<WorldRuntime>();
            var worldStateFile = services.GetRequiredService<WorldStateFile>();
            try
            {
                lock (services.GetRequiredService<ProviderConfigurationStore>().WorldMutationGate)
                {
                    if (request.Action.WorldId != worldRuntime.Society.WorldId)
                        return Results.Conflict(new OwnerControlFailure("world_mismatch",
                            "Return to the world where this order was sent before cancelling it."));
                    var receipt = worldRuntime.CancelOrder(new OwnerOrderCancelRequest(
                        request.Action.IdempotencyKey,
                        $"owner-device:{authorization.Value!.DeviceId}",
                        request.Action.WorldId,
                        request.Action.TargetAgentId,
                        request.Action.OrderId));
                    worldStateFile.Save(worldRuntime);
                    return Results.Ok(receipt);
                }
            }
            catch (ArgumentException exception)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["action"] = [exception.Message],
                });
            }
            catch (InvalidOperationException exception)
            {
                return Results.Conflict(new OwnerControlFailure("idempotency_conflict", exception.Message));
            }
        });
    }
}
