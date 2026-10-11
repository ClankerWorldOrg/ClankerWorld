using System.Collections.Concurrent;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Harness;
using ClankerWorld.Simulation.Playtest;
using ClankerWorld.Simulation.Society;
using ClankerWorld.Viewer.Observation;

namespace ClankerWorld.Simulation.Tests;

public sealed partial class GrownAgentHelperMemoryTests
{
    private static async Task<PrivateWorldRuntimeState> FormNativeLandHouseholdAsync(
        PrivateWorldRuntimeState state, string actor, bool quietFirstRefusal = false)
    {
        var choices = new NativeLandFormationChoices(actor, quietFirstRefusal);
        using var world = PrivateWorldRuntime.Restore(state, _ => choices);
        world.Resume();
        world.SubmitInstruction(new("consider-native-household", "owner:test", actor, OwnerInstructionKind.Suggestive,
            "Consider founding your own household when existing households refuse your request to join."));
        var folder = Directory.CreateTempSubdirectory("native-land-household-");
        var trace = new List<string>();
        var prompted = new HashSet<(string Phase, string Id, long Generation)>();
        try
        {
            var file = new PrivateWorldStateFile(Path.Combine(folder.FullName, "world.json"));
            file.Save(world);
            var presence = new OwnerClientPresenceLease(TimeSpan.FromMinutes(10));
            presence.RecordAuthenticatedReconnect("land-household-owner");
            using var host = new PrivateWorldRuntimeService(world, file, presence);
            bool HasFoundedHousehold() => choices.Founded &&
                world.ExportState().Events.Any(item => item.Kind == "household_founded" &&
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
                await WaitForRequests(world);
                var saved = File.ReadAllBytes(file.Path);
                using var restored = PrivateWorldRuntime.Restore(PrivateWorldRuntimeCodec.Decode(saved), _ => new QuietProvider());
                Assert.Equal(saved, PrivateWorldRuntimeCodec.Encode(restored.ExportState()));
                var current = world.ExportState();
                foreach (var (id, observation) in choices.QuietRefusals)
                {
                    var intention = current.Society.Cognition.Runtimes.Single(runtime => runtime.InhabitantId == id).CurrentIntention;
                    if (intention is { CandidateId: "safe_idle", Provider: DecisionProviderKind.LargeLanguageModel } &&
                        intention.RunEpoch == observation.RunEpoch && intention.DecisionGeneration == observation.DecisionGeneration &&
                        intention.ObservationDigest == observation.ObservationDigest)
                        choices.QuietAdmissions.Add(id);
                }
                var housing = current.Inhabitants.Single(person => person.InhabitantId == actor).Housing;
                trace.Add($"tick={world.WorldTick} home={world.Society.GetInhabitant(actor).HouseholdId} " +
                    $"request={housing?.Request?.HouseholdId} refusals={housing?.Refusals?.Count ?? 0} " +
                    $"quietAdmissions={choices.QuietAdmissions.Count}");
            }
            if (quietFirstRefusal) Assert.NotEmpty(choices.QuietAdmissions);
            var household = world.Society.GetInhabitant(actor).HouseholdId;
            Assert.True(choices.Founded, string.Join("\n", trace) + "\n" + string.Join("\n", choices.Selected));
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
        finally { folder.Delete(true); }
    }

    private sealed class NativeLandFormationChoices(string actor, bool quietFirstRefusal) : IDecisionProvider
    {
        public bool Founded { get; private set; }
        public ConcurrentDictionary<string, InhabitantObservation> QuietRefusals { get; } = new(StringComparer.Ordinal);
        public HashSet<string> QuietAdmissions { get; } = new(StringComparer.Ordinal);
        public ConcurrentQueue<string> Selected { get; } = new();
        public DecisionProviderKind Kind => DecisionProviderKind.LargeLanguageModel;
        public long ProviderEpoch => 0;
        public ValueTask<CognitionDecisionResponse> DecideAsync(CognitionDecisionRequest request, CancellationToken cancellationToken = default)
        {
            var observation = request.Observation;
            var own = observation.InhabitantId == actor && !Founded;
            var selected = observation.Candidates.FirstOrDefault(candidate => own && candidate.Id == "household_found") ??
                observation.Candidates.FirstOrDefault(candidate => own && candidate.Id == "household_leave") ??
                observation.Candidates.FirstOrDefault(candidate => own && candidate.Id.StartsWith("household_ask:", StringComparison.Ordinal)) ??
                observation.Candidates.FirstOrDefault(candidate => observation.InhabitantId != actor && candidate.Id == "household_refuse:" + actor) ??
                observation.Candidates.Single(candidate => candidate.Id == "safe_idle");
            if (quietFirstRefusal && selected.Id == "household_refuse:" + actor &&
                QuietRefusals.TryAdd(observation.InhabitantId, observation))
                selected = observation.Candidates.Single(candidate => candidate.Id == "safe_idle");
            if (selected.Id == "household_found") Founded = true;
            Selected.Enqueue($"tick={observation.WorldTick} actor={observation.InhabitantId} " +
                $"choice={selected.Id} guidance={observation.ObserverGuidance?.Count ?? 0}");
            return ValueTask.FromResult(new CognitionDecisionResponse(request.RequestId, observation.InhabitantId, Kind, ProviderEpoch,
                observation.RunEpoch, observation.DecisionGeneration, observation.ObservationDigest, selected.Id, 1,
                new Dictionary<string, double> { [selected.Id] = 1 },
                ChosenName: observation.NeedsName ? observation.Self!.Name : null,
                ChosenPersonality: observation.NeedsPersonality ? "patient" : null,
                ChosenAspiration: observation.NeedsAspiration ? "build a home" : null));
        }
    }
}
