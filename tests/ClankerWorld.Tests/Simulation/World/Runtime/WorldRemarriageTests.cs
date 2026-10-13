using System.Text.Json;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.World;
using ClankerWorld.Simulation.Society;
using ClankerWorld.Server.Observation;

namespace ClankerWorld.Tests;

public sealed partial class WorldConversationTests
{
    private static readonly string[] MarriagePendingFields = ["pendingModelCalls", "pendingConversationTurns"];
    [Fact]
    public async Task UnilateralSeparationAllowsANewMarriageProposalAndKeepsBothNames()
    {
        IDecisionProvider Route(string id) => new MarriagePersonalProvider(id);
        using var world = MarriedWorldSetup("remarriage-after-separation", Route);
        await AdvanceRemarriageUntil(world, () => world.Marriages.Any(item => item.CompletedTick is not null));
        world.Pause();
        var firstName = world.Society.GetAgent(InitiatorId).Name;
        var secondName = world.Society.GetAgent(InviteeId).Name;
        var ended = world.ApplyDeveloperEdit(MarriageEdit(world, "end_partnership", InviteeId, InitiatorId));
        Assert.True(ended.Applied, ended.Failure);
        Assert.Equal(firstName, world.Society.GetAgent(InitiatorId).Name);
        Assert.Equal(secondName, world.Society.GetAgent(InviteeId).Name);
        Assert.Equal(SocietyRelationshipState.Revoked, world.Society.GetRelationship("partnership:marriage-test").State);
        var history = Assert.Single(world.Marriages);
        Assert.Equal(world.WorldTick, history.EndedTick);
        Assert.Equal(SocietyRelationshipState.Revoked, history.EndReceipt!.State);
        Assert.Contains(new OwnerWorldObservationStore(world).GetSnapshot().Agents.Single(person => person.Id == InitiatorId).SocialNotes,
            note => note.Contains("ended by separation", StringComparison.Ordinal));
        var newPartner = world.ApplyDeveloperEdit(MarriageEdit(world, "start_partnership", InitiatorId, ListenerId));
        Assert.True(newPartner.Applied, newPartner.Failure);
        Assert.True(AgentMarriageRules.CanPropose(world.Society, world.Marriages, InitiatorId, ListenerId));
        Assert.True(world.ApplyDeveloperEdit(MarriageEdit(world, "start_partnership", InviteeId, DistantId)).Applied);
        Assert.True(AgentMarriageRules.CanPropose(world.Society, world.Marriages, InviteeId, DistantId));
        Assert.True(world.RenameAgent(InitiatorId, "Aster Vale"));
        Assert.Equal(secondName, world.Society.GetAgent(InviteeId).Name);
        var bytes = WorldRuntimeCodec.Encode(world.ExportState());
        using var restored = WorldRuntime.Restore(WorldRuntimeCodec.Decode(bytes), Route);
        Assert.Equal(bytes, WorldRuntimeCodec.Encode(restored.ExportState()));
        Assert.Equal(JsonSerializer.SerializeToUtf8Bytes(history), JsonSerializer.SerializeToUtf8Bytes(Assert.Single(restored.Marriages)));
    }

    [Fact]
    public async Task PersonalChoiceEndsMarriageWithoutAnotherPartnersConsentAndEmitsOneEnding()
    {
        var first = new MarriagePersonalProvider(InitiatorId);
        IDecisionProvider Route(string id) => id == InitiatorId ? first : new MarriagePersonalProvider(id);
        using var world = RemarriageWorldSetup("native-unilateral-marriage-ending", Route);
        await AdvanceRemarriageUntil(world, () => world.Marriages.Any(item => item.CompletedTick is not null));
        var names = world.Society.Agents.Select(person => person.Name).ToArray();
        world.Pause();
        first.EndPartnership = true;
        using var endingWorld = WorldRuntime.Restore(world.ExportState(), Route);
        endingWorld.Resume();
        await AdvanceRemarriageUntil(endingWorld, () => endingWorld.WorldTick > world.WorldTick);
        Assert.Null(endingWorld.Marriages[0].EndedTick);
        var before = WorldRuntimeCodec.Encode(endingWorld.ExportState());
        Assert.False((await endingWorld.TickModelWorkAsync(() => false)).Advanced);
        Assert.Equal(before, WorldRuntimeCodec.Encode(endingWorld.ExportState()));
        await AdvanceRemarriageUntil(endingWorld, () => endingWorld.Marriages[0].EndedTick is not null, maximumSteps: 340);
        Assert.Equal(names, endingWorld.Society.Agents.Select(person => person.Name));
        Assert.Equal(SocietyRelationshipState.Revoked, endingWorld.Marriages[0].EndReceipt!.State);
        Assert.Equal(endingWorld.WorldTick, endingWorld.Marriages[0].EndedTick);
        var ending = Assert.Single(endingWorld.ExportState().Events, item => item.Kind == "marriage_ended");
        using var restored = WorldRuntime.Restore(WorldRuntimeCodec.Decode(WorldRuntimeCodec.Encode(endingWorld.ExportState())), Route);
        await AdvanceRemarriageUntil(restored, () => restored.WorldTick > endingWorld.WorldTick);
        Assert.Equal(ending, Assert.Single(restored.ExportState().Events, item => item.Kind == "marriage_ended"));
    }

