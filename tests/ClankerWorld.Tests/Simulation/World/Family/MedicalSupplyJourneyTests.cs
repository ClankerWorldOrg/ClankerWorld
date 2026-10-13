using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.World;

namespace ClankerWorld.Tests;

public sealed partial class MedicalSupplyJourneyTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public async Task AHealthyCaregiverFetchesOneDoseAndReturnsToTheConsentingPatientAfterReload(bool doseAlreadyCarried, bool reloadAfterCollection)
    {
        var (state, caregiver, patient, household, houseId) = await CreateStateAsync(doseAlreadyCarried);
        var choices = new CareChooser();
        using var world = Restore(state, caregiver, choices);
        Assert.True((await world.AdvanceOneTickAsync()).Advanced);
        if (!doseAlreadyCarried)
        {
            Assert.Contains("medical_collect:medicine", choices.Offered);
            Assert.NotEqual(state.Agents.Single(person => person.AgentId == caregiver).Position,
                world.Agents.Single(person => person.AgentId == caregiver).Position);
            Assert.Equal((household, houseId, 2),
                (world.Society.Inventory.GetLot("fetch-test-medicine").OwnerId,
                 world.Society.Inventory.GetLot("fetch-test-medicine").StorageBuildingId,
                 world.Society.Inventory.GetLot("fetch-test-medicine").Quantity));
        }
        if (reloadAfterCollection)
        {
            for (var tick = 0; tick < 80 && !HasCarriedMedicine(world, caregiver); tick++)
                Assert.True((await world.AdvanceOneTickAsync()).Advanced);
            Assert.True(HasCarriedMedicine(world, caregiver));
        }
        if (!doseAlreadyCarried)
        {
            var trip = Assert.IsType<MedicalSupplyTrip>(world.Agents.Single(person => person.AgentId == caregiver).MedicalSupplyTrip);
            Assert.Equal(patient, trip.PatientId);
            Assert.Equal(state.Agents.Single(person => person.AgentId == patient).Position, trip.LastSeenPosition);
        }
        var bytes = WorldRuntimeCodec.Encode(world.ExportState());
        using var resumed = Restore(WorldRuntimeCodec.Decode(bytes), caregiver, new CareChooser());
        Assert.Equal(bytes, WorldRuntimeCodec.Encode(resumed.ExportState()));
        for (var tick = 0; tick < 140 && !resumed.ExportState().Events.Any(item => item.Kind == "medical_treatment_started"); tick++)
        {
            Assert.True((await world.AdvanceOneTickAsync()).Advanced);
            Assert.True((await resumed.AdvanceOneTickAsync()).Advanced);
        }
        Assert.Single(resumed.ExportState().Events, item => item.Kind == "medical_treatment_started" &&
            item.Detail.StartsWith(patient + ":medicine:" + caregiver + ":", StringComparison.Ordinal));
        Assert.Null(resumed.Agents.Single(person => person.AgentId == caregiver).MedicalSupplyTrip);
        for (var tick = 0; tick < 20 && !resumed.ExportState().Events.Any(item => item.Kind == "medical_treatment_completed"); tick++)
        {
            Assert.True((await world.AdvanceOneTickAsync()).Advanced);
            Assert.True((await resumed.AdvanceOneTickAsync()).Advanced);
        }
        Assert.Single(resumed.ExportState().Events, item => item.Kind == "medical_treatment_completed" && item.Detail == patient);
        Assert.Equal(WorldRuntimeCodec.Encode(world.ExportState()), WorldRuntimeCodec.Encode(resumed.ExportState()));
        if (doseAlreadyCarried) Assert.DoesNotContain(resumed.Society.Inventory.Lots, lot => lot.Id == "fetch-test-medicine");
        else Assert.Equal((household, houseId, 1),
            (resumed.Society.Inventory.GetLot("fetch-test-medicine").OwnerId,
             resumed.Society.Inventory.GetLot("fetch-test-medicine").StorageBuildingId,
             resumed.Society.Inventory.GetLot("fetch-test-medicine").Quantity));
        Assert.Contains(caregiver, resumed.Agents.Single(person => person.AgentId == patient).MedicalConsent!.CaregiverIds);
        resumed.Validate();
    }

    [Fact]
    public async Task WithdrawingPermissionDuringTheFetchStopsTheTripWithoutTakingOrConsumingMedicine()
    {
        var (state, caregiver, patient, household, houseId) = await CreateStateAsync(false);
        using var fetching = Restore(state, caregiver, new CareChooser());
        Assert.True((await fetching.AdvanceOneTickAsync()).Advanced);
        Assert.NotNull(fetching.Agents.Single(person => person.AgentId == caregiver).MedicalSupplyTrip);
        var chooser = new CareChooser();
        using var world = WorldRuntime.Restore(WorldRuntimeCodec.Decode(WorldRuntimeCodec.Encode(fetching.ExportState())),
            id => id == caregiver ? chooser : id == patient ? new ConsentRevoker(caregiver) : new ActionCoverageRecorder(chooseIdle: true));
        world.SubmitInstruction(new("withdraw-fetch-permission", "owner:test", patient, OwnerInstructionKind.Suggestion,
            "Withdraw permission for this caregiver."));
        for (var tick = 0; tick < 20 && world.Agents.Single(person => person.AgentId == patient).MedicalConsent is not null; tick++)
            Assert.True((await world.AdvanceOneTickAsync()).Advanced);
        Assert.Null(world.Agents.Single(person => person.AgentId == patient).MedicalConsent);
        Assert.Contains(world.ExportState().Events, item => item.Kind == "medical_care_revoked");
        Assert.Null(world.Agents.Single(person => person.AgentId == caregiver).MedicalSupplyTrip);
        for (var tick = 0; tick < 20; tick++) Assert.True((await world.AdvanceOneTickAsync()).Advanced);
        Assert.DoesNotContain(world.ExportState().Events, item => item.Kind == "medical_treatment_started");
        Assert.False(HasCarriedMedicine(world, caregiver));
        Assert.Equal((household, houseId, 2),
            (world.Society.Inventory.GetLot("fetch-test-medicine").OwnerId,
             world.Society.Inventory.GetLot("fetch-test-medicine").StorageBuildingId,
             world.Society.Inventory.GetLot("fetch-test-medicine").Quantity));
        Assert.NotEmpty(WorldRuntimeCodec.Encode(world.ExportState()));
    }

    [Fact]
    public async Task APatientWhoMovesOutOfSightIsNotTrackedBeyondTheirLastSeenPosition()
    {
        var (state, caregiver, patient, household, houseId) = await CreateStateAsync(false);
        using var fetching = Restore(state, caregiver, new CareChooser());
        Assert.True((await fetching.AdvanceOneTickAsync()).Advanced);
        var saved = fetching.ExportState();
        var lastSeen = Assert.IsType<MedicalSupplyTrip>(fetching.Agents.Single(person => person.AgentId == caregiver).MedicalSupplyTrip)
            .LastSeenPosition;
        var remote = saved.Map.Tiles.First(tile => saved.Map.IsPassable(tile.Position) &&
            Math.Abs(tile.Position.X - lastSeen.X) + Math.Abs(tile.Position.Y - lastSeen.Y) > 30 &&
            saved.Agents.All(person => person.Position != tile.Position)).Position;
        saved = saved with
        {
            Agents = saved.Agents.Select(person => person.AgentId == patient
                ? person with { Position = remote } : person).ToArray(),
        };
        var chooser = new CareChooser();
        using var world = Restore(WorldRuntimeCodec.Decode(WorldRuntimeCodec.Encode(saved)), caregiver, chooser);
        for (var tick = 0; tick < 140 && world.Agents.Single(person => person.AgentId == caregiver).MedicalSupplyTrip is not null; tick++)
            Assert.True((await world.AdvanceOneTickAsync()).Advanced);
        var returned = world.Agents.Single(person => person.AgentId == caregiver);
        Assert.Null(returned.MedicalSupplyTrip);
        Assert.InRange(saved.Map.FootDistance(returned.Position, lastSeen), 0, 1);
        Assert.Equal(remote, world.Agents.Single(person => person.AgentId == patient).Position);
        Assert.DoesNotContain("medical_treat:" + patient + ":medicine", chooser.Offered);
        Assert.DoesNotContain(world.ExportState().Events, item => item.Kind == "medical_treatment_started");
        Assert.True(HasCarriedMedicine(world, caregiver));
        Assert.Equal((household, houseId, 1),
            (world.Society.Inventory.GetLot("fetch-test-medicine").OwnerId,
             world.Society.Inventory.GetLot("fetch-test-medicine").StorageBuildingId,
             world.Society.Inventory.GetLot("fetch-test-medicine").Quantity));
        Assert.NotEmpty(WorldRuntimeCodec.Encode(world.ExportState()));
    }

    [Fact]
    public async Task SavedSupplyTripsRequireARealAcceptedPatientAnInMapPositionAndANonFutureStart()
    {
        var (state, caregiver, patient, _, _) = await CreateStateAsync(false);
        using var world = Restore(state, caregiver, new CareChooser());
        Assert.True((await world.AdvanceOneTickAsync()).Advanced);
        var saved = world.ExportState();
        var trip = Assert.IsType<MedicalSupplyTrip>(world.Agents.Single(person => person.AgentId == caregiver).MedicalSupplyTrip);
        foreach (var changed in new[]
        {
            trip with { PatientId = "missing-patient" },
            trip with { PatientId = state.Agents.First(person => person.AgentId != caregiver && person.AgentId != patient).AgentId },
            trip with { LastSeenPosition = new(-1, -1) },
            trip with { StartedTick = world.WorldTick + 1 },
        })
            Assert.Throws<InvalidDataException>(() => WorldRuntimeCodec.Encode(saved with
            {
                Agents = saved.Agents.Select(person => person.AgentId == caregiver
                    ? person with { MedicalSupplyTrip = changed } : person).ToArray(),
            }));
    }

    private static bool HasCarriedMedicine(WorldRuntime world, string caregiver) => world.Society.Inventory.Lots.Any(lot =>
        lot.ItemKind == "medicine" && lot.OwnerId == caregiver && PersonalEquipmentRules.IsCarried(lot, caregiver));

    private static async Task<(WorldRuntimeState State, string Caregiver, string Patient, string Household, string HouseId)> CreateStateAsync(bool doseAlreadyCarried)
    {
        using var generated = NormalPathWorld.CreateGenerated("medical-fetch-audit",
            _ => new ActionCoverageRecorder(chooseIdle: true));
        Assert.True((await generated.AdvanceOneTickAsync()).Advanced);
        var state = generated.ExportState();
        var caregiver = state.Agents[0].AgentId;
        var patient = state.Agents[1].AgentId;
        var household = state.Society.Society.GetAgent(caregiver).HouseholdId!;
        var house = state.WorldSimulation!.Buildings.Single(building => building.InstanceId == "first-town-house-a");
        Assert.Equal(household, house.HouseholdId);
        var inventory = InventoryAuthority.AddLot(state.Society.Society.Inventory, "fetch-test-medicine", "medicine",
            doseAlreadyCarried ? caregiver : household, doseAlreadyCarried ? 1 : 2, storageBuildingId: doseAlreadyCarried ? null : house.InstanceId);
        state = state with
        {
            Society = state.Society with { Society = state.Society.Society with { Inventory = inventory } },
            Agents = state.Agents.Select(person => person with
            {
                Position = person.AgentId == caregiver ? new(116, 46) :
                    person.AgentId == patient ? new(116, 45) : person.Position,
                FullnessBasisPoints = 9_500,
                Project = null,
                LastDecisionContext = null,
                MedicalConsent = person.AgentId == patient ? new([caregiver]) : null,
                Survival = (person.Survival ?? new()) with
                { IllnessBasisPoints = person.AgentId == patient ? 8_000 : 0 },
            }).ToArray(),
        };
        return (state, caregiver, patient, household, house.InstanceId);
    }

    private static WorldRuntime Restore(WorldRuntimeState state, string caregiver, IDecisionProvider choices) =>
        WorldRuntime.Restore(state, id => id == caregiver ? choices : new ActionCoverageRecorder(chooseIdle: true));

    private sealed class ConsentRevoker(string caregiver) : IDecisionProvider
    {
        public DecisionProviderKind Kind => DecisionProviderKind.LargeLanguageModel;
        public long ProviderEpoch => 0;

        public ValueTask<CognitionDecisionResponse> DecideAsync(CognitionDecisionRequest request,
            CancellationToken cancellationToken = default)
        {
            var wanted = "medical_revoke:" + caregiver;
            var selected = request.Observation.Candidates.Any(candidate => candidate.Id == wanted) ? wanted : "safe_idle";
            return ValueTask.FromResult(new CognitionDecisionResponse(request.RequestId, request.Observation.AgentId,
                Kind, request.ProviderEpoch, request.Observation.RunEpoch, request.Observation.DecisionGeneration,
                request.Observation.ObservationDigest, selected, 1, new Dictionary<string, double> { [selected] = 1 }));
        }
    }

    private sealed class CareChooser : IDecisionProvider
    {
        public DecisionProviderKind Kind => DecisionProviderKind.Deterministic;
        public long ProviderEpoch => 0;
        public MedicalSupplyActionPlan? Plan { get; private set; }
        public HashSet<string> Offered { get; } = new(StringComparer.Ordinal);

        public ValueTask<CognitionDecisionResponse> DecideAsync(CognitionDecisionRequest request,
            CancellationToken cancellationToken = default)
        {
            foreach (var candidate in request.Observation.Candidates) Offered.Add(candidate.Id);
            if (request.Observation.Candidates.FirstOrDefault(item => item.ActionPlan is MedicalSupplyActionPlan) is { ActionPlan: MedicalSupplyActionPlan plan }) Plan = plan;
            var selected = request.Observation.Candidates.FirstOrDefault(candidate => candidate.Id.StartsWith("medical_treat:", StringComparison.Ordinal))?.Id ??
                request.Observation.Candidates.FirstOrDefault(candidate => candidate.Id.StartsWith("medical_collect:", StringComparison.Ordinal))?.Id ?? "safe_idle";
            return ValueTask.FromResult(new CognitionDecisionResponse(request.RequestId, request.Observation.AgentId,
                Kind, request.ProviderEpoch, request.Observation.RunEpoch, request.Observation.DecisionGeneration,
                request.Observation.ObservationDigest, selected, 1, new Dictionary<string, double> { [selected] = 1 }));
        }
    }
}
