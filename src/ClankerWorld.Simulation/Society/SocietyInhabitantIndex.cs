namespace ClankerWorld.Simulation.Society;

/// <summary>Pure lookup state keyed by the immutable inhabitant-list snapshot.</summary>
internal static class SocietyInhabitantIndex
{
    private sealed record Index(Dictionary<string, SocietyInhabitant> ById, bool HasInvalidIds);

    private static readonly Derived<IReadOnlyList<SocietyInhabitant>, Index> Indexes = new(Build, Equal);

    public static SocietyInhabitant Get(IReadOnlyList<SocietyInhabitant> inhabitants, string id)
    {
        var index = Indexes.Get(inhabitants);
        if (!index.HasInvalidIds && id is not null && index.ById.TryGetValue(id, out var person))
            return person;
        // Keep Single's existing missing/duplicate/null-ID behavior for malformed
        // inputs. A duplicate unrelated to the requested ID must not throw here.
        return inhabitants.Single(item => string.Equals(item.Id, id, StringComparison.Ordinal));
    }

    private static Index Build(IReadOnlyList<SocietyInhabitant> inhabitants)
    {
        var byId = new Dictionary<string, SocietyInhabitant>(inhabitants.Count, StringComparer.Ordinal);
        var invalid = false;
        foreach (var person in inhabitants)
            if (person.Id is null || !byId.TryAdd(person.Id, person)) invalid = true;
        return new(byId, invalid);
    }

    private static bool Equal(Index left, Index right) => left.HasInvalidIds == right.HasInvalidIds &&
        left.ById.Count == right.ById.Count && left.ById.All(pair =>
            right.ById.TryGetValue(pair.Key, out var person) && ReferenceEquals(pair.Value, person));
}
