using ClankerWorld.Simulation.Cognition;

namespace ClankerWorld.Tests;

public sealed class CognitionRuntimeTests
{
    [Fact]
    public void ObserverReplyIsAdmittedOnlyForTheExactRequestedMessageAndWorld()
    {
        var runtime = new CognitionRuntime("actor-scout", new FixedProvider(DecisionProviderKind.LargeLanguageModel, 4));
        var message = new CognitionObserverGuidance("private-instruction-0000000001", "owner:test", "actor-scout",
            "suggestive", "Try the shore berries.", 4, 0, 1, null, true);
        var observation = CreateObservation() with
        {
            WorldId = "world-guidance-runtime-test",
            ObserverGuidance = [message],
        };
        var request = runtime.IssueRequest(observation);
        var response = ResponseFor(request, DecisionProviderKind.LargeLanguageModel, 4, "safe_idle", 1) with
        {
            ObserverReplies = [new CognitionObserverReply(message.InstructionId, "I will try.")],
        };

        var accepted = runtime.ApplyResponse(response);

        Assert.True(accepted.Accepted);
        var observed = Assert.IsType<CognitionObserverGuidanceResult>(accepted.ObserverGuidance);
        Assert.Equal("world-guidance-runtime-test", observed.WorldId);
        Assert.Equal("actor-scout", observed.InhabitantId);
        Assert.Equal(request.RequestId, observed.RequestId);
        Assert.Equal(observation.DecisionGeneration, observed.DecisionGeneration);
        Assert.Equal(observation.ObservationDigest, observed.ObservationDigest);
        Assert.Equal(message, Assert.Single(observed.Messages));
        Assert.Equal(new CognitionObserverReply(message.InstructionId, "I will try."), Assert.Single(observed.Replies));

        var retryRuntime = new CognitionRuntime("actor-scout", new FixedProvider(DecisionProviderKind.LargeLanguageModel, 4));
        var retryRequest = retryRuntime.IssueRequest(observation with { DecisionGeneration = 1 });
        var wrongId = retryRuntime.ApplyResponse(ResponseFor(retryRequest,
            DecisionProviderKind.LargeLanguageModel, 4, "safe_idle", 1) with
        {
            ObserverReplies = [new CognitionObserverReply("private-instruction-newer", "I will try.")],
        });
        Assert.False(wrongId.Accepted);
        Assert.Equal("observer_reply_not_requested", wrongId.Outcome);
        Assert.Null(wrongId.ObserverGuidance);
    }

    [Fact]
    public void StaleObservationDigestIsRejectedWithoutChangingTheCurrentIntention()
    {
        var runtime = new CognitionRuntime("actor-scout", new FixedProvider(DecisionProviderKind.Jev, 0));
        var request = runtime.IssueRequest(CreateObservation());

        var rejected = runtime.ApplyResponse(ResponseFor(
            request,
            DecisionProviderKind.Jev,
            providerEpoch: 0,
            selectedCandidateId: "seek_food",
            confidence: 0.9) with
        {
            ObservationDigest = "sha256:old-observation",
        });

        Assert.False(rejected.Accepted);
        Assert.Equal("observation_digest", rejected.Outcome);
        Assert.Null(runtime.Capture().CurrentIntention);
        Assert.Equal(CognitionRequestState.Rejected, runtime.Capture().Requests.Single().State);
    }

    [Theory]
    [InlineData("never_offered", 1)]
    public void InvalidAnswerWithStaleIdentityCannotApplyFallback(string candidate, double confidence)
    {
        var runtime = new CognitionRuntime("actor-scout", new FixedProvider(DecisionProviderKind.Jev, 0));
        var request = runtime.IssueRequest(CreateObservation());
        var result = runtime.ApplyResponse(ResponseFor(request, DecisionProviderKind.Jev, 0, candidate, confidence) with
        {
            ObservationDigest = "sha256:old-observation",
        });
        Assert.False(result.Accepted);
        Assert.False(result.FellBack);
        Assert.Equal("observation_digest", result.Outcome);
        Assert.Null(runtime.Capture().CurrentIntention);
    }

