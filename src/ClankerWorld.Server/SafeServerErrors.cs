using ClankerWorld.Server.Pairing;
using ClankerWorld.Server.Saves;
using Microsoft.AspNetCore.Diagnostics;

namespace ClankerWorld.Server;

internal static partial class SafeServerErrors
{
    public static void Configure(IApplicationBuilder app, ILogger logger) => app.UseExceptionHandler(new ExceptionHandlerOptions
    {
        SuppressDiagnosticsCallback = _ => true,
        AllowStatusCode404Response = true,
        ExceptionHandler = context =>
        {
            var failure = context.Features.Get<IExceptionHandlerFeature>();
            if (failure?.Error is SavedFileLimitException limit)
                return Results.Conflict(new OwnerControlFailure("save_too_large", limit.Message)).ExecuteAsync(context);
            var status = failure?.Error is BadHttpRequestException requestError
                ? requestError.StatusCode : StatusCodes.Status500InternalServerError;
            if (status >= StatusCodes.Status500InternalServerError)
                Failed(logger, failure?.Error.GetType().Name ?? "unknown",
                    (failure?.Endpoint as RouteEndpoint)?.RoutePattern.RawText ?? "unmatched");
            return Results.Problem(statusCode: status,
                title: status >= StatusCodes.Status500InternalServerError
                    ? "The server could not finish this request."
                    : "The request could not be read.").ExecuteAsync(context);
        },
    });

    [LoggerMessage(EventId = 2326, Level = LogLevel.Error, Message = "server_request outcome=failed error_type={ErrorType} route={Route}")]
    private static partial void Failed(ILogger logger, string errorType, string route);
}
