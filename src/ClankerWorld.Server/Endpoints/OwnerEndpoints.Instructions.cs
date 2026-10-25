using ClankerWorld.Simulation;
using ClankerWorld.Simulation.World;
using ClankerWorld.Server.Pairing;
using ClankerWorld.Server.Saves;

namespace ClankerWorld.Server.Endpoints;

internal static partial class OwnerEndpoints
{
    private static void MapInstructions(WebApplication app)
    {
        app.MapPost("/api/v1/owner/instructions", (
            OwnerSignedHttpRequest<OwnerInstructionAction> request,
            IServiceProvider services) =>
        {
            var action = request.Action;
            var kind = action.Kind.Trim().Equals("order", StringComparison.OrdinalIgnoreCase)
                ? OwnerInstructionKind.Order : OwnerInstructionKind.Suggestion;
            var worldRuntime = services.GetRequiredService<WorldRuntime>();
            var worldStateFile = services.GetRequiredService<WorldStateFile>();
            try
            {
                // Selection and load use this same gate. Keep the world check,
                // mutation and durable save together so selection cannot retarget a retry.
                var receipt = worldRuntime.SubmitInstruction(new OwnerInstructionRequest(
                    request.Action.IdempotencyKey,
                    DerivedId.Create($"owner-device:{request.DeviceId}"),
                    request.Action.TargetAgentId,
                    kind,
                request.Action.Text,
                request.Action.Queue));
                worldStateFile.Save(worldRuntime);
                return Results.Ok(receipt);

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


        }).ForWorld<OwnerInstructionAction>(action => action.WorldId,
            OwnerHttpBinding.InstructionPayload,
            (_, _) => Results.Conflict(new OwnerControlFailure("world_mismatch", "Return to the world where this instruction was sent before retrying.")),
            validate: action => !TryParseInstructionKind(action.Kind, out _) ? Results.ValidationProblem(new Dictionary<string, string[]> { ["action.kind"] = ["Instruction kind must be suggestion or order."] }) : null,
            missingAction: () => Results.ValidationProblem(new Dictionary<string, string[]> { ["action.kind"] = ["Instruction kind must be suggestion or order."] }));

        app.MapPost("/api/v1/owner/orders/cancel", (
            OwnerSignedHttpRequest<OwnerOrderCancelAction> request,
            IServiceProvider services) =>
        {
            var action = request.Action;
            var worldRuntime = services.GetRequiredService<WorldRuntime>();
            var worldStateFile = services.GetRequiredService<WorldStateFile>();
            try
            {
                var receipt = worldRuntime.CancelOrder(new OwnerOrderCancelRequest(
                    request.Action.IdempotencyKey,
                    DerivedId.Create($"owner-device:{request.DeviceId}"),
                    request.Action.WorldId,
                    request.Action.TargetAgentId,
                    request.Action.OrderId));
                worldStateFile.Save(worldRuntime);
                return Results.Ok(receipt);

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
        }).ForWorld<OwnerOrderCancelAction>(action => action.WorldId,
            OwnerHttpBinding.OrderCancelPayload,
            (_, _) => Results.Conflict(new OwnerControlFailure("world_mismatch", "Return to the world where this order was sent before cancelling it.")),
            missingAction: () => Results.ValidationProblem(new Dictionary<string, string[]> { ["action"] = ["An order cancellation is required."] }));
    }
}
