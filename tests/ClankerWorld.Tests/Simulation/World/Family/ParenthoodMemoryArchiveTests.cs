using ClankerWorld.Simulation;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.World;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Tests;

public sealed partial class ParenthoodTests
{
    [Theory]
    [InlineData("requested", false)]
    [InlineData("preparing", false)]
    [InlineData("postponed", true)]
    [InlineData("completed", true)]
    public async Task DailyMemoryArchiveProtectsOnlyAnActiveNativeParenthoodPlan(string stage, bool archives)
    {
        var seed = await PreparedState();
        var owner = seed.Agents[0].AgentId;
        var partner = seed.Agents[1].AgentId;
        var day = seed.Society.Society.Config.TicksPerWorldDay;
        var boundary = 4L * day;
        var proposing = new ParentProvider("parent_propose:");
        using var family = WorldRuntime.Restore(seed, actor => actor == owner ? proposing : new ParentProvider(
            actor == partner && stage != "requested" ? stage == "postponed" ? "parent_postpone:" : "parent_accept:" : "safe_idle"));
        // Move only idle calendar time. The actual offered choices create each plan stage.
        if (stage != "completed")
        {
            PositionFamilyFixtureAt(family, boundary - 24);
            family.Pause();
            var fed = family.ExportState();
            // The calendar jump spoils starter food. Supply fresh household bread as
            // a fixture control; consent and plan transitions still use offered choices.
            family.LoadPausedCheckpoint(fed with
            {
                Society = fed.Society with
                {
                    Society = fed.Society.Society with
                    {
                        Inventory = InventoryAuthority.AddLot(fed.Society.Society.Inventory, "archive-parent-food", "bread",
                            fed.Society.Society.GetAgent(owner).HouseholdId!, 24),
                    },
                },
            });
            family.Resume();
        }
        for (var attempt = 0; attempt < 10 && family.Agents.Single(person => person.AgentId == owner).Parenthood is null; attempt++)
            Assert.True((await family.AdvanceOneTickAsync()).Advanced);
        Assert.True(family.Agents.Single(person => person.AgentId == owner).Parenthood is not null,
            $"Age={family.Society.GetAgent(owner).AgeBand}; food={WorldRuntime.ParenthoodFoodReadiness(family.Society, owner, family.Towns)}; " +
            string.Join(";", proposing.SeenCandidates.Select(candidate => candidate.Id)));
        Assert.Equal("requested", family.Agents.Single(person => person.AgentId == owner).Parenthood!.Stage);
        if (stage != "requested")
            for (var attempt = 0; attempt < 10 && family.Agents.Single(person => person.AgentId == owner).Parenthood!.Stage == "requested"; attempt++)
                Assert.True((await family.AdvanceOneTickAsync()).Advanced);
        if (stage == "completed")
        {
            var preparation = family.Agents.Single(person => person.AgentId == owner).Parenthood!;
            Assert.Equal("preparing", preparation.Stage);
            PositionFamilyFixtureAt(family, preparation.LastTransitionTick + 599);
            Assert.True((await family.AdvanceOneTickAsync()).Advanced);
            var birth = Assert.Single(family.Society.Births);
            Assert.Equal(birth.ChildId, family.Agents.Single(person => person.AgentId == owner).Parenthood!.ChildId);
        }
        PositionFamilyFixtureAt(family, boundary - 1);
        var plan = family.Agents.Single(person => person.AgentId == owner).Parenthood!;
        Assert.Equal(stage, plan.Stage);
        Assert.Equal(boundary - 1, family.WorldTick);
        var state = family.ExportState();
        var ordinary = new SocietySocialMemory(Identity.Parse("parent-old-experience"), owner, partner, "An old ordinary experience.", "private", 0);
        var control = new SocietySocialMemory(Identity.Parse("partner-old-experience"), partner, owner, "An independent old experience.", "private", 0);
        var durable = new SocietySocialMemory(Identity.Parse("parent-life-event"), owner, partner, "A lasting family event.", "private", 0)
        { Kind = SocietyMemoryKind.LifeEvent };
        var belief = new SocietyAgentBelief(Identity.Parse("parent-old-belief"), owner, "An old report about a path.",
            SocietyBeliefProvenance.Hearsay, 4_200, 0, SourceAgentId: partner);
        state = state with
        {
            JevEnabled = false,
            RoutineHelper = RoutineHelperSettings.Off,
            Society = state.Society with
            {
                Society = state.Society.Society with
                {
                    Memories = state.Society.Society.Memories.Concat([ordinary, control, durable])
                        .OrderBy(item => item.Id.Value, StringComparer.Ordinal).ToArray(),
                    Beliefs = (state.Society.Society.Beliefs ?? []).Append(belief)
                        .OrderBy(item => item.Id.Value, StringComparer.Ordinal).ToArray(),
                },
            },
        };
        var bytes = WorldRuntimeCodec.Encode(state);
        using var world = WorldRuntime.Restore(WorldRuntimeCodec.Decode(bytes), _ => new ParentProvider("safe_idle"));
        using var replay = WorldRuntime.Restore(WorldRuntimeCodec.Decode(bytes), _ => new ParentProvider("safe_idle"));
        Assert.False((await world.AdvanceOneTickAsync(commitPermitted: () => false)).Advanced);
        Assert.Equal(bytes, WorldRuntimeCodec.Encode(world.ExportState()));
        Assert.True((await world.AdvanceOneTickAsync()).Advanced);
        Assert.True((await replay.AdvanceOneTickAsync()).Advanced);
        var saved = world.ExportState();
        Assert.Equal(boundary, world.WorldTick);
        Assert.Equal(plan, saved.Agents.Single(person => person.AgentId == owner).Parenthood);
        Assert.Equal(archives, world.Society.ArchivedMemories.Any(item => item.Memory.Id == ordinary.Id));
        Assert.Equal(archives, world.Society.ArchivedBeliefs.Any(item => item.Belief.Id == belief.Id));
        Assert.Equal(!archives, world.Society.Memories.Contains(ordinary));
        Assert.Equal(!archives, world.Society.Beliefs!.Contains(belief));
        Assert.Contains(durable, world.Society.Memories);
        var independent = Assert.Single(world.Society.ArchivedMemories, item => item.Memory.Id == control.Id);
        Assert.Equal((control, boundary), (independent.Memory, independent.ArchivedTick));
        if (archives)
        {
            var archived = Assert.Single(world.Society.ArchivedMemories, item => item.Memory.Id == ordinary.Id);
            Assert.Equal((ordinary, boundary), (archived.Memory, archived.ArchivedTick));
            var archivedBelief = Assert.Single(world.Society.ArchivedBeliefs, item => item.Belief.Id == belief.Id);
            Assert.Equal((belief, boundary), (archivedBelief.Belief, archivedBelief.ArchivedTick));
        }
        var after = WorldRuntimeCodec.Encode(saved);
        Assert.Equal(after, WorldRuntimeCodec.Encode(replay.ExportState()));
        using var loaded = WorldRuntime.Restore(WorldRuntimeCodec.Decode(after), _ => new ParentProvider("safe_idle"));
        Assert.Equal(after, WorldRuntimeCodec.Encode(loaded.ExportState()));
        Assert.Equal(plan, loaded.Agents.Single(person => person.AgentId == owner).Parenthood);
    }
}
