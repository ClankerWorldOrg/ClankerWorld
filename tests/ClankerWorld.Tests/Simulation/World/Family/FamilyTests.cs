using ClankerWorld.Simulation;
using ClankerWorld.Simulation.World;
using ClankerWorld.Simulation.Society;
using ClankerWorld.Server.Observation;
using ClankerWorld.Server.Runtime;
using ClankerWorld.Server.Saves;

namespace ClankerWorld.Tests;

public sealed partial class FamilyTests
{
    [Fact]
    public async Task LegacyRevokedParentageStillCountsAsFamilyHistory()
    {
        var state = await PreparedState();
        var first = state.Agents[0].AgentId;
        var second = state.Agents[1].AgentId;
        var parent = state.Agents[2].AgentId;
        var tick = state.Society.Society.WorldTick;
        var edges = new[] { first, second }.Select(child => new SocietyRelationship("legacy-parent:" + child, 1,
            SocietyRelationshipType.BiologicalParentage, parent, child, SocietyRelationshipState.Revoked,
            SocietyConsentState.Revoked, tick, tick, "family"));
        state = state with
        {
            Society = state.Society with
            {
                Society = state.Society.Society with
                {
                    Relationships = state.Society.Society.Relationships.Concat(edges).OrderBy(edge => edge.Id, StringComparer.Ordinal).ToArray(),
                }
            }
        };
        using var world = WorldRuntime.Restore(state, actor => new PrefixChoiceProvider(actor == first ? "partner_propose:" : "safe_idle"));
        await world.AdvanceOneTickAsync();
        Assert.DoesNotContain(world.Society.Relationships, edge => edge.Type == SocietyRelationshipType.Partnership);
    }

