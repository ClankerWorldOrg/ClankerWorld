using System.Text.Json;
using System.Text.Json.Serialization;

namespace ClankerWorld.Contracts;

/// <summary>Unusable optional build diagnostics read as unknown without refusing a world or save.</summary>
public sealed class SavedBuildTextConverter : JsonConverter<string>
{
    public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String)
        {
            reader.Skip();
            return null;
        }
        var value = reader.GetString();
        return ClankerWorld.Simulation.BoundedText.IsValid(value, 64, allowWhitespaceOnly: true) ? value : null;
    }

    public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value);
}
