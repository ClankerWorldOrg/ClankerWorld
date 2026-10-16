using ClankerWorld.Contracts;
using ClankerWorld.Server.Observation;
using ClankerWorld.Simulation.World;

namespace ClankerWorld.Tests;

public sealed class ServerNeedStateTests
{
    [Theory]
    [InlineData(2_499, 3_499, 1_499, "very hungry", "cold", "healthy")]
    [InlineData(2_500, 3_500, 1_500, "hungry", "warm", "ill")]
    [InlineData(3_499, 3_999, 1_501, "hungry", "warm", "ill")]
    [InlineData(3_500, 4_000, 0, "fed", "warm", "healthy")]
    [InlineData(7_000, 8_000, 0, "well fed", "warm", "healthy")]
    public void TheGameReadsTheStatesProducedByTheServer(int fullness, int warmth, int illness,
        string expectedFullness, string expectedWarmth, string expectedIllness)
    {
        using var original = new WorldRuntime("need-state-edges");
        original.Pause();
        var state = original.ExportState();
        var actor = state.Agents[0].AgentId;
        state = state with
        {
            Survival = new SurvivalState(state.Society.Society.WorldTick, []),
            Agents = state.Agents.Select(person => person.AgentId == actor ? person with
            {
                FullnessBasisPoints = fullness,
                Survival = (person.Survival ?? new SurvivalCondition()) with { WarmthBasisPoints = warmth, IllnessBasisPoints = illness },
            } : person).ToArray(),
        };
        using var world = WorldRuntime.Restore(state);
        var agent = new OwnerWorldObservationStore(world).GetSnapshot().Agents.Single(person => person.Id == actor);
        Assert.Equal(expectedFullness, GameUiText.NeedLabel(agent, "fullness-state"));
        Assert.Equal(expectedWarmth, GameUiText.NeedLabel(agent, "warmth-state"));
        Assert.Equal(expectedIllness, GameUiText.NeedLabel(agent, "illness-state"));
        Assert.Equal(state.Agents.Single(person => person.AgentId == actor).FullnessBasisPoints, agent.FullnessBasisPoints);
    }

    [Fact]
    public void TheGameDoesNotGuessAStateWhenItsFactorIsAbsent()
    {
        var agent = new OwnerWorldAgent("long-agent-id", "Zoë 🌲", "active", new(1, 1), 0, [], [],
            new("idle", null, null, [], string.Empty), new(new(1, 1), [], []), false);
        Assert.Empty(GameUiText.NeedLabel(agent, "fullness-state"));
    }
    [Fact]
    public void PackedTerrainCodesKeepTheExistingServerBytesAndNames()
    {
        foreach (var terrain in Enum.GetValues<TerrainKind>())
        {
            var encoded = TerrainCodes.Encode(terrain.ToString());
            Assert.Equal((byte)terrain, encoded);
            Assert.Equal(terrain.ToString(), TerrainCodes.NameFor(TerrainCodes.RenderKind(encoded)));
        }
        Assert.Throws<InvalidDataException>(() => TerrainCodes.RenderKind(255));
    }

}
