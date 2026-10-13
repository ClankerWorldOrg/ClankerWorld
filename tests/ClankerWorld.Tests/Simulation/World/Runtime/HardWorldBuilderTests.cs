using ClankerWorld.Simulation;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Society;
using ClankerWorld.Simulation.World;

namespace ClankerWorld.Tests;

public sealed class HardWorldBuilderTests
{
    [Theory]
    [InlineData("hard-defaults", false, false)]
    [InlineData("hard-short-days", true, false)]
    [InlineData("hard-chosen-single-names", false, true)]
    public async Task SharedWorldUsesMaximumFounderIdsUnicodeNamesAndAcceptedBirths(string seed, bool shortDays,
        bool chosenSingleNames)
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
        var initial = world.ExportState();
        if (chosenSingleNames)
        {
            var society = SocietyRules.RenameAgent(initial.Society.Society, NormalPathWorld.Founder1, "Élodie").Checkpoint;
            society = SocietyRules.RenameAgent(society, NormalPathWorld.Founder2, "Jose\u0301").Checkpoint;
            Assert.All(new[] { NormalPathWorld.Founder1, NormalPathWorld.Founder2 }, id =>
            {
                Assert.True(society.GetAgent(id).HasChosenName);
                Assert.Null(AgentNameRules.SurnameKey(society.GetAgent(id).Name));
            });
            initial = initial with { Society = initial.Society with { Society = society } };
        }
        var (state, child) = SecondGenerationFixture.Add(initial);
        if (chosenSingleNames)
        {
            Assert.Equal("Élodie Vale", state.Society.Society.GetAgent(NormalPathWorld.Founder1).Name);
            Assert.Equal("Jose\u0301 Vale", state.Society.Society.GetAgent(NormalPathWorld.Founder2).Name);
            Assert.Equal("Vale", AgentNameRules.SurnameKey(state.Society.Society.GetAgent(child).Name));
        }
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
