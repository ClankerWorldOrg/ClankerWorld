using ClankerWorld.Simulation;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;
using ClankerWorld.Simulation.World;

namespace ClankerWorld.Tests;

public sealed class TownDonationDerivedIdentityTests
{
    [Fact]
    public async Task OrdinaryDonationUsesTheSameBoundedOperationAsPublicServiceReceipts()
    {
        using var original = NormalPathWorld.CreateGenerated("long-town-donation", _ => new DonationProvider());
        original.Pause();
        var state = original.ExportState();
        var checkpoint = state.Society.Society;
        var prior = checkpoint.Agents[0];
        var actor = DerivedId.Create("founder:" + new string('a', DerivedId.MaximumLength - "founder:".Length));
        checkpoint = SocietyRules.UndoFounderPlacement(checkpoint, prior.Id).Checkpoint;
        checkpoint = SocietyRules.PlaceFounder(checkpoint, SocietyRules.CreateFounder(actor, "Élodie 🌿 Vale",
            config: checkpoint.Config, startingAge: checkpoint.Config.FounderStartingAge), prior.HouseholdId!).Checkpoint;
        var town = state.Towns![0];
        var warehouse = state.WorldSimulation!.Buildings.Single(building => building.InstanceId == "first-town-warehouse");
        var inventory = InventoryAuthority.AddLot(checkpoint.Inventory, "donation-stone", "stone", actor, 8);
        state = state with
        {
            Society = state.Society with { Society = checkpoint with { Inventory = inventory } },
            FounderSetup = state.FounderSetup! with { FounderIds = state.FounderSetup.FounderIds.Select(id => id == prior.Id ? actor : id).ToArray() },
            Agents = state.Agents.Select(person => person.AgentId == prior.Id
                ? person with { AgentId = actor, Position = warehouse.Position, FullnessBasisPoints = 9_000 } : person)
                .OrderBy(person => person.AgentId, StringComparer.Ordinal).ToArray(),
            Towns = [town with
            {
                ResidentIds = town.ResidentIds.Select(id => id == prior.Id ? actor : id).Order(StringComparer.Ordinal).ToArray(),
                Council = town.Council! with
                {
                    Members = town.Council.Members.Select(id => id == prior.Id ? actor : id).Order(StringComparer.Ordinal).ToArray(),
                    ElectionHistory = town.Council.ElectionHistory.Select(election => election with
                    { Voters = election.Voters.Select(id => id == prior.Id ? actor : id).Order(StringComparer.Ordinal).ToArray() }).ToArray(),
                },
            }],
        };
        using var world = WorldRuntime.Restore(WorldRuntimeCodec.Decode(WorldRuntimeCodec.Encode(state)), _ => new DonationProvider(actor));
        world.Resume();
        Assert.True((await world.AdvanceOneTickAsync()).Advanced);
        var operation = WorldRuntime.CivicSharedRules.WarehouseStockId(world.WorldTick, actor);
        Assert.InRange(operation.Length, 1, DerivedId.MaximumLength);
        var expected = InventoryAuthority.Transfer(inventory, operation, actor, town.Id, "donation-stone", 4,
            "town_resources_stored", warehouse.InstanceId);
        var expectedShipment = Assert.Single(expected.Lots, lot => lot.ProvenanceLotId == "donation-stone");
        var shipment = Assert.Single(world.Society.Inventory.Lots, lot => lot.ProvenanceLotId == "donation-stone");
        Assert.Equal(expectedShipment.Id, shipment.Id);
        Assert.Equal((town.Id, warehouse.InstanceId, 4), (shipment.OwnerId, shipment.StorageBuildingId, shipment.Quantity));
        Assert.Equal(4, world.Society.Inventory.GetLot("donation-stone").Quantity);
        var bytes = WorldRuntimeCodec.Encode(world.ExportState());
        using var restored = WorldRuntime.Restore(WorldRuntimeCodec.Decode(bytes));
        Assert.Equal(bytes, WorldRuntimeCodec.Encode(restored.ExportState()));
    }

    private sealed class DonationProvider(string? actor = null) : IDecisionProvider
    {
        public DecisionProviderKind Kind => DecisionProviderKind.Deterministic;
        public long ProviderEpoch => 0;
        public ValueTask<CognitionDecisionResponse> DecideAsync(CognitionDecisionRequest request, CancellationToken cancellationToken = default)
        {
            var observation = request.Observation;
            var choice = observation.Candidates.Single(candidate => candidate.Id ==
                (observation.AgentId == actor ? "store_town_resources" : "safe_idle"));
            return ValueTask.FromResult(new CognitionDecisionResponse(request.RequestId, observation.AgentId, Kind,
                ProviderEpoch, observation.RunEpoch, observation.DecisionGeneration, observation.ObservationDigest,
                choice.Id, 1, new Dictionary<string, double> { [choice.Id] = 1 }));
        }
    }
}
