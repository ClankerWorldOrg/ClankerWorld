using ClankerWorld.Simulation;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.World;
using ClankerWorld.Simulation.Society;
using ClankerWorld.Server.Observation;

namespace ClankerWorld.Tests;

public sealed class DeceasedAgentArchiveTests
{
    [Fact]
    public async Task DeathRemovesTheActiveActorButKeepsAnInspectableSavedRecord()
    {
        using var seed = new WorldRuntime("deceased-archive");
        var state = seed.ExportState();
        var society = state.Society.Society;
        var config = society.Config with
        {
            TicksPerWorldDay = 1,
            DaysPerWorldYear = 4,
            AdultYears = 17,
            ElderYears = 18,
            BaseNaturalMortalityBasisPoints = 9_999,
            NaturalMortalitySlopeBasisPoints = 0,
        };
        society = society with
        {
            Config = config,
            Memories = society.Agents.Select(person => new SocietySocialMemory(
                Identity.Create($"last-memory:{person.Id}"), person.Id, "founder-scout",
                "I remember the first harvest.", "private", 0)).ToArray(),
            Beliefs = society.Agents.Select(person => new SocietyAgentBelief(
                Identity.Create($"last-belief:{person.Id}"), person.Id, person.Id + " saw the first harvest.",
                SocietyBeliefProvenance.Firsthand, 7_000, 0)).ToArray(),
            Agents = society.Agents.Select(person => person with
            {
                BirthTick = -75,
                BirthLifeTick = null,
                AgeBand = SocietyAgeBand.Elder,
                LastLifecycleYearChecked = 18,
            }).ToArray(),
        };
        state = state with
        {
            Society = state.Society with { Society = society },
            WorldSystems = state.WorldSystems! with
            {
                RegionalWeather = null,
                Config = state.WorldSystems!.Config with
                {
                    TicksPerDay = 1,
                    CalendarOffsetTicks = 0,
                    DaysPerYear = 4,
                    SpringDays = 1,
                    SummerDays = 1,
                    AutumnDays = 1,
                    WinterDays = 1,
                },
            },
            Agents = state.Agents.Select(person => person with
            {
                RecentThoughts = [new RecentThought(0, "I hope the farm lasts.")],
                Skills = [new(SkillKind.Building, 0,
                    person.AgentId == "founder-ilya" ? "founder-scout" : "founder-ilya")],
            }).ToArray(),
        };
        using var world = WorldRuntime.Restore(state, _ => new DeterministicDecisionProvider());

        Assert.True((await world.AdvanceOneTickAsync()).Advanced);
        Assert.True((await world.AdvanceOneTickAsync()).Advanced);
        Assert.True((await world.AdvanceOneTickAsync()).Advanced);
        var archived = world.ExportState();
        Assert.NotEmpty(archived.DeceasedAgents ?? []);
        var deceased = archived.DeceasedAgents![0];
        Assert.Equal(deceased.LastPhysical.AgentId, deceased.AgentId);
        Assert.DoesNotContain(archived.Agents, person => person.AgentId == deceased.AgentId);

        Assert.DoesNotContain(archived.Society.Society.Memories, item => item.OwnerId == deceased.AgentId);
        Assert.Contains(archived.Society.Society.ArchivedMemories, item => item.Memory.OwnerId == deceased.AgentId);
        Assert.Contains(archived.Society.Society.ArchivedBeliefs, item => item.Belief.OwnerId == deceased.AgentId);
        var bytes = WorldRuntimeCodec.Encode(archived);
        using var restored = WorldRuntime.Restore(WorldRuntimeCodec.Decode(bytes));
        var projected = new OwnerWorldObservationStore(restored).GetSnapshot();
        var historical = Assert.Single(projected.Agents, person => person.Id == deceased.AgentId);
        Assert.Equal("dead", historical.Lifecycle);
        Assert.Equal("deceased", historical.Route.Status);
        Assert.Equal(deceased.DeathTick.ToString(System.Globalization.CultureInfo.InvariantCulture),
            historical.DecisionFactors.Single(factor => factor.Key == "death-tick").Detail);
        Assert.Null(historical.PublicIntention);
        Assert.Equal("I hope the farm lasts.", Assert.Single(historical.RecentPrivateThoughts).Text);
        Assert.Equal("I remember the first harvest.", Assert.Single(historical.RecentMemories).Summary);
        var belief = Assert.Single(historical.RecentBeliefs);
        Assert.Equal(deceased.AgentId + " saw the first harvest.", belief.Statement);
        Assert.Equal(("firsthand", 7_000), (belief.Provenance, belief.ConfidenceBasisPoints));
        var remembered = Assert.Single(historical.Skills);
        var savedSkill = Assert.Single(deceased.LastPhysical.Skills!);
        Assert.Equal("building", remembered.Kind);
        Assert.Equal(savedSkill.LearnedTick, remembered.LearnedTick);
        Assert.Equal(savedSkill.TeacherId, remembered.TeacherId);
        Assert.Equal(restored.Society.GetAgent(savedSkill.TeacherId!).Name, remembered.TeacherName);
        Assert.DoesNotContain(restored.Agents, person => person.AgentId == deceased.AgentId);

        var invalid = archived with
        {
            DeceasedAgents = archived.DeceasedAgents!
                .Select(person => person.AgentId == deceased.AgentId
                    ? person with { DeathTick = person.DeathTick + 1 }
                    : person).ToArray(),
        };
        Assert.Throws<InvalidDataException>(() => WorldRuntimeCodec.Encode(invalid));
        foreach (var forgedSkill in new[]
        {
            savedSkill with { TeacherId = "unknown-teacher" },
            savedSkill with { LearnedTick = deceased.DeathTick + 1 },
        })
        {
            Assert.Throws<InvalidDataException>(() => WorldRuntime.Restore(archived with
            {
                DeceasedAgents = archived.DeceasedAgents.Select(person => person.AgentId == deceased.AgentId
                    ? person with { LastPhysical = person.LastPhysical with { Skills = [forgedSkill] } } : person).ToArray(),
            }));
        }
    }

