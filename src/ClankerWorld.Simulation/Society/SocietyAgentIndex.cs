namespace ClankerWorld.Simulation.Society;

/// <summary>Pure lookup state keyed by the immutable agent-list snapshot.</summary>
internal static class SocietyAgentIndex
{
    private sealed record Index(Dictionary<string, SocietyAgent> ById, bool HasInvalidIds);

    private static readonly Derived<IReadOnlyList<SocietyAgent>, Index> Indexes = new(Build, Equal);

    public static SocietyAgent Get(IReadOnlyList<SocietyAgent> agents, string id)
    {
        var index = Indexes.Get(agents);
        if (!index.HasInvalidIds && id is not null && index.ById.TryGetValue(id, out var person))
            return person;
        // Keep Single's existing missing/duplicate/null-ID behavior for malformed
        // inputs. A duplicate unrelated to the requested ID must not throw here.
        return agents.Single(item => string.Equals(item.Id, id, StringComparison.Ordinal));
    }

    private static Index Build(IReadOnlyList<SocietyAgent> agents)
    {
        var byId = new Dictionary<string, SocietyAgent>(agents.Count, StringComparer.Ordinal);
        var invalid = false;
        foreach (var person in agents)
            if (person.Id is null || !byId.TryAdd(person.Id, person)) invalid = true;
        return new(byId, invalid);
    }

    private static bool Equal(Index left, Index right) => left.HasInvalidIds == right.HasInvalidIds &&
        left.ById.Count == right.ById.Count && left.ById.All(pair =>
            right.ById.TryGetValue(pair.Key, out var person) && ReferenceEquals(pair.Value, person));
}
