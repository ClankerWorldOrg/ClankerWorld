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
        world.SubmitInstruction(new("native-recipe", "owner:test", smith, OwnerInstructionKind.Suggestive, "Consider crafting tools."));
        for (var tick = 0; tick < 8 && world.ExportState().Inhabitants.Single(person => person.InhabitantId == smith).Project is null; tick++)
            Assert.True((await world.AdvanceOneTickAsync()).Advanced);
        Assert.Contains(choices.Offered, candidate => candidate.Id == "build:recipe:" + recipe.CanonicalId);
        var project = world.ExportState().Inhabitants.Single(person => person.InhabitantId == smith).Project;
        Assert.NotNull(project);
        Assert.Equal("build:recipe:" + recipe.CanonicalId, project.CandidateId);
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
