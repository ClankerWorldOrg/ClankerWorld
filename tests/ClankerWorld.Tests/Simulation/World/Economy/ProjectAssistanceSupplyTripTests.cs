using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.World;

namespace ClankerWorld.Tests;

public sealed partial class AnimalPipelineTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ProjectAssistanceLeavesNativeAnimalSupplyStockPromised(bool promise)
    {
        var (state, helper, home, yard) = CreateYard("project-assistance-supply-trip");
        var requester = state.Society.Society.Agents.First(person => person.Id != helper &&
            person.HouseholdId == home && LifeStageRules.IsAdult(person.AgeBand)).Id;
        var house = state.WorldSimulation!.Buildings.First(building => building.HouseholdId == home &&
            state.WorldContent!.Buildings.Any(definition => definition.CanonicalId == building.DefinitionId && definition.Tags.Contains("house")));
        var recipe = state.WorldContent!.Recipes.Single(item => item.LocalId == "mill-grain");
        var removed = state.Society.Society.Inventory.Lots.Where(lot => lot.OwnerId == helper ||
            lot.ItemKind == "grain" && (lot.OwnerId == home || lot.OwnerId == requester)).Select(lot => lot.Id).ToHashSet(StringComparer.Ordinal);
        var inventory = state.Society.Society.Inventory with
        {
            Lots = state.Society.Society.Inventory.Lots.Where(lot => !removed.Contains(lot.Id)).ToArray(),
            Reservations = state.Society.Society.Inventory.Reservations.Where(item => !removed.Contains(item.LotId)).ToArray(),
        };
        const string grain = "project-assistance-supply-grain";
        inventory = InventoryAuthority.AddLot(inventory, grain, "grain", helper, 2);
        inventory = InventoryAuthority.AddLot(inventory, "project-assistance-food-reserve", "cultivated_greens", helper, 6);
        var sheep = new AnimalState("project-assistance-sheep", "Project sheep", "sheep", "female",
            -(long)AnimalRules.Definition("sheep").AdultDays * state.WorldSystems!.Config.TicksPerDay,
            yard.Position, "household:" + home, home, yard.InstanceId);
        state = At(state, helper, house.Position, inventory, [sheep]);
        // Arrange a valid paused request; both conflicting goods actions below
        // are actual personal choices offered and applied by the runtime.
        state = state with
        {
            Agents = state.Agents.Select(person => person.AgentId == requester ? person with
            {
                Project = new AgentProject("build:recipe:" + recipe.CanonicalId, recipe.DisplayName, state.Society.Society.WorldTick,
                    "paused", Blocker: "Awaiting grain", LastTransitionTick: state.Society.Society.WorldTick, RequiresFreshChoice: true),
            } : person.AgentId == helper ? person with { Equipment = null } : person).ToArray(),
        };
        var chooser = new ProjectSupplyChoices(helper, promise);
        using var world = WorldRuntime.Restore(WorldRuntimeCodec.Decode(WorldRuntimeCodec.Encode(state)), _ => chooser);
        world.Validate();
        Assert.True(world.DestinationRoom(house.InstanceId) >= 1);
        Assert.True((await world.AdvanceOneTickAsync()).Advanced);
        if (promise)
        {
            var pickup = Assert.IsType<AnimalSupplyActionPlan>(chooser.Pickup);
            Assert.True(pickup.Carried);
            Assert.Equal((grain, 2), (pickup.Choice.StockId, pickup.Quantity));
            Assert.Contains(new InventoryHold(InventoryHoldPurpose.SupplyTrip, helper), world.Society.Inventory.GetLot(grain).Holds!);
            Assert.Contains(world.ExportState().AnimalWorld!.SupplyTrips, trip => trip.ActorId == helper && trip.LotId == grain);
        }
        else Assert.Empty(world.ExportState().AnimalWorld!.SupplyTrips);
        world.Validate();
        using (var before = WorldRuntime.Restore(WorldRuntimeCodec.Decode(WorldRuntimeCodec.Encode(world.ExportState())))) before.Validate();

        chooser.Assist = true;
        world.SubmitInstruction(new("help-after-supply-choice", "owner:test", helper,
            OwnerInstructionKind.Suggestion, "Help the requested grain project with spare grain."));
        var result = await world.AdvanceOneTickAsync();
        Assert.True(result.Advanced);
        var decision = Assert.Single(result.Decisions, item => item.AgentId == helper);
        Assert.True(decision.Admission.Accepted);
        Assert.False(decision.Admission.FellBack);
        Assert.Equal(DecisionProviderKind.LargeLanguageModel, decision.Admission.Intention!.Provider);
        if (promise)
        {
            Assert.Null(chooser.Contribution);
            Assert.Equal((helper, 2), (world.Society.Inventory.GetLot(grain).OwnerId, world.Society.Inventory.GetLot(grain).Quantity));
            Assert.Contains(new InventoryHold(InventoryHoldPurpose.SupplyTrip, helper), world.Society.Inventory.GetLot(grain).Holds!);
            Assert.DoesNotContain(result.Events, item => item.Kind == "project_request_fulfilled");
        }
        else
        {
            var plan = Assert.IsType<ProjectAssistanceActionPlan>(chooser.Contribution);
            Assert.Same(plan, decision.ActionPlan);
            Assert.Equal((requester, home, house.InstanceId, grain, 1),
                (plan.RequesterId, plan.RecipientId, plan.HouseId, plan.LotId, plan.Quantity));
            Assert.Null(decision.PlanRefusal);
            Assert.Contains(result.Events, item => item.Kind == "project_request_fulfilled" && item.Detail == $"{helper}:{requester}:grain:1");
            Assert.Equal(1, world.Society.Inventory.Lots.Where(lot => lot.OwnerId == home && lot.ItemKind == "grain" && lot.StorageBuildingId == house.InstanceId).Sum(lot => lot.Quantity));
        }
        world.Validate();
        using var restored = WorldRuntime.Restore(WorldRuntimeCodec.Decode(WorldRuntimeCodec.Encode(world.ExportState())));
        restored.Validate();
    }

    private sealed class ProjectSupplyChoices(string helper, bool promise) : IDecisionProvider
    {
        public bool Assist { get; set; }
        public AnimalSupplyActionPlan? Pickup { get; private set; }
        public ProjectAssistanceActionPlan? Contribution { get; private set; }
        public DecisionProviderKind Kind => DecisionProviderKind.LargeLanguageModel;
        public long ProviderEpoch => 0;
        public ValueTask<CognitionDecisionResponse> DecideAsync(CognitionDecisionRequest request, CancellationToken cancellationToken = default)
        {
            CognitionCandidate? candidate = null;
            if (request.Observation.AgentId == helper)
            {
                if (Assist)
                {
                    candidate = request.Observation.Candidates.FirstOrDefault(item => item.Id == "assist:grain");
                    if (candidate is not null) Contribution = Assert.IsType<ProjectAssistanceActionPlan>(candidate.ActionPlan);
                }
                else if (promise)
                {
                    candidate = request.Observation.Candidates.FirstOrDefault(item => item.ActionPlan is AnimalSupplyActionPlan
                    { Carried: true, Quantity: 2, Choice.StockId: "project-assistance-supply-grain", Choice.AnimalId: null });
                    if (candidate is not null) Pickup = Assert.IsType<AnimalSupplyActionPlan>(candidate.ActionPlan);
                }
            }
            var selected = candidate?.Id ?? "safe_idle";
            return ValueTask.FromResult(new CognitionDecisionResponse(request.RequestId, request.Observation.AgentId,
                Kind, ProviderEpoch, request.Observation.RunEpoch, request.Observation.DecisionGeneration,
                request.Observation.ObservationDigest, selected, 1, new Dictionary<string, double> { [selected] = 1 }));
        }
    }
}
