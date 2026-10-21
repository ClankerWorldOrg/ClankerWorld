using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;
using System.Collections.ObjectModel;
using System.Security.Cryptography;
using System.Text.Json.Serialization;
using System.Text.Json;
using System.Text;

namespace ClankerWorld.Simulation.World;

internal sealed partial class CognitionSystem
{
    internal static void ApplyBuildDecision(ICognitionConstructionWorld world,
        string agentId,
        AgentState state,
        string candidateId)
    {
        if (!TownConstructionCandidateIds.TryParse(candidateId, out var selection))
        {
            world.AppendEvent(new BuildRejectedEventPayload(agentId, candidateId, "unknown_target"));
            return;
        }

        if (selection.IsBuilding)
        {
            var buildingPlan = state.Project is { } constructionProject ? world.RetainedConstruction(agentId, constructionProject) : null;
            if (buildingPlan is not null && world.CheckConstruction(buildingPlan).Refusal is { } refusal)
            { world.RefuseChoice(buildingPlan, refusal); return; }
            var definition = world.Content.Buildings.SingleOrDefault(item => item.CanonicalId == selection.DefinitionId);
            var layout = world.CreateTownLayoutContext(agentId, selection.SitePosition, definition);
            TownConstructionSiteCandidate? rankedSite = null;
            if (definition is not null)
            {
                if (selection.SitePosition is { } selectedSite)
                    TownLayoutService.TryEvaluateConstructionSite(layout, definition, selectedSite, out rankedSite);
                else
                {
                    var sites = TownLayoutService.RankConstructionSites(layout, definition);
                    rankedSite = sites.Count == 0 ? null : sites[0];
                }
            }

            if (definition is null || rankedSite is null)
            {
                world.AppendEvent(new BuildRejectedEventPayload(agentId, candidateId, "selected_site_no_longer_legal"));
                if (selection.SitePosition is { } rejectedSite && definition is not null)
                    world.AppendEvent(new TownLayoutSiteRejectedEventPayload(agentId, world.TownForResident(agentId) ?? "none", definition.CanonicalId, rejectedSite.X, rejectedSite.Y));
                return;
            }
            var householdProperty = definition.Tags.Any(world.IsHouseholdBuildingTag);
            var houseOwner = householdProperty
                ? world.Society.GetAgent(agentId).HouseholdId : null;
            if (householdProperty && houseOwner is null)
            {
                world.AppendEvent(new BuildRejectedEventPayload(agentId, candidateId, "household_required"));
                return;
            }
            if (definition.Tags.Contains("house", StringComparer.Ordinal) && houseOwner is not null &&
                world.HouseForHousehold(houseOwner) is not null)
            {
                world.AppendEvent(new BuildRejectedEventPayload(agentId, candidateId, "existing_house"));
                return;
            }

            var position = rankedSite.Position;
            if (state.Position != position)
            {
                world.MoveToward(agentId, state, position, "build");
                return;
            }

            var constructionOrder = world.ConstructionInstructionForProject(agentId, state.Project);
            if (state.Project?.OrderInstructionId is not null && (constructionOrder is null ||
                state.Project.WorkDone < world.ProjectWorkTicks ||
                !world.TryConstructionOrderSite(constructionOrder, state, out _, out _, out _, out _)))
            {
                world.AppendEvent(new BuildRejectedEventPayload(agentId, candidateId, "ordered_project_changed"));
                return;
            }
            var materialOwner = buildingPlan?.MaterialOwnerId ?? world.BuildingConstructionOwner(agentId, definition);
            var placement = world.PlaceBuildingCore(
                buildingPlan?.InstanceId ?? constructionOrder?.Task!.ConstructionInstanceId ?? world.BuildInstanceId(agentId, definition),
                definition.CanonicalId,
                position,
                "build_completed",
                world.TownForResident(agentId),
                houseOwner,
                materialOwner,
                constructionOrder?.InstructionId, buildingPlan?.Inputs);
            if (!placement.Applied)
            {
                world.AppendEvent(new BuildRejectedEventPayload(agentId, candidateId, placement.Failure));
            }
            else
            {
                world.CreditCompletedWork(agentId, "building");
                if (constructionOrder is not null)
                    world.RecordConstructionOrderCompletion(constructionOrder, materialOwner);
            }

            return;
        }

        var recipeId = selection.DefinitionId;
        var recipe = world.Content.Recipes.SingleOrDefault(item => item.CanonicalId == recipeId);
        var plan = state.Project is { } recipeProject ? world.RetainedRecipeProject(agentId, recipeProject) : null;
        var siteId = plan?.Workstation.InstanceId;
        var sitePosition = plan?.Workstation.Position ?? default;
        if (recipe is null || plan is null && !world.TryFindRecipeSite(recipe, out siteId, out sitePosition, agentId))
        {
            world.AppendEvent(new BuildRejectedEventPayload(agentId, candidateId, "no_valid_site"));
            return;
        }

        if (state.Position != sitePosition)
        {
            world.MoveToward(agentId, state, sitePosition, "build");
            return;
        }

        var productionInstruction = world.PendingInstructionFor(agentId);
        var orderInstructionId = productionInstruction is not null &&
            world.IsProductionOrderProject(productionInstruction, state.Project) &&
            state.Project?.WorkDone == world.ProjectWorkTicks && productionInstruction.Task!.ProductionBuildingId == siteId
                ? productionInstruction.InstructionId : null;
        var started = world.StartProductionCore(recipe.CanonicalId, siteId!, agentId, "build_started", orderInstructionId, plan);
        if (!started.Applied)
        {
            world.AppendEvent(new BuildRejectedEventPayload(agentId, candidateId, started.Failure));
        }
    }

}
