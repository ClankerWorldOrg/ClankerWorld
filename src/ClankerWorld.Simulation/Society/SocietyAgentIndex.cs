namespace ClankerWorld.Simulation.Society;

/// <summary>Pure lookup state keyed by the immutable agent-list snapshot.</summary>
internal static class SocietyAgentIndex
{
    private sealed record Index(Dictionary<string, SocietyAgent>? ById, SocietyAgent[] Agents);

    private static readonly Derived<IReadOnlyList<SocietyAgent>, Index> Indexes = new(Build, Equal);

    public static SocietyAgent Get(IReadOnlyList<SocietyAgent> agents, string id) =>
        Indexes.Get(agents) is { ById: { } byId, Agents: var snapshot } &&
        byId.Count == snapshot.Length && id is not null && byId.TryGetValue(id, out var person)
            ? person : GetSingle(agents, id);

    // Keep Single's existing missing/duplicate/null-ID behavior for malformed
    // inputs. A duplicate unrelated to the requested ID must not throw here.
    private static SocietyAgent GetSingle(IReadOnlyList<SocietyAgent> agents, string? id) => agents.Single(item => item.Id == id);

    private static Index Build(IReadOnlyList<SocietyAgent> agents)
    {
        var snapshot = agents.ToArray();
        Dictionary<string, SocietyAgent>? byId = snapshot.Length <= 8
            ? null : new(snapshot.Length, StringComparer.Ordinal);
        foreach (var person in snapshot)
            if (person.Id is not null) byId?.TryAdd(person.Id, person);
        return new(byId, snapshot);
    }

    private static bool Equal(Index left, Index right) => left.Agents.AsSpan().SequenceEqual(right.Agents, ReferenceEqualityComparer.Instance);
}
