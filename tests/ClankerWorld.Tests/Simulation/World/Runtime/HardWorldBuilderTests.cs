using ClankerWorld.Simulation;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Society;
using ClankerWorld.Simulation.World;

namespace ClankerWorld.Tests;

public sealed class HardWorldBuilderTests
{
    [Theory]
    [InlineData("hard-defaults", false)]
    [InlineData("hard-short-days", true)]
    public async Task SharedWorldUsesMaximumFounderIdsUnicodeNamesAndAcceptedBirths(string seed, bool shortDays)
    {
        using var world = NormalPathWorld.CreateGenerated(seed, _ => new DeterministicDecisionProvider(),
            ticksPerDay: shortDays ? 8 : null);
        Assert.All(world.Society.Agents, person =>
        {
            Assert.Equal(DerivedId.MaximumLength, person.Id.Length);
            Assert.Contains(person.Name, character => character > 127);
        });
        Assert.Contains(world.Society.Agents, person => person.Name.Contains('\u0301'));
        Assert.Contains(world.Society.Agents, person => person.Name.EnumerateRunes().Any(rune => rune.Value > 0xffff));
        var (state, child) = SecondGenerationFixture.Add(world.ExportState());
        var birth = Assert.Single(state.Society.Society.Births);
        Assert.Equal(child, birth.ChildId);
        Assert.Contains(state.Society.Society.Relationships, edge => edge.Type == SocietyRelationshipType.BiologicalParentage &&
            edge.TargetId == child);
        Assert.Contains(state.Agents, person => person.AgentId == child);
        Assert.Contains(state.Towns!, town => town.ResidentIds.Contains(child, StringComparer.Ordinal));
        var bytes = WorldRuntimeCodec.Encode(state);
        using var restored = WorldRuntime.Restore(WorldRuntimeCodec.Decode(bytes));
        Assert.Equal(bytes, WorldRuntimeCodec.Encode(restored.ExportState()));
        Assert.True((await restored.AdvanceOneTickWithCheckpointAsync(proposed =>
        {
            WorldRuntimeCodec.Decode(WorldRuntimeCodec.Encode(proposed.ExportState()));
            return () => { };
        })).Advanced);
    }
}
