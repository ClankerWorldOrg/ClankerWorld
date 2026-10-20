using System.Text.Json;
using ClankerWorld.Contracts;
using ClankerWorld.Server.Observation;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.World;
using ClankerWorld.Tests;

// No model service, credentials or owner saves. Reuse the generated first Town,
// hard founders and accepted-birth fixtures, then advance normal nonblocking ticks.
using var founders = NormalPathWorld.CreateGenerated("agent-profile-2333", _ => new ProfileCheckProvider());
var family = SecondGenerationFixture.Add(founders.ExportState(), key: "profile");
var scheduler = new ManualModelWorkScheduler();
using var world = WorldRuntime.Restore(family.State, _ => new ProfileCheckProvider(), modelWorkScheduler: scheduler);
var days = 3;
var targetTick = world.WorldTick + days * world.ExportState().WorldSystems!.Config.TicksPerDay;
while (world.WorldTick < targetTick)
{
    var step = await world.AdvanceOneTickNonBlockingAsync();
    if (!step.Advanced) throw new InvalidOperationException("Profile check world stopped: " + step.Outcome);
    scheduler.RunPendingWork();
    await Task.WhenAll(world.PendingModelWork.Select(work => work.Completion));
}
world.Validate();
// Exercise the same strict reload and owner projection as the normal server.
using var restored = WorldRuntime.Restore(WorldRuntimeCodec.Decode(WorldRuntimeCodec.Encode(world.ExportState())));
var store = new OwnerWorldObservationStore(restored);
var reconnect = new OwnerWorldReconnect(store.GetOwnerHandshake(), store.GetReconnectBaseline(0));
var agents = reconnect.Baseline.Snapshot.Agents;
Console.WriteLine($"PROFILE_CHECK_DATA agents={agents.Count} child={agents.Any(agent => agent.Id == family.ChildId)} thoughts={agents.Sum(agent => agent.RecentPrivateThoughts.Count)}");
File.WriteAllText(args[0], JsonSerializer.Serialize(reconnect));
if (agents.Count < 5 || agents.All(agent => agent.Id != family.ChildId) ||
    agents.All(agent => agent.RecentPrivateThoughts.Count == 0))
    throw new InvalidOperationException("The profile check needs the born child and admitted private thoughts.");
Console.WriteLine($"PROFILE_CHECK_WORLD days={days} tick={world.WorldTick} agents={agents.Count}");

// Local, immediate replies pass through native cognition admission. Names come
// from the hard fixture; the model text is bounded, synthetic test data.
sealed class ProfileCheckProvider : IDecisionProvider
{
    private readonly DeterministicDecisionProvider chooser = new();
    public DecisionProviderKind Kind => DecisionProviderKind.LargeLanguageModel;
    public long ProviderEpoch => 1;

    public async ValueTask<CognitionDecisionResponse> DecideAsync(CognitionDecisionRequest request,
        CancellationToken cancellationToken = default) =>
        (await chooser.DecideAsync(request, cancellationToken)) with
        {
            Provider = Kind,
            ProviderEpoch = ProviderEpoch,
            ChosenPersonality = "Patient and curious; I enjoy helping our neighbours and learning useful skills.",
            ChosenAspiration = "Build a welcoming Town with enough food and warm homes before winter.",
            PrivateThought = "I want to help our neighbours 🌿 and keep this place safe. There is still time to gather supplies and learn something new before winter.",
        };
}
