using System.Collections;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ClankerWorld.Simulation.Kernel;

/// <summary>Immutable, ordered hold values, including equality after a checkpoint reload.</summary>
[CollectionBuilder(typeof(InventoryHoldSet), nameof(Create))]
[JsonConverter(typeof(InventoryHoldSetJsonConverter))]
public sealed class InventoryHoldSet(IEnumerable<InventoryHold> holds) : IReadOnlyList<InventoryHold>, IEquatable<InventoryHoldSet>
{
    private readonly InventoryHold[] items = holds.ToArray();
    private static readonly InventoryHoldSet Empty = new(Array.Empty<InventoryHold>());
    public int Count => items.Length;
    public InventoryHold this[int index] => items[index];
    public static InventoryHoldSet Create(ReadOnlySpan<InventoryHold> holds) => holds.IsEmpty ? Empty : new(holds.ToArray());
    public IEnumerator<InventoryHold> GetEnumerator() => ((IEnumerable<InventoryHold>)items).GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    public bool Equals(InventoryHoldSet? other) => other is not null && items.SequenceEqual(other.items);
    public override bool Equals(object? obj) => obj is InventoryHoldSet other && Equals(other);
    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var hold in items) hash.Add(hold);
        return hash.ToHashCode();
    }
}

public sealed class InventoryHoldSetJsonConverter : JsonConverter<InventoryHoldSet>
{
    public override InventoryHoldSet Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        new(JsonSerializer.Deserialize<InventoryHold[]>(ref reader, options) ??
            throw new JsonException("A stock hold list cannot be null."));

    public override void Write(Utf8JsonWriter writer, InventoryHoldSet value, JsonSerializerOptions options) =>
        JsonSerializer.Serialize(writer, value.ToArray(), options);
}
