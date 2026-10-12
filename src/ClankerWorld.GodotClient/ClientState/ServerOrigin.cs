using System.Net;

namespace ClankerWorld.GodotClient.ClientState;

/// <summary>
/// Canonicalizes the one server origin a paired Windows device is allowed to
/// contact. A device key is bound to server/world identity by the protocol;
/// pinning the transport origin as well prevents a saved registration from
/// quietly being redirected through an arbitrary HTTPS relay.
/// </summary>
public static class ServerOrigin
{
    public static bool TryResolve(string? value, out Uri origin)
    {
        origin = null!;
        if (!Uri.TryCreate(value?.Trim(), UriKind.Absolute, out var candidate) ||
            !UsesApprovedTransport(candidate) ||
            !string.IsNullOrEmpty(candidate.UserInfo) ||
            !string.IsNullOrEmpty(candidate.Query) ||
            !string.IsNullOrEmpty(candidate.Fragment) ||
            !string.Equals(candidate.AbsolutePath, "/", StringComparison.Ordinal))
        {
            return false;
        }

        origin = new UriBuilder(candidate.Scheme, candidate.Host, candidate.Port).Uri;
        return true;
    }

    public static bool Same(Uri left, Uri right)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);
        return string.Equals(left.Scheme, right.Scheme, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(left.Host, right.Host, StringComparison.OrdinalIgnoreCase) &&
            left.Port == right.Port;
    }

    private static bool UsesApprovedTransport(Uri uri)
    {
        if (string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var literalHost = uri.Host.Trim('[', ']');
        return string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) &&
            IPAddress.TryParse(literalHost, out var address) &&
            IPAddress.IsLoopback(address);
    }
}
