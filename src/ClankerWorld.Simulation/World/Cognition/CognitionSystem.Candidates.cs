using ChoiceContext = ClankerWorld.Simulation.World.WorldRuntime.ChoiceContext;
using ChoiceGroup = ClankerWorld.Simulation.World.WorldRuntime.ChoiceGroup;
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
    internal static List<CognitionCandidate> CreateCandidates(ICognitionCandidatesWorld world,
        string agentId,
        AgentState state,
        bool restrictForOrder = true) => world.CreateRegisteredCandidates(agentId, state, restrictForOrder);

    internal static List<CognitionCandidate> CreateUnfilteredCandidates(ICognitionCandidatesWorld world,
        string agentId, AgentState state, bool restrictForOrder)
    {
        if (world.PassengerBoat(agentId) is not null)
        {
            var aboard = new List<CognitionCandidate> { world.WaitingCandidate(agentId, "Stay aboard while the boat travels or waits for a safe Port.") };
            if (state.FullnessBasisPoints < world.ComfortableFullness)
                world.AddFoodPlanCandidate(aboard, world.PlanFoodConsumption(agentId), "Eat one carried food item aboard the boat.", 0);
            if (state.FullnessBasisPoints < world.ComfortableFullness) world.AddMilkCandidate(aboard, agentId);
            return aboard;
        }
        var currentConversation = world.ConversationFor(agentId);
        var candidates = currentConversation is null
            ? new List<CognitionCandidate>()
            : world.ConversationCandidates(agentId).ToList();
        if (currentConversation is not null &&
            (currentConversation.Status is AgentConversationStatus.Ready or AgentConversationStatus.AwaitingSpeaker or AgentConversationStatus.WrapUp ||
             currentConversation.Status == AgentConversationStatus.Proposed && currentConversation.InitiatorId == agentId))
        {
            if (candidates.Count == 0)
                candidates.Add(world.WaitingCandidate(agentId, "Continue safely without starting a new task."));
            return candidates;
        }
        var context = new ChoiceContext(candidates, agentId, state, currentConversation is not null);
        world.AddRegisteredChoices(ChoiceGroup.Prelude, context);
        if (world.IsAdult(agentId)) world.AddRegisteredChoices(ChoiceGroup.Adult, context);
        if (!world.NeedsUrgentWarmth(state) && world.IsChild(agentId)) world.AddRegisteredChoices(ChoiceGroup.Child, context);
        if (!world.NeedsUrgentFood(state) && world.IsAdult(agentId))
        {
            var agent = world.Society.GetAgent(agentId);
            var reachableToolCache = new Dictionary<(ToolFamily Family, int Tier), ToolDefinition?>();
            var accessibleWarehouses = new Lazy<PlacedBuilding[]>(() => world.WarehousesAccessibleTo(agentId).ToArray(),
                LazyThreadSafetyMode.None);
            var buildingsWithoutSites = new List<string>();
            world.AddRegisteredChoices(ChoiceGroup.UnhurriedAdult, context with
            {
                Agent = agent,
                ReachableTools = reachableToolCache,
                AccessibleWarehouses = accessibleWarehouses,
                BuildingsWithoutSites = buildingsWithoutSites,
            });
        }
        if (world.NeedsUrgentFood(state) && world.IsAdult(agentId)) world.AddRegisteredChoices(ChoiceGroup.UrgentAdult, context);
        world.AddRegisteredChoices(ChoiceGroup.Final, context);
        if (restrictForOrder && world.PendingInstructionFor(agentId) is { } order)
            return world.RestrictCandidatesForOrder(candidates, order, state);
        return candidates;
    }


    internal static List<CognitionCandidate> RestrictCandidatesForOrder(ICognitionCandidatesWorld world,
        List<CognitionCandidate> candidates,
        OwnerQueuedInstruction order,
        AgentState state)
    {
        var safeIdle = candidates.Single(item => item.Id == "safe_idle");
        var urgent = world.NeedsUrgentFood(state) || world.NeedsUrgentWarmth(state);
        var urgentCandidate = urgent
            ? world.SelectOrderSurvivalCandidate(candidates, state, order)
            : null;
        if (world.TalkOrderHasConversation(order))
            return candidates.Where(item => item.Id == "safe_idle" || item.Id.StartsWith("conversation_", StringComparison.Ordinal) ||
                urgent && world.IsSurvivalCandidate(state.AgentId, item.Id)).ToList();
        var taskCandidate = world.OrderCandidateFor(order, state);
        if (world.ShouldInterruptOrder(state, order, taskCandidate, urgentCandidate))
            taskCandidate = null;
        if (taskCandidate is null)
        {
            return candidates.Where(item => item.Id == "safe_idle" || urgent && world.IsSurvivalCandidate(state.AgentId, item.Id))
                .ToList();
        }

        var selected = candidates.Where(item => item.Id == taskCandidate.Id ||
            urgent && world.IsSurvivalCandidate(state.AgentId, item.Id)).ToList();
        if (world.IsShelterOrder(order.Task!.Action))
            selected.RemoveAll(item => item.Id == taskCandidate.Id);
        if (selected.All(item => item.Id != taskCandidate.Id)) selected.Add(taskCandidate);
        if (selected.All(item => item.Id != safeIdle.Id)) selected.Add(safeIdle);
        return selected;
    }


    internal static void AddBuildCandidates(ICognitionCandidatesWorld world,
        List<CognitionCandidate> candidates,
        SocietyAgent agent,
        AgentState state,
        Dictionary<(ToolFamily Family, int Tier), ToolDefinition?> reachableToolCache,
        Lazy<PlacedBuilding[]> accessibleWarehouses,
        List<string> buildingsWithoutSites)
    {
        var needsWarmth = world.NeedsUrgentWarmth(state);
        if (agent.HouseholdId is { } planningHousehold)
            world.AddHouseholdBuildingPlans(candidates, agent, needsWarmth, planningHousehold, buildingsWithoutSites);

        // Work follows what the household holds, not a role: crops need the
        // household's Farmhouse, and workstation recipes need a building the
        // household holds or a communal one (see TryFindRecipeSite).
        var placedWorkstations = world.Simulation.Buildings.Select(building => building.DefinitionId).ToHashSet(StringComparer.Ordinal);
        foreach (var recipe in world.Content.Recipes.Where(item =>
                     !item.Outputs.Any(output => output.ResourceId == "bedding") &&
                     !item.IsCrop && !world.IsGenericFoodRecipe(item) &&
                     (!needsWarmth || item.Outputs.Any(output => PersonalEquipmentRules.IsGarment(output.ResourceId))) &&
                     item.WorkstationBuildingId is { } placedId && placedWorkstations.Contains(placedId)))
        {
            var householdWorkstation = recipe.WorkstationBuildingId is { } workstationId &&
                world.Content.BuildingDefinitions.TryGetValue(workstationId, out var workstation) && world.IsHouseholdLayout(workstation);
            var personalCart = world.IsHandcartRecipe(recipe);
            var recipeOwner = personalCart ? agent.Id : world.ProductionOwnerFor(null, agent.Id);
            if (personalCart && (!world.HasCarriedUnreservedQuantities(agent.Id, recipe.Inputs) ||
                world.Society.Inventory.Lots.Any(lot => lot.ItemKind == InventoryContainerRules.Handcart && lot.OwnerId == agent.Id)))
                continue;
            if (!world.NeedsRecipeOutput(recipe, recipeOwner, agent.Id) || world.AnotherAgentWaitsForWorkSite(agent.Id, recipe) ||
                !world.CanAcquireProjectInputs(recipe.Inputs, recipeOwner, agent.Id, reachableToolCache, accessibleWarehouses) ||
                !world.TryFindRecipeSite(recipe, out var siteId, out var position, agent.Id) ||
                householdWorkstation && !personalCart &&
                (recipeOwner is null || !world.HasIngredientsAtBuilding(recipe.Inputs, recipeOwner, siteId)))
            {
                continue;
            }

            var plan = world.PlanRecipeProject(agent.Id, recipe, world.Simulation.Buildings.Single(item => item.InstanceId == siteId));
            if (world.CheckActionPlan(plan).Allowed) candidates.Add(new CognitionCandidate(
                plan.CandidateId,
                $"{recipe.DisplayName} at the household work site.",
                recipe.Tags.Contains("named-meal", StringComparer.Ordinal) ? 20 : recipe.IsCrop ? 20 :
                    HouseToolsContent.IsCrudeToolRecipe(recipe) ? 23 :
                    world.OutdoorExposure(state.Position) > 0 && recipe.Outputs.Any(output => PersonalEquipmentRules.IsGarment(output.ResourceId)) ? 25 : 30,
                IdentityRules.PrefixedReference("build-site:", $"{position.X},{position.Y}"))
            { ActionPlan = plan });
        }
    }


    internal static string? DestinationNameForModel(ICognitionCandidatesWorld world, string? id)
    {
        if (id is null) return null;
        return world.Society.Households.SingleOrDefault(item => item.Id == id)?.Name ??
            world.Animal(id)?.Name ??
            world.Towns.SingleOrDefault(item => item.Id == id)?.Name ??
            world.Society.Agents.SingleOrDefault(item => item.Id == id)?.Name ??
            world.Map.Resources.SingleOrDefault(item => item.Id == id)?.Kind.Replace('_', ' ');
    }


    internal static int PriorityFor(ICognitionCandidatesWorld world, AgentState state) => world.NeedsUrgentFood(state) || world.NeedsUrgentWarmth(state) ? 20 : 0;
}
