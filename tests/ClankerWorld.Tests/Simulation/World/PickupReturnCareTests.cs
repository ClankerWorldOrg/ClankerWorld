using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.World;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Tests;

public sealed partial class PickupReturnJourneyTests
{
    [Theory]
    [InlineData(4, true)]
    [InlineData(3, true)]
    [InlineData(4, false)]
    public async Task MedicinePickupReturnsToThePatientsRememberedPosition(int stones, bool swim)
    {
        var (state, actor, home) = await Generated();
        var patient = state.Agents.First(item => item.AgentId != actor).AgentId;
        var destination = new GridPoint(107, 64);
        var source = swim ? new GridPoint(103, 47) : new GridPoint(100, 57);
        Routes(state, source, destination, swim);
        var inventory = InventoryAuthority.AddLot(Empty(state), "pickup-stones", "stone", actor, 3);
        inventory = InventoryAuthority.AddLot(inventory, "pickup-medicine", "medicine", home, 1, groundPosition: new(source.X, source.Y));
        state = Prepare(state, inventory, actor, new GridPoint(106, 63));
        state = state with
        {
            Agents = state.Agents.Select(item => item.AgentId == patient ? item with
            { Position = destination, MedicalConsent = new([actor]), Survival = new(IllnessBasisPoints: 8_000) } : item).ToArray()
        };
        using var setup = Restore(state, actor, new Choices("medical_collect:medicine"));
        Assert.True((await setup.AdvanceOneTickAsync()).Advanced);
        var trip = Assert.IsType<MedicalSupplyTrip>(setup.Agents.Single(item => item.AgentId == actor).MedicalSupplyTrip);
        Assert.Equal((patient, destination), (trip.PatientId, trip.LastSeenPosition));
        state = setup.ExportState();
        inventory = state.Society.Society.Inventory with
        { Lots = state.Society.Society.Inventory.Lots.Select(item => item.Id == "pickup-stones" ? item with { Quantity = stones } : item).ToArray() };
        state = state with
        {
            Society = state.Society with { Society = state.Society.Society with { Inventory = inventory } },
            Agents = state.Agents.Select(item => item.AgentId == actor ? item with
            { Position = source, TravelCooldownTicks = 0, LastDecisionContext = null } : item).ToArray(),
        };
        var chooser = new Choices("medical_collect:medicine", "medical_treat:");
        using var world = Restore(state, actor, chooser);
        var blocked = swim && stones == 4;
        await AdvanceUntil(world, () => world.ExportState().Events.Any(item => item.Kind == "medical_treatment_started"), blocked ? 4 : 140);
        if (blocked)
        {
            Assert.DoesNotContain(chooser.Offered, id => id == "medical_collect:medicine");
            Assert.Equal((home, 1, (string?)null), (world.Society.Inventory.GetLot("pickup-medicine").OwnerId,
                world.Society.Inventory.GetLot("pickup-medicine").Quantity, world.Society.Inventory.GetLot("pickup-medicine").CarrierId));
            Assert.Equal(destination, world.Agents.Single(item => item.AgentId == actor).MedicalSupplyTrip!.LastSeenPosition);
        }
        else
        {
            Assert.Single(world.ExportState().Events, item => item.Kind == "medical_treatment_started");
            Assert.DoesNotContain(world.Society.Inventory.Lots, item => item.Id == "pickup-medicine");
        }
        Assert.Contains(actor, world.Agents.Single(item => item.AgentId == patient).MedicalConsent!.CaregiverIds);
        Assert.Equal(stones, world.Society.Inventory.GetLot("pickup-stones").Quantity);
        Checkpoint(world, actor);
    }

    [Theory]
    [InlineData(4, true)]
    [InlineData(3, true)]
    [InlineData(4, false)]
    public async Task OtherCaregiverCanBringOneServingBackToTheInfant(int stones, bool swim)
    {
        var (state, child, houseId, _) = await ChildHouseholdHelpingTests.Prepared("wood");
        var checkpoint = state.Society.Society;
        var birth = checkpoint.Births.Single(item => item.ChildId == child);
        var primary = birth.PrimaryCaregiverId;
        var actor = checkpoint.Relationships.Single(edge => edge.Type == SocietyRelationshipType.Caregiver &&
            edge.State == SocietyRelationshipState.Accepted && edge.TargetId == child && edge.ProposerId != primary).ProposerId;
        var house = state.WorldSimulation!.Buildings.Single(item => item.InstanceId == houseId);
        var source = swim ? new GridPoint(72, 32) : new GridPoint(110, 59);
        Routes(state, source, house.Position, swim);
        var infant = checkpoint.GetAgent(child) with
        { BirthTick = birth.CommittedTick, BirthLifeTick = checkpoint.LifeClock is null ? null : checkpoint.LifeTickAt(birth.CommittedTick), AgeBand = SocietyAgeBand.Infant, LastLifecycleYearChecked = 0 };
        checkpoint = checkpoint with { Agents = checkpoint.Agents.Select(item => item.Id == child ? infant : item).ToArray() };
        state = state with { Society = state.Society with { Society = checkpoint } };
        var inventory = InventoryAuthority.AddLot(Empty(state), "pickup-stones", "stone", actor, stones);
        inventory = InventoryAuthority.AddLot(inventory, "pickup-bread", "bread", house.HouseholdId!, 2, groundPosition: new(source.X, source.Y));
        state = Prepare(state, inventory, actor, source);
        state = state with
        {
            Agents = state.Agents.Select(item => item.AgentId == child ? item with
            { Position = house.Position, HungerBasisPoints = 5_000 } : item.AgentId == primary ? item with { Position = house.Position } : item).ToArray()
        };
        using var world = Restore(state, actor, new Choices("care:" + child));
        var blocked = swim && stones == 4;
        await AdvanceUntil(world, () => world.ExportState().Events.Any(item => item.Kind == "child_cared_for" && item.Detail == child), blocked ? 4 : 140);
        if (blocked)
        {
            Assert.Equal((house.HouseholdId, 2), (world.Society.Inventory.GetLot("pickup-bread").OwnerId, world.Society.Inventory.GetLot("pickup-bread").Quantity));
            Assert.DoesNotContain(world.Society.Inventory.Lots, item => item.OwnerId == actor && item.ItemKind == "bread");
        }
        else
        {
            Assert.Single(world.ExportState().Events, item => item.Kind == "child_cared_for" && item.Detail == child);
            Assert.Equal(1, world.Society.Inventory.Lots.Where(item => item.ItemKind == "bread").Sum(item => item.Quantity));
            Assert.True(world.Agents.Single(item => item.AgentId == child).HungerBasisPoints > 7_000);
        }
        Assert.Equal(house.Position, world.Agents.Single(item => item.AgentId == child).Position);
        Assert.Equal(primary, world.Society.GetAgent(child).PrimaryCaregiverId);
        Assert.Equal(stones, world.Society.Inventory.GetLot("pickup-stones").Quantity);
        Checkpoint(world, actor);
    }
}
