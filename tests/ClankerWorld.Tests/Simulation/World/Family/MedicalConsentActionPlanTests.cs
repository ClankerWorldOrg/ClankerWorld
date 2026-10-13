using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.World;

namespace ClankerWorld.Tests;

public sealed partial class MedicalPermissionChoiceTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AConsentPlanKeepsItsCaregiverAndDoseAcrossRollbackReplayAndReload(bool revoke)
    {
        var (state, patient, _, caregiver) = await PreparedState(14);
        if (revoke)
        {
            using var allowed = WorldRuntime.Restore(state, _ => new ConsentPlanChooser(patient, caregiver, true, true));
            for (var tick = 0; tick < 4 && allowed.Agents.Single(person => person.AgentId == patient).MedicalConsent?.CaregiverIds.Contains(caregiver) != true; tick++)
                Assert.True((await allowed.AdvanceOneTickAsync()).Advanced);
            Assert.True(allowed.TreatPatient(caregiver, patient, "medicine").Applied);
            state = allowed.ExportState() with
            { Agents = allowed.Agents.Select(person => person with { LastDecisionContext = null }).ToArray() };
        }
        var chooser = new ConsentPlanChooser(patient, caregiver, !revoke, true);
        using var world = WorldRuntime.Restore(state, _ => chooser);
        var bytes = WorldRuntimeCodec.Encode(world.ExportState());
        Assert.False((await world.AdvanceOneTickAsync(commitPermitted: () => false)).Advanced);
        Assert.Equal(bytes, WorldRuntimeCodec.Encode(world.ExportState()));
        using var replay = WorldRuntime.Restore(WorldRuntimeCodec.Decode(bytes), _ => new ConsentPlanChooser(patient, caregiver, !revoke, true));
        var samePlan = false;
        for (var tick = 0; tick < 4; tick++)
        {
            var step = await world.AdvanceOneTickAsync();
            Assert.True(step.Advanced);
            samePlan |= step.Decisions.Any(decision => decision.AgentId == patient && chooser.Plan is not null && ReferenceEquals(decision.ActionPlan, chooser.Plan));
            Assert.True((await replay.AdvanceOneTickAsync()).Advanced);
            Assert.Equal(WorldRuntimeCodec.Encode(world.ExportState()), WorldRuntimeCodec.Encode(replay.ExportState()));
            if (world.Agents.Single(person => person.AgentId == patient).MedicalConsent?.CaregiverIds.Contains(caregiver) == !revoke) break;
        }
        Assert.True(chooser.Plan is not null, string.Join(", ", chooser.Offered));
        var plan = Assert.IsType<MedicalConsentActionPlan>(chooser.Plan);
        Assert.True(samePlan);
        Assert.Equal(caregiver, plan.Choice.CaregiverId);
        var person = world.Agents.Single(item => item.AgentId == patient);
        Assert.Equal(plan.ResultingCaregivers, person.MedicalConsent!.CaregiverIds);
        Assert.Equal(!revoke, person.MedicalConsent.CaregiverIds.Contains(caregiver));
        if (revoke)
        {
            Assert.NotNull(plan.InterruptedDoseId);
            Assert.Null(person.MedicalTreatment);
            var receipt = world.Society.Inventory.GetReservation(plan.InterruptedDoseId);
            Assert.EndsWith(":closed", receipt.Purpose, StringComparison.Ordinal);
            Assert.DoesNotContain(world.Society.Inventory.Lots, lot => lot.Id == Dose);
        }
        using var reopened = WorldRuntime.Restore(WorldRuntimeCodec.Decode(WorldRuntimeCodec.Encode(world.ExportState())));
        reopened.Validate();
    }

    [Fact]
    public async Task AConsentPlanRefusesChangedPermissionOrAnAbsentCaregiverWithoutChangingState()
    {
        var (state, patient, first, caregiver) = await PreparedState(14);
        var chooser = new ConsentPlanChooser(patient, caregiver, true, false);
        using var world = WorldRuntime.Restore(state, _ => chooser);
        for (var tick = 0; tick < 4 && chooser.Plan is null; tick++) Assert.True((await world.AdvanceOneTickAsync()).Advanced);
        Assert.True(chooser.Plan is not null, string.Join(", ", chooser.Offered));
        var plan = Assert.IsType<MedicalConsentActionPlan>(chooser.Plan);
        var checkpoint = world.ExportState();
        var existing = checkpoint.Agents.Single(person => person.AgentId == patient).MedicalConsent!.CaregiverIds;
        using var changed = WorldRuntime.Restore(checkpoint with
        {
            Agents = checkpoint.Agents.Select(person => person.AgentId == patient
                ? person with { MedicalConsent = new(existing.Append(first).Order(StringComparer.Ordinal).ToArray()) } : person).ToArray(),
        });
        var bytes = WorldRuntimeCodec.Encode(changed.ExportState());
        Assert.Equal(ActionRefusalReason.TargetUnavailable, changed.CheckActionPlan(plan).Refusal!.Reason);
        Assert.Equal(bytes, WorldRuntimeCodec.Encode(changed.ExportState()));
        var away = checkpoint.Agents.First(person => person.AgentId != patient && person.AgentId != first && person.AgentId != caregiver).Position;
        using var moved = WorldRuntime.Restore(checkpoint with
        { Agents = checkpoint.Agents.Select(person => person.AgentId == caregiver ? person with { Position = away } : person).ToArray() });
        Assert.Equal(ActionRefusalReason.TargetUnavailable, moved.CheckActionPlan(plan).Refusal!.Reason);
        Assert.Equal(existing, moved.Agents.Single(person => person.AgentId == patient).MedicalConsent!.CaregiverIds);
    }

    private sealed class ConsentPlanChooser(string patient, string caregiver, bool allow, bool choose) : IDecisionProvider
    {
        public MedicalConsentActionPlan? Plan { get; private set; }
        public List<string> Offered { get; } = [];
        public DecisionProviderKind Kind => DecisionProviderKind.LargeLanguageModel;
        public long ProviderEpoch => 1;
        public ValueTask<CognitionDecisionResponse> DecideAsync(CognitionDecisionRequest request, CancellationToken cancellationToken = default)
        {
            if (request.Observation.AgentId == patient) Offered.AddRange(request.Observation.Candidates.Where(item =>
                item.Id.StartsWith("medical_", StringComparison.Ordinal)).Select(item => item.Id + ":" + item.ActionPlan?.GetType().Name));
            var candidate = request.Observation.AgentId == patient ? request.Observation.Candidates.FirstOrDefault(item =>
                item.ActionPlan is MedicalConsentActionPlan plan && plan.Choice.CaregiverId == caregiver && plan.Choice.Allow == allow) : null;
            if (candidate is not null) Plan = Assert.IsType<MedicalConsentActionPlan>(candidate.ActionPlan);
            var selected = choose ? candidate?.Id ?? "safe_idle" : "safe_idle";
            return ValueTask.FromResult(new CognitionDecisionResponse(request.RequestId, request.Observation.AgentId, Kind,
                ProviderEpoch, request.Observation.RunEpoch, request.Observation.DecisionGeneration, request.Observation.ObservationDigest,
                selected, 1, new Dictionary<string, double> { [selected] = 1 }));
        }
    }
}
