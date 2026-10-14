using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.World;

namespace ClankerWorld.Tests;

public sealed partial class ToolMakingRequestTests
{
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
        Assert.True((await world.AdvanceOneTickAsync()).Advanced);
        var recipe = Assert.Single(world.WorldContent.Recipes, item => item.PackageDigest == package.PackageDigest);
        world.SubmitInstruction(new("native-recipe", "owner:test", smith, OwnerInstructionKind.Suggestion, "Consider crafting tools."));
        var plans = new List<RecipeProjectActionPlan>();
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
