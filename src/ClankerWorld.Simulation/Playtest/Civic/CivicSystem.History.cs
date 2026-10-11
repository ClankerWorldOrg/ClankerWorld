namespace ClankerWorld.Simulation.Playtest;

internal sealed partial class CivicSystem
{
    private static readonly Derived<TownGovernanceState, CivicHistoryIndex> History = new(
        state => new CivicHistoryIndex(state), (left, right) => left.EquivalentTo(right));

    internal static CivicHistoryIndex CivicHistory(TownRuntimeState town) => History.Get(town.Governance!);

    internal sealed class CivicHistoryIndex
    {
        private static readonly IReadOnlySet<string> Empty = new HashSet<string>(StringComparer.Ordinal);
        private readonly Dictionary<string, HashSet<string>> knownByActor = new(StringComparer.Ordinal);
        private readonly Dictionary<string, HashSet<string>> subjectsByActor = new(StringComparer.Ordinal);
        private readonly Dictionary<string, HashSet<(string Kind, string Subject)>> kindsByActor = new(StringComparer.Ordinal);
        private readonly Dictionary<string, TownCivicReceipt[]> recentByActor = new(StringComparer.Ordinal);
        public TownGovernanceState State { get; }
        public Dictionary<string, TownCivicNotice> Notices { get; }

        public CivicHistoryIndex(TownGovernanceState state)
        {
            State = state;
            Notices = state.Notices.ToDictionary(n => n.Id, StringComparer.Ordinal);
            foreach (var actor in state.Knowledge.GroupBy(k => k.AgentId, StringComparer.Ordinal))
            {
                knownByActor[actor.Key] = actor.Select(k => k.NoticeId).ToHashSet(StringComparer.Ordinal);
                subjectsByActor[actor.Key] = actor.Select(k => Notices[k.NoticeId].SubjectId).ToHashSet(StringComparer.Ordinal);
                kindsByActor[actor.Key] = actor.Select(k => (Notices[k.NoticeId].Kind, Notices[k.NoticeId].SubjectId)).ToHashSet();
                recentByActor[actor.Key] = actor.OrderByDescending(k => k.LearnedTick)
                    .ThenByDescending(k => Notices[k.NoticeId].PostedTick).Take(3).ToArray();
            }
        }

        internal bool EquivalentTo(CivicHistoryIndex other) =>
            Notices.Count == other.Notices.Count && Notices.All(pair => other.Notices.TryGetValue(pair.Key, out var notice) && notice == pair.Value) &&
            knownByActor.Count == other.knownByActor.Count && knownByActor.All(pair => other.knownByActor.TryGetValue(pair.Key, out var known) && pair.Value.SetEquals(known)) &&
            subjectsByActor.Count == other.subjectsByActor.Count && subjectsByActor.All(pair => other.subjectsByActor.TryGetValue(pair.Key, out var subjects) && pair.Value.SetEquals(subjects)) &&
            kindsByActor.Count == other.kindsByActor.Count && kindsByActor.All(pair => other.kindsByActor.TryGetValue(pair.Key, out var kinds) && pair.Value.SetEquals(kinds)) &&
            recentByActor.Count == other.recentByActor.Count && recentByActor.All(pair => other.recentByActor.TryGetValue(pair.Key, out var recent) && pair.Value.SequenceEqual(recent));

        public IReadOnlySet<string> Known(string actor) => knownByActor.TryGetValue(actor, out var known) ? known : Empty;
        public bool Knows(string actor, string subject) => subjectsByActor.TryGetValue(actor, out var known) && known.Contains(subject);
        public bool Knows(string actor, string kind, string subject) => kindsByActor.TryGetValue(actor, out var known) && known.Contains((kind, subject));
        public TownCivicReceipt[] Recent(string actor) => recentByActor.TryGetValue(actor, out var recent) ? recent : [];
    }

}
