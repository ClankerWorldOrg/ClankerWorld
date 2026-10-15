using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.World;

namespace ClankerWorld.Tests;

public sealed class TownProjectBorrowedToolTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DepartedResidentsUseTheirStillBorrowedToolForAnApprovedProject(bool hammer)
    {
        using var scenario = await TownProjectScenario.ApprovedAsync(initialTownStock: hammer);
        if (hammer)
        {
            scenario.Policy.Supply = true;
            await scenario.UntilAsync(() => scenario.Project.Plan.Budget.All(cost =>
                TownProjectRules.DeliveredQuantity(scenario.Project, scenario.World.Towns[0].Id,
                    scenario.World.Society.Inventory, cost.ResourceId) >= cost.Amount), 240);
            scenario.Policy.Supply = false;
            Assert.NotEqual("completed", scenario.Project.Stage);
        }
        scenario.World.Pause();
        var state = scenario.World.ExportState();
        const string actor = TownProjectScenario.Author;
        var household = scenario.World.Society.GetAgent(actor).HouseholdId!;
        var inventory = InventoryAuthority.AddLot(state.Society.Society.Inventory, "borrowed-project-tool",
            hammer ? "wooden_hammer" : "wooden_pickaxe", household, 1);
        inventory = InventoryAuthority.Relocate(inventory, "borrow-project-tool", "borrowed-project-tool", household,
            1, actor, holdAccess: StockHoldTestState.Access(inventory, "borrowed-project-tool"));
        state = state with
        {
            Society = state.Society with { Society = state.Society.Society with { Inventory = inventory } },
            Agents = state.Agents.Select(person => person.AgentId == actor
                ? person with { Position = hammer ? TownProjectRules.WorkSite(scenario.Project.Plan) : person.Position, LastDecisionContext = null }
                : person).ToArray(),
        };
        var provider = new BorrowedToolChoice(actor, hammer);
        using var world = WorldRuntime.Restore(WorldRuntimeCodec.Decode(WorldRuntimeCodec.Encode(state)), _ => provider);
        Assert.True(world.DisplaceAdult(actor));
        Assert.Null(world.Society.GetAgent(actor).HouseholdId);
        Assert.Contains(actor, world.Towns[0].ResidentIds);
        AssertBorrowed(world);
        var departed = WorldRuntimeCodec.Encode(world.ExportState());
        using var restored = WorldRuntime.Restore(WorldRuntimeCodec.Decode(departed), _ => provider);
        Assert.Equal(departed, WorldRuntimeCodec.Encode(restored.ExportState()));
        restored.SubmitInstruction(new("borrowed-project-turn", "owner:test", actor,
            OwnerInstructionKind.Suggestion, "Consider helping the approved Town project."));
        restored.Resume();
        TownProjectWorkActionPlan? applied = null;
        for (var tick = 0; tick < 100 && applied is null; tick++)
        {
            var result = await restored.AdvanceOneTickAsync();
            Assert.True(result.Advanced);
            applied = result.Decisions.Where(decision => decision.ActionPlan is TownProjectWorkActionPlan plan &&
                    plan.Actor == actor && decision.Admission.Accepted && decision.PlanRefusal is null &&
                    result.Events.Any(item => hammer
                        ? item.Kind == "town_project_worked" && item.Detail.StartsWith(actor + ":" + plan.Choice.ProjectId + ":", StringComparison.Ordinal)
                        : plan.Harvest is { } harvest && item.Kind == "material_gathered" && item.Detail == actor + ":stone:" + harvest.Quantity))
                .Select(decision => (TownProjectWorkActionPlan)decision.ActionPlan!).FirstOrDefault();
        }
        Assert.NotNull(applied);
        Assert.Equal("borrowed-project-tool", hammer ? applied.Hammer!.ToolLotId : applied.Harvest!.ToolLotId);
        if (!hammer)
        {
            Assert.Equal(household, applied.ToolOwnerId);
            Assert.Equal("wooden_pickaxe", applied.ToolKind);
            Assert.NotNull(applied.Source);
            Assert.Contains(restored.Society.Inventory.Lots, lot => lot.OwnerId == actor && lot.ItemKind == "stone" &&
                PersonalEquipmentRules.IsCarried(lot, actor) && lot.Quantity == applied.Harvest!.Quantity);
        }
        AssertBorrowed(restored);
        Assert.True(restored.Society.Inventory.GetLot("borrowed-project-tool").ConditionBasisPoints < 10_000);
        restored.Pause();
        var after = WorldRuntimeCodec.Encode(restored.ExportState());
        using var reload = WorldRuntime.Restore(WorldRuntimeCodec.Decode(after), _ => provider);
        Assert.Equal(after, WorldRuntimeCodec.Encode(reload.ExportState()));

        void AssertBorrowed(WorldRuntime current)
        {
            var tool = current.Society.Inventory.GetLot("borrowed-project-tool");
            Assert.Equal(household, tool.OwnerId);
            Assert.Equal(actor, tool.CarrierId);
        }
    }

    private sealed class BorrowedToolChoice(string actor, bool hammer) : IDecisionProvider
    {
        public DecisionProviderKind Kind => DecisionProviderKind.LargeLanguageModel;
        public long ProviderEpoch => 1;

        public ValueTask<CognitionDecisionResponse> DecideAsync(CognitionDecisionRequest request, CancellationToken cancellationToken = default)
        {
            var observation = request.Observation;
            var selected = observation.AgentId == actor
                ? Assert.Single(observation.Candidates, candidate => candidate.ActionPlan is TownProjectWorkActionPlan plan &&
                    (hammer ? plan.Choice.ItemKind is null : plan.Choice.ItemKind == "stone")) : observation.Candidates.Single(candidate => candidate.Id == "safe_idle");
            return ValueTask.FromResult(new CognitionDecisionResponse(request.RequestId, observation.AgentId, Kind, ProviderEpoch,
                observation.RunEpoch, observation.DecisionGeneration, observation.ObservationDigest, selected.Id, 1,
                observation.Candidates.ToDictionary(candidate => candidate.Id, candidate => candidate.Id == selected.Id ? 1d : 0d, StringComparer.Ordinal)));
        }
    }
}
