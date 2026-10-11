using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace ClankerWorld.Simulation;

/// <summary>Bounds identities derived from other identities without changing their short form.</summary>
public static class DerivedId
{
    public const int MaximumLength = 128;
    private const int DigestLength = 64;

    /// <summary>Preserves a short identity; otherwise hashes the complete value under its semantic prefix.</summary>
    public static string Create(string value, string? hashPrefix = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        foreach (var character in value)
            if (char.IsControl(character))
                throw new ArgumentException("An identity cannot contain control characters.", nameof(value));
        if (value.Length <= MaximumLength) return value;

        if (hashPrefix is null)
        {
            var separator = value.IndexOf(':', StringComparison.Ordinal);
            hashPrefix = separator >= 0 && separator + 1 + "sha256:".Length + DigestLength <= MaximumLength
                ? value[..(separator + 1)] + "sha256:"
                : "derived:sha256:";
        }
        return Hash(value, hashPrefix);
    }

    /// <summary>Keeps a sequence suffix available to strict record validators while hashing the complete long identity.</summary>
    public static string Numbered(string prefix, long number)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(prefix);
        if (prefix.Any(char.IsControl))
            throw new ArgumentException("An identity prefix cannot contain control characters.", nameof(prefix));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(number);
        var suffix = number.ToString(CultureInfo.InvariantCulture);
        var value = prefix + suffix;
        if (value.Length <= MaximumLength) return Create(value);
        var separator = prefix.IndexOf(':', StringComparison.Ordinal);
        var hashPrefix = separator >= 0 && separator + 1 + "sha256:".Length + DigestLength + 1 + suffix.Length <= MaximumLength
            ? prefix[..(separator + 1)] + "sha256:"
            : "sequence:sha256:";
        return Hash(value, hashPrefix) + ":" + suffix;
    }

    /// <summary>Checks the exact generated spelling, counter and shared bound without searching prior sequence numbers.</summary>
    public static bool MatchesNumbered(string? value, string prefix, long maximumNumber)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > MaximumLength) return false;
        var separator = value.LastIndexOf(':');
        return separator >= 0 && long.TryParse(value.AsSpan(separator + 1), NumberStyles.None,
            CultureInfo.InvariantCulture, out var number) && number > 0 && number <= maximumNumber &&
            value == Numbered(prefix, number);
    }

    /// <summary>Shares hashing for generators whose existing short format already uses a digest.</summary>
    public static string Hash(string value, string prefix = "", int hexadecimalCharacters = DigestLength)
    {
        ArgumentNullException.ThrowIfNull(value);
        ArgumentNullException.ThrowIfNull(prefix);
        if (hexadecimalCharacters is < 1 or > DigestLength)
            throw new ArgumentOutOfRangeException(nameof(hexadecimalCharacters));
        if (prefix.Length + hexadecimalCharacters > MaximumLength || prefix.Any(char.IsControl))
            throw new ArgumentException("The complete identity must fit the shared limit.", nameof(prefix));
        return prefix + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)))[..hexadecimalCharacters];
    }

    /// <summary>Preserves the existing unpadded URL-safe digest spelling of land-grant identities.</summary>
    public static string HashBase64Url(string value, string prefix = "")
    {
        ArgumentNullException.ThrowIfNull(value);
        ArgumentNullException.ThrowIfNull(prefix);
        if (prefix.Length + 43 > MaximumLength || prefix.Any(char.IsControl))
            throw new ArgumentException("The complete identity must fit the shared limit.", nameof(prefix));
        return prefix + Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(value)))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

}