    [Fact]
    public void OwnerMemoryProjectionKeepsPrivateRecordsWithTheirOwnerAndOmitsForgottenOnes()
    {
        using var seed = new WorldRuntime("memory-inspection");
        var state = seed.ExportState();
        state = state with
        {
            Society = state.Society with
            {
                Society = state.Society.Society with
                {
                    Memories = new[]
                    {
                        new SocietySocialMemory(Identity.Parse("secret"), "founder-scout", "founder-mira",
                            "I hid a tool behind the storehouse.", "private", 0),
                        new SocietySocialMemory(Identity.Parse("other"), "founder-mira", "founder-scout",
                            "Scout helped me harvest.", "public", 0),
                        new SocietySocialMemory(Identity.Parse("forgotten"), "founder-scout", "founder-mira",
                            "An old abandoned belief.", "private", 0, TombstonedTick: 0),
                    }.OrderBy(memory => memory.Id.Value, StringComparer.Ordinal).ToArray(),
                },
            },
        };
        using var world = WorldRuntime.Restore(state);
        var agents = new OwnerWorldObservationStore(world).GetSnapshot().Agents;
        var scout = agents.Single(person => person.Id == "founder-scout");
        var mira = agents.Single(person => person.Id == "founder-mira");
        Assert.Equal("I hid a tool behind the storehouse.", Assert.Single(scout.RecentMemories).Summary);
        Assert.Equal("private", scout.RecentMemories[0].Visibility);
        Assert.Equal("Mira", scout.RecentMemories[0].SubjectName);
        Assert.DoesNotContain(scout.SocialNotes, note => note.Contains("hid a tool", StringComparison.Ordinal));
        Assert.Equal("Scout helped me harvest.", Assert.Single(mira.RecentMemories).Summary);
        Assert.DoesNotContain(mira.RecentMemories, memory => memory.Summary.Contains("hid a tool", StringComparison.Ordinal));
    }
}
