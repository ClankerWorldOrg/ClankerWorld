using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.World;

namespace ClankerWorld.Tests;

public sealed partial class MedicalSupplyJourneyTests
{
    [Fact]
    public async Task ACollectionPlanRetainsItsPatientStockAndTripAcrossRollbackAndReplay()
    {
        var (state, caregiver, patient, household, houseId) = await CreateStateAsync(false);
        var chooser = new CareChooser();
        using var world = Restore(state, caregiver, chooser);
        var bytes = WorldRuntimeCodec.Encode(world.ExportState());
        Assert.False((await world.AdvanceOneTickAsync(commitPermitted: () => false)).Advanced);
        Assert.Equal(bytes, WorldRuntimeCodec.Encode(world.ExportState()));
        using var replay = Restore(WorldRuntimeCodec.Decode(bytes), caregiver, new CareChooser());
        var step = await world.AdvanceOneTickAsync();
        Assert.True(step.Advanced);
        Assert.True((await replay.AdvanceOneTickAsync()).Advanced);
        Assert.Equal(WorldRuntimeCodec.Encode(world.ExportState()), WorldRuntimeCodec.Encode(replay.ExportState()));
        var plan = Assert.IsType<MedicalSupplyPickupPlan>(chooser.Plan);
        Assert.Same(plan, step.Decisions.Single(decision => decision.AgentId == caregiver).ActionPlan);
        Assert.Equal((patient, "fetch-test-medicine", household, houseId),
            (plan.PatientId, plan.Pickup.StockId, plan.Pickup.OwnerId, world.Society.Inventory.GetLot(plan.Pickup.StockId).StorageBuildingId));
        Assert.Equal(plan.LastSeenPosition, world.Agents.Single(person => person.AgentId == caregiver).MedicalSupplyTrip!.LastSeenPosition);
        Assert.Null(plan.OriginalTrip);
        using var reopened = Restore(WorldRuntimeCodec.Decode(WorldRuntimeCodec.Encode(world.ExportState())), caregiver, new CareChooser());
        reopened.Validate();
    }

    [Fact]
    public async Task CollectionRefusesTheSelectedReservedStockAndChangedPatientWithoutSubstitution()
    {
        var (state, caregiver, patient, household, houseId) = await CreateStateAsync(false);
        var chooser = new CareChooser();
        using var capture = Restore(state, caregiver, chooser);
        Assert.True((await capture.AdvanceOneTickAsync()).Advanced);
        var plan = Assert.IsType<MedicalSupplyPickupPlan>(chooser.Plan);
        var inventory = InventoryAuthority.AddLot(state.Society.Society.Inventory, "a-new-care-dose", "medicine", household, 2,
            storageBuildingId: houseId);
        inventory = InventoryAuthority.Reserve(inventory, "held-care-stock", household, plan.Pickup.StockId, 2, "test", state.Society.Society.WorldTick + 10);
        using var held = Restore(HouseRelocationTestWorld.WithInventory(state, inventory), caregiver, new CareChooser());
        var bytes = WorldRuntimeCodec.Encode(held.ExportState());
        Assert.Equal(ActionRefusalReason.StockUnavailable, held.CheckActionPlan(plan).Refusal!.Reason);
        Assert.Equal(bytes, WorldRuntimeCodec.Encode(held.ExportState()));
        using var withdrawn = Restore(state with
        { Agents = state.Agents.Select(person => person.AgentId == patient ? person with { MedicalConsent = null } : person).ToArray() },
            caregiver, new CareChooser());
        Assert.Equal(ActionRefusalReason.TargetUnavailable, withdrawn.CheckActionPlan(plan).Refusal!.Reason);
        Assert.Null(withdrawn.Agents.Single(person => person.AgentId == caregiver).MedicalSupplyTrip);
    }

    [Fact]
    public async Task AReturnPlanKeepsTheLastSeenPositionAndOriginalCarriedDose()
    {
        var (state, caregiver, patient, _, _) = await CreateStateAsync(true);
        var lastSeen = state.Agents.Single(person => person.AgentId == patient).Position;
        state = state with
        {
            Agents = state.Agents.Select(person => person.AgentId == caregiver
                ? person with { Position = new(116, 48), MedicalSupplyTrip = new(patient, lastSeen, state.Society.Society.WorldTick) } : person).ToArray(),
        };
        var chooser = new CareChooser();
        using var capture = Restore(state, caregiver, chooser);
        Assert.True((await capture.AdvanceOneTickAsync()).Advanced);
        var plan = Assert.IsType<MedicalSupplyReturnPlan>(chooser.Plan);
        Assert.Equal((patient, lastSeen, "fetch-test-medicine"), (plan.PatientId, plan.LastSeenPosition, plan.StockId));
        var inventory = InventoryAuthority.Reserve(state.Society.Society.Inventory, "held-return-dose", caregiver, plan.StockId, 1,
            "test", state.Society.Society.WorldTick + 10);
        using var held = Restore(HouseRelocationTestWorld.WithInventory(state, inventory), caregiver, new CareChooser());
        Assert.Equal(ActionRefusalReason.StockUnavailable, held.CheckActionPlan(plan).Refusal!.Reason);
        using var changed = Restore(state with
        {
            Agents = state.Agents.Select(person => person.AgentId == caregiver
                ? person with { MedicalSupplyTrip = person.MedicalSupplyTrip! with { LastSeenPosition = new(116, 44) } } : person).ToArray(),
        }, caregiver, new CareChooser());
        Assert.Equal(ActionRefusalReason.TargetUnavailable, changed.CheckActionPlan(plan).Refusal!.Reason);
        Assert.DoesNotContain(changed.ExportState().Events, item => item.Kind == "medical_treatment_started");
    }
}
