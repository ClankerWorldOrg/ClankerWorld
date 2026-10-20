namespace ClankerWorld.Simulation;

/// <summary>Validates complete references without shortening or rewriting their saved spelling.</summary>
public static class IdentityRules
{
    /// <summary>Builds an action reference without hashing or shortening its complete target.</summary>
    public static string PrefixedReference(string prefix, string? reference)
    {
        ArgumentNullException.ThrowIfNull(prefix);
        var value = prefix + reference;
        if (!Reference(value))
            throw new ArgumentException("A reference must be nonblank and free of controls.", nameof(reference));
        return value;
    }

    public static bool Reference(string? value, int maximumLength = int.MaxValue,
        bool requireTrimmed = false, bool forbidWhitespace = false) =>
        BoundedText.IsValid(value, maximumLength, requireTrimmed: requireTrimmed) &&
        (!forbidWhitespace || !value!.Any(char.IsWhiteSpace));

    // Belief handles also enter telemetry, where caller-provided prose is forbidden.
    public static bool Belief(string? value) => Reference(value, DerivedId.MaximumLength, requireTrimmed: true) &&
        value!.All(character => char.IsAsciiLetterOrDigit(character) || character is ':' or '-' or '_');
}
