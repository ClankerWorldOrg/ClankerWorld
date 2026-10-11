using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Playtest;

namespace ClankerWorld.Simulation.Tests;

internal static class ContentIdFixture
{
    internal static ContentPackageManifest Package(string workstation, string kind, string localId = "resource")
    {
        const string packageId = "content-id-boundary";
        var version = ContentVersion.Parse("1.0.0");
        var digest = "sha256:" + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(packageId)));
        var recipe = new RecipeDefinition(digest, localId, version, "Make mod goods",
            [new("wood", 1)], [new(kind, 1)], 1, workstation, []);
        var definition = new ContentDefinition(RecipeDefinition.SchemaKind, localId, version, recipe.DisplayName,
            recipe.PayloadDigest, JsonSerializer.Serialize(new
            {
                schema = "recipe/v1",
                recipe.Inputs,
                recipe.Outputs,
                recipe.DurationTicks,
                recipe.WorkstationBuildingId,
                recipe.Tags,
            }, JsonSerializerOptions.Web));
        return new(packageId, version, digest, [], [definition], []);
    }

    internal static async Task<PrivateWorldRuntimeState> Produce(PrivateWorldRuntimeState state,
        string buildingId, string worker, string kind)
    {
        var building = state.WorldSimulation!.Buildings.Single(item => item.InstanceId == buildingId);
        // Controlled input and worker proximity; package activation and output production are native.
        var inventory = InventoryFixture.AddLot(state.Society.Society.Inventory, "mod-production-input",
            "wood", building.HouseholdId!, 1, storageBuildingId: buildingId);
        state = state with
        {
            Society = state.Society with { Society = state.Society.Society with { Inventory = inventory } },
            Inhabitants = state.Inhabitants.Select(person => person.InhabitantId == worker
                ? person with { Position = building.Position, Project = null, LastDecisionContext = null } : person).ToArray(),
        };
        using var world = PrivateWorldRuntime.Restore(state, _ => new Idle());
        var package = Package(building.DefinitionId, kind);
        world.ProposeContent(package);
        var resolution = world.ResolveContent(package.PackageId);
        Assert.True(resolution.IsSuccess, resolution.Diagnostic);
        world.ValidateContent(package.PackageId, resolution);
        world.ApproveContent(package.PackageId);
        world.StageContent(package.PackageId);
        Assert.True((await world.AdvanceOneTickAsync()).Advanced);
        var recipe = Assert.Single(world.WorldContent.Recipes, item => item.PackageDigest == package.PackageDigest);
        var started = world.StartProduction(recipe.CanonicalId, buildingId, worker);
        Assert.True(started.Applied, started.Failure);
        for (var tick = 0; tick < 8 && !world.Society.Inventory.Lots.Any(lot => lot.ItemKind == kind); tick++)
            Assert.True((await world.AdvanceOneTickAsync()).Advanced);
        Assert.Single(world.Society.Inventory.Lots, lot => lot.ItemKind == kind);
        return Saved(world);
    }

    internal static PrivateWorldRuntimeState Saved(PrivateWorldRuntime world)
    {
        world.Validate();
        var bytes = PrivateWorldRuntimeCodec.Encode(world.ExportState());
        var state = PrivateWorldRuntimeCodec.Decode(bytes);
        Assert.Equal(bytes, PrivateWorldRuntimeCodec.Encode(state));
        return state;
    }

    internal sealed class Idle(DecisionProviderKind kind = DecisionProviderKind.LargeLanguageModel) : IDecisionProvider
    {
        public DecisionProviderKind Kind => kind;
        public long ProviderEpoch => 1;
        public ValueTask<CognitionDecisionResponse> DecideAsync(CognitionDecisionRequest request, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(new CognitionDecisionResponse(request.RequestId, request.Observation.InhabitantId, Kind,
                request.ProviderEpoch, request.Observation.RunEpoch, request.Observation.DecisionGeneration,
                request.Observation.ObservationDigest, "safe_idle", 1, new Dictionary<string, double> { ["safe_idle"] = 1 }));
    }
}
