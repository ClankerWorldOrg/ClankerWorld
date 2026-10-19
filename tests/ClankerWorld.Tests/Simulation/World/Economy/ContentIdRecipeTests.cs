using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.World;

namespace ClankerWorld.Tests;

public sealed partial class ToolMakingRequestTests
{
    [Fact]
    public async Task HandcartRecipeOffersNoticeWorkstationRemovalAndStayAbsentAfterReload()
    {
        var (state, _, smith, shop) = Prepared();
        var inventory = state.Society.Society.Inventory with
        { Lots = state.Society.Society.Inventory.Lots.Where(lot => lot.StorageBuildingId != shop.InstanceId).ToArray() };
        foreach (var (kind, quantity) in new[] { ("wood", 4), ("iron_fittings", 2), ("rope", 1) })
            inventory = InventoryAuthority.AddLot(inventory, "recipe-cart-" + kind, kind, smith, quantity);
        state = state with
        {
            Society = state.Society with { Society = state.Society.Society with { Inventory = inventory } },
            JevEnabled = false,
            RoutineHelper = RoutineHelperSettings.Off,
        };
        var choices = new RequestChoices(DecisionProviderKind.Deterministic);
        using var world = WorldRuntime.Restore(state,
            actor => actor == smith ? choices : new RequestChoices(DecisionProviderKind.Deterministic));
        var recipe = world.WorldContent.Recipes.Single(item => item.LocalId == "handcart");
        var candidateId = "build:recipe:" + recipe.CanonicalId;
        await Refresh("recipe-before-removal");
        Assert.Contains(choices.Offered, candidate => candidate.Id == candidateId);
        var removed = world.RemoveBuilding(shop.InstanceId, shop.TownId, shop.HouseholdId);
        Assert.True(removed.Applied, removed.Failure);
        await Refresh("recipe-after-removal");
        Assert.DoesNotContain(choices.Offered, candidate => candidate.Id == candidateId);

        var bytes = WorldRuntimeCodec.Encode(world.ExportState());
        var reloadedChoices = new RequestChoices(DecisionProviderKind.Deterministic);
        using var reloaded = WorldRuntime.Restore(WorldRuntimeCodec.Decode(bytes),
            actor => actor == smith ? reloadedChoices : new RequestChoices(DecisionProviderKind.Deterministic));
        using var replay = WorldRuntime.Restore(WorldRuntimeCodec.Decode(bytes),
            _ => new RequestChoices(DecisionProviderKind.Deterministic));
        foreach (var runtime in new[] { reloaded, replay })
            runtime.SubmitInstruction(new("recipe-reloaded-refresh", "owner:planning-test", smith,
                OwnerInstructionKind.Suggestion, "Consider crafting a cart."));
        for (var tick = 0; tick < 8; tick++)
        {
            Assert.True((await reloaded.AdvanceOneTickAsync()).Advanced);
            Assert.True((await replay.AdvanceOneTickAsync()).Advanced);
            Assert.Equal(WorldRuntimeCodec.Encode(reloaded.ExportState()), WorldRuntimeCodec.Encode(replay.ExportState()));
        }
        Assert.NotEmpty(reloadedChoices.Offered);
        Assert.DoesNotContain(reloadedChoices.Offered, candidate => candidate.Id == candidateId);

        async Task Refresh(string instructionId)
        {
            choices.Offered.Clear();
            world.SubmitInstruction(new(instructionId, "owner:planning-test", smith,
                OwnerInstructionKind.Suggestion, "Consider crafting a cart."));
            for (var tick = 0; tick < 8; tick++) Assert.True((await world.AdvanceOneTickAsync()).Advanced);
            Assert.NotEmpty(choices.Offered);
        }
    }