    [Theory]
    [InlineData(true, true)]
    public async Task AncestryAndSiblingExclusionsSurviveInterveningRelativeDeath(bool ancestor, bool relativeDies)
    {
        var state = await PreparedState();
        var first = state.Agents[0].AgentId;
        var second = state.Agents[1].AgentId;
        var middle = state.Agents[2].AgentId;
        var tick = state.Society.Society.WorldTick;
        SocietyRelationship[] edges =
        [
            new("ancestry:1", 1, SocietyRelationshipType.BiologicalParentage, ancestor ? first : middle, ancestor ? middle : first,
                SocietyRelationshipState.Accepted, SocietyConsentState.ProtectedLifecycle, tick, tick, "household"),
            new("ancestry:2", 1, SocietyRelationshipType.BiologicalParentage, middle, second,
                SocietyRelationshipState.Accepted, SocietyConsentState.ProtectedLifecycle, tick, tick, "household"),
        ];
        state = state with
        {
            Society = state.Society with
            {
                Society = state.Society.Society with
                {
                    Relationships = state.Society.Society.Relationships.Concat(edges).OrderBy(edge => edge.Id, StringComparer.Ordinal).ToArray(),
                }
            }
        };
        if (relativeDies)
        {
            using var society = SocietyRuntime.Restore(state.Society);
            society.Apply(checkpoint => SocietyRules.Kill(checkpoint, middle, SocietyDeathCause.Accident, tick));
            state = state with { Society = society.ExportState(), Agents = state.Agents.Where(person => person.AgentId != middle).ToArray() };
        }
        using var world = WorldRuntime.Restore(state, actor => new PrefixChoiceProvider(actor == first ? "partner_propose:" : "safe_idle"));
        await world.AdvanceOneTickAsync();
        Assert.DoesNotContain(world.Society.Relationships, edge => edge.Type == SocietyRelationshipType.Partnership);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task AuntsAndUnclesCannotPairWithNiecesOrNephews(bool auntProposes, bool fullSiblings)
    {
        var state = await PreparedState();
        var proposer = state.Agents[0].AgentId;
        var other = state.Agents[1].AgentId;
        var (aunt, niece) = auntProposes ? (proposer, other) : (other, proposer);
        var nieceParent = state.Agents[2].AgentId;
        var grandparent = state.Agents[3].AgentId;
        state = FamilyTreeFixture.WithRelationships(state, SocietyRelationshipType.BiologicalParentage,
            (grandparent, aunt), (grandparent, nieceParent), (nieceParent, niece));
        if (fullSiblings)
        {
            (state, var otherGrandparent) = FamilyTreeFixture.WithDeadAncestor(state, "grandparent");
            state = FamilyTreeFixture.WithRelationships(state, SocietyRelationshipType.BiologicalParentage,
                (otherGrandparent, aunt), (otherGrandparent, nieceParent));
        }
        using var world = WorldRuntime.Restore(state, actor => new PrefixChoiceProvider(actor == proposer ? "partner_propose:" : "partner_accept:"));
        for (var tick = 0; tick < 3; tick++)
        {
            await world.AdvanceOneTickAsync();
        }
        Assert.DoesNotContain(world.Society.Relationships, edge => edge.Type == SocietyRelationshipType.Partnership);
    }

    [Fact]
    public async Task NieceOrNephewCannotAcceptAnAuntOrUnclesProposal()
    {
        var state = await PreparedState();
        var aunt = state.Agents[0].AgentId;
        var niece = state.Agents[1].AgentId;
        var nieceParent = state.Agents[2].AgentId;
        var grandparent = state.Agents[3].AgentId;
        state = FamilyTreeFixture.WithRelationships(state, SocietyRelationshipType.BiologicalParentage,
            (grandparent, aunt), (grandparent, nieceParent), (nieceParent, niece));
        using (var society = SocietyRuntime.Restore(state.Society))
        {
            society.Apply(checkpoint => SocietyRules.ProposeRelationship(checkpoint,
                new("earlier-proposal", 1, SocietyRelationshipType.Partnership, aunt, niece, checkpoint.WorldTick, PrivacyClass: "public")));
            state = state with { Society = society.ExportState() };
        }
        using var world = WorldRuntime.Restore(state, actor => new PrefixChoiceProvider(actor == niece ? "partner_accept:" : "safe_idle"));
        for (var tick = 0; tick < 3; tick++)
        {
            await world.AdvanceOneTickAsync();
        }
        Assert.Equal(SocietyRelationshipState.Proposed, world.Society.GetRelationship("earlier-proposal").State);
    }

    [Fact]
    public async Task FirstCousinsCanStillPair()
    {
        var state = await PreparedState();
        var first = state.Agents[0].AgentId;
        var second = state.Agents[1].AgentId;
        var firstParent = state.Agents[2].AgentId;
        var secondParent = state.Agents[3].AgentId;
        (state, var grandparent) = FamilyTreeFixture.WithDeadAncestor(state, "grandparent");
        state = FamilyTreeFixture.WithRelationships(state, SocietyRelationshipType.BiologicalParentage,
            (grandparent, firstParent), (grandparent, secondParent), (firstParent, first), (secondParent, second));
        using var world = WorldRuntime.Restore(state, actor => new PrefixChoiceProvider(actor == first ? "partner_propose:" : "partner_accept:"));
        await world.AdvanceOneTickAsync();
        await world.AdvanceOneTickAsync();
        var partnership = Assert.Single(world.Society.Relationships, edge => edge.Type == SocietyRelationshipType.Partnership);
        Assert.Equal((first, second, SocietyRelationshipState.Accepted), (partnership.ProposerId, partnership.TargetId, partnership.State));
    }

    [Fact]
    public async Task SharedHouseholdsAndCaregiversDoNotCountAsKinship()
    {
        var state = await PreparedState();
        var first = state.Agents[0].AgentId;
        var second = state.Agents[1].AgentId;
        var caregiver = state.Agents[2].AgentId;
        Assert.Equal(state.Society.Society.GetAgent(first).HouseholdId, state.Society.Society.GetAgent(second).HouseholdId);
        state = FamilyTreeFixture.WithRelationships(state, SocietyRelationshipType.Caregiver,
            (caregiver, first), (caregiver, second), (first, second));
        using var world = WorldRuntime.Restore(state, actor => new PrefixChoiceProvider(actor == first ? "partner_propose:" : "partner_accept:"));
        await world.AdvanceOneTickAsync();
        await world.AdvanceOneTickAsync();
        Assert.Equal(SocietyRelationshipState.Accepted,
            Assert.Single(world.Society.Relationships, edge => edge.Type == SocietyRelationshipType.Partnership).State);
    }

    [Theory]
    [InlineData("partner_accept:", SocietyRelationshipState.Accepted)]
    [InlineData("partner_refuse:", SocietyRelationshipState.Rejected)]
    public async Task PartnershipRequiresAnIndependentResponseAcrossRestart(string response, SocietyRelationshipState expected)
    {
        var state = await PreparedState();
        var proposer = state.Agents[0].AgentId;
        using var world = WorldRuntime.Restore(state, actor => new PrefixChoiceProvider(actor == proposer ? "partner_propose:" : "safe_idle"));
        await world.AdvanceOneTickAsync();
        var proposal = Assert.Single(world.Society.Relationships, item => item.Type == SocietyRelationshipType.Partnership);
        Assert.Equal(SocietyRelationshipState.Proposed, proposal.State);
        world.Pause();
        var bytes = WorldRuntimeCodec.Encode(world.ExportState());
        using var restored = WorldRuntime.Restore(WorldRuntimeCodec.Decode(bytes), _ => new PrefixChoiceProvider(response));
        Assert.False((await restored.AdvanceOneTickAsync()).Advanced);
        Assert.Equal(bytes, WorldRuntimeCodec.Encode(restored.ExportState()));
        restored.Resume();
        await restored.AdvanceOneTickAsync();
        Assert.Equal(expected, restored.Society.GetRelationship(proposal.Id).State);
        Assert.Empty(restored.Society.Births);
        Assert.Equal(4, restored.Agents.Count);
        if (expected == SocietyRelationshipState.Accepted)
        {
            Assert.Contains(new OwnerWorldObservationStore(restored).GetSnapshot().Agents.Single(item => item.Id == proposer).Relationships,
                item => item.Type == "partnership" && item.State == "accepted");
        }
        for (var tick = 0; tick < 5; tick++)
        {
            await restored.AdvanceOneTickAsync();
        }
        Assert.Single(restored.Society.Relationships, item => item.Type == SocietyRelationshipType.Partnership);
    }

    [Fact]
    public async Task EitherPartnerCanLeaveWithoutTheOthersPermission()
    {
        var state = await PreparedState();
        var proposer = state.Agents[0].AgentId;
        using var world = WorldRuntime.Restore(state, actor => new PrefixChoiceProvider(actor == proposer ? "partner_propose:" : "partner_accept:"));
        await world.AdvanceOneTickAsync();
        await world.AdvanceOneTickAsync();
        var relationship = Assert.Single(world.Society.Relationships, item => item.Type == SocietyRelationshipType.Partnership);
        Assert.Equal(SocietyRelationshipState.Accepted, relationship.State);
        using var restored = WorldRuntime.Restore(world.ExportState(), _ => new PrefixChoiceProvider("partner_leave:"));
        for (var tick = 0; tick < 40 && restored.Society.GetRelationship(relationship.Id).State == SocietyRelationshipState.Accepted; tick++)
        {
            await restored.AdvanceOneTickAsync();
        }
        Assert.Equal(SocietyRelationshipState.Revoked, restored.Society.GetRelationship(relationship.Id).State);
    }

    [Fact]
    public async Task UnansweredProposalsExpireInsteadOfBecomingConsent()
    {
        var state = await PreparedState();
        var proposer = state.Agents[0].AgentId;
        using var world = WorldRuntime.Restore(state, actor => new PrefixChoiceProvider(actor == proposer ? "partner_propose:" : "safe_idle"));
        for (var tick = 0; tick < 125; tick++)
        {
            await world.AdvanceOneTickAsync();
        }
        Assert.Equal(SocietyRelationshipState.Revoked,
            Assert.Single(world.Society.Relationships, item => item.Type == SocietyRelationshipType.Partnership).State);
        Assert.Contains(world.ExportState().Events, item => item.Kind == "partnership_expired");
    }

    [Fact]
    public async Task NoProposalWithoutPriorCooperation()
    {
        var state = await PreparedState();
        state = state with { Society = state.Society with { Society = state.Society.Society with { Memories = [] } } };
        using var world = WorldRuntime.Restore(state, _ => new PrefixChoiceProvider("partner_propose:"));
        for (var tick = 0; tick < 5; tick++)
        {
            await world.AdvanceOneTickAsync();
        }
        Assert.DoesNotContain(world.Society.Relationships, item => item.Type == SocietyRelationshipType.Partnership);
    }

    [Fact]
    public async Task MinorsCannotReceivePartnershipProposals()
    {
        var state = await PreparedState();
        var target = state.Agents[1].AgentId;
        state = state with
        {
            Society = state.Society with
            {
                Society = state.Society.Society with
                {
                    Agents = state.Society.Society.Agents.Select(person => person.Id == target
                        ? person with { BirthTick = state.Society.Society.WorldTick, AgeBand = SocietyAgeBand.Infant, LastLifecycleYearChecked = 0 }
                        : person).ToArray(),
                },
            },
        };
        using var world = WorldRuntime.Restore(state, _ => new PrefixChoiceProvider("partner_propose:"));
        await world.AdvanceOneTickAsync();
        Assert.DoesNotContain(world.Society.Relationships, item => item.Type == SocietyRelationshipType.Partnership);
    }

    [Fact]
    public async Task PartnershipJournalDoesNotIncludeNamesOrModelText()
    {
        var directory = Directory.CreateTempSubdirectory("clankerworld-family-log-");
        try
        {
            var state = await PreparedState();
            var proposer = state.Agents[0].AgentId;
            state = state with
            {
                Society = state.Society with
                {
                    Society = state.Society.Society with
                    {
                        Agents = state.Society.Society.Agents.Select(person => person with { Name = $"family-private-text-secret-{person.Id}" }).ToArray(),
                    },
                },
            };
            using var world = WorldRuntime.Restore(state, actor => new PrefixChoiceProvider(actor == proposer ? "partner_propose:" : "safe_idle"));
            var presence = new OwnerClientPresenceLease(TimeSpan.FromSeconds(30));
            presence.RecordAuthenticatedReconnect("owner");
            var logger = new RecordingLogger<WorldRuntimeService>();
            using var service = new WorldRuntimeService(world, new WorldStateFile(Path.Combine(directory.FullName, "world.json")), presence, logger);
            Assert.True(await service.TryAdvanceOnceAsync());
            Assert.Contains(logger.Messages, message => message.Contains("family_activity", StringComparison.Ordinal) &&
                message.Contains("event=partnership_proposed", StringComparison.Ordinal));
            Assert.DoesNotContain(logger.Messages, message => message.Contains("family-private-text-secret", StringComparison.Ordinal));
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    private static async Task<WorldRuntimeState> PreparedState()
    {
        using var world = new WorldRuntime("settlement-family", _ => new PrefixChoiceProvider("safe_idle"));
        world.StageStarterContent();
        for (var tick = 0; tick < 5; tick++)
        {
            await world.AdvanceOneTickAsync();
        }
        var state = world.ExportState();
        var proposer = state.Agents[0].AgentId;
        var other = state.Agents[1].AgentId;
        using var society = SocietyRuntime.Restore(state.Society);
        society.Apply(checkpoint => SocietyRules.RecordSocialMemory(checkpoint,
            new(Identity.Parse("trust-memory:" + proposer + ":" + other), proposer, other, "Exchanged needed materials.", "public", checkpoint.WorldTick)));
        return state with { Society = society.ExportState(), Agents = state.Agents.Select(person => person with { LastDecisionContext = null }).ToArray() };
    }

}
