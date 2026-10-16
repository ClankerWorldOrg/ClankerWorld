using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using ClankerWorld.Contracts;
using ClankerWorld.Presentation;
using ClankerWorld.Server.Observation;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.World;

namespace ClankerWorld.Tests;

public sealed class EventLogPolicyTests
{
    private static readonly JsonSerializerOptions Wire = new(JsonSerializerDefaults.Web);

    [Fact]
    public void RoutineChoresAndAutomaticSourceChangesStayInHistoryButNotTheEventLog()
    {
        WorldEventPayload[] history =
        [
            new PersonalGoodsStoredEventPayload("agent", "wood", 1),
            new PersonalGoodsCollectedEventPayload("agent", "wood", 1, "agent"),
            new StoreStockDeliveredEventPayload("agent", "lot", 1, "store"),
            new ChoiceRefusedEventPayload("agent", "Élodie 🌿", "gather wood", "The selected material source is no longer available"),
        ];
        var events = history.Select((payload, index) => new OwnerWorldEvent(index + 1, 1, payload.Kind, payload.DiagnosticDetail)
        { Payload = payload }).ToArray();
        Assert.Empty(GameUiText.PlayerEvents(events));
        Assert.Equal(4, events.Length);
        Assert.All(events, item => Assert.NotEmpty(item.Detail));
    }

    [Fact]
    public void EveryAcceptedEventKindHasAnExplicitClassification()
    {
        var kinds = typeof(WorldEventPayload).GetCustomAttributes<JsonDerivedTypeAttribute>()
            .Select(attribute => Assert.IsType<string>(attribute.TypeDiscriminator)).ToArray();
        Assert.NotEmpty(kinds);
        Assert.All(kinds, kind => Assert.NotEqual(EventLogImportance.Unknown, EventLogPolicy.Importance(kind)));
        Assert.False(EventLogPolicy.CanAppear("future_diagnostic"));
    }

    [Theory]
    [InlineData(40, 0)]
    [InlineData(40, 20)]
    [InlineData(360, 180)]
    public void RefusalsUseCompleteAgentAndTargetReferencesAndResetOnlyAtWorldMidnight(int ticksPerDay, int offset)
    {
        var actor = NormalPathWorld.Founder1;
        var otherActor = NormalPathWorld.Founder2;
        var target = "house:original:source:one";
        var otherTarget = target + ":child";
        var midnight = ticksPerDay - offset;
        OwnerWorldEvent Refusal(long id, long tick, string person, string candidate) =>
            Event(id, tick, new ActionRefusedEventPayload(person, candidate, tick, tick + ticksPerDay, "check-" + id));
        OwnerWorldEvent[] history =
        [
            Refusal(1, 0, actor, target),
            Refusal(2, midnight - 1, actor, target),
            Refusal(3, midnight - 1, otherActor, target),
            Refusal(4, midnight - 1, actor, otherTarget),
            Refusal(5, midnight, actor, target),
            Refusal(6, midnight + 1, actor, target),
            Refusal(7, midnight + ticksPerDay, actor, target),
        ];
        var pace = new OwnerWorldCalendarPace(ticksPerDay, 365, 90, 90, 90, 95, offset);
        var rows = GameUiText.PlayerEvents(history.Reverse(), pace).ToArray();
        Assert.Equal(new long[] { 1, 3, 4, 5, 7 }, rows.Select(item => item.EventId));
        Assert.Equal(rows, GameUiText.PlayerEvents(history, pace));
        Assert.Equal(7, history.Length);
    }

    [Fact]
    public void RetainedRefusalHistoryDoesNotPromoteOldDuplicatesAfterTheirFirstRowExpires()
    {
        var history = new EventLogRefusalHistory();
        var pace = new OwnerWorldCalendarPace(40, 365, 90, 90, 90, 95, 20);
        OwnerWorldEvent Refusal(long id, long tick, string target = "house:original:source:one") =>
            Event(id, tick, new ActionRefusedEventPayload(NormalPathWorld.Founder1, target, tick, tick + 40, "check"));
        var first = Refusal(1, 1);
        var duplicate = Refusal(2, 2);
        Assert.Equal(new long[] { 1 }, GameUiText.PlayerEvents([first, duplicate], pace, history).Select(item => item.EventId));
        Assert.Empty(GameUiText.PlayerEvents([duplicate], pace, history));
        Assert.Empty(GameUiText.PlayerEvents([duplicate], pace, history));
        var otherTarget = Refusal(3, 3, "house:original:source:one:child");
        Assert.Equal(new long[] { 3 }, GameUiText.PlayerEvents([duplicate, otherTarget], pace, history).Select(item => item.EventId));
        var nextDay = Refusal(4, 20);
        Assert.Equal(new long[] { 4 }, GameUiText.PlayerEvents([nextDay], pace, history).Select(item => item.EventId));
        var laterDuplicate = Refusal(5, 21);
        Assert.Empty(GameUiText.PlayerEvents([laterDuplicate], pace, history));
        history.Clear();
        Assert.Equal(new long[] { 5 }, GameUiText.PlayerEvents([laterDuplicate], pace, history).Select(item => item.EventId));
    }

