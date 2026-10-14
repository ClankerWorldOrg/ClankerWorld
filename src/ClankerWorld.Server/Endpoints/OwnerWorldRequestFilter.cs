using ClankerWorld.Server.Pairing;
using ClankerWorld.Server.Providers;
using ClankerWorld.Simulation.World;

namespace ClankerWorld.Server.Endpoints;

/// <summary>Authenticate first, then hold selection, the world check, mutation and save together.</summary>
internal sealed class OwnerWorldRequestFilter<TAction>(
    int requestIndex,
    Func<TAction, string> worldId,
    Func<TAction, string> payload,
    Func<HttpContext, TAction, IResult> wrongWorld,
    Func<TAction, IResult?>? validate = null,
    Func<HttpContext, ArgumentException, IResult>? invalidPayload = null,
    Func<IResult>? missingAction = null) : IEndpointFilter where TAction : class
{
    public ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var request = context.GetArgument<OwnerSignedHttpRequest<TAction>?>(requestIndex);
        if (request?.Action is not { } action)
            return Result(missingAction?.Invoke() ?? InvalidAction("An action is required."));
        if (validate?.Invoke(action) is { } failure) return Result(failure);
        string canonical;
        try { canonical = payload(action); }
        catch (ArgumentException exception)
        {
            return Result(invalidPayload?.Invoke(context.HttpContext, exception) ?? InvalidAction(exception.Message));
        }
        var services = context.HttpContext.RequestServices;
        var authorization = services.GetRequiredService<OwnerRequestAuthorizer>().Authorize(request,
            context.HttpContext.Request.Method, context.HttpContext.Request.Path, canonical);
        if (!authorization.IsSuccess) return Result(OwnerFailures.ToHttpResult(authorization.Failure));
        lock (services.GetRequiredService<ProviderConfigurationStore>().WorldMutationGate)
        {
            if (!string.Equals(worldId(action), services.GetRequiredService<WorldRuntime>().Society.WorldId,
                StringComparison.Ordinal))
                return Result(wrongWorld(context.HttpContext, action));
            // These mutation handlers are synchronous: keep the monitor through
            // the complete transition and durable save, never across an await.
            var result = next(context);
            if (!result.IsCompleted)
                throw new InvalidOperationException("World mutation filters must complete synchronously under the selection lock.");
            return Result(result.GetAwaiter().GetResult());
        }
    }

    private static IResult InvalidAction(string message) =>
        Results.ValidationProblem(new Dictionary<string, string[]> { ["action"] = [message] });

    private static ValueTask<object?> Result(object? value) => ValueTask.FromResult(value);
}

internal static class OwnerWorldRequestEndpoints
{
    public static RouteHandlerBuilder ForWorld<TAction>(this RouteHandlerBuilder route,
        Func<TAction, string> worldId, Func<TAction, string> payload,
        Func<HttpContext, TAction, IResult> wrongWorld,
        Func<TAction, IResult?>? validate = null,
        Func<HttpContext, ArgumentException, IResult>? invalidPayload = null,
        Func<IResult>? missingAction = null) where TAction : class =>
        route.AddEndpointFilterFactory((factory, next) =>
        {
            if (typeof(Task).IsAssignableFrom(factory.MethodInfo.ReturnType) ||
                factory.MethodInfo.ReturnType.Name.StartsWith("ValueTask", StringComparison.Ordinal))
                throw new InvalidOperationException("World mutation handlers must complete synchronously under the selection lock.");
            var index = Array.FindIndex(factory.MethodInfo.GetParameters(),
                parameter => parameter.ParameterType == typeof(OwnerSignedHttpRequest<TAction>));
            if (index < 0) throw new InvalidOperationException("A world endpoint must bind its signed action.");
            var filter = new OwnerWorldRequestFilter<TAction>(index, worldId, payload, wrongWorld,
                validate, invalidPayload, missingAction);
            return context => filter.InvokeAsync(context, next);
        });
}
