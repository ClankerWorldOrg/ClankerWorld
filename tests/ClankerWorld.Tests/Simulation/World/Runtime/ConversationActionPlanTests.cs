using ClankerWorld.Simulation;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.World;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Tests;

public sealed partial class ConversationSystemTests
{
    private static readonly string[] ResumePlanEvents = ["conversation_proposed", "conversation_accepted", "conversation_resume_requested", "conversation_resumed"];
    [Fact]
    public void TalkPlansRetainThePersonAndRefuseChangedPresenceWithoutMutatingDialogue()
    {
        var world = new FakeWorld();
        var system = new ConversationSystem(new([], [], []));
        var candidate = system.ConversationCandidates(world, "agent-a").Single(item => item.Id == "talk:agent-b");
        var plan = Assert.IsType<ConversationActionPlan>(candidate.ActionPlan);
        Assert.Equal("agent-b", plan.Choice.TargetId);
        Assert.Equal(plan.Choice, ConversationChoice.Decode(plan.CandidateId));
        var state = system.State;
        world.Move("agent-b", new(4, 0));
        Assert.Equal(ActionRefusalReason.TargetUnavailable, system.CheckConversationPlan(world, plan).Refusal!.Reason);
        Assert.False(system.ApplyConversationPlan(world, plan));
        Assert.Same(state, system.State);
        Assert.Empty(world.Events);
    }

    [Fact]
    public void AnInvitationPlanCannotAuthorizeAChangedConversationRevision()
    {
        var world = new FakeWorld();
        var system = new ConversationSystem(new([], [], []));
        Assert.True(system.ProposeConversation(world, "agent-a", "agent-b"));
        var invitation = system.ConversationFor("agent-b")!;
        var candidate = system.ConversationCandidates(world, "agent-b").Single(item => item.Id == "conversation_accept:" + invitation.Id);
        var plan = Assert.IsType<ConversationActionPlan>(candidate.ActionPlan);
        Assert.Equal((invitation.Revision, "agent-a", "agent-b"), (plan.Revision, plan.InitiatorId, plan.InviteeId));
        Assert.True(system.DeclineConversation(world, "agent-b", invitation.Id));
        var state = system.State;
        var count = world.Events.Count;
        Assert.Equal(ActionRefusalReason.TargetUnavailable, system.CheckConversationPlan(world, plan).Refusal!.Reason);
        Assert.False(system.ApplyConversationPlan(world, plan));
        Assert.Same(state, system.State);
        Assert.Equal(count, world.Events.Count);
    }

