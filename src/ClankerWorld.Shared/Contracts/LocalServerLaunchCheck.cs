using System.Security.Cryptography;
using System.Text;

namespace ClankerWorld.Contracts;

/// <summary>The local server's answer to a launch check.</summary>
public sealed record LocalServerLaunchCheckAnswer(string Answer);

/// <summary>
/// How the game confirms that the local server on its ports holds this
/// launch's secret. The game sends a fresh random challenge and the server
/// answers with an HMAC of it keyed by the secret, so the secret itself never
/// goes over the connection for this check.
/// </summary>
public static class LocalServerLaunchCheck
{
    public const string ChallengeHeader = "X-ClankerWorld-Launch-Check";
    private const int ChallengeBytes = 32;
    private const string Domain = "clankerworld-local-server-launch-check.v1\n";

    // 32 random bytes as unpadded base64url.
    private static readonly int ChallengeLength = (ChallengeBytes * 4 + 2) / 3;

    public static string NewChallenge() => Base64Url(RandomNumberGenerator.GetBytes(ChallengeBytes));

    public static bool IsChallenge(string? value) =>
        value is not null && value.Length == ChallengeLength &&
        value.All(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_');

    public static string Answer(string secret, string challenge) =>
        Base64Url(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(Domain + challenge)));

    public static bool Matches(string secret, string challenge, string? answer) =>
        answer is not null && CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(Answer(secret, challenge)), Encoding.UTF8.GetBytes(answer));

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
