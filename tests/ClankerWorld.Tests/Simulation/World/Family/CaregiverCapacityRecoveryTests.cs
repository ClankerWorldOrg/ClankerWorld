using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.World;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Tests;

public sealed class CaregiverCapacityRecoveryTests
{
    private const string Cargo = "care-capacity-stone";

    [Theory]
    [InlineData(false, 8, 9_000, false)]
    [InlineData(true, 8, 9_000, false)]
    [InlineData(false, 7, 9_000, false)]
    [InlineData(false, 8, 6_000, false)]
    [InlineData(false, 8, 9_000, true)]
    public async Task HungryInfantGetsAnActualServingAfterItsCaregiverRecoversExactlyOneSpaceAcrossReload(
        bool inPot, int carried, int parentFullness, bool unfiltered)
    {
        var (state, actor, child, sourceId) = await Family(inPot, carried, parentFullness);
        var initial = state.Society.Society.Inventory;
        Assert.Equal(carried, PersonalEquipmentRules.CarriedQuantity(initial, actor, null));
        var sourceQuantity = initial.GetLot(sourceId).Quantity;
        Assert.True(sourceQuantity >= 4);
        Assert.Equal(SocietyAgeBand.Infant, state.Society.Society.GetAgent(child).AgeBand);
        Assert.Contains(state.Society.Society.Relationships, relationship =>
            relationship.Type == SocietyRelationshipType.Caregiver && relationship.ProposerId == actor &&
            relationship.TargetId == child && relationship.State == SocietyRelationshipState.Accepted);
        string[] permitted = unfiltered ? [string.Empty] : ["care:", "make_room_for_food", "consume_food", "collect_shared_food"];
        var choices = new FoodCapacityTestFixture.Choices(permitted);
        using var world = FoodCapacityTestFixture.Restore(state, actor, choices);
        using var waiting = FoodCapacityTestFixture.Restore(state, actor, new FoodCapacityTestFixture.Choices());
        var sawStored = false;
        var sawPickup = false;
        var started = world.WorldTick;
        for (var tick = 0; world.WorldTick - started < 40 && !world.ExportState().Events.Any(item => item.Kind == "child_cared_for" && item.Detail == child); tick++)
        {
            Assert.True((await world.AdvanceOneTickAsync()).Advanced);
            Assert.True((await waiting.AdvanceOneTickAsync()).Advanced);
            Assert.InRange(PersonalEquipmentRules.CarriedQuantity(world.Society.Inventory, actor, null), 0, 8);
            if (!sawStored && world.ExportState().Events.Any(item => item.Kind == "spare_cargo_stored"))
            {
                sawStored = true;
                if (world.WorldTick - started < 40)
                {
                    await FoodCapacityTestFixture.AssertReplay(world, actor, permitted);
                    Assert.True((await waiting.AdvanceOneTickAsync()).Advanced);
                }
            }
            if (!sawPickup && world.Society.Inventory.Lots.Any(lot => lot.OwnerId == actor &&
                    InventoryContainerRules.IsFood(lot.ItemKind)))
            {
                sawPickup = true;
                if (world.WorldTick - started < 40)
                {
                    await FoodCapacityTestFixture.AssertReplay(world, actor, permitted);
                    Assert.True((await waiting.AdvanceOneTickAsync()).Advanced);
                }
            }
        }

        Assert.Contains(world.ExportState().Events, item => item.Kind == "child_cared_for" && item.Detail == child);
        Assert.InRange(world.WorldTick - started, 1, 40);
        Assert.Equal(world.WorldTick, waiting.WorldTick);
        Assert.Equal(3_000, world.Agents.Single(person => person.AgentId == child).FullnessBasisPoints -
            waiting.Agents.Single(person => person.AgentId == child).FullnessBasisPoints);
        Assert.Equal(sourceQuantity - 1, world.Society.Inventory.GetLot(sourceId).Quantity);
        Assert.Equal(sourceQuantity, waiting.Society.Inventory.GetLot(sourceId).Quantity);
        Assert.True(sawPickup, "Care must first acquire the real household serving.");
        Assert.Equal(carried == 8, sawStored);
        if (carried == 8)
        {
            Assert.Single(world.ExportState().Events, item => item.Kind == "spare_cargo_stored" &&
                item.Detail == $"{actor}:stone:1:{FoodCapacityTestFixture.House}");
            Assert.Equal(7, world.Society.Inventory.GetLot(Cargo).Quantity);
            Assert.Single(world.Society.Inventory.Lots, lot => lot.ProvenanceLotId == Cargo &&
                lot.OwnerId == FoodCapacityTestFixture.Household && lot.StorageBuildingId == FoodCapacityTestFixture.House && lot.Quantity == 1);
        }
        Assert.Equal(carried, world.Society.Inventory.Lots.Where(lot => lot.Id == Cargo || lot.ProvenanceLotId == Cargo).Sum(lot => lot.Quantity));
        Assert.All(choices.Offers, offered => Assert.InRange(offered.Count(id => id == "make_room_for_food"), 0, 1));
        Assert.Contains(world.Society.Inventory.Reservations, reservation => reservation.OwnerId == actor &&
            reservation.Quantity == 1 && reservation.State == InventoryReservationState.Completed &&
            reservation.Purpose == "direct_consumption");
        if (inPot)
        {
            Assert.Equal("care-capacity-pot", world.Society.Inventory.GetLot(sourceId).ContainerLotId);
            Assert.Equal(FoodCapacityTestFixture.House, world.Society.Inventory.GetLot("care-capacity-pot").StorageBuildingId);
            Assert.Contains(world.Society.Inventory.Events, item => item.Kind == "container_contents_taken");
        }
        await FoodCapacityTestFixture.AssertReplay(world, actor, permitted);
    }

