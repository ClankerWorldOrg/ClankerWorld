using System.Collections.Concurrent;
using ClankerWorld.Simulation;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.World;
using ClankerWorld.Simulation.Society;
using ClankerWorld.Server.Observation;

namespace ClankerWorld.Tests;

public sealed partial class GrownAgentHelperMemoryTests
{
    private static async Task<WorldRuntimeState> FormNativeLandHouseholdAsync(
        WorldRuntimeState state, string actor, bool quietFirstRefusal = false, bool heldIdentityReply = false,
        bool cancelFoundReply = false)
    {
        const string identityActor = "founder:00000000000000000000000000000001";
        var choices = new NativeLandFormationChoices(actor, quietFirstRefusal)
        { HeldIdentityId = heldIdentityReply ? identityActor : null, HoldFoundReply = cancelFoundReply };
        Func<WorldRuntime, Task> awaitRequests = WaitForTransferChoices;
        var identityWaitChecked = false;
        var foundCancellationChecked = false;
        using var world = WorldRuntime.Restore(state, _ => choices);
        var hosted = cancelFoundReply ? Assert.IsAssignableFrom<System.Collections.IDictionary>(typeof(WorldRuntime)
            .GetField("pendingHosted", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .GetValue(world)) : null;
        world.Resume();
        world.SubmitInstruction(new("consider-native-household", "owner:test", actor, OwnerInstructionKind.Suggestive,
            "Consider founding your own household when existing households refuse your request to join."));
        var folder = Directory.CreateTempSubdirectory("native-land-household-");
        var prompted = new HashSet<(string Phase, string Id, long Generation)>();
        try
        {
            var file = new WorldStateFile(Path.Combine(folder.FullName, "world.json"));
            file.Save(world);
            var presence = new OwnerClientPresenceLease(TimeSpan.FromMinutes(10));
            presence.RecordAuthenticatedReconnect("land-household-owner");
            using var host = new WorldRuntimeService(world, file, presence);
            bool HasFoundedHousehold() => world.ExportState().Events.Any(item => item.Kind == "household_founded" &&
                    item.Detail == actor + "|" + world.Society.GetInhabitant(actor).HouseholdId);
            for (var tick = 0; tick < 40 && !HasFoundedHousehold(); tick++)
            {
                var before = world.ExportState();
                var housingBefore = before.Inhabitants.Single(person => person.InhabitantId == actor).Housing;
                var request = housingBefore?.Request;
                var phase = request?.HouseholdId ??
                    $"{world.Society.GetInhabitant(actor).HouseholdId}:{housingBefore?.Refusals?.Count ?? 0}";
                var recipients = request is null ? [actor] : request.Members
                    .Except(request.Approvals, StringComparer.Ordinal).Except(request.Rejections, StringComparer.Ordinal).ToArray();
                var ordinal = 0;
                foreach (var id in recipients.Order(StringComparer.Ordinal))
                {
                    if (before.Society.Society.GetInhabitant(id).Status != SocietyInhabitantStatus.Active)
                        continue;
                    var intention = before.Society.Cognition.Runtimes.Single(runtime => runtime.InhabitantId == id).CurrentIntention;
                    if (intention is not { CandidateId: "safe_idle" } || !prompted.Add((phase, id, intention.DecisionGeneration)))
                        continue;
                    // The fixture needs another personal opportunity, whereas a
                    // healthy idle intention may otherwise persist for 300 ticks.
                    // One suggestion per admitted idle choice keeps replies free
                    // to make only the household choices actually offered.
                    world.SubmitInstruction(new($"native-household-{world.WorldTick}-{ordinal++}", "owner:test", id,
                        OwnerInstructionKind.Suggestive, request is null
                            ? "Consider your next household choice, including founding after existing households refuse."
                            : "Consider the pending request to join your household and make your own choice."));
                }
                Assert.True(await host.TryAdvanceOnceAsync());
                if (cancelFoundReply && !foundCancellationChecked && hosted![actor] is { } pending &&
                    pending.GetType().GetProperty("Request")!.GetValue(pending) is CognitionDecisionRequest foundRequest &&
                    foundRequest.Observation.Candidates.Any(candidate => candidate.Id == "household_found"))
                {
                    var offered = await choices.FoundCalled.Task.WaitAsync(TimeSpan.FromSeconds(30));
                    Assert.Equal(foundRequest.RequestId, offered.RequestId);
                    Assert.Equal(actor, offered.Observation.InhabitantId);
                    Assert.Null(world.Society.GetInhabitant(actor).HouseholdId);
                    var task = Assert.IsAssignableFrom<Task>(pending.GetType().GetProperty("Task")!.GetValue(pending));
                    world.Pause();
                    world.Resume();
                    var cancelledState = WorldRuntimeCodec.Encode(world.ExportState());
                    var cancelledCheckpoint = File.ReadAllBytes(file.Path);
                    try { await task.WaitAsync(TimeSpan.FromSeconds(30)); }
                    finally { choices.ReleaseFound.TrySetResult(); }
                    Assert.Equal(cancelledState, WorldRuntimeCodec.Encode(world.ExportState()));
                    Assert.Equal(cancelledCheckpoint, File.ReadAllBytes(file.Path));
                    Assert.Null(world.Society.GetInhabitant(actor).HouseholdId);
                    foundCancellationChecked = true;
                }
                else if (heldIdentityReply && !identityWaitChecked && world.Inhabitants
                    .Single(person => person.InhabitantId == identityActor).IdentityMoments?
                    .Any(moment => moment.Outcome == "requested") == true)
                {
                    await WaitForRequests(world);
                    var identity = await choices.IdentityCalled.Task.WaitAsync(TimeSpan.FromSeconds(30));
                    Assert.Equal(identityActor, identity.Observation.InhabitantId);
                    Assert.NotNull(identity.Observation.IdentityMoment);
                    var heldState = WorldRuntimeCodec.Encode(world.ExportState());
                    var heldCheckpoint = File.ReadAllBytes(file.Path);
                    var waiting = awaitRequests(world);
                    try { Assert.False(waiting.IsCompleted, "The household fixture must wait for its member's native life-moment reply before advancing again."); }
                    finally { choices.ReleaseIdentity.TrySetResult(); }
                    await waiting;
                    Assert.Equal(heldState, WorldRuntimeCodec.Encode(world.ExportState()));
                    Assert.Equal(heldCheckpoint, File.ReadAllBytes(file.Path));
                    identityWaitChecked = true;
                }
                else await awaitRequests(world);
                var saved = File.ReadAllBytes(file.Path);
                using var restored = WorldRuntime.Restore(WorldRuntimeCodec.Decode(saved), _ => new QuietProvider());
                Assert.Equal(saved, WorldRuntimeCodec.Encode(restored.ExportState()));
                var current = world.ExportState();
                foreach (var (id, observation) in choices.QuietRefusals)
                {
                    var intention = current.Society.Cognition.Runtimes.Single(runtime => runtime.InhabitantId == id).CurrentIntention;
                    if (intention is { CandidateId: "safe_idle", Provider: DecisionProviderKind.LargeLanguageModel } &&
                        intention.RunEpoch == observation.RunEpoch && intention.DecisionGeneration == observation.DecisionGeneration &&
                        intention.ObservationDigest == observation.ObservationDigest)
                        choices.QuietAdmissions.Add(id);
                }
            }
            if (quietFirstRefusal) Assert.NotEmpty(choices.QuietAdmissions);
            if (cancelFoundReply)
            {
                Assert.True(foundCancellationChecked);
                Assert.True(choices.Selected.Count(selected => selected.Contains(" choice=household_found ", StringComparison.Ordinal)) >= 2);
            }
            if (heldIdentityReply)
            {
                Assert.True(identityWaitChecked);
                var identity = await choices.IdentityCalled.Task;
                Assert.Contains(world.Inhabitants.Single(person => person.InhabitantId == identityActor).IdentityMoments!,
                    moment => moment.Outcome == "kept" && moment.RequestedTick == identity.Observation.WorldTick);
            }
            var household = world.Society.GetInhabitant(actor).HouseholdId;
            Assert.Contains(choices.Selected, selected => selected.Contains(" choice=household_found ", StringComparison.Ordinal));
            Assert.NotNull(household);
            var founded = Assert.Single(world.ExportState().Events,
                item => item.Kind == "household_founded" && item.Detail == actor + "|" + household);
            Assert.Equal(DerivedId.Create("household:solo:" + actor + ":" + founded.WorldTick), household);
            Assert.InRange(household.Length, 1, DerivedId.MaximumLength);
            Assert.Contains(actor, world.Society.GetHousehold(household).MemberIds);
            Assert.Equal(state.Society.Society.Births.Select(birth => birth.ChildId), world.Society.Births.Select(birth => birth.ChildId));
            world.Pause();
            return world.ExportState();
        }
        finally { choices.ReleaseIdentity.TrySetResult(); choices.ReleaseFound.TrySetResult(); folder.Delete(true); }
    }

    private sealed class NativeLandFormationChoices(string actor, bool quietFirstRefusal) : IDecisionProvider
    {
        public ConcurrentDictionary<string, InhabitantObservation> QuietRefusals { get; } = new(StringComparer.Ordinal);
        public HashSet<string> QuietAdmissions { get; } = new(StringComparer.Ordinal);
        public ConcurrentQueue<string> Selected { get; } = new();
        public string? HeldIdentityId { get; init; }
        public TaskCompletionSource<CognitionDecisionRequest> IdentityCalled { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource ReleaseIdentity { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public bool HoldFoundReply { get; init; }
        public TaskCompletionSource<CognitionDecisionRequest> FoundCalled { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource ReleaseFound { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public DecisionProviderKind Kind => DecisionProviderKind.LargeLanguageModel;
        public long ProviderEpoch => 0;
        private async ValueTask<CognitionDecisionResponse> KeepIdentityAsync(CognitionDecisionRequest request, CancellationToken cancellationToken)
        {
            if (request.Observation.InhabitantId == HeldIdentityId && IdentityCalled.TrySetResult(request))
                await ReleaseIdentity.Task.WaitAsync(cancellationToken);
            return KeepNativeIdentity(request);
        }
        private async ValueTask<CognitionDecisionResponse> HoldFoundAsync(CognitionDecisionResponse response, CancellationToken cancellationToken)
        {
            await ReleaseFound.Task.WaitAsync(cancellationToken);
            return response;
        }
        public ValueTask<CognitionDecisionResponse> DecideAsync(CognitionDecisionRequest request, CancellationToken cancellationToken = default)
        {
            var observation = request.Observation;
            if (observation.IdentityMoment is not null) return KeepIdentityAsync(request, cancellationToken);
            var own = observation.InhabitantId == actor;
            var selected = observation.Candidates.FirstOrDefault(candidate => own && candidate.Id == "household_found") ??
                observation.Candidates.FirstOrDefault(candidate => own && candidate.Id == "household_leave") ??
                observation.Candidates.FirstOrDefault(candidate => own && candidate.Id.StartsWith("household_ask:", StringComparison.Ordinal)) ??
                observation.Candidates.FirstOrDefault(candidate => observation.InhabitantId != actor && candidate.Id == "household_refuse:" + actor) ??
                observation.Candidates.Single(candidate => candidate.Id == "safe_idle");
            if (quietFirstRefusal && selected.Id == "household_refuse:" + actor &&
                QuietRefusals.TryAdd(observation.InhabitantId, observation))
                selected = observation.Candidates.Single(candidate => candidate.Id == "safe_idle");
            Selected.Enqueue($"tick={observation.WorldTick} actor={observation.InhabitantId} " +
                $"choice={selected.Id} guidance={observation.ObserverGuidance?.Count ?? 0}");
            var response = new CognitionDecisionResponse(request.RequestId, observation.InhabitantId, Kind, ProviderEpoch,
                observation.RunEpoch, observation.DecisionGeneration, observation.ObservationDigest, selected.Id, 1,
                new Dictionary<string, double> { [selected.Id] = 1 },
                ChosenName: observation.NeedsName ? observation.Self!.Name : null,
                ChosenPersonality: observation.NeedsPersonality ? "patient" : null,
                ChosenAspiration: observation.NeedsAspiration ? "build a home" : null);
            return HoldFoundReply && selected.Id == "household_found" && FoundCalled.TrySetResult(request)
                ? HoldFoundAsync(response, cancellationToken) : ValueTask.FromResult(response);
        }
    }

    private static CognitionDecisionResponse KeepNativeIdentity(CognitionDecisionRequest request)
    {
        var observation = request.Observation;
        var choice = Assert.Single(observation.Candidates);
        Assert.Equal("identity_optional", choice.Id);
        return new(request.RequestId, observation.InhabitantId, DecisionProviderKind.LargeLanguageModel, request.ProviderEpoch,
            observation.RunEpoch, observation.DecisionGeneration, observation.ObservationDigest,
            choice.Id, 1, new Dictionary<string, double> { [choice.Id] = 1 });
    }
}
