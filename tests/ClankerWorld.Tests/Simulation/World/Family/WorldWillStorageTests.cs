using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.World;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Tests;

public sealed class WorldWillStorageTests
{
    [Fact]
    public async Task NaturalDeathPreservesHouseStockLivingCustodyAndWholeCarriedVesselsAcrossReload()
    {
        var (state, actor, carrier, house) = DeathState();
        using var world = WorldRuntime.Restore(state, _ => new ActionCoverageRecorder(chooseIdle: true));
        var position = state.Agents.Single(person => person.AgentId == actor).Position;
        var ground = new InventoryGroundPosition(position.X, position.Y);

        Assert.True((await world.TickModelWorkAsync()).Advanced);

        var estate = Assert.Single(world.Society.Estates);
        Assert.Equal(actor, estate.DeceasedId);
        Assert.Equal("default", estate.WillStatus);
        Assert.Equal(house, world.Society.Inventory.GetLot("stored-wood").StorageBuildingId);
        Assert.Equal(house, estate.FrozenLots!.Single(lot => lot.LotId == "stored-wood").StorageBuildingId);
        Assert.Equal(carrier, world.Society.Inventory.GetLot("living-held-stone").CarrierId);
        Assert.Equal(ground, world.Society.Inventory.GetLot("carried-pot").GroundPosition);
        Assert.Null(world.Society.Inventory.GetLot("carried-berries").GroundPosition);
        Assert.Equal("carried-pot", world.Society.Inventory.GetLot("carried-berries").ContainerLotId);
        Assert.Equal(2, world.Society.Inventory.GetLot("carried-berries").Quantity);
        Assert.Equal(carrier, world.Society.Inventory.GetLot("borrowed-wood").OwnerId);
        Assert.Equal(ground, world.Society.Inventory.GetLot("borrowed-wood").GroundPosition);
        Assert.Null(world.Society.Inventory.GetLot("borrowed-wood").CarrierId);
        world.Validate();

        var saved = world.ExportState();
        var encoded = WorldRuntimeCodec.Encode(saved);
        using var restored = WorldRuntime.Restore(WorldRuntimeCodec.Decode(encoded),
            _ => new ActionCoverageRecorder(chooseIdle: true));
        Assert.Equal(encoded, WorldRuntimeCodec.Encode(restored.ExportState()));
        var shortened = saved with
        {
            Society = saved.Society with
            {
                Society = saved.Society.Society with
                {
                    Estates = saved.Society.Society.Estates.Select(item => item with
                    { ExpiryTick = saved.Society.Society.WorldTick + 1 }).ToArray(),
                },
            },
        };
        using var settled = WorldRuntime.Restore(shortened, _ => new ActionCoverageRecorder(chooseIdle: true));
        Assert.True((await settled.TickModelWorkAsync()).Advanced);
        Assert.True(settled.Society.GetEstate(estate.Id).Settled);
        var wood = Assert.Single(settled.Society.Inventory.Lots, lot => lot.ProvenanceLotId == "stored-wood");
        Assert.Equal((house, 4), (wood.StorageBuildingId, wood.Quantity));
        Assert.Equal(ground, settled.Society.Inventory.GetLot("carried-pot").GroundPosition);
        Assert.Null(settled.Society.Inventory.GetLot("carried-berries").GroundPosition);
        Assert.Equal(2, settled.Society.Inventory.GetLot("carried-berries").Quantity);
        settled.Validate();
        Assert.Equal(WorldRuntimeCodec.Encode(settled.ExportState()),
            WorldRuntimeCodec.Encode(WorldRuntimeCodec.Decode(WorldRuntimeCodec.Encode(settled.ExportState()))));
    }

    [Fact]
    public async Task ADefaultEstateCannotClaimAnotherHouseAsItsFrozenStorage()
    {
        var (state, _, _, house) = DeathState();
        using var world = WorldRuntime.Restore(state, _ => new ActionCoverageRecorder(chooseIdle: true));
        Assert.True((await world.TickModelWorkAsync()).Advanced);
        Assert.Equal("default", Assert.Single(world.Society.Estates).WillStatus);
        var saved = world.ExportState();
        var otherHouse = saved.WorldSimulation!.Buildings.First(building => building.InstanceId != house &&
            saved.WorldContent!.Buildings.Single(definition => definition.CanonicalId == building.DefinitionId)
                .Tags.Contains("house", StringComparer.Ordinal)).InstanceId;
        var checkpoint = saved.Society.Society;
        var forged = saved with
        {
            Society = saved.Society with
            {
                Society = checkpoint with
                {
                    Inventory = checkpoint.Inventory with
                    {
                        Lots = checkpoint.Inventory.Lots.Select(lot => lot.Id == "stored-wood"
                            ? lot with { StorageBuildingId = otherHouse } : lot).ToArray(),
                    },
                },
            },
        };
        Assert.Throws<InvalidDataException>(() => WorldRuntime.Restore(forged));
    }

    private static (WorldRuntimeState State, string Actor, string Carrier, string House) DeathState()
    {
        using var generated = NormalPathWorld.CreateGenerated("will-physical-death",
            _ => new ActionCoverageRecorder(chooseIdle: true));
        var state = generated.ExportState();
        var actor = state.Agents[0].AgentId;
        var carrier = state.Agents[1].AgentId;
        var checkpoint = state.Society.Society;
        var household = checkpoint.GetAgent(actor).HouseholdId;
        var house = state.WorldSimulation!.Buildings.First(building => building.HouseholdId == household &&
            state.WorldContent!.Buildings.Single(definition => definition.CanonicalId == building.DefinitionId)
                .Tags.Contains("house", StringComparer.Ordinal)).InstanceId;
        var inventory = InventoryAuthority.AddLot(checkpoint.Inventory, "stored-wood", "wood", actor, 4,
            storageBuildingId: house);
        inventory = InventoryAuthority.AddLot(inventory, "carried-pot", InventoryContainerRules.StoragePot, actor, 1);
        inventory = InventoryAuthority.AddLot(inventory, "carried-berries", "berries", actor, 2, containerLotId: "carried-pot");
        inventory = InventoryAuthority.AddLot(inventory, "living-held-stone", "stone", actor, 3);
        inventory = InventoryAuthority.Relocate(inventory, "living-custody", "living-held-stone", actor, 3, carrierId: carrier);
        inventory = InventoryAuthority.AddLot(inventory, "borrowed-wood", "wood", carrier, 1);
        inventory = InventoryAuthority.Relocate(inventory, "borrowed-custody", "borrowed-wood", carrier, 1, carrierId: actor);
        var lastDay = Assert.IsType<SocietyDayLifecycle>(checkpoint.Config.DayLifecycle).MaximumDay;
        var birth = checkpoint.LifeTickAt(checkpoint.WorldTick + 1) - lastDay * checkpoint.Config.TicksPerLifecycleAge;
        checkpoint = checkpoint with
        {
            Inventory = inventory,
            Agents = checkpoint.Agents.Select(person => person.Id == actor ? person with
            {
                BirthTick = checkpoint.LifeClock is null ? birth : person.BirthTick,
                BirthLifeTick = checkpoint.LifeClock is null ? null : birth,
                AgeBand = SocietyAgeBand.Elder,
                LastLifecycleYearChecked = lastDay - 1,
            } : person).ToArray(),
        };
        return (state with { Society = state.Society with { Society = checkpoint } }, actor, carrier, house);
    }
}
