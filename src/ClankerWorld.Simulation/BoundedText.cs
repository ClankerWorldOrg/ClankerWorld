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

    public static bool IsValid(string? value, int maximumLength, bool requireTrimmed = false) =>
        !string.IsNullOrWhiteSpace(value) && value.Length <= maximumLength &&
        (!requireTrimmed || value == value.Trim()) && !value.Any(char.IsControl);

    public override string ToString() => Value;
    public static implicit operator string(BoundedText value) => value.Value;
}
