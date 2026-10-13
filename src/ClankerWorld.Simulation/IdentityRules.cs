namespace ClankerWorld.Simulation;

/// <summary>Validates complete references without shortening or rewriting their saved spelling.</summary>
public static class IdentityRules
{
    public static bool Reference(string? value, int maximumLength = int.MaxValue,
        bool requireTrimmed = false, bool forbidWhitespace = false) =>
        BoundedText.IsValid(value, maximumLength, requireTrimmed) &&
        (!forbidWhitespace || !value!.Any(char.IsWhiteSpace));

    // Belief handles also enter telemetry, where caller-provided prose is forbidden.
    public static bool Belief(string? value) => Reference(value, DerivedId.MaximumLength, requireTrimmed: true) &&
        value!.All(character => char.IsAsciiLetterOrDigit(character) || character is ':' or '-' or '_');
}