    [Fact]
    public async Task CaregiverUsesTheChildsAuthorizedHouseholdServingEvenWhenItsOwnCollectionPolicyRefusesIt()
    {
        var (state, actor, child, sourceId) = await Family(false, 8, 9_000);
        var inventory = state.Society.Society.Inventory;
        var sourceQuantity = inventory.GetLot(sourceId).Quantity;
        foreach (var food in inventory.Lots.Where(lot => lot.OwnerId != FoodCapacityTestFixture.Household &&
                     InventoryContainerRules.IsFood(lot.ItemKind)).ToArray())
            inventory = InventoryAuthority.Reserve(inventory, "policy-reserve:" + food.Id, food.OwnerId, food.Id,
                food.Quantity, "policy_control", inventory.WorldTick + 100);
        state = FoodCapacityTestFixture.WithInventory(state, inventory) with
        {
            FoodStewardship = new(null, "essential_first", state.Society.Society.WorldTick),
        };
        var choices = new FoodCapacityTestFixture.Choices("care:", "make_room_for_food");
        using var world = FoodCapacityTestFixture.Restore(state, actor, choices);
        for (var tick = 0; tick < 40 && !world.ExportState().Events.Any(item => item.Kind == "child_cared_for" && item.Detail == child); tick++)
            Assert.True((await world.AdvanceOneTickAsync()).Advanced);
        Assert.Contains(world.ExportState().Events, item => item.Kind == "child_cared_for" && item.Detail == child);
        Assert.All(choices.Offers, offer => Assert.DoesNotContain("collect_shared_food", offer));
        Assert.Equal(sourceQuantity - 1, world.Society.Inventory.GetLot(sourceId).Quantity);
        Assert.Equal(7, world.Society.Inventory.GetLot(Cargo).Quantity);
        await FoodCapacityTestFixture.AssertReplay(world, actor, "care:", "make_room_for_food");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ReservedCargoCannotBeFreedAndReservedPotContentsCannotFeedTheDependent(bool reserveContents)
    {
        var (state, actor, child, sourceId) = await Family(reserveContents, 8, 9_000);
        var inventory = state.Society.Society.Inventory;
        var sourceQuantity = inventory.GetLot(sourceId).Quantity;
        inventory = reserveContents
            ? InventoryAuthority.Reserve(inventory, "care-protected", FoodCapacityTestFixture.Household, sourceId, 1,
                "care_capacity_control", inventory.WorldTick + 100)
            : InventoryAuthority.Reserve(inventory, "care-protected", actor, Cargo, 8,
                "care_capacity_control", inventory.WorldTick + 100);
        state = FoodCapacityTestFixture.WithInventory(state, inventory);
        var choices = new FoodCapacityTestFixture.Choices("care:", "make_room_for_food");
        using var world = FoodCapacityTestFixture.Restore(state, actor, choices);
        for (var tick = 0; tick < 8; tick++) Assert.True((await world.AdvanceOneTickAsync()).Advanced);
        if (!reserveContents)
        {
            Assert.DoesNotContain(world.ExportState().Events, item => item.Kind == "child_cared_for" && item.Detail == child);
            Assert.DoesNotContain(world.ExportState().Events, item => item.Kind == "spare_cargo_stored");
            Assert.Equal(8, world.Society.Inventory.GetLot(Cargo).Quantity);
        }
        else
        {
            // The claim protects this actual pot family; other reachable wild
            // food and unreserved cargo remain legitimate caregiving options.
            Assert.DoesNotContain(world.Society.Inventory.Events, item => item.Kind == "container_contents_taken" &&
                item.Detail.Contains("care-capacity-pot:", StringComparison.Ordinal));
            Assert.Equal((FoodCapacityTestFixture.Household, FoodCapacityTestFixture.House, 1),
                (world.Society.Inventory.GetLot("care-capacity-pot").OwnerId,
                    world.Society.Inventory.GetLot("care-capacity-pot").StorageBuildingId,
                    world.Society.Inventory.GetLot("care-capacity-pot").Quantity));
            Assert.Equal("care-capacity-pot", world.Society.Inventory.GetLot(sourceId).ContainerLotId);
            Assert.Equal(8, world.Society.Inventory.Lots.Where(lot => lot.Id == Cargo || lot.ProvenanceLotId == Cargo).Sum(lot => lot.Quantity));
        }
        Assert.Equal(sourceQuantity, world.Society.Inventory.GetLot(sourceId).Quantity);
        Assert.Equal(InventoryReservationState.Reserved, world.Society.Inventory.GetReservation("care-protected").State);
        await FoodCapacityTestFixture.AssertReplay(world, actor, "care:", "make_room_for_food");
    }

    [Fact]
    public async Task AFedCaregiverStoresOrdinaryCargoAndKeepsOrchardSeedsReserved()
    {
        var (state, actor, child, sourceId) = await Family(false, 8, 9_000);
        var inventory = state.Society.Society.Inventory;
        var sourceQuantity = inventory.GetLot(sourceId).Quantity;
        inventory = inventory with
        {
            Lots = inventory.Lots.Select(lot => lot.Id == Cargo ? lot with { Quantity = 1 } : lot).ToArray(),
        };
        inventory = InventoryAuthority.AddLot(inventory, "care-orchard-seeds", "orchard_seed", actor, 7);
        inventory = InventoryAuthority.Reserve(inventory, "orchard-replant:care-orchard-seeds", actor,
            "care-orchard-seeds", 7, "orchard_replanting", long.MaxValue);
        var claim = inventory.GetReservation("orchard-replant:care-orchard-seeds");
        var choices = new FoodCapacityTestFixture.Choices("care:", "make_room_for_food");
        using var world = FoodCapacityTestFixture.Restore(FoodCapacityTestFixture.WithInventory(state, inventory), actor, choices);
        for (var tick = 0; tick < 40 && !world.ExportState().Events.Any(item => item.Kind == "child_cared_for" && item.Detail == child); tick++)
            Assert.True((await world.AdvanceOneTickAsync()).Advanced);

        Assert.Contains(world.ExportState().Events, item => item.Kind == "child_cared_for" && item.Detail == child);
        Assert.Single(world.ExportState().Events, item => item.Kind == "spare_cargo_stored" &&
            item.Detail == $"{actor}:stone:1:{FoodCapacityTestFixture.House}");
        Assert.Equal(claim, world.Society.Inventory.GetReservation(claim.Id));
        Assert.Equal(7, world.Society.Inventory.GetLot("care-orchard-seeds").Quantity);
        Assert.True(PersonalEquipmentRules.IsCarried(world.Society.Inventory.GetLot("care-orchard-seeds"), actor));
        Assert.Equal(sourceQuantity - 1, world.Society.Inventory.GetLot(sourceId).Quantity);
        Assert.True(world.Agents.Single(person => person.AgentId == actor).FullnessBasisPoints > 7_000);
        await FoodCapacityTestFixture.AssertReplay(world, actor, "care:", "make_room_for_food");
    }

    private static async Task<(WorldRuntimeState State, string Actor, string Child, string SourceId)> Family(
        bool inPot, int carried, int parentFullness)
    {
        var (state, actor, point) = await FoodCapacityTestFixture.Generated("audit-town-invariants");
        var society = ChosenBirthNameTestFixture.NameParent(state.Society.Society, actor);
        var partner = society.Agents.First(person => person.HouseholdId == FoodCapacityTestFixture.Household && person.Id != actor).Id;
        society = SocietyRules.ProposeRelationship(society, new("capacity-care-parents", 1,
            SocietyRelationshipType.Partnership, actor, partner, society.WorldTick)).Checkpoint;
        society = SocietyRules.AcceptRelationship(society, "capacity-care-parents", 1, partner).Checkpoint;
        var food = society.Inventory.Lots.First(lot => lot.OwnerId == FoodCapacityTestFixture.Household &&
            lot.ItemKind == "food" && lot.StorageBuildingId == FoodCapacityTestFixture.House && lot.Quantity >= 8);
        var birth = SocietyRules.CommitBirth(society, new($"capacity-family:{actor}:{society.WorldTick}", 1,
            actor, partner, FoodCapacityTestFixture.Household, [actor, partner], [actor, partner], food.Id, 4, society.WorldTick,
            ChildName: ChosenBirthNameTestFixture.ChildName(society, actor, "Ari"), PrimaryCaregiverId: actor));
        var child = Assert.IsType<string>(birth.CreatedId);
        society = birth.Checkpoint;
        Assert.Equal(food.Quantity - 4, society.Inventory.GetLot(food.Id).Quantity);
        Assert.Contains(society.Inventory.Reservations, reservation => reservation.LotId == food.Id && reservation.Quantity == 4 &&
            reservation.OwnerId == FoodCapacityTestFixture.Household && reservation.State == InventoryReservationState.Completed &&
            reservation.Purpose.StartsWith("birth:", StringComparison.Ordinal));
        var childPoint = state.Map.FootNeighbors(point).First(candidate => state.Map.IsBuildable(candidate) &&
            !state.Agents.Any(person => person.Position == candidate) && !state.Map.Resources.Any(resource => resource.Position == candidate));
        var inventory = society.Inventory with { Lots = society.Inventory.Lots.Where(lot => lot.OwnerId != actor).ToArray() };
        inventory = InventoryAuthority.AddLot(inventory, Cargo, "stone", actor, carried);
        var sourceId = food.Id;
        if (inPot)
        {
            inventory = InventoryAuthority.AddLot(inventory, "care-capacity-pot", InventoryContainerRules.StoragePot,
                FoodCapacityTestFixture.Household, 1, storageBuildingId: FoodCapacityTestFixture.House);
            inventory = InventoryAuthority.PutIntoContainer(inventory, "care-capacity-pot-fill", FoodCapacityTestFixture.Household,
                "care-capacity-pot", food.Id, 4);
            sourceId = inventory.Lots.Single(lot => lot.ContainerLotId == "care-capacity-pot").Id;
        }
        var remainingLoose = inventory.Lots.SingleOrDefault(lot => lot.Id == food.Id && lot.ContainerLotId is null);
        if (remainingLoose is not null && remainingLoose.Quantity > (inPot ? 0 : 4))
            inventory = InventoryAuthority.Reserve(inventory, "care-unused-birth-stock", FoodCapacityTestFixture.Household,
                remainingLoose.Id, remainingLoose.Quantity - (inPot ? 0 : 4), "care_capacity_control", inventory.WorldTick + 100);
        state = state with
        {
            Society = state.Society with { Society = society with { Inventory = inventory } },
            Survival = new SurvivalState(0, []),
            Agents = state.Agents.Select(person => person.AgentId == actor
                    ? person with { FullnessBasisPoints = parentFullness } : person)
                .Append(new AgentState(child, childPoint, 1_000, 0, "curious", "grow", Survival: new SurvivalCondition(10_000)))
                .OrderBy(person => person.AgentId, StringComparer.Ordinal).ToArray(),
            Towns = state.Towns!.Select(town => town.ResidentIds.Contains(actor) ? town with
            {
                ResidentIds = town.ResidentIds.Append(child).Order(StringComparer.Ordinal).ToArray(),
            } : town).ToArray(),
        };
        _ = WorldRuntimeCodec.Decode(WorldRuntimeCodec.Encode(state));
        return (state, actor, child, sourceId);
    }
}
