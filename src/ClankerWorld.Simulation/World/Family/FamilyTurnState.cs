namespace ClankerWorld.Simulation.World;

internal sealed class FamilyTurnState
{
    private readonly HashSet<string> actions = new(StringComparer.Ordinal);
    internal bool Contains(string actor) => actions.Contains(actor);
    internal void RecordAction(string actor) => actions.Add(actor);
    internal void RecordActions(IEnumerable<string> actors) => actions.UnionWith(actors);
    internal void Reset() => actions.Clear();
}