    [Fact]
    public async Task RemarriageHasFreshConsentAndRenameChangesOnlyTheCurrentSpouseAcrossReplay()
    {
        IDecisionProvider FirstRoute(string id) => new MarriagePersonalProvider(id);
        using var original = RemarriageWorldSetup("native-remarriage-replay", FirstRoute);
        await AdvanceRemarriageUntil(original, () => original.Marriages.Any(item => item.CompletedTick is not null));
        original.Pause();
        Assert.True(original.ApplyDeveloperEdit(MarriageEdit(original, "end_partnership", InitiatorId, InviteeId)).Applied);
        var previous = Assert.Single(original.Marriages);
        Assert.True(original.ApplyDeveloperEdit(MarriageEdit(original, "start_partnership", InitiatorId, ListenerId)).Applied);
        IDecisionProvider Route(string id) => new MarriagePersonalProvider(id)
        { TalkTargetId = ListenerId, UseAllowedSurnameOptions = true };
        var saved = WorldRuntimeCodec.Encode(original.ExportState());
        using var world = WorldRuntime.Restore(WorldRuntimeCodec.Decode(saved), Route);
        using var replay = WorldRuntime.Restore(WorldRuntimeCodec.Decode(saved), Route);
        world.Resume();
        replay.Resume();
        for (var attempt = 0; attempt < 160 && !world.Marriages.Any(item => item.Id != previous.Id && item.CompletedTick is not null); attempt++)
        {
            await AdvanceRemarriageUntil(world, () => world.WorldTick > replay.WorldTick);
            await AdvanceRemarriageUntil(replay, () => replay.WorldTick == world.WorldTick);
            Assert.Equal(WorldRuntimeCodec.Encode(world.ExportState()), WorldRuntimeCodec.Encode(replay.ExportState()));
        }
        Assert.True(world.Marriages.Count == 2, MarriageProgress(world));
        var current = Assert.Single(world.Marriages, item => item.EndReceipt is null);
        Assert.NotNull(current.CompletedTick);
        Assert.Equal(ListenerId, current.InviteeId);
        Assert.NotEqual(previous.Consent.Id, current.Consent.Id);
        Assert.NotEqual(previous.SurnameConversationId, current.SurnameConversationId);
        Assert.Equal(JsonSerializer.SerializeToUtf8Bytes(previous), JsonSerializer.SerializeToUtf8Bytes(Assert.Single(world.Marriages, item => item.Id == previous.Id)));
        Assert.False(AgentMarriageRules.CanPropose(world.Society, world.Marriages, InitiatorId, ListenerId));
        world.Pause();
        replay.Pause();
        Assert.True(world.RenameAgent(InitiatorId, "Aster Vale"));
        Assert.True(replay.RenameAgent(InitiatorId, "Aster Vale"));
        Assert.Equal("Willow Vale", world.Society.GetAgent(ListenerId).Name);
        Assert.Equal("Rowan Ash", world.Society.GetAgent(InviteeId).Name);
        Assert.Equal(JsonSerializer.SerializeToUtf8Bytes(previous), JsonSerializer.SerializeToUtf8Bytes(Assert.Single(world.Marriages, item => item.Id == previous.Id)));
        var complete = WorldRuntimeCodec.Encode(world.ExportState());
        Assert.Equal(complete, WorldRuntimeCodec.Encode(replay.ExportState()));
        using var reloaded = WorldRuntime.Restore(WorldRuntimeCodec.Decode(complete), Route);
        Assert.Equal(complete, WorldRuntimeCodec.Encode(reloaded.ExportState()));
        var invalidHistory = reloaded.ExportState() with
        {
            Marriages = reloaded.Marriages.Select(item => item.Id == previous.Id ? item with { EndReceipt = null } : item).ToArray()
        };
        Assert.Throws<InvalidDataException>(() => WorldRuntimeCodec.Encode(invalidHistory));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NativePartnerDeathEndsCompletedOrUnfinishedMarriageAndPermitsRemarriage(bool unfinished)
    {
        IDecisionProvider Route(string id) => new MarriagePersonalProvider(id) { UseAllowedSurnameOptions = true };
        using var prepared = MarriedWorldSetup("marriage-widowhood-" + unfinished, Route);
        await AdvanceRemarriageUntil(prepared, () => prepared.Marriages.Any(item => unfinished || item.CompletedTick is not null));
        prepared.Pause();
        var state = prepared.ExportState();
        var society = state.Society.Society;
        state = state with
        {
            Society = state.Society with
            {
                Society = society with
                {
                    Agents = society.Agents.Select(person => person.Id == InviteeId ? person with
                    {
                        BirthTick = society.LifeTickAt(society.WorldTick) - 60L * society.Config.TicksPerWorldDay + 1,
                        BirthLifeTick = null,
                        AgeBand = SocietyAgeBand.Elder,
                        LastLifecycleYearChecked = 59,
                    } : person).ToArray()
                }
            }
        };
        using var world = WorldRuntime.Restore(state, Route);
        world.Resume();
        Assert.True((await world.AdvanceOneTickAsync()).Advanced);
        var marriage = Assert.Single(world.Marriages);
        Assert.Equal(world.Society.GetAgent(InviteeId).DeathTick, marriage.EndedTick);
        Assert.Equal(SocietyRelationshipState.EndedByDeath, marriage.EndReceipt!.State);
        Assert.Equal(state.Society.Society.GetAgent(InitiatorId).Name, world.Society.GetAgent(InitiatorId).Name);
        if (unfinished)
        {
            Assert.Null(marriage.CompletedTick);
            Assert.Equal("participant_unavailable", marriage.SurnameReceipt!.Outcome);
        }
        Assert.Single(world.ExportState().Events, item => item.Kind == "marriage_ended");
        world.Pause();
        Assert.True(world.ApplyDeveloperEdit(MarriageEdit(world, "start_partnership", InitiatorId, ListenerId)).Applied);
        Assert.True(AgentMarriageRules.CanPropose(world.Society, world.Marriages, InitiatorId, ListenerId));
        Assert.True(world.RenameAgent(InitiatorId, "Aster Vale"));
        var bytes = WorldRuntimeCodec.Encode(world.ExportState());
        using var reloaded = WorldRuntime.Restore(WorldRuntimeCodec.Decode(bytes), Route);
        Assert.Equal(bytes, WorldRuntimeCodec.Encode(reloaded.ExportState()));
        var bad = marriage.EndReceipt with { EffectiveTick = world.WorldTick + 1 };
        Assert.Throws<InvalidDataException>(() => WorldRuntimeCodec.Encode(world.ExportState() with
        { Marriages = [marriage with { EndReceipt = bad }] }));
    }

    private static WorldDeveloperEdit MarriageEdit(WorldRuntime world, string operation, string actor, string other) =>
        new(world.Society.WorldId, world.ExportState().Events[^1].EventId,
            actor, operation, "partnership", OtherAgentId: other);

    [Fact]
    public async Task SeparationClosesAnUnfinishedSurnameSessionWithoutRenamingEitherPartner()
    {
        IDecisionProvider Route(string id) => new MarriagePersonalProvider(id);
        using var world = MarriedWorldSetup("unfinished-marriage-separation", Route);
        await AdvanceRemarriageUntil(world, () => world.Marriages.Count == 1 &&
            world.Conversations.Single(item => item.Id == world.Marriages[0].SurnameConversationId).Turns.Count == 1);
        world.Pause();
        var names = world.Society.Agents.Select(person => person.Name).ToArray();
        var beforeEnding = world.ExportState();
        var unfinishedSession = Assert.Single(beforeEnding.Conversations!,
            item => item.Id == Assert.Single(beforeEnding.Marriages).SurnameConversationId);
        Assert.Single(unfinishedSession.Turns);
        Assert.True(world.ApplyDeveloperEdit(MarriageEdit(world, "end_partnership", InitiatorId, InviteeId)).Applied);
        var marriage = Assert.Single(world.Marriages);
        Assert.NotNull(marriage.EndedTick);
        Assert.Null(marriage.CompletedTick);
        Assert.Equal("participant_unavailable", marriage.SurnameReceipt!.Outcome);
        Assert.Equal(AgentConversationStatus.Closed, marriage.SurnameReceipt.Status);
        Assert.Equal(names, world.Society.Agents.Select(person => person.Name));
        Assert.True(world.RenameAgent(InitiatorId, "Aster Vale"));
        Assert.Equal("Rowan Reed", world.Society.GetAgent(InviteeId).Name);
        var bytes = WorldRuntimeCodec.Encode(world.ExportState());
        using var restored = WorldRuntime.Restore(WorldRuntimeCodec.Decode(bytes), Route);
        Assert.Equal(bytes, WorldRuntimeCodec.Encode(restored.ExportState()));
        var endedState = world.ExportState();
        var reopened = endedState with
        {
            Marriages = [marriage with { SurnameReceipt = null }],
            Conversations = endedState.Conversations!.Select(item =>
                item.Id == unfinishedSession.Id ? unfinishedSession : item).ToArray(),
        };
        Assert.Throws<InvalidDataException>(() => WorldRuntimeCodec.Encode(reopened));

        world.Resume();
        Assert.True((await world.AdvanceOneTickAsync()).Advanced);
        world.Pause();
        endedState = world.ExportState();
        var afterEnding = marriage.SurnameReceipt with { LastUpdatedTick = world.WorldTick };
        Assert.True(afterEnding.LastUpdatedTick > marriage.EndedTick);
        var lateReceipt = endedState with
        {
            Marriages = [marriage with { SurnameReceipt = afterEnding }],
            Conversations = endedState.Conversations!.Select(item =>
                item.Id == afterEnding.Id ? afterEnding : item).ToArray(),
        };
        Assert.Throws<InvalidDataException>(() => WorldRuntimeCodec.Encode(lateReceipt));

        var actualTurn = Assert.Single(marriage.SurnameReceipt.Turns);
        var lateTurnSession = marriage.SurnameReceipt with
        {
            Turns = [actualTurn with { WorldTick = world.WorldTick }],
        };
        // Keep the public/retained receipt and its hearing evidence internally
        // consistent; only the activity after the ending is malformed.
        var lateTurn = endedState with
        {
            Marriages = [marriage with { SurnameReceipt = lateTurnSession }],
            Conversations = endedState.Conversations!.Select(item =>
                item.Id == lateTurnSession.Id ? lateTurnSession : item).ToArray(),
            Society = endedState.Society with
            {
                Society = endedState.Society.Society with
                {
                    Beliefs = (endedState.Society.Society.Beliefs ?? []).Select(belief =>
                        belief.SourceTurnId == actualTurn.Id ? belief with { FormedTick = world.WorldTick } : belief).ToArray(),
                },
            },
        };
        Assert.Throws<InvalidDataException>(() => WorldRuntimeCodec.Encode(lateTurn));
    }

    [Fact]
    public async Task FormerPartnersLaterDeathDoesNotRewriteTheEarlierSeparation()
    {
        IDecisionProvider Route(string id) => new MarriagePersonalProvider(id);
        using var prepared = MarriedWorldSetup("historical-marriage-ending", Route);
        await AdvanceRemarriageUntil(prepared, () => prepared.Marriages.Any(item => item.CompletedTick is not null));
        prepared.Pause();
        Assert.True(prepared.ApplyDeveloperEdit(MarriageEdit(prepared, "end_partnership", InviteeId, InitiatorId)).Applied);
        var original = JsonSerializer.SerializeToUtf8Bytes(Assert.Single(prepared.Marriages));
        var state = prepared.ExportState();
        var society = state.Society.Society;
        state = state with
        {
            Society = state.Society with
            {
                Society = society with
                {
                    Agents = society.Agents.Select(person => person.Id == InviteeId ? person with
                    {
                        BirthTick = society.LifeTickAt(society.WorldTick) - 60L * society.Config.TicksPerWorldDay + 1,
                        BirthLifeTick = null,
                        AgeBand = SocietyAgeBand.Elder,
                        LastLifecycleYearChecked = 59,
                    } : person).ToArray()
                }
            }
        };
        using var world = WorldRuntime.Restore(state, Route);
        world.Resume();
        Assert.True((await world.AdvanceOneTickAsync()).Advanced);
        Assert.Equal(SocietyAgentStatus.Dead, world.Society.GetAgent(InviteeId).Status);
        Assert.Equal(original, JsonSerializer.SerializeToUtf8Bytes(Assert.Single(world.Marriages)));
        Assert.Single(world.ExportState().Events, item => item.Kind == "marriage_ended");
        var bytes = WorldRuntimeCodec.Encode(world.ExportState());
        using var restored = WorldRuntime.Restore(WorldRuntimeCodec.Decode(bytes), Route);
        Assert.Equal(bytes, WorldRuntimeCodec.Encode(restored.ExportState()));
    }

    private static WorldRuntime RemarriageWorldSetup(string seed, Func<string, IDecisionProvider> route)
    {
        var options = new GeographyOptions(seed, WorldSizePreset.Small);
        var world = new WorldRuntime(seed, route, startPace: WorldStartPace.FounderSetup, geographyOptions: options);
        var map = world.ExportState().Map;
        var anchor = NormalPathWorld.FindStartingTownSite(map);
        world.InitializeFirstTownContent();
        world.AcceptFirstTownLayout(anchor);
        var blocked = world.WorldSimulation.Buildings.SelectMany(building => WorldContentSimulationRules.Footprint(
                world.WorldContent.Buildings.Single(item => item.CanonicalId == building.DefinitionId), building.Position))
            .Concat(world.RoadTiles).Concat(map.Resources.Select(item => item.Position))
            .Concat(map.MapObjects.Select(item => item.Position)).ToHashSet();
        var available = map.Tiles.Where(tile => Math.Abs(tile.Position.X - anchor.X) <= 5 &&
                Math.Abs(tile.Position.Y - anchor.Y) <= 5 && map.IsBuildable(tile.Position) && !blocked.Contains(tile.Position))
            .Select(tile => tile.Position).ToArray();
        var first = available.First(point => available.Count(other => other != point && map.FootDistance(point, other) == 1) >= 2);
        var neighbours = available.Where(point => point != first && map.FootDistance(first, point) == 1).Take(2).ToArray();
        var distant = available.First(point => point != first && !neighbours.Contains(point) && map.FootDistance(first, point) > 3);
        world.PlaceFounder(InitiatorId, first);
        world.PlaceFounder(InviteeId, neighbours[0]);
        world.PlaceFounder(ListenerId, neighbours[1]);
        world.PlaceFounder(DistantId, distant);
        world.RenameAgent(InitiatorId, "Aster Ash");
        world.RenameAgent(InviteeId, "Rowan Reed");
        world.RenameAgent(ListenerId, "Willow Stone");
        world.RenameAgent(DistantId, "Mira Pine");
        world.StartWorld(resume: false);
        Assert.True(world.ApplyDeveloperEdit(MarriageEdit(world, "start_partnership", InitiatorId, InviteeId)).Applied);
        world.Resume();
        return world;
    }

    private static async Task AdvanceRemarriageUntil(WorldRuntime world, Func<bool> finished, int maximumSteps = 160)
    {
        for (var attempt = 0; attempt < maximumSteps && !finished(); attempt++)
        {
            Assert.True((await world.TickModelWorkAsync()).Advanced);
            await ModelWorkHarness.WaitForExistingAsync(world);
        }
        Assert.True(finished(), MarriageProgress(world));
    }

    private static string MarriageProgress(WorldRuntime world) =>
        "The normal marriage path did not reach its expected transition.\n" +
        string.Join("\n", world.Conversations.Select(item => $"{item.Id}: {item.Status}: {item.Outcome}")) + "\n" +
        string.Join("\n", world.ExportState().Events.Where(item => item.Kind != "tick_advanced").TakeLast(20).Select(item => $"{item.WorldTick}: {item.Kind}: {item.Detail}"));
}
