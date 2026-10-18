using Microsoft.AspNetCore.Diagnostics;

namespace ClankerWorld.Server;

internal static partial class SafeServerErrors
{
    public static void Configure(IApplicationBuilder app, ILogger logger) => app.UseExceptionHandler(new ExceptionHandlerOptions
    {
        SuppressDiagnosticsCallback = _ => true,
        ExceptionHandler = context =>
        {
            var failure = context.Features.Get<IExceptionHandlerFeature>();
            Failed(logger, failure?.Error.GetType().Name ?? "unknown",
                (failure?.Endpoint as RouteEndpoint)?.RoutePattern.RawText ?? "unmatched");
            return Results.Problem(statusCode: StatusCodes.Status500InternalServerError,
                title: "The server could not finish this request.").ExecuteAsync(context);
        },
    });

    [LoggerMessage(EventId = 2326, Level = LogLevel.Error, Message = "server_request outcome=failed error_type={ErrorType} route={Route}")]
    private static partial void Failed(ILogger logger, string errorType, string route);
}
