using ClankerWorld.Simulation.World;

namespace ClankerWorld.Tests;

internal static class MarketTestRules
{
    internal static WorldRuntimeState Fresh(WorldRuntimeState state) => state with
    {
        Agents = state.Agents.Select(person => person with
        { FullnessBasisPoints = 9_500, Survival = new SurvivalCondition(), LastDecisionContext = null, Project = null }).ToArray(),
    };

    internal static MarketRulesPolicy Policy(string actor, params string[] actions) => new()
    {
        Choose = (person, candidates) => person == actor
            ? actions.Select(prefix => candidates.FirstOrDefault(candidate => candidate.Id.StartsWith(prefix, StringComparison.Ordinal)))
                .FirstOrDefault(candidate => candidate is not null) ?? candidates.Single(candidate => candidate.Id == "safe_idle")
            : candidates.Single(candidate => candidate.Id == "safe_idle"),
    };

    internal static void AssertReload(WorldRuntime world)
    {
        world.Validate();
        var bytes = WorldRuntimeCodec.Encode(world.ExportState());
        using var restored = WorldRuntime.Restore(WorldRuntimeCodec.Decode(bytes));
        restored.Validate();
        Assert.Equal(bytes, WorldRuntimeCodec.Encode(restored.ExportState()));
    }
}