    [Fact]
    public void PlayerOrderProblemsAndBrokenPromisesRemainVisibleWithoutPublishingRoutineWaits()
    {
        OwnerWorldEvent[] events =
        [
            Event(1, 1, new InstructionOrderStatusEventPayload("agent", "order", "doing")),
            Event(2, 2, new InstructionOrderStatusEventPayload("agent", "order", "blocked")),
            Event(3, 3, new InstructionOrderStatusEventPayload("agent", "order", "doing")),
            Event(4, 4, new InstructionOrderStatusEventPayload("agent", "order", "blocked")),
            Event(5, 5, new InstructionOrderCancelledEventPayload("agent", "order", "game_error")),
            Event(6, 6, new InstructionOrderCancelledEventPayload("agent", "other-order", "owner")),
            Event(7, 7, new InstructionOrderCancelledEventPayload("agent", "other-order", "replaced")),
            Event(8, 8, new ToolRequestInterruptedEventPayload("agent", "promise")),
            Event(9, 9, new ToolRequestInterruptedEventPayload("agent", "promise")),
            Event(10, 10, new InstructionNotUnderstoodEventPayload("agent", "new-order")),
        ];
        Assert.Equal(new long[] { 2, 5, 8, 10 }, GameUiText.PlayerEvents(events).Select(item => item.EventId));
        Assert.Equal("Agent's order is blocked. See their Orders for the reason.",
            WorldEventFormatter.Describe(new(2, 2, events[1].Kind, "") { Payload = events[1].Payload }, null));
        Assert.Equal("Agent's order was cancelled. See their Orders for details.",
            WorldEventFormatter.Describe(new(5, 5, events[4].Kind, "") { Payload = events[4].Payload }, null));
    }

    [Fact]
    public async Task SeveralGeneratedWorldDaysKeepRoutineHistoryOutOfTheEventLogAndReplayExactly()
    {
        const int day = 20;
        using var world = NormalPathWorld.CreateGenerated("2355-event-log", _ => new DeterministicDecisionProvider(), ticksPerDay: day);
        var work = ModelWorkHarness.For(world);
        for (var tick = 0; tick < day * 3; tick++) await work.TickAsync();
        await work.SettleAsync();
        Assert.Equal(day * 3, world.WorldTick);
        var saved = WorldRuntimeCodec.Encode(world.ExportState());
        var store = new OwnerWorldObservationStore(world);
        var wire = JsonSerializer.Serialize(store.GetEventsAfter(0), Wire);
        var events = JsonSerializer.Deserialize<OwnerWorldEventSlice>(wire, Wire)!.Events;
        var pace = new OwnerWorldCalendarPace(day, 365, 90, 90, 90, 95);
        var rows = GameUiText.PlayerEvents(events, pace).ToArray();
        Assert.NotEmpty(events);
        Assert.NotEmpty(rows);
        Assert.Contains(events, item => EventLogPolicy.Importance(item.Kind) == EventLogImportance.Hidden);
        Assert.DoesNotContain(rows, item => EventLogPolicy.Importance(item.Kind) == EventLogImportance.Hidden);
        Assert.DoesNotContain(rows, item => item.Kind is "personal_goods_stored" or "personal_goods_collected" or
            "store_stock_delivered" or "choice_refused" or "owner_stock_delivered" or "market_stock_delivered");
        var problems = rows.Where(item => EventLogPolicy.Importance(item.Kind) is EventLogImportance.Refusal or EventLogImportance.OrderProblem);
        Assert.All(problems.GroupBy(item => (Day: item.WorldTick / day, item.Kind, item.Payload!.ActorId,
            Target: string.Join("\0", item.Payload.SubjectIds))), group => Assert.Single(group));
        Assert.Equal(saved, WorldRuntimeCodec.Encode(world.ExportState()));
        Assert.All(events.Where(item => item.Kind == "choice_refused"), item => Assert.IsType<ChoiceRefusedEventPayload>(item.Payload));
        using var restored = WorldRuntime.Restore(WorldRuntimeCodec.Decode(saved), _ => new DeterministicDecisionProvider());
        Assert.Equal(saved, WorldRuntimeCodec.Encode(restored.ExportState()));
        await work.AdvanceTogetherAsync(ModelWorkHarness.For(restored));
        Assert.Equal(WorldRuntimeCodec.Encode(world.ExportState()), WorldRuntimeCodec.Encode(restored.ExportState()));
    }

    private static OwnerWorldEvent Event(long id, long tick, WorldEventPayload payload) =>
        new(id, tick, payload.Kind, payload.DiagnosticDetail) { Payload = payload };
}
