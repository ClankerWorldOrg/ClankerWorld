using System.Collections.Concurrent;
using ClankerWorld.Simulation;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.World;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Tests;

public sealed partial class GrownAgentHelperMemoryTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NativeThirdGenerationToolRepairInventoryKeepsActualSplitsGiftsInputsAndRecall(bool borrowed)
    {
        var state = WorldRuntimeCodec.Decode(await LaterAdult.Value);
        var births = state.Society.Society.Births.OrderBy(birth => birth.CommittedTick).ToArray();
        Assert.Equal(3, births.Length);
        var actor = births[^1].ChildId;
        var relative = births[^2].ChildId;
        var home = state.Society.Society.GetAgent(actor).HouseholdId!;
        var smith = state.WorldSimulation!.Buildings.SingleOrDefault(building => building.HouseholdId == home &&
            state.WorldContent!.Buildings.Single(definition => definition.CanonicalId == building.DefinitionId).Tags.Contains("blacksmith"));
        var society = state.Society.Society;
        var smithId = "native-smith-" + new string('s', 115);
        if (smith is null)
        {
            var definition = state.WorldContent!.Buildings.Single(definition => definition.CanonicalId == BlacksmithContent.Blacksmith1x2().CanonicalId);
            var house = state.WorldSimulation.Buildings.First(building => building.HouseholdId == home &&
                state.WorldContent.Buildings.Single(item => item.CanonicalId == building.DefinitionId).Tags.Contains("house"));
            var supplied = society.Inventory;
            foreach (var cost in definition.BuildCosts)
                supplied = InventoryAuthority.AddLot(supplied, "native-smith-cost:" + cost.ResourceId, cost.ResourceId,
                    home, cost.Amount, storageBuildingId: house.InstanceId);
            var totals = definition.BuildCosts.ToDictionary(cost => cost.ResourceId,
                cost => supplied.Lots.Where(lot => lot.OwnerId == home && lot.ItemKind == cost.ResourceId).Sum(lot => lot.Quantity), StringComparer.Ordinal);
            using var setup = WorldRuntime.Restore(state with
            { Society = state.Society with { Society = society with { Inventory = supplied } } });
            var origin = state.Agents.Single(person => person.AgentId == actor).Position;
            foreach (var tile in state.Map.Tiles.OrderBy(tile => state.Map.FootDistance(tile.Position, origin)))
                if (setup.PlaceBuilding(smithId, definition.CanonicalId, tile.Position, home).Applied) break;
            smith = Assert.Single(setup.WorldSimulation.Buildings, building => building.InstanceId == smithId);
            Assert.Equal(DerivedId.MaximumLength, smith.InstanceId.Length);
            state = setup.ExportState();
            society = state.Society.Society;
            foreach (var cost in definition.BuildCosts)
                Assert.Equal(totals[cost.ResourceId] - cost.Amount,
                    society.Inventory.Lots.Where(lot => lot.OwnerId == home && lot.ItemKind == cost.ResourceId).Sum(lot => lot.Quantity));
        }
        var stockId = "knife:" + new string('k', 122);
        var inventory = InventoryAuthority.AddLot(society.Inventory, stockId, "iron_knife", borrowed ? home : actor, 2,
            conditionBasisPoints: 3_000);
        if (borrowed)
            inventory = InventoryAuthority.Relocate(inventory, "controlled-borrowed-repair", stockId, home, 2, carrierId: actor);
        var materials = ToolProgressionRules.RepairMaterials("iron_knife");
        foreach (var material in materials)
            inventory = InventoryAuthority.AddLot(inventory, "repair-stock:" + material.ResourceId + ":" + new string('m', 100),
                material.ResourceId, actor, material.Amount + 1);
        var priorLots = inventory.Lots.Select(lot => lot.Id).ToHashSet(StringComparer.Ordinal);
        var priorHolds = inventory.Reservations.Select(hold => hold.Id.Value).ToHashSet(StringComparer.Ordinal);
        state = state with
        {
            JevEnabled = false,
            RoutineHelper = RoutineHelperSettings.Off,
            Society = state.Society with { Society = society with { Inventory = inventory } },
            Agents = state.Agents.Select(person => person with
            {
                Position = person.AgentId == actor ? smith.Position : person.Position,
                LastDecisionContext = null,
                Equipment = person.AgentId == actor ? null : person.Equipment,
                Project = person.AgentId == actor ? null : person.Project,
                Survival = person.AgentId == actor ? new() : person.Survival,
                FullnessBasisPoints = 10_000,
            }).ToArray(),
        };
        var choices = new NativeInventoryRepairChoices(actor, stockId);
        using var world = WorldRuntime.Restore(WorldRuntimeCodec.Decode(WorldRuntimeCodec.Encode(state)), _ => choices);
        world.Resume();
        OwnerInstructionReceipt? order = borrowed ? null : world.SubmitInstruction(new("native-repair", "owner:test", actor,
            OwnerInstructionKind.Order, "repair my iron knife"));
        using var host = new NativeGatheringHost(world);
        for (var tick = 0; tick < 20 && !world.Society.Inventory.Lots.Any(lot => !priorLots.Contains(lot.Id) &&
            lot.ItemKind == "iron_knife" && lot.ConditionBasisPoints == 10_000); tick++) await host.Advance();
        var repaired = Assert.Single(world.Society.Inventory.Lots, lot => !priorLots.Contains(lot.Id) && lot.ItemKind == "iron_knife");
        Assert.Equal((borrowed ? home : actor, 1, 10_000, stockId),
            (repaired.OwnerId, repaired.Quantity, repaired.ConditionBasisPoints, repaired.ProvenanceLotId));
        Assert.Equal((1, 3_000), (world.Society.Inventory.GetLot(stockId).Quantity, world.Society.Inventory.GetLot(stockId).ConditionBasisPoints));
        var holds = world.Society.Inventory.Reservations.Where(hold => !priorHolds.Contains(hold.Id)).ToArray();
        Assert.Equal(materials.Count, holds.Length);
        Assert.All(holds, hold => Assert.Equal((borrowed ? home : actor, InventoryReservationState.Completed), (hold.OwnerId, hold.State)));
        if (borrowed)
            Assert.All(holds, hold => Assert.DoesNotContain(hold.LotId, priorLots));
        if (borrowed)
            Assert.Contains(choices.Selected, row => row.Actor == actor && row.Choice == "repair_tool:" + stockId);
        var ids = world.Society.Inventory.Lots.Where(lot => !priorLots.Contains(lot.Id)).Select(lot => lot.Id)
            .Concat(holds.Select(hold => hold.Id.Value))
            .Concat(holds.Where(hold => !priorLots.Contains(hold.LotId)).Select(hold => hold.LotId)).ToList();
        if (order is not null)
        {
            var result = world.ExportState().Instructions!.Single(instruction => instruction.InstructionId == order.InstructionId).Task!;
            Assert.Equal(("finished", 1), (result.Status, result.CompletedUnits));
            ids.Add(Assert.IsType<Identity>(result.LastEffectId).Value);
        }
        world.Pause();
        state = world.ExportState();
        foreach (var id in ids)
        {
            Assert.InRange(id.Length, 1, DerivedId.MaximumLength);
            const string summary = "I remember nativereservationrepair work.";
            var checkpoint = SocietyRules.RecordSocialMemory(state.Society.Society,
                new(Identity.Parse(id), actor, relative, summary, "private", world.WorldTick)).Checkpoint;
            var bytes = WorldRuntimeCodec.Encode(state with { Society = state.Society with { Society = checkpoint } });
            using var restored = WorldRuntime.Restore(WorldRuntimeCodec.Decode(bytes));
            Assert.Equal(bytes, WorldRuntimeCodec.Encode(restored.ExportState()));
            var recalled = WorldMemoryRetrieval.Retrieve(restored.Society.Memories, restored.Society.Beliefs ?? [],
                null, actor, restored.WorldTick, [new("safe_idle", summary, 0)], restored.Society);
            var memory = Assert.Single(recalled, memory => memory.Id == id);
            Assert.Equal((actor, relative, summary), (memory.OwnerId, memory.SubjectId, memory.Summary));
            new AgentObservation(actor, restored.WorldTick, 0, 1, "sha256:repair-inventory", 0,
                [new("safe_idle", summary, 0)], RetrievedMemories: recalled).Validate();
        }
    }

    private sealed class NativeInventoryRepairChoices(string actor, string lot) : IDecisionProvider
    {
        public ConcurrentQueue<(string Actor, string Choice)> Selected { get; } = new();
        public DecisionProviderKind Kind => DecisionProviderKind.LargeLanguageModel;
        public long ProviderEpoch => 0;
        public ValueTask<CognitionDecisionResponse> DecideAsync(CognitionDecisionRequest request, CancellationToken cancellationToken = default)
        {
            request.Validate();
            var observation = request.Observation;
            var choice = observation.AgentId == actor ? observation.Candidates.FirstOrDefault(candidate =>
                candidate.Id == "repair_tool" || candidate.Id == "repair_tool:" + lot) : null;
            choice ??= observation.Candidates.FirstOrDefault(candidate => candidate.Id == "safe_idle") ??
                observation.Candidates.Single(candidate => candidate.Id == "identity_optional");
            Selected.Enqueue((observation.AgentId, choice.Id));
            return ValueTask.FromResult(new CognitionDecisionResponse(request.RequestId, observation.AgentId, Kind,
                request.ProviderEpoch, observation.RunEpoch, observation.DecisionGeneration, observation.ObservationDigest,
                choice.Id, 1, new Dictionary<string, double> { [choice.Id] = 1 }));
        }
    }
}
