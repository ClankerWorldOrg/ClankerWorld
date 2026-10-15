using System.Collections;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.World;

namespace ClankerWorld.Tests;

public sealed class CognitionSystemTests
{
    private static CognitionDecisionRequest Request => new("identity-request", 0,
        new("actor", 0, 0, 0, "digest", 10_000, [new("identity_optional", "Choose an identity.")]));
    private static CognitionDecisionResponse Response => new("identity-request", "actor", DecisionProviderKind.LargeLanguageModel,
        0, 0, 0, "digest", "identity_optional", 1, new Dictionary<string, double> { ["identity_optional"] = 1 });

    [Fact]
    public void HostViewsKeepLiveRequestOrderAndRejectMutation()
    {
        var system = new CognitionSystem(null, 4);
        var view = system.ModelCalls;
        using var cancellation = new CancellationTokenSource();
        var pending = new CognitionSystem.PendingModelDecision(Request,
            Task.FromResult(new CognitionSystem.ModelDecisionOutcome(null, null)), cancellation, null);
        system.QueueModelDecision("actor", pending);
        Assert.Same(pending, Assert.Single(view).Value);
        Assert.Throws<NotSupportedException>(() => Assert.IsAssignableFrom<IDictionary>(view).Clear());
        Assert.True(system.ForgetModelDecision("actor", out var removed));
        Assert.Same(pending, removed);
        Assert.Empty(view);
    }

    [Fact]
    public void WrongActorFailsBeforeItsRequestIsQueued()
    {
        var system = new CognitionSystem(null, 4);
        using var cancellation = new CancellationTokenSource();
        var pending = new CognitionSystem.PendingIdentityMoment(AgentIdentityMomentKind.Midlife, Request,
            Task.FromResult(new CognitionSystem.IdentityMomentOutcome(null)), cancellation);
        var failure = Assert.Throws<InvalidDataException>(() => system.QueueIdentityMoment("other", pending));
        Assert.Contains("pending identity moment", failure.Message, StringComparison.Ordinal);
        Assert.Empty(system.IdentityMoments);
    }

    [Fact]
    public void IdentityAndModelComponentFaultsFailBeforeTheirPhysicalRecordIsPublished()
    {
        var store = new AgentStore();
        store.PlaceFounder("actor", new(1, 1));
        var before = store.Living["actor"];
        Assert.Throws<InvalidDataException>(() => store.RecordIdentityMoments("actor", before,
            [new(AgentIdentityMomentKind.Midlife, 0, "accepted", 0, 0)]));
        Assert.Throws<InvalidDataException>(() => store.RecordModelAttempt("actor", before, new("invalid-status", 0)));
        Assert.Same(before, store.Living["actor"]);
    }

    [Fact]
    public void MalformedOrMismatchedIdentityRepliesRemainUnaccepted()
    {
        Assert.True(CognitionSystem.IsMatchingIdentityMomentResponse(Request, Response));
        Assert.False(CognitionSystem.IsMatchingIdentityMomentResponse(Request, Response with { Confidence = double.NaN }));
        Assert.False(CognitionSystem.IsMatchingIdentityMomentResponse(Request, Response with { RequestId = "different" }));
        Assert.False(CognitionSystem.IsMatchingIdentityMomentResponse(Request, null));
    }

    [Fact]
    public void UnexpectedValidationFailuresAreNotHiddenAsBadIdentityReplies()
    {
        var response = Response with { Probabilities = new BrokenProbabilities() };
        var failure = Assert.Throws<InvalidOperationException>(() => CognitionSystem.IsMatchingIdentityMomentResponse(Request, response));
        Assert.Equal("unexpected validation failure", failure.Message);
    }

    private sealed class BrokenProbabilities : IReadOnlyDictionary<string, double>
    {
        public int Count => 1;
        public IEnumerable<string> Keys => ["identity_optional"];
        public IEnumerable<double> Values => [1];
        public double this[string key] => 1;
        public bool ContainsKey(string key) => true;
        public bool TryGetValue(string key, out double value) { value = 1; return true; }
        public IEnumerator<KeyValuePair<string, double>> GetEnumerator() => throw new InvalidOperationException("unexpected validation failure");
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