    [Fact]
    public void MarriageAgreementKeepsTheOriginalPartnershipReceipt()
    {
        var world = new FakeWorld();
        world.Apply(checkpoint => new(checkpoint with
        { Agents = checkpoint.Agents.Select(person => person with { HasChosenName = true }).ToArray() }));
        world.Apply(checkpoint => SocietyRules.ProposeRelationship(checkpoint,
            new("original-partnership", 1, SocietyRelationshipType.Partnership, "agent-a", "agent-b", world.WorldTick)));
        world.Apply(checkpoint => SocietyRules.AcceptRelationship(checkpoint, "original-partnership", 1, "agent-b"));
        var system = new ConversationSystem(new([], [], []));
        Assert.True(system.ProposeConversation(world, "agent-a", "agent-b"));
        var id = system.ConversationFor("agent-a")!.Id;
        Assert.True(system.AcceptConversation(world, "agent-b", id));
        var current = system.ConversationFor("agent-a")!;
        for (var ordinal = 0; ordinal < 7; ordinal++)
        {
            world.AdvanceTo(ordinal + 1);
            Assert.True(AgentConversationRules.TryBeginTurn(current, world.WorldTick, world.Society.RunEpoch, out var begun));
            var wrapUp = ordinal == 6;
            var speaker = begun.CurrentSpeakerId!;
            var other = speaker == "agent-a" ? "agent-b" : "agent-a";
            var request = new AgentConversationTurnRequest(DerivedId.Create(id + ":request:" + ordinal), id, begun.Revision,
                world.Society.RunEpoch, world.WorldTick, wrapUp ? AgentConversationPurpose.WrapUp : AgentConversationPurpose.PublicTurn,
                speaker, world.Society.GetAgent(speaker).Name, other, world.Society.GetAgent(other).Name, "patient", "learn", begun.Turns,
                wrapUp ? [AgentConversationEffect.Marriage] : [AgentConversationEffect.None]);
            var response = new AgentConversationTurnResponse(request.RequestId, id, request.Revision, request.RunEpoch, speaker,
                wrapUp ? "I propose marriage." : "A public turn.", AgentConversationDisposition.Continue,
                wrapUp ? AgentConversationEffect.Marriage : AgentConversationEffect.None);
            Assert.True(AgentConversationRules.TryAdmitTurn(begun, request, response, [other], world.WorldTick, world.Society.RunEpoch,
                out current, out _));
        }
        system.SetConversation(current);
        var plan = Assert.IsType<ConversationActionPlan>(system.ConversationCandidates(world, "agent-a")
            .Single(item => item.Id == "conversation_wrapup_accept:" + id).ActionPlan);
        Assert.Equal("original-partnership", plan.Partnership!.Id);
        world.Apply(checkpoint => new(checkpoint with
        {
            Relationships = checkpoint.Relationships.Select(edge => edge.Id == plan.Partnership.Id
                ? edge with { AcceptedBy = edge.AcceptedBy?.ToArray() } : edge).ToArray(),
        }));
        Assert.True(system.CheckConversationPlan(world, plan).Allowed);
        world.Apply(checkpoint => SocietyRules.RevokeRelationship(checkpoint, "original-partnership", "agent-a"));
        world.Apply(checkpoint => SocietyRules.ProposeRelationship(checkpoint,
            new("replacement-partnership", 1, SocietyRelationshipType.Partnership, "agent-a", "agent-b", world.WorldTick)));
        world.Apply(checkpoint => SocietyRules.AcceptRelationship(checkpoint, "replacement-partnership", 1, "agent-b"));
        var state = system.State;
        Assert.True(system.CanProposeMarriage(world, current));
        Assert.False(system.CheckConversationPlan(world, plan).Allowed);
        Assert.False(system.ApplyConversationPlan(world, plan));
        Assert.Same(state, system.State);
        Assert.Empty(system.State.Marriages);
    }

    [Fact]
    public void ResumePlansKeepEachParticipantsConsentAndRejectTheEarlierRevision()
    {
        var world = new FakeWorld();
        var system = new ConversationSystem(new([], [], []));
        Assert.True(system.ProposeConversation(world, "agent-a", "agent-b"));
        var id = system.ConversationFor("agent-a")!.Id;
        Assert.True(system.AcceptConversation(world, "agent-b", id));
        system.SetConversation(AgentConversationRules.Suspend(system.ConversationFor("agent-a")!, AgentConversationInterruption.ProviderUnavailable, world.WorldTick));
        var first = Assert.IsType<ConversationActionPlan>(system.ConversationCandidates(world, "agent-a")
            .Single(item => item.Id == "conversation_resume:" + id).ActionPlan);
        Assert.True(system.ApplyConversationPlan(world, first));
        Assert.Equal(["agent-a"], system.ConversationFor("agent-b")!.ResumeAcceptedBy);
        Assert.False(system.CheckConversationPlan(world, first).Allowed);
        var second = Assert.IsType<ConversationActionPlan>(system.ConversationCandidates(world, "agent-b")
            .Single(item => item.Id == "conversation_resume:" + id).ActionPlan);
        Assert.True(system.ApplyConversationPlan(world, second));
        Assert.Equal(AgentConversationStatus.Ready, system.ConversationFor("agent-a")!.Status);
        Assert.Equal(ResumePlanEvents,
            world.Events.Select(item => item.Kind));
    }
}

