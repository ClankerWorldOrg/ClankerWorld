using System.Collections.ObjectModel;

namespace ClankerWorld.Simulation.Playtest;

/// <summary>Immutable field snapshot; writes replace it and leave prepared siblings untouched.</summary>
internal sealed record FarmingState
{
    internal static FarmingState Empty { get; } = new([]);

    internal FarmingState(IEnumerable<FarmFieldState> fields) => Fields = Array.AsReadOnly(fields.ToArray());

    internal ReadOnlyCollection<FarmFieldState> Fields { get; }
}
