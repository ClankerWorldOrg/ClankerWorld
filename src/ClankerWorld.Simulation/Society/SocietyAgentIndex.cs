namespace ClankerWorld.Simulation.Society;

/// <summary>Pure lookup state keyed by the immutable agent-list snapshot.</summary>
internal static class SocietyAgentIndex
{
    private sealed class Index(SocietyAgent[] agents) : Dictionary<string, SocietyAgent>(agents.Length, StringComparer.Ordinal)
    {
        public SocietyAgent[] Agents { get; } = agents;
    }

    private static readonly Derived<IReadOnlyList<SocietyAgent>, Index> Indexes = new(Build, Equal);

    public static SocietyAgent Get(IReadOnlyList<SocietyAgent> agents, string id)
    {
        var index = Indexes.Get(agents);
        return index.Count == index.Agents.Length && id is not null && index.TryGetValue(id, out var person)
            ? person : GetSingle(agents, id);
    }

    // Keep Single's existing missing/duplicate/null-ID behavior for malformed
    // inputs. A duplicate unrelated to the requested ID must not throw here.
    private static SocietyAgent GetSingle(IReadOnlyList<SocietyAgent> agents, string? id) => agents.Single(item => item.Id == id);

    private static Index Build(IReadOnlyList<SocietyAgent> agents)
    {
        var index = new Index(agents.ToArray());
        foreach (var person in index.Agents)
            if (person.Id is not null) index.TryAdd(person.Id, person);
        return index;
    }

    private static bool Equal(Index left, Index right) => left.Agents.AsSpan().SequenceEqual(right.Agents, ReferenceEqualityComparer.Instance);
}
