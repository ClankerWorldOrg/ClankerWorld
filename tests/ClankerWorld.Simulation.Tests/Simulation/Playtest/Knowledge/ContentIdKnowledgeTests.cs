using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Playtest;

namespace ClankerWorld.Simulation.Tests;

public sealed class ContentIdKnowledgeTests
{
    [Theory]
    [InlineData(48, true)]
    [InlineData(49, true)]
    [InlineData(512, true)]
    [InlineData(513, false)]
    public void KnowledgeAndItsModelContextUseTheImportLimit(int length, bool accepted)
    {
        using var generated = NormalPathWorld.CreateGenerated("content-id-knowledge", _ => new ContentIdFixture.Idle());
        var state = generated.ExportState();
        var actor = state.Inhabitants[0].InhabitantId;
        var point = state.Inhabitants[0].Position;
        var terrain = state.Map.Tiles.Single(tile => tile.Position == point).Terrain.ToString();
        var kind = "a." + new string('x', length - 2);
        // Explicit knowledge-boundary arrangement; this is not evidence of a native discovery.
        var fact = new AgentKnowledgeFact("content-resource-fact", actor, actor, point, terrain, [kind], state.Society.Society.WorldTick, "firsthand");
        state = state with { Knowledge = state.Knowledge! with { Facts = [fact] } };
        var observation = new InhabitantObservation(actor, state.Society.Society.WorldTick, state.Society.Society.RunEpoch,
            1, "content-resource-observation", 8_000, [new("safe_idle", "Wait", 0)],
            KnownMapFacts: [new(point.X, point.Y, terrain, [kind], actor, fact.LearnedTick, "firsthand")]);
        if (accepted)
        {
            new ContentQuantity(kind, 1).Validate();
            observation.Validate();
            using var restored = PrivateWorldRuntime.Restore(state, _ => new ContentIdFixture.Idle());
            var saved = ContentIdFixture.Saved(restored);
            Assert.Equal(kind, Assert.Single(saved.Knowledge!.Facts).ResourceKinds[0]);
        }
        else
        {
            Assert.Throws<ArgumentException>(() => new ContentQuantity(kind, 1).Validate());
            Assert.Throws<ArgumentException>(observation.Validate);
            Assert.Throws<InvalidDataException>(() => PrivateWorldRuntime.Restore(state));
        }
    }
}
