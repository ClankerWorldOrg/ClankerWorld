using System.Text.Json;
using System.Text.Json.Serialization;

namespace ClankerWorld.Simulation;

/// <summary>A generated or parsed handle. Complete references retain their separate, unbounded contract.</summary>
[JsonConverter(typeof(IdentityJsonConverter))]
public sealed record Identity : IComparable<Identity>
{
    public string Value { get; }
    private Identity(string value) => Value = value;

    public static Identity Create(string value, string? hashPrefix = null) => new(DerivedId.Create(value, hashPrefix));

    /// <summary>Reads an existing spelling without hashing, trimming or normalization.</summary>
    public static Identity Parse(string value)
    {
        if (!IdentityRules.Reference(value, DerivedId.MaximumLength))
            throw new ArgumentException("An identity must be nonblank, bounded and free of controls.", nameof(value));
        return new(value);
    }

    public int CompareTo(Identity? other) => StringComparer.Ordinal.Compare(Value, other?.Value);
    public static bool operator <(Identity? left, Identity? right) => StringComparer.Ordinal.Compare(left?.Value, right?.Value) < 0;
    public static bool operator <=(Identity? left, Identity? right) => StringComparer.Ordinal.Compare(left?.Value, right?.Value) <= 0;
    public static bool operator >(Identity? left, Identity? right) => StringComparer.Ordinal.Compare(left?.Value, right?.Value) > 0;
    public static bool operator >=(Identity? left, Identity? right) => StringComparer.Ordinal.Compare(left?.Value, right?.Value) >= 0;
    public override string ToString() => Value;
    public static implicit operator string(Identity value) => value.Value;
}

public sealed class IdentityJsonConverter : JsonConverter<Identity>
{
    public override Identity Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String)
            throw new JsonException("An identity must be a string.");
        var value = reader.GetString();
        if (!IdentityRules.Reference(value, DerivedId.MaximumLength))
            throw new JsonException("An identity must be nonblank, bounded and free of controls.");
        return Identity.Parse(value!);
    }

    public override void Write(Utf8JsonWriter writer, Identity value, JsonSerializerOptions options) => writer.WriteStringValue(value.Value);
    public override Identity ReadAsPropertyName(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) => ReadProperty(reader.GetString());
    public override void WriteAsPropertyName(Utf8JsonWriter writer, Identity value, JsonSerializerOptions options) => writer.WritePropertyName(value.Value);

    private static Identity ReadProperty(string? value)
    {
        if (!IdentityRules.Reference(value, DerivedId.MaximumLength)) throw new JsonException("Invalid identity key.");
        return Identity.Parse(value!);
    }
}
