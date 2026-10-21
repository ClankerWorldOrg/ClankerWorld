using System.Text;

namespace ClankerWorld.Simulation;

public enum TextNormalization
{
    None,
    Whitespace,
    ControlsAndSpaces,
}

/// <summary>Text bounded in UTF-16 units, with complete Unicode scalar values at the cut.</summary>
public sealed record BoundedText
{
    public string Value { get; }
    private BoundedText(string value) => Value = value;

    public static BoundedText Create(string value, int maximumLength) => Excerpt(value, maximumLength);

    public static BoundedText Excerpt(string value, int maximumLength, string suffix = "",
        TextNormalization normalization = TextNormalization.None)
    {
        ArgumentNullException.ThrowIfNull(value);
        ArgumentNullException.ThrowIfNull(suffix);
        ArgumentOutOfRangeException.ThrowIfNegative(maximumLength);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(suffix.Length, maximumLength, nameof(suffix));
        var text = normalization switch
        {
            TextNormalization.None => value,
            TextNormalization.Whitespace => string.Join(' ', value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)),
            TextNormalization.ControlsAndSpaces => string.Join(' ', new string(value.Select(character =>
                char.IsControl(character) ? ' ' : character).ToArray()).Split(' ', StringSplitOptions.RemoveEmptyEntries)),
            _ => throw new ArgumentOutOfRangeException(nameof(normalization)),
        };
        if (text.Length <= maximumLength) return new(text);
        var length = maximumLength - suffix.Length;
        if (length > 0 && char.IsHighSurrogate(text[length - 1]) && char.IsLowSurrogate(text[length])) length--;
        return new(text[..length] + suffix);
    }

    public static bool IsWithinLimit(string value, int maximumLength) => value.Length <= maximumLength;

    public static bool IsValid(string? value, int maximumLength, bool requireTrimmed = false,
        bool allowWhitespaceOnly = false) =>
        (allowWhitespaceOnly ? !string.IsNullOrEmpty(value) : !string.IsNullOrWhiteSpace(value)) && IsWithinLimit(value, maximumLength) &&
        (!requireTrimmed || value == value.Trim()) && !value.Any(char.IsControl);

    public static bool IsInvisibleFormatting(Rune rune) =>
        Rune.GetUnicodeCategory(rune) == System.Globalization.UnicodeCategory.Format ||
        rune.Value is 0x034f or 0x17b4 or 0x17b5;

    /// <summary>Remove invisible formatting before checking or comparing model prose.</summary>
    public static string WithoutInvisibleFormatting(string value) => string.Concat(value.EnumerateRunes()
        .Where(rune => !IsInvisibleFormatting(rune))
        .Select(rune => rune.ToString()));

    public static string? NormalizeModelText(string? value, int maximumLength)
    {
        var text = value is null ? null : WithoutInvisibleFormatting(value).Trim();
        return IsValid(text, maximumLength, requireTrimmed: true) ? text : null;
    }

    public override string ToString() => Value;
    public static implicit operator string(BoundedText value) => value.Value;
}