public sealed partial class WorldConversationTests
{
    [Fact]
    public async Task AConversationOfferRetainsItsPlanThroughNativeRollbackReplayAndStrictReload()
    {
        var chooser = new ConversationPlanChooser();
        using var setup = NewWorld("conversation-retained-plan", _ => chooser);
        setup.StartWorld();
        var state = setup.ExportState();
        state = state with
        {
            Society = state.Society with
            {
                Society = state.Society.Society with
                {
                    Agents = state.Society.Society.Agents.Select(person => person with
                    { Name = person.Id == InitiatorId ? "Áster 🐦" : person.Id == InviteeId ? "Élodie 🌿" : person.Name }).ToArray(),
                },
            },
        };
        using var world = WorldRuntime.Restore(state, _ => chooser);
        var bytes = WorldRuntimeCodec.Encode(world.ExportState());
        Assert.False((await world.AdvanceOneTickAsync(commitPermitted: () => false)).Advanced);
        Assert.Equal(bytes, WorldRuntimeCodec.Encode(world.ExportState()));
        using var replay = WorldRuntime.Restore(WorldRuntimeCodec.Decode(bytes), _ => new ConversationPlanChooser());
        var step = await world.AdvanceOneTickAsync();
        Assert.True(step.Advanced);
        Assert.True((await replay.AdvanceOneTickAsync()).Advanced);
        Assert.Equal(WorldRuntimeCodec.Encode(world.ExportState()), WorldRuntimeCodec.Encode(replay.ExportState()));
        var plan = Assert.IsType<ConversationActionPlan>(chooser.Plan);
        Assert.Same(plan, step.Decisions.Single(decision => decision.AgentId == InitiatorId).ActionPlan);
        var conversation = Assert.Single(world.Conversations);
        Assert.Equal((InitiatorId, InviteeId, AgentConversationStatus.Proposed),
            (conversation.InitiatorId, conversation.InviteeId, conversation.Status));
        Assert.Single(world.ExportState().Events, item => item.Kind == "conversation_proposed");
        using var reopened = WorldRuntime.Restore(WorldRuntimeCodec.Decode(WorldRuntimeCodec.Encode(world.ExportState())));
        reopened.Validate();
    }

    private sealed class ConversationPlanChooser : IDecisionProvider, IAgentConversationProvider
    {
        public ConversationActionPlan? Plan { get; private set; }
        public DecisionProviderKind Kind => DecisionProviderKind.LargeLanguageModel;
        public long ProviderEpoch => 0;
        public bool CanSpeakAs(string actor) => actor == InitiatorId || actor == InviteeId;
        public ValueTask<CognitionDecisionResponse> DecideAsync(CognitionDecisionRequest request, CancellationToken cancellationToken = default)
        {
            var candidate = request.Observation.AgentId == InitiatorId ? request.Observation.Candidates.FirstOrDefault(item =>
                item.ActionPlan is ConversationActionPlan plan && plan.Choice.Kind == ConversationChoiceKind.Talk && plan.Choice.TargetId == InviteeId) : null;
            if (candidate is not null) Plan = Assert.IsType<ConversationActionPlan>(candidate.ActionPlan);
            var selected = candidate?.Id ?? "safe_idle";
            return ValueTask.FromResult(new CognitionDecisionResponse(request.RequestId, request.Observation.AgentId, Kind,
                request.ProviderEpoch, request.Observation.RunEpoch, request.Observation.DecisionGeneration,
                request.Observation.ObservationDigest, selected, 1, new Dictionary<string, double> { [selected] = 1 },
                ChosenName: request.Observation.NeedsName ? request.Observation.Self?.Name ?? "Test Agent" : null,
                ChosenPersonality: request.Observation.NeedsPersonality ? "patient and curious" : null,
                ChosenAspiration: request.Observation.NeedsAspiration ? "learn the valley" : null));
        }
        public ValueTask<AgentConversationTurnResponse> SpeakAsync(AgentConversationTurnRequest request, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(new AgentConversationTurnResponse(request.RequestId, request.ConversationId, request.Revision,
                request.RunEpoch, request.SpeakerId, "A public conversation.", AgentConversationDisposition.Continue));
    }
}
