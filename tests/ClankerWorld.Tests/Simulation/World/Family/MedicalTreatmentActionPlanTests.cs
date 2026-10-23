using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.World;
using static ClankerWorld.Tests.FarmFieldTests;

namespace ClankerWorld.Tests;

public sealed partial class MedicalTreatmentConsentTests
{
    [Fact]
    public async Task TheOfferedTreatmentUsesTheSameDosePlanAcrossRollbackReplayAndReload()
    {
        var state = PreparedState();
        var patient = state.Agents[0].AgentId;
        state = WithMedicine(state, patient, 1);
        var chooser = new TreatmentPlanChooser(patient, choose: true);
        using var world = Restore(state, patient, chooser);
        var bytes = WorldRuntimeCodec.Encode(world.ExportState());
        Assert.False((await world.AdvanceOneTickAsync(commitPermitted: () => false)).Advanced);
        Assert.Equal(bytes, WorldRuntimeCodec.Encode(world.ExportState()));
        using var replay = Restore(WorldRuntimeCodec.Decode(bytes), patient, new TreatmentPlanChooser(patient, choose: true));
        var samePlan = false;
        for (var tick = 0; tick < 4; tick++)
        {
            var step = await world.AdvanceOneTickAsync();
            Assert.True(step.Advanced);
            samePlan |= step.Decisions.Any(decision => decision.AgentId == patient && chooser.Plan is not null &&
                ReferenceEquals(decision.ActionPlan, chooser.Plan));
            Assert.True((await replay.AdvanceOneTickAsync()).Advanced);
            Assert.Equal(WorldRuntimeCodec.Encode(world.ExportState()), WorldRuntimeCodec.Encode(replay.ExportState()));
            if (Physical(world, patient).MedicalTreatment is not null) break;
        }
        var plan = Assert.IsType<MedicalTreatmentActionPlan>(chooser.Plan);
        Assert.True(samePlan);
        Assert.Equal((patient, "medical-test-dose", patient), (plan.Choice.PatientId, plan.StockId, plan.StockOwner));
        var treatment = Assert.IsType<MedicalTreatmentState>(Physical(world, patient).MedicalTreatment);
        Assert.Equal(plan.StockId, treatment.SupplyLotId);
        Assert.DoesNotContain(world.Society.Inventory.Lots, lot => lot.Id == plan.StockId);
        using var reopened = Restore(WorldRuntimeCodec.Decode(WorldRuntimeCodec.Encode(world.ExportState())));
        reopened.Validate();
    }

    [Fact]
    public async Task TreatmentRechecksTheOriginalDoseAndPatientWithoutSubstitutingNewStock()
    {
        var state = PreparedState();
        var patient = state.Agents[0].AgentId;
        var chooser = new TreatmentPlanChooser(patient, choose: false);
        using var world = Restore(WithMedicine(state, patient, 1), patient, chooser);
        for (var tick = 0; tick < 4 && chooser.Plan is null; tick++) Assert.True((await world.AdvanceOneTickAsync()).Advanced);
        var plan = Assert.IsType<MedicalTreatmentActionPlan>(chooser.Plan);
        var checkpoint = world.ExportState();
        var inventory = InventoryAuthority.AddLot(checkpoint.Society.Society.Inventory, "a-new-dose", "medicine", patient, 1);
        using var additional = Restore(WithInventory(checkpoint, inventory));
        Assert.True(additional.CheckActionPlan(plan).Allowed);
        Assert.Equal("medical-test-dose", plan.StockId);
        inventory = InventoryAuthority.Reserve(inventory, "held-original-dose", patient, plan.StockId, 1, "test", world.WorldTick + 10);
        using var held = Restore(WithInventory(checkpoint, inventory));
        var bytes = WorldRuntimeCodec.Encode(held.ExportState());
        Assert.Equal(ActionRefusalReason.StockUnavailable, held.CheckActionPlan(plan).Refusal!.Reason);
        Assert.Equal(bytes, WorldRuntimeCodec.Encode(held.ExportState()));
        using var recovered = Restore(checkpoint with
        {
            Agents = checkpoint.Agents.Select(person => person.AgentId == patient
                ? person with { Survival = person.Survival! with { IllnessBasisPoints = 0 } } : person).ToArray(),
        });
        Assert.Equal(ActionRefusalReason.TargetUnavailable, recovered.CheckActionPlan(plan).Refusal!.Reason);
        Assert.Null(Physical(recovered, patient).MedicalTreatment);
    }

    [Fact]
    public async Task ThePublicTreatmentKeepsItsLowerIllnessThresholdAndBandageRefusal()
    {
        var state = PreparedState();
        var patient = state.Agents[0].AgentId;
        state = WithMedicine(state, patient, 1) with
        {
            Agents = state.Agents.Select(person => person.AgentId == patient
                ? person with { Survival = person.Survival! with { IllnessBasisPoints = 100 } } : person).ToArray(),
        };
        var chooser = new TreatmentPlanChooser(patient, choose: false);
        using var world = Restore(state, patient, chooser);
        Assert.True((await world.AdvanceOneTickAsync()).Advanced);
        Assert.Null(chooser.Plan);
        Assert.False(world.TreatPatient(patient, patient, "bandage").Applied);
        Assert.True(world.TreatPatient(patient, patient, "medicine").Applied);
        Assert.Equal("medical-test-dose", Physical(world, patient).MedicalTreatment!.SupplyLotId);
        world.Validate();
    }

    private sealed class TreatmentPlanChooser(string patient, bool choose) : IDecisionProvider
    {
        public MedicalTreatmentActionPlan? Plan { get; private set; }
        public DecisionProviderKind Kind => DecisionProviderKind.Deterministic;
        public long ProviderEpoch => 0;
        public ValueTask<CognitionDecisionResponse> DecideAsync(CognitionDecisionRequest request, CancellationToken cancellationToken = default)
        {
            var candidate = request.Observation.AgentId == patient ? request.Observation.Candidates.FirstOrDefault(item =>
                item.ActionPlan is MedicalTreatmentActionPlan plan && plan.Choice.PatientId == patient) : null;
            if (candidate is not null) Plan = Assert.IsType<MedicalTreatmentActionPlan>(candidate.ActionPlan);
            var selected = choose ? candidate?.Id ?? "safe_idle" : "safe_idle";
            return ValueTask.FromResult(new CognitionDecisionResponse(request.RequestId, request.Observation.AgentId, Kind,
                request.ProviderEpoch, request.Observation.RunEpoch, request.Observation.DecisionGeneration,
                request.Observation.ObservationDigest, selected, 1, new Dictionary<string, double> { [selected] = 1 }));
        }
    }
}