    [Fact]
    public void PauseSupersedesAnInFlightRequestAndLateResponseCannotApply()
    {
        var runtime = new CognitionRuntime("actor-scout", new FixedProvider(DecisionProviderKind.Jev, 0));
        var request = runtime.IssueRequest(CreateObservation());

        Assert.True(runtime.Pause(worldTick: 4));
        var late = runtime.ApplyResponse(ResponseFor(
            request,
            DecisionProviderKind.Jev,
            providerEpoch: 0,
            selectedCandidateId: "seek_food",
            confidence: 0.9));

        Assert.False(late.Accepted);
        Assert.Equal("superseded_request", late.Outcome);
        Assert.Null(runtime.Capture().CurrentIntention);
        Assert.True(runtime.Resume(worldTick: 4));
        Assert.Equal(1, runtime.Capture().RunEpoch);
    }

    [Theory]
    [InlineData("seek_food", 0.9)]
    [InlineData("never_offered", 1.2)]
    public void LatePausedReplyCannotRetireTheNewerRequest(string candidate, double confidence)
    {
        var runtime = new CognitionRuntime("actor-scout", new FixedProvider(DecisionProviderKind.Jev, 7));
        var oldRequest = runtime.IssueRequest(CreateObservation());
        Assert.True(runtime.Pause(worldTick: 5));
        Assert.True(runtime.Resume(worldTick: 6));
        var currentRequest = runtime.IssueRequest(CreateObservation(runEpoch: 1, decisionGeneration: 1) with
        {
            WorldTick = 7,
            ObservationDigest = "sha256:observation-7",
        });
        Assert.Equal(7, currentRequest.ProviderEpoch);
        var before = runtime.Capture();
        Assert.Equal(DecisionProviderKind.Jev, before.ProviderKind);
        Assert.Equal(currentRequest.Observation.DecisionGeneration, before.DecisionGeneration);

        var exception = Assert.Throws<InvalidOperationException>(() => runtime.IssueRequest(
            CreateObservation(runEpoch: 1, decisionGeneration: 2)));
        Assert.Contains("one cognition request", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(currentRequest.RequestId, runtime.Capture().InFlightRequestId);
        Assert.Equal(before.Requests, runtime.Capture().Requests);

        var late = runtime.ApplyResponse(ResponseFor(oldRequest, DecisionProviderKind.Jev, 7, candidate, confidence));

        Assert.False(late.Accepted);
        Assert.False(late.FellBack);
        Assert.Equal("request_id", late.Outcome);
        Assert.Equal(currentRequest.RequestId, runtime.Capture().InFlightRequestId);
        Assert.Equal(before.Requests, runtime.Capture().Requests);
        Assert.Equal(before.CurrentIntention, runtime.Capture().CurrentIntention);
        Assert.Equal(oldRequest.RequestId + ":request_id", runtime.Capture().Events[^1].Detail);

        var current = runtime.ApplyResponse(ResponseFor(currentRequest, DecisionProviderKind.Jev, 7, "seek_food", 0.9));
        Assert.True(current.Accepted);
        Assert.False(current.FellBack);
        Assert.Equal("provider_decision", current.Outcome);
        Assert.Equal("seek_food", current.Intention?.CandidateId);
        Assert.Equal(DecisionProviderKind.Jev, current.Intention?.Provider);
        Assert.Null(runtime.Capture().InFlightRequestId);
        Assert.Equal(CognitionRequestState.Applied,
            runtime.Capture().Requests.Single(item => item.Request.RequestId == currentRequest.RequestId).State);
        var restored = CognitionRuntime.Restore(runtime.ExportState());
        Assert.Equal(runtime.Capture().CurrentIntention, restored.Capture().CurrentIntention);
        Assert.Equal(runtime.Capture().Events, restored.Capture().Events);
    }

    [Theory]
    [InlineData(false)]
    public void DuplicateOrUnknownReplyPreservesTheNextRequestAndPreviousIntention(bool unknown)
    {
        var runtime = new CognitionRuntime("actor-scout", new FixedProvider(DecisionProviderKind.Jev, 0));
        var firstRequest = runtime.IssueRequest(CreateObservation());
        var firstResponse = ResponseFor(firstRequest, DecisionProviderKind.Jev, 0, "seek_food", 0.9);
        Assert.True(runtime.ApplyResponse(firstResponse).Accepted);
        var admitted = runtime.Capture();
        var duplicate = runtime.ApplyResponse(firstResponse);
        Assert.False(duplicate.Accepted);
        Assert.Equal("superseded_request", duplicate.Outcome);
        Assert.Single(runtime.Capture().Requests);
        Assert.Equal(CognitionRequestState.Applied, runtime.Capture().Requests.Single().State);
        Assert.Equal(admitted.CurrentIntention, runtime.Capture().CurrentIntention);
        Assert.Equal(admitted.Requests, runtime.Capture().Requests);

        var currentRequest = runtime.IssueRequest(CreateObservation(decisionGeneration: 1));
        var before = runtime.Capture();

        var rejected = runtime.ApplyResponse(unknown ? firstResponse with { RequestId = "cognition-unknown" } : firstResponse);

        Assert.False(rejected.Accepted);
        Assert.False(rejected.FellBack);
        Assert.Equal("request_id", rejected.Outcome);
        Assert.Equal(currentRequest.RequestId, runtime.Capture().InFlightRequestId);
        Assert.Equal(before.Requests, runtime.Capture().Requests);
        Assert.Equal(before.CurrentIntention, runtime.Capture().CurrentIntention);
        Assert.True(runtime.ApplyResponse(ResponseFor(currentRequest, DecisionProviderKind.Jev, 0, "safe_idle", 1)).Accepted);
        Assert.Equal("safe_idle", runtime.Capture().CurrentIntention?.CandidateId);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void LateDiagnosticsKeepTheRetiredRequestsWorldTime(bool failure)
    {
        var runtime = new CognitionRuntime("actor-scout", new FixedProvider(DecisionProviderKind.Jev, 0));
        var request = runtime.IssueRequest(CreateObservation(runEpoch: 2) with { WorldTick = 5_040 });
        var response = ResponseFor(request, DecisionProviderKind.Jev, 0, "seek_food", 0.9);
        Assert.True(runtime.ApplyResponse(response).Accepted);
        var before = runtime.Capture();

        var rejected = failure
            ? runtime.FailRequest(request.RequestId, "provider_failure")
            : runtime.ApplyResponse(response with { RunEpoch = 999 });

        Assert.False(rejected.Accepted);
        Assert.False(rejected.FellBack);
        Assert.Equal(failure ? "no_in_flight_request" : "superseded_request", rejected.Outcome);
        var after = runtime.Capture();
        Assert.Equal(before.CurrentIntention, after.CurrentIntention);
        Assert.Equal(before.Requests, after.Requests);
        Assert.Null(after.InFlightRequestId);
        var diagnostic = after.Events[^1];
        Assert.Equal(failure ? "cognition_failure_ignored" : "cognition_response_rejected", diagnostic.Kind);
        Assert.Equal(5_040, diagnostic.WorldTick);
        Assert.Equal(before.Events[^1].EventId + 1, diagnostic.EventId);
        Assert.Equal(after.Events, CognitionRuntime.Restore(runtime.ExportState()).Capture().Events);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DiagnosticsAfterRestoreUseTheRetainedWorldTime(bool failure)
    {
        var runtime = new CognitionRuntime("actor-scout", new FixedProvider(DecisionProviderKind.Jev, 0));
        var request = runtime.IssueRequest(CreateObservation(runEpoch: 2) with { WorldTick = 5_040 });
        Assert.True(runtime.Pause(worldTick: 5_050));
        var restored = CognitionRuntime.Restore(runtime.ExportState());
        var before = restored.Capture();

        var rejected = failure
            ? restored.FailRequest(request.RequestId, "provider_failure")
            : restored.ApplyResponse(ResponseFor(request, DecisionProviderKind.Jev, 0, "seek_food", 0.9));

        Assert.False(rejected.Accepted);
        Assert.Equal("no_in_flight_request", rejected.Outcome);
        var after = restored.Capture();
        Assert.Equal(5_050, after.Events[^1].WorldTick);
        Assert.Equal(before.CurrentIntention, after.CurrentIntention);
        Assert.Equal(before.Requests, after.Requests);
        Assert.True(after.IsPaused);
        Assert.Equal(after.Events, CognitionRuntime.Restore(restored.ExportState()).Capture().Events);
    }

    [Fact]
    public void AnIgnoredFailureUsesTheCurrentObservationWithoutRetiringIt()
    {
        var runtime = new CognitionRuntime("actor-scout", new FixedProvider(DecisionProviderKind.Jev, 0));
        var request = runtime.IssueRequest(CreateObservation(runEpoch: 2) with { WorldTick = 5_040 });
        var before = runtime.Capture();

        var rejected = runtime.FailRequest("cognition-unknown", "provider_failure");

        Assert.False(rejected.Accepted);
        var after = runtime.Capture();
        Assert.Equal(5_040, after.Events[^1].WorldTick);
        Assert.Equal(request.RequestId, after.InFlightRequestId);
        Assert.Equal(before.Requests, after.Requests);
        Assert.True(runtime.ApplyResponse(ResponseFor(request, DecisionProviderKind.Jev, 0, "seek_food", 0.9)).Accepted);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("cognition-unknown")]
    public void AnUnknownReplyBeforeTheFirstRequestUsesZeroWorldTime(string? requestId)
    {
        var runtime = new CognitionRuntime("actor-scout", new FixedProvider(DecisionProviderKind.Jev, 0));
        var preview = runtime.PreviewRequest(CreateObservation());

        var rejected = runtime.ApplyResponse(ResponseFor(preview, DecisionProviderKind.Jev, 0, "seek_food", 0.9) with
        {
            RequestId = requestId!,
            RunEpoch = 999,
        });

        Assert.False(rejected.Accepted);
        Assert.Equal("no_in_flight_request", rejected.Outcome);
        var after = runtime.Capture();
        Assert.Equal(0, Assert.Single(after.Events).WorldTick);
        Assert.Empty(after.Requests);
        Assert.Null(after.CurrentIntention);
        Assert.Equal(after.Events, CognitionRuntime.Restore(runtime.ExportState()).Capture().Events);
    }

    private static InhabitantObservation CreateObservation(
        long runEpoch = 0,
        long decisionGeneration = 0) => new(
        "actor-scout",
        WorldTick: 4,
        runEpoch,
        decisionGeneration,
        "sha256:observation-4",
        HungerBasisPoints: 2_000,
        [
            new CognitionCandidate("safe_idle", "Continue the current safe routine.", 0),
            new CognitionCandidate("seek_food", "Travel toward available food.", 10, "berry-patch"),
        ]);

    private static CognitionDecisionResponse ResponseFor(
        CognitionDecisionRequest request,
        DecisionProviderKind provider,
        long providerEpoch,
        string selectedCandidateId,
        double confidence) => new(
        request.RequestId,
        request.Observation.InhabitantId,
        provider,
        providerEpoch,
        request.Observation.RunEpoch,
        request.Observation.DecisionGeneration,
        request.Observation.ObservationDigest,
        selectedCandidateId,
        confidence,
        new Dictionary<string, double>(StringComparer.Ordinal)
        {
            [selectedCandidateId] = confidence,
        });

    private sealed class FixedProvider(DecisionProviderKind kind, long providerEpoch) : IDecisionProvider
    {
        public DecisionProviderKind Kind => kind;

        public long ProviderEpoch => providerEpoch;

        public ValueTask<CognitionDecisionResponse> DecideAsync(
            CognitionDecisionRequest request,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(ResponseFor(request, kind, providerEpoch, "seek_food", 0.9));
    }
}
