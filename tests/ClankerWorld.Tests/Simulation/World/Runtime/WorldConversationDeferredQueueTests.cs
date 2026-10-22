using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.World;

namespace ClankerWorld.Tests;

public sealed partial class WorldConversationTests
{
    [Fact]
    public async Task NewGuidanceWaitsForCurrentChoicesAfterAnOlderReplyStartsAConversation()
    {
        var provider = new ConversationProvider(new HashSet<string>([InitiatorId, InviteeId], StringComparer.Ordinal));
        var held = new HeldFirstConversationPlanningProvider(provider);
        using var world = NewWorld("conversation-call-baseline", id =>
            id is InitiatorId or InviteeId ? held : new DeterministicDecisionProvider());
        world.StartWorld();
        Assert.True((await world.TickModelWorkAsync()).Advanced);
        await ModelWorkHarness.For(world).WaitAsync(held.Started.Task);
        var guidance = world.SubmitInstruction(new OwnerInstructionRequest(
            "guidance-during-talk", "owner:test", InitiatorId, OwnerInstructionKind.Suggestion,
            "Remember the river path after this conversation."));
        Assert.True((await world.TickModelWorkAsync()).Advanced);
        Assert.Contains(world.ExportState().Society.Cognition.Queue, entry =>
            entry.AgentId == InitiatorId && entry.Observation.ObserverGuidance!.Any(message =>
                message.InstructionId == guidance.InstructionId));

        held.Release.TrySetResult();
        await ModelWorkHarness.WaitForExistingAsync(world);
        var admitted = await world.TickModelWorkAsync();
        Assert.Contains(admitted.Decisions, decision => decision.AgentId == InitiatorId &&
            decision.Admission.Intention?.CandidateId == $"talk:{InviteeId}");
        var proposal = Assert.Single(world.Conversations);
        var proposedAt = world.WorldTick;
        Assert.Equal(AgentConversationStatus.Proposed, proposal.Status);
        await ModelWorkHarness.WaitForExistingAsync(world);
        Assert.Single(provider.PlanningRequests, request => request.Observation.AgentId == InitiatorId);

        for (var tick = 0; tick < 64 && world.ExportState().Instructions!.Single(instruction =>
                 instruction.InstructionId == guidance.InstructionId).ObservedTick is null; tick++)
        {
            await ModelWorkHarness.WaitForExistingAsync(world);
            Assert.True((await world.TickModelWorkAsync()).Advanced);
        }
        await ModelWorkHarness.WaitForExistingAsync(world);

        Assert.NotNull(world.ExportState().Instructions!.Single(instruction =>
            instruction.InstructionId == guidance.InstructionId).ObservedTick);
        var later = Assert.Single(provider.PlanningRequests, request =>
            request.Observation.AgentId == InitiatorId && request.Observation.ObserverGuidance!.Any(message =>
                message.InstructionId == guidance.InstructionId));
        Assert.True(later.Observation.WorldTick > proposedAt,
            "The retained guidance must use choices observed after the older talk action was applied.");
        var calls = provider.PlanningRequests.Where(request => request.Observation.AgentId == InitiatorId).ToArray();
        Assert.Equal(calls.Length, calls.Select(request =>
            (request.RequestId, request.Observation.RunEpoch, request.Observation.DecisionGeneration,
                request.Observation.ObservationDigest)).Distinct().Count());
        world.Validate();
    }

    private sealed class HeldFirstConversationPlanningProvider(ConversationProvider provider) :
        IDecisionProvider, IAgentConversationProvider
    {
        private int held;
        public InlineCompletionSource Started { get; } = new(TaskCreationOptions.None);
        public InlineCompletionSource Release { get; } = new(TaskCreationOptions.None);
        public DecisionProviderKind Kind => provider.Kind;
        public long ProviderEpoch => provider.ProviderEpoch;
        public bool CanSpeakAs(string agentId) => provider.CanSpeakAs(agentId);

        public async ValueTask<CognitionDecisionResponse> DecideAsync(
            CognitionDecisionRequest request, CancellationToken cancellationToken = default)
        {
            if (request.Observation.AgentId == InitiatorId && Interlocked.Exchange(ref held, 1) == 0)
            {
                Started.TrySetResult();
                await Release.Task.WaitAsync(cancellationToken);
            }
            return await provider.DecideAsync(request, cancellationToken);
        }

        public ValueTask<AgentConversationTurnResponse> SpeakAsync(
            AgentConversationTurnRequest request, CancellationToken cancellationToken = default) =>
            provider.SpeakAsync(request, cancellationToken);
    }
}