    [Theory]
    [InlineData(480)]
    [InlineData(481)]
    [InlineData(512)]
    public async Task AdmittedRecipeStartsItsNativeProjectAcrossReload(int length)
    {
        var (state, buyer, smith, shop) = Prepared();
        var shortPackage = ContentIdFixture.Package(shop.DefinitionId, "stone");
        var overhead = shortPackage.Definitions[0].CanonicalId(shortPackage.PackageDigest).Length - shortPackage.Definitions[0].LocalId.Length;
        var package = ContentIdFixture.Package(shop.DefinitionId, "stone", new string('a', length - overhead));
        var choices = new RequestChoices("build:recipe:" + package.Definitions[0].CanonicalId(package.PackageDigest));
        using var world = Restore(state, buyer, smith, new RequestChoices(), choices);
        world.ProposeContent(package);
        var resolution = world.ResolveContent(package.PackageId);
        Assert.True(resolution.IsSuccess, resolution.Diagnostic);
        world.ValidateContent(package.PackageId, resolution);
        world.ApproveContent(package.PackageId);
        world.StageContent(package.PackageId);
        var plans = new List<RecipeProjectActionPlan>();
        var activation = await world.AdvanceOneTickAsync();
        Assert.True(activation.Advanced);
        plans.AddRange(activation.Decisions.Where(decision => decision.ActionPlan is RecipeProjectActionPlan plan && plan.Actor == smith &&
                plan.Recipe.PackageDigest == package.PackageDigest && decision.Admission.Accepted && decision.PlanRefusal is null)
            .Select(decision => Assert.IsType<RecipeProjectActionPlan>(decision.ActionPlan)));
        var recipe = Assert.Single(world.WorldContent.Recipes, item => item.PackageDigest == package.PackageDigest);
        world.SubmitInstruction(new("native-recipe", "owner:test", smith, OwnerInstructionKind.Suggestion, "Consider crafting tools."));
        for (var tick = 0; tick < 8 && world.ExportState().Agents.Single(person => person.AgentId == smith).Project is null; tick++)
        {
            var result = await world.AdvanceOneTickAsync();
            Assert.True(result.Advanced);
            plans.AddRange(result.Decisions.Where(decision => decision.ActionPlan is RecipeProjectActionPlan && decision.Admission.Accepted && decision.PlanRefusal is null)
                .Select(decision => Assert.IsType<RecipeProjectActionPlan>(decision.ActionPlan)));
        }
        Assert.Contains(choices.Offered, candidate => candidate.Id == "build:recipe:" + recipe.CanonicalId);
        var project = world.ExportState().Agents.Single(person => person.AgentId == smith).Project;
        Assert.NotNull(project);
        Assert.Equal("build:recipe:" + recipe.CanonicalId, project.CandidateId);
        var plan = Assert.Single(plans);
        Assert.Equal((recipe.CanonicalId, shop.InstanceId, shop.HouseholdId), (plan.Recipe.CanonicalId, plan.Workstation.InstanceId, plan.OwnerId));
        Assert.Equal(shop.InstanceId, plan.RequiredStorageBuildingId);
        Assert.False(plan.RequireCarried);
        var plannedInput = Assert.Single(plan.Inputs!);
        Assert.Equal(("smith-input", 1), (plannedInput.LotId, plannedInput.Quantity));
        for (var tick = 0; tick < 64 && !world.WorldSimulation.ProductionJobs.Any(job => job.WorkerId == smith && job.RecipeId == recipe.CanonicalId); tick++)
            Assert.True((await world.AdvanceOneTickAsync()).Advanced);
        var job = Assert.Single(world.WorldSimulation.ProductionJobs, job => job.WorkerId == smith && job.RecipeId == recipe.CanonicalId);
        Assert.Equal(plan.Workstation.InstanceId, job.BuildingInstanceId);
        Assert.NotNull(plan.Inputs);
        Assert.Equal(plan.Inputs.Select(input => (input.LotId, input.Quantity)), job.InputReservationIds
            .Select(id => world.Society.Inventory.GetReservation(id)).Select(input => (input.LotId, input.Quantity)));
        Assert.False(world.CheckActionPlan(plan).Allowed);
        world.Pause();
        var saved = Roundtrip(world);
        using var continuing = WorldRuntime.Restore(saved, _ => new RequestChoices(DecisionProviderKind.Deterministic));
        using var reloaded = WorldRuntime.Restore(saved, _ => new RequestChoices(DecisionProviderKind.Deterministic));
        continuing.Resume();
        reloaded.Resume();
        for (var tick = 0; tick < 3; tick++)
        {
            Assert.True((await continuing.AdvanceOneTickAsync()).Advanced);
            Assert.True((await reloaded.AdvanceOneTickAsync()).Advanced);
            Assert.Equal(WorldRuntimeCodec.Encode(continuing.ExportState()), WorldRuntimeCodec.Encode(reloaded.ExportState()));
        }
    }
}
