using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.World;

namespace ClankerWorld.Tests;

public sealed partial class BuildingExpansionTests
{
    [Theory]
    [InlineData("same")]
    [InlineData("rebuild")]
    [InlineData("expand")]
    public async Task GuestInvitationsTrackTheOriginalHouseAcrossRebuildAndExpansion(string change)
    {
        using var prepared = PreparedWorld("first-town-house-a", out var actor, out var original);
        var state = prepared.ExportState();
        var house = original with { Entrance = new(original.Position.X, original.Position.Y + 1) };
        var guest = state.Society.Society.Agents.First(person => person.HouseholdId != house.HouseholdId).Id;
        var inventory = state.Society.Society.Inventory;
        if (change == "rebuild")
        {
            var stored = inventory.Lots.Where(lot => lot.StorageBuildingId == house.InstanceId ||
                    lot.DeliveryBuildingId == house.InstanceId).Select(lot => lot.Id).ToHashSet(StringComparer.Ordinal);
            inventory = inventory with
            {
                Lots = inventory.Lots.Where(lot => !stored.Contains(lot.Id) &&
                    (lot.ContainerLotId is null || !stored.Contains(lot.ContainerLotId))).ToArray(),
                Reservations = inventory.Reservations.Where(reservation => !stored.Contains(reservation.LotId)).ToArray(),
            };
            inventory = InventoryAuthority.AddLot(inventory, "guest-rebuild-timber", "wood", house.HouseholdId!, 8);
        }
        state = state with
        {
            WorldSimulation = state.WorldSimulation! with
            {
                Buildings = state.WorldSimulation.Buildings.Select(building => building.InstanceId == house.InstanceId
                    ? house : building).ToArray(),
            },
            Society = state.Society with
            { Society = state.Society.Society with { Inventory = StockHoldTestState.Custody(inventory) } },
            Agents = state.Agents.Select(person => person with
            {
                Position = person.AgentId == guest ? new(house.Position.X + 1, house.Position.Y) : person.Position,
                FullnessBasisPoints = 10_000,
                LastDecisionContext = null,
            }).ToArray(),
        };
        var chooser = new IdleProvider();
        using var world = WorldRuntime.Restore(WorldRuntimeCodec.Decode(WorldRuntimeCodec.Encode(state)),
            id => id == actor ? chooser : new IdleProvider());
        HouseGuestActionPlan? plan = null;
        for (var tick = 0; tick < 20 && plan is null; tick++)
        {
            Assert.True((await world.AdvanceOneTickAsync()).Advanced);
            plan = chooser.Seen.SelectMany(request => request.Observation.Candidates)
                .Select(candidate => candidate.ActionPlan).OfType<HouseGuestActionPlan>()
                .FirstOrDefault(candidate => candidate.Choice.GuestId == guest && candidate.Choice.Invited);
        }
        Assert.NotNull(plan);
        Assert.Equal((actor, house, house.HouseholdId), (plan.Actor, plan.House, plan.ActorHouseholdId));
        Assert.True(world.CheckActionPlan(plan).Allowed);
        Assert.DoesNotContain(world.WorldSimulation.GuestInvitations ?? [], invitation =>
            invitation.HouseInstanceId == house.InstanceId && invitation.GuestId == guest);

        if (change == "rebuild")
        {
            Assert.True(world.RemoveBuilding(house.InstanceId, house.TownId, house.HouseholdId).Applied);
            var result = world.PlaceBuilding(house.InstanceId, house.DefinitionId, house.Position, house.HouseholdId!);
            Assert.True(result.Applied, result.Failure);
            var replacement = Assert.Single(world.WorldSimulation.Buildings, building => building.InstanceId == house.InstanceId);
            Assert.Equal((house.InstanceId, house.DefinitionId, house.Position, house.TownId, house.HouseholdId),
                (replacement.InstanceId, replacement.DefinitionId, replacement.Position, replacement.TownId, replacement.HouseholdId));
            Assert.True(replacement.PlacedTick > house.PlacedTick);
            Assert.Contains(world.Society.Inventory.Reservations, reservation =>
                reservation.Purpose == "building:" + house.InstanceId && reservation.LotId == "guest-rebuild-timber" &&
                reservation.State == InventoryReservationState.Completed && reservation.Quantity > 0);
        }
        else if (change == "expand")
        {
            var started = world.StartBuildingExpansion(actor, house.InstanceId);
            Assert.True(started.Applied, started.Failure);
            var job = Assert.Single(world.WorldSimulation.BuildingExpansions!, item => item.JobId == started.JobId);
            Assert.NotEqual(house.Position, job.TargetPosition);
            while (world.WorldTick < job.CompletionTick) Assert.True((await world.AdvanceOneTickAsync()).Advanced);
            var finished = Assert.Single(world.WorldSimulation.BuildingExpansions!, item => item.JobId == job.JobId);
            Assert.Equal(WorldProductionJobState.Completed, finished.State);
            Assert.NotEmpty(finished.InputReservationIds);
            Assert.All(finished.InputReservationIds, id =>
                Assert.Equal(InventoryReservationState.Completed, world.Society.Inventory.GetReservation(id).State));
            var expanded = Assert.Single(world.WorldSimulation.Buildings, building => building.InstanceId == house.InstanceId);
            Assert.Equal((house.InstanceId, house.DefinitionId, house.PlacedTick, house.HouseholdId, house.TownId),
                (expanded.InstanceId, expanded.DefinitionId, expanded.PlacedTick, expanded.HouseholdId, expanded.TownId));
            Assert.NotEqual(house.Position, expanded.Position);
        }

        world.Validate();
        var bytes = WorldRuntimeCodec.Encode(world.ExportState());
        using var reopened = Reload(world);
        reopened.Validate();
        Assert.Equal(bytes, WorldRuntimeCodec.Encode(reopened.ExportState()));
        var check = world.CheckActionPlan(plan);
        Assert.Equal(bytes, WorldRuntimeCodec.Encode(world.ExportState()));
        Assert.Equal(check.Allowed, reopened.CheckActionPlan(plan).Allowed);
        Assert.True(check.Allowed == (change != "rebuild"),
            $"guest_house_identity_boundary:{change}: retained choice allowed={check.Allowed}");
        if (change == "rebuild") Assert.Equal(ActionRefusalReason.TargetUnavailable, check.Refusal!.Reason);
        Assert.DoesNotContain(world.WorldSimulation.GuestInvitations ?? [], invitation =>
            invitation.HouseInstanceId == house.InstanceId && invitation.GuestId == guest);
    }
}
