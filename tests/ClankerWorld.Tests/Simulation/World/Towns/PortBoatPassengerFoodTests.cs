using System.Collections.Concurrent;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.World;

namespace ClankerWorld.Tests;

public sealed partial class PortBoatRuntimeTests
{
    [Theory]
    [InlineData("land")]
    [InlineData("milk")]
    [InlineData("reserved")]
    [InlineData("spoiled")]
    [InlineData("meal")]
    public async Task PassengerSurvivalUsesOnlyAvailableCarriedFoodAcrossReload(string food)
    {
        var aboard = food != "land";
        var state = WorldRuntimeCodec.Decode(await (aboard ? Underway : PaidBoat).Value);
        var actor = BoatPolicy.Author;
        var inventory = state.Society.Society.Inventory;
        inventory = inventory with
        {
            Lots = inventory.Lots.Where(lot => lot.Id != "travel-water" &&
                !(lot.OwnerId == actor && lot.ItemKind == "food" && PersonalEquipmentRules.IsCarried(lot, actor))).ToArray()
        };
        var kind = food == "meal" ? "food" : "milk";
        inventory = InventoryAuthority.AddLot(inventory, "passenger-food", kind, actor, 2);
        if (food == "reserved")
            inventory = InventoryAuthority.Reserve(inventory, "passenger-food-held", actor, "passenger-food", 2,
                "other-personal-work", long.MaxValue);
        if (food == "spoiled")
            inventory = inventory with
            { Lots = inventory.Lots.Select(lot => lot.Id == "passenger-food" ? lot with { FreshnessBasisPoints = 0 } : lot).ToArray() };
        state = state with
        {
            Society = state.Society with { Society = state.Society.Society with { Inventory = inventory } },
            Agents = state.Agents.Select(person => person.AgentId == actor
                ? person with { HungerBasisPoints = 1_500 } : person).ToArray()
        };
        var provider = new PassengerFoodProvider(actor, food == "meal" ? "consume_food" : "drink_milk");
        using var world = WorldRuntime.Restore(state, _ => provider);
        world.SubmitInstruction(new("passenger-consider-food", "owner:test", actor, OwnerInstructionKind.Suggestive,
            "Consider your carried food while traveling."));
        var saved = WorldRuntimeCodec.Encode(world.ExportState());
        Assert.False((await world.AdvanceOneTickAsync(() => false)).Advanced);
        Assert.Equal(saved, WorldRuntimeCodec.Encode(world.ExportState()));
        using var replay = WorldRuntime.Restore(WorldRuntimeCodec.Decode(saved),
            _ => new PassengerFoodProvider(actor, food == "meal" ? "consume_food" : "drink_milk"));
        Assert.Equal(saved, WorldRuntimeCodec.Encode(replay.ExportState()));
        var drinkCount = state.Events.Count(item => item.Kind == "milk_drunk");
        for (var tick = 0; tick < 4; tick++)
        {
            Assert.True((await world.AdvanceOneTickAsync()).Advanced);
            Assert.True((await replay.AdvanceOneTickAsync()).Advanced);
            Assert.Equal(WorldRuntimeCodec.Encode(world.ExportState()), WorldRuntimeCodec.Encode(replay.ExportState()));
            if (aboard) Assert.Equal(actor, Assert.Single(world.Boats).Journey!.PassengerId);
        }
        var usable = food is "land" or "milk" or "meal";
        Assert.Equal(usable ? 1 : 2, world.Society.Inventory.GetLot("passenger-food").Quantity);
        Assert.Equal(drinkCount + (usable && kind == "milk" ? 1 : 0), world.ExportState().Events.Count(item => item.Kind == "milk_drunk"));
        var hunger = world.Agents.Single(person => person.AgentId == actor).HungerBasisPoints;
        Assert.Equal(usable ? 4_484 : 1_484, hunger);
        var jug = world.Society.Inventory.GetLot("travel-jug");
        Assert.Equal((actor, 1, 10_000), (jug.OwnerId, jug.Quantity, jug.ConditionBasisPoints));
        if (aboard)
            Assert.All(provider.Observed.Where(observation => observation.AgentId == actor), observation =>
                Assert.All(observation.Candidates, candidate => Assert.True(candidate.Id is "safe_idle" or "consume_food" or "drink_milk", candidate.Id)));
        if (food == "reserved") Assert.Equal(InventoryReservationState.Reserved,
            world.Society.Inventory.GetReservation("passenger-food-held").State);
        world.Validate();
        replay.Validate();
        var final = WorldRuntimeCodec.Encode(world.ExportState());
        using var restored = WorldRuntime.Restore(WorldRuntimeCodec.Decode(final), _ => new PassengerFoodProvider(actor, "safe_idle"));
        Assert.Equal(final, WorldRuntimeCodec.Encode(restored.ExportState()));
    }

    private sealed class PassengerFoodProvider(string actor, string preferred) : IDecisionProvider
    {
        public DecisionProviderKind Kind => DecisionProviderKind.Deterministic;
        public long ProviderEpoch => 1;
        internal ConcurrentQueue<AgentObservation> Observed { get; } = new();
        public ValueTask<CognitionDecisionResponse> DecideAsync(CognitionDecisionRequest request, CancellationToken cancellationToken = default)
        {
            var observation = request.Observation;
            Observed.Enqueue(observation);
            var choice = observation.Candidates.FirstOrDefault(candidate => observation.AgentId == actor && candidate.Id == preferred)
                ?? observation.Candidates.Single(candidate => candidate.Id == "safe_idle");
            return ValueTask.FromResult(new CognitionDecisionResponse(request.RequestId, observation.AgentId, Kind, ProviderEpoch,
                observation.RunEpoch, observation.DecisionGeneration, observation.ObservationDigest, choice.Id, 1,
                observation.Candidates.ToDictionary(candidate => candidate.Id, candidate => candidate.Id == choice.Id ? 1d : 0d, StringComparer.Ordinal)));
        }
    }
}
