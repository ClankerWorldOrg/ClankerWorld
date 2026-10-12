using Microsoft.AspNetCore.Http.Features;

namespace ClankerWorld.Server.Pairing;

/// <summary>
/// Caps every request body before an endpoint binds it. Signed owner requests
/// are verified only after their JSON is parsed, so these caps apply to every
/// caller, paired or not, and each route gets only the room it needs.
/// </summary>
public static class RequestBodyLimits
{
    /// <summary>Starting, polling, activating and locally approving a pairing: one key or proof.</summary>
    public const long Pairing = 16 * 1024;

    /// <summary>
    /// Every other route that is not a signed owner action: handshake, status,
    /// challenge requests, the host-local routes and static files. This is also
    /// the listener's default.
    /// </summary>
    public const long Unauthenticated = 64 * 1024;

    /// <summary>
    /// Signed owner actions. The largest the game and the tests send, New World
    /// and content proposals, are about 2 KB; signed fields and keys are capped
    /// at a few KB each. Content package definitions carry JSON without a fixed
    /// size, so this leaves room for them with wide margin.
    /// </summary>
    public const long SignedOwnerAction = 1024 * 1024;

    public static long For(PathString path) => Classify(path).Limit;

    /// <summary>
    /// Refuses a body whose declared length is over its route's limit with 413
    /// before anything reads it, and caps a body without a declared length at the
    /// same size, so the server stops reading it there.
    /// </summary>
    public static void UseRequestBodyLimits(this WebApplication app)
    {
        var refusals = app.Services.GetRequiredService<RequestRefusals>();
        app.Use(async (context, next) =>
        {
            var (kind, limit) = Classify(context.Request.Path);
            if (context.Request.ContentLength > limit)
            {
                refusals.BodyTooLarge(kind, limit);
                context.Response.StatusCode = StatusCodes.Status413PayloadTooLarge;
                return;
            }

            var bodyLimit = context.Features.Get<IHttpMaxRequestBodySizeFeature>();
            if (bodyLimit is { IsReadOnly: false }) bodyLimit.MaxRequestBodySize = limit;
            await next(context);
        });
    }

    // Path matching ignores case, as routing does. Anything unexpected gets the
    // small limit; only the signed owner routes get the larger one.
    private static (string Kind, long Limit) Classify(PathString path)
    {
        if (path.StartsWithSegments("/api/v1/pairings") || path.StartsWithSegments("/api/v1/approval/pairings"))
            return ("pairing", Pairing);
        if (path.StartsWithSegments("/api/v1/owner") && !path.StartsWithSegments("/api/v1/owner/challenges"))
            return ("owner", SignedOwnerAction);
        return ("unauthenticated", Unauthenticated);
    }
}
