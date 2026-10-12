using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.World;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Tests;

public sealed class InheritedBelongingsCollectionTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AnHeirCollectsStoredAndDroppedBequestsWithoutTakingOtherHouseholdProperty(bool sameHousehold)
    {
        using var generated = NormalPathWorld.CreateGenerated("social-estate-d9efa858",
            _ => new ActionCoverageRecorder(chooseIdle: true));
        var state = generated.ExportState();
        var checkpoint = state.Society.Society;
        var deceased = state.Agents[0].AgentId;
        var household = checkpoint.GetAgent(deceased).HouseholdId!;
        var heir = checkpoint.Agents.First(person => person.Id != deceased &&
            (person.HouseholdId == household) == sameHousehold).Id;
        var heirHousehold = checkpoint.GetAgent(heir).HouseholdId;
        var heirName = checkpoint.GetAgent(heir).Name;
        var house = state.WorldSimulation!.Buildings.Single(building => building.InstanceId == "first-town-house-a");
        Assert.Equal(household, house.HouseholdId);
        var inventory = InventoryAuthority.AddLot(checkpoint.Inventory, "bequest-wood", "wood", deceased, 4,
            storageBuildingId: house.InstanceId);
        inventory = InventoryAuthority.AddLot(inventory, "bequest-stone", "stone", deceased, 3);
        inventory = InventoryAuthority.AddLot(inventory, "private-household-clay", "clay", household, 2,
            storageBuildingId: house.InstanceId);
        var lastDay = Assert.IsType<SocietyDayLifecycle>(checkpoint.Config.DayLifecycle).MaximumDay;
        var birth = checkpoint.LifeTickAt(checkpoint.WorldTick + 1) - lastDay * checkpoint.Config.TicksPerLifecycleAge;
        state = state with
        {
            Society = state.Society with
            {
                Society = checkpoint with
                {
                    Inventory = inventory,
                    Agents = checkpoint.Agents.Select(person => person.Id == deceased ? person with
                    {
                        BirthTick = checkpoint.LifeClock is null ? birth : person.BirthTick,
                        BirthLifeTick = checkpoint.LifeClock is null ? null : birth,
                        AgeBand = SocietyAgeBand.Elder,
                        LastLifecycleYearChecked = lastDay - 1,
                    } : person).ToArray(),
                },
            },
            Agents = state.Agents.Select(person => person with
            { FullnessBasisPoints = 10_000, Project = null, LastDecisionContext = null }).ToArray(),
        };
        var will = new PostDeathWillTests.WillProvider(CognitionWillContext.HeirsCandidateId,
            observation => new([PostDeathWillTests.HeirKey(observation, heirName)], CognitionWillContext.EqualSplit));
        using var dying = WorldRuntime.Restore(WorldRuntimeCodec.Decode(WorldRuntimeCodec.Encode(state)),
            id => id == deceased ? will : new ActionCoverageRecorder(chooseIdle: true));
        Assert.True((await dying.AdvanceOneTickNonBlockingAsync()).Advanced);
        await will.Completed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        for (var tick = 0; tick < 20 && dying.Society.Estates.Single().WillStatus != "accepted"; tick++)
            Assert.True((await dying.AdvanceOneTickNonBlockingAsync()).Advanced);
        var estate = Assert.Single(dying.Society.Estates);
        Assert.Equal("accepted", estate.WillStatus);
        Assert.Equal([heir], estate.WillHeirIds);
        var escrow = dying.ExportState();
        escrow = escrow with
        {
            Society = escrow.Society with
            {
                Society = escrow.Society.Society with
                {
                    Estates = escrow.Society.Society.Estates.Select(item => item with
                    { ExpiryTick = escrow.Society.Society.WorldTick + 1 }).ToArray(),
                },
            },
        };
        using var settling = WorldRuntime.Restore(WorldRuntimeCodec.Decode(WorldRuntimeCodec.Encode(escrow)),
            _ => new ActionCoverageRecorder(chooseIdle: true));
        Assert.True((await settling.AdvanceOneTickNonBlockingAsync()).Advanced);
        Assert.True(settling.Society.GetEstate(estate.Id).Settled);
        var wood = Assert.Single(settling.Society.Inventory.Lots, lot => lot.ProvenanceLotId == "bequest-wood");
        var stone = Assert.Single(settling.Society.Inventory.Lots, lot => lot.ProvenanceLotId == "bequest-stone");
        Assert.Equal((heir, house.InstanceId, 4), (wood.OwnerId, wood.StorageBuildingId, wood.Quantity));
        Assert.Equal(heir, stone.OwnerId);
        Assert.NotNull(stone.GroundPosition);

        // Leave two committed units in storage and only one free carrying slot.
        var pickup = settling.ExportState();
        inventory = InventoryAuthority.Reserve(pickup.Society.Society.Inventory, "heir-commitment", heir,
            wood.Id, 2, "work", long.MaxValue);
        var equipment = pickup.Agents.Single(person => person.AgentId == heir).Equipment;
        var room = PersonalEquipmentRules.FreeCapacity(inventory, heir, equipment);
        inventory = InventoryAuthority.AddLot(inventory, "heir-carried-ballast", "stone", heir, room - 1);
        pickup = pickup with
        {
            Society = pickup.Society with { Society = pickup.Society.Society with { Inventory = inventory } },
            Agents = pickup.Agents.Select(person => person.AgentId == heir ? person with
            { Position = house.Position, FullnessBasisPoints = 10_000, Project = null, LastDecisionContext = null }
                : person).ToArray(),
        };
        var entrance = pickup.Map.FootNeighbors(house.Position).First(point => pickup.Map.IsPassable(point) &&
            pickup.Agents.All(person => person.Position != point));
        pickup = pickup with
        {
            Agents = pickup.Agents.Select(person => person.AgentId == heir
                ? person with { Position = entrance } : person).ToArray(),
        };
        var chooser = new CollectionChooser("household_collect:" + wood.Id);
        var bytes = WorldRuntimeCodec.Encode(pickup);
        using var collecting = WorldRuntime.Restore(WorldRuntimeCodec.Decode(bytes),
            id => id == heir ? chooser : new ActionCoverageRecorder(chooseIdle: true));
        Assert.Equal(bytes, WorldRuntimeCodec.Encode(collecting.ExportState()));
        for (var tick = 0; tick < 20 && collecting.Society.Inventory.GetLot(wood.Id).Quantity == 4; tick++)
            Assert.True((await collecting.AdvanceOneTickNonBlockingAsync()).Advanced);
        Assert.Contains("household_collect:" + wood.Id, chooser.Offered);
        Assert.Equal((heir, house.InstanceId, 3),
            (collecting.Society.Inventory.GetLot(wood.Id).OwnerId,
             collecting.Society.Inventory.GetLot(wood.Id).StorageBuildingId,
             collecting.Society.Inventory.GetLot(wood.Id).Quantity));
        Assert.Equal(1, collecting.Society.Inventory.Lots.Where(lot => lot.ItemKind == "wood" &&
            PersonalEquipmentRules.IsCarried(lot, heir)).Sum(lot => lot.Quantity));
        Assert.Equal(InventoryReservationState.Reserved, collecting.Society.Inventory.GetReservation("heir-commitment").State);
        Assert.Equal(heirHousehold, collecting.Society.GetAgent(heir).HouseholdId);
        Assert.Empty(collecting.Agents.Single(person => person.AgentId == heir).Departures ?? []);
        Assert.DoesNotContain("household_collect:private-household-clay", chooser.Offered);
        Assert.Equal((household, house.InstanceId, 2),
            (collecting.Society.Inventory.GetLot("private-household-clay").OwnerId,
             collecting.Society.Inventory.GetLot("private-household-clay").StorageBuildingId,
             collecting.Society.Inventory.GetLot("private-household-clay").Quantity));

        // Reload with carrying room at the dropped bequest; ordinary ground pickup still works.
        var groundPickup = collecting.ExportState();
        var ground = Assert.IsType<InventoryGroundPosition>(stone.GroundPosition);
        groundPickup = groundPickup with
        {
            Society = groundPickup.Society with
            {
                Society = groundPickup.Society.Society with
                {
                    Inventory = InventoryAuthority.Relocate(groundPickup.Society.Society.Inventory, "set-down-ballast",
                        "heir-carried-ballast", heir, room - 1, groundPosition: new(house.Position.X, house.Position.Y)),
                },
            },
            Agents = groundPickup.Agents.Select(person => person.AgentId == heir ? person with
            { Position = new(ground.X, ground.Y), LastDecisionContext = null } : person).ToArray(),
        };
        chooser = new CollectionChooser("household_collect:" + stone.Id);
        using var groundWorld = WorldRuntime.Restore(WorldRuntimeCodec.Decode(WorldRuntimeCodec.Encode(groundPickup)),
            id => id == heir ? chooser : new ActionCoverageRecorder(chooseIdle: true));
        for (var tick = 0; tick < 20 && groundWorld.Society.Inventory.GetLot(stone.Id).GroundPosition is not null; tick++)
            Assert.True((await groundWorld.AdvanceOneTickNonBlockingAsync()).Advanced);
        Assert.Contains("household_collect:" + stone.Id, chooser.Offered);
        Assert.True(PersonalEquipmentRules.IsCarried(groundWorld.Society.Inventory.GetLot(stone.Id), heir));
        Assert.Equal((heir, 3), (groundWorld.Society.Inventory.GetLot(stone.Id).OwnerId, groundWorld.Society.Inventory.GetLot(stone.Id).Quantity));
        groundWorld.Validate();
        var final = WorldRuntimeCodec.Encode(groundWorld.ExportState());
        using var reloaded = WorldRuntime.Restore(WorldRuntimeCodec.Decode(final));
        Assert.Equal(final, WorldRuntimeCodec.Encode(reloaded.ExportState()));
    }

    private sealed class CollectionChooser(string wanted) : IDecisionProvider
    {
        public DecisionProviderKind Kind => DecisionProviderKind.Deterministic;
        public long ProviderEpoch => 1;
        public HashSet<string> Offered { get; } = new(StringComparer.Ordinal);

        public ValueTask<CognitionDecisionResponse> DecideAsync(CognitionDecisionRequest request,
            CancellationToken cancellationToken = default)
        {
            foreach (var candidate in request.Observation.Candidates) Offered.Add(candidate.Id);
            var selected = request.Observation.Candidates.Any(candidate => candidate.Id == wanted) ? wanted : "safe_idle";
            return ValueTask.FromResult(new CognitionDecisionResponse(request.RequestId, request.Observation.AgentId,
                Kind, request.ProviderEpoch, request.Observation.RunEpoch, request.Observation.DecisionGeneration,
                request.Observation.ObservationDigest, selected, 1, new Dictionary<string, double> { [selected] = 1 }));
        }
    }
}
