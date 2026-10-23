namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private ICognitionCandidatesWorld CognitionCandidatesWorld => new CognitionCandidatesAdapter(this);

    private sealed class CognitionCandidatesAdapter(WorldRuntime runtime) : AgentRuntimeAdapter(runtime), ICognitionCandidatesWorld
    {
        public global::System.Collections.Generic.List<global::ClankerWorld.Simulation.Cognition.CognitionCandidate> CreateRegisteredCandidates(string agentId, global::ClankerWorld.Simulation.World.AgentState state, bool restrictForOrder) => Runtime.CreateRegisteredCandidates(agentId, state, restrictForOrder);
        public global::ClankerWorld.Simulation.World.BoatState? PassengerBoat(string actor) => Runtime.PassengerBoat(actor);
        public global::ClankerWorld.Simulation.Cognition.CognitionCandidate WaitingCandidate(string actor, string text) => Runtime.WaitingCandidate(actor, text);
        public int ComfortableFullness => WorldRuntime.ComfortableFullness;
        public void AddFoodPlanCandidate(global::System.Collections.Generic.List<global::ClankerWorld.Simulation.Cognition.CognitionCandidate> candidates, global::ClankerWorld.Simulation.World.FoodActionPlan? plan, string description, int priority, string? destination = null) => Runtime.AddFoodPlanCandidate(candidates, plan, description, priority, destination);
        public global::ClankerWorld.Simulation.World.ConsumeFoodActionPlan? PlanFoodConsumption(string actor, global::ClankerWorld.Simulation.Kernel.InventoryLot? selected = null) => Runtime.PlanFoodConsumption(actor, selected);
        public void AddMilkCandidate(global::System.Collections.Generic.List<global::ClankerWorld.Simulation.Cognition.CognitionCandidate> candidates, string actor) => Runtime.AddMilkCandidate(candidates, actor);
        public global::ClankerWorld.Simulation.Cognition.AgentConversation? ConversationFor(string agentId) => Runtime.ConversationFor(agentId);
        public global::System.Collections.Generic.List<global::ClankerWorld.Simulation.Cognition.CognitionCandidate> ConversationCandidates(string agentId) => Runtime.ConversationCandidates(agentId);
        public void AddRegisteredChoices(global::ClankerWorld.Simulation.World.WorldRuntime.ChoiceGroup group, global::ClankerWorld.Simulation.World.WorldRuntime.ChoiceContext context) => Runtime.AddRegisteredChoices(group, context);
        public bool IsAdult(string actor) => Runtime.IsAdult(actor);
        public bool NeedsUrgentFood(global::ClankerWorld.Simulation.World.AgentState person) => WorldRuntime.NeedsUrgentFood(person);
        public global::System.Collections.Generic.IEnumerable<global::ClankerWorld.Simulation.World.PlacedBuilding> WarehousesAccessibleTo(string actor) => Runtime.WarehousesAccessibleTo(actor);
        public global::ClankerWorld.Simulation.World.OwnerQueuedInstruction? PendingInstructionFor(string agentId) => Runtime.PendingInstructionFor(agentId);
        public global::System.Collections.Generic.List<global::ClankerWorld.Simulation.Cognition.CognitionCandidate> RestrictCandidatesForOrder(global::System.Collections.Generic.List<global::ClankerWorld.Simulation.Cognition.CognitionCandidate> candidates, global::ClankerWorld.Simulation.World.OwnerQueuedInstruction order, global::ClankerWorld.Simulation.World.AgentState state) => Runtime.RestrictCandidatesForOrder(candidates, order, state);
        public global::ClankerWorld.Simulation.Cognition.CognitionCandidate? SelectOrderSurvivalCandidate(global::System.Collections.Generic.IEnumerable<global::ClankerWorld.Simulation.Cognition.CognitionCandidate> candidates, global::ClankerWorld.Simulation.World.AgentState person, global::ClankerWorld.Simulation.World.OwnerQueuedInstruction? instruction) => Runtime.SelectOrderSurvivalCandidate(candidates, person, instruction);
        public bool IsSurvivalCandidate(string actor, string candidateId) => Runtime.IsSurvivalCandidate(actor, candidateId);
        public bool IsShelterOrder(string action) => WorldRuntime.IsShelterOrder(action);
        public void AddHouseholdBuildingPlans(global::System.Collections.Generic.List<global::ClankerWorld.Simulation.Cognition.CognitionCandidate> candidates, global::ClankerWorld.Simulation.Society.SocietyAgent agent, bool needsWarmth, string householdId, global::System.Collections.Generic.List<string> buildingsWithoutSites) => Runtime.AddHouseholdBuildingPlans(candidates, agent, needsWarmth, householdId, buildingsWithoutSites);
        public global::ClankerWorld.Simulation.World.WorldContentSimulationState Simulation => Runtime.worldSimulation;
        public bool IsGenericFoodRecipe(global::ClankerWorld.Simulation.Content.RecipeDefinition recipe) => WorldRuntime.IsGenericFoodRecipe(recipe);
        public bool IsHouseholdLayout(global::ClankerWorld.Simulation.Content.BuildingDefinition? building) => WorldRuntime.IsHouseholdLayout(building);
        public bool IsHandcartRecipe(global::ClankerWorld.Simulation.Content.RecipeDefinition recipe) => ProductionSystem.IsHandcartRecipe(recipe);
        public string ProductionOwnerFor(global::ClankerWorld.Simulation.World.PlacedBuilding? building, string workerId) => Runtime.ProductionOwnerFor(building, workerId);
        public bool HasCarriedUnreservedQuantities(string actor, global::System.Collections.Generic.IReadOnlyList<global::ClankerWorld.Simulation.Content.ContentQuantity> quantities) => Runtime.HasCarriedUnreservedQuantities(actor, quantities);
        public bool NeedsRecipeOutput(global::ClankerWorld.Simulation.Content.RecipeDefinition recipe, string? ownerId = null, string? requestWorker = null) => Runtime.NeedsRecipeOutput(recipe, ownerId, requestWorker);
        public bool AnotherAgentWaitsForWorkSite(string actor, global::ClankerWorld.Simulation.Content.RecipeDefinition recipe) => Runtime.AnotherAgentWaitsForWorkSite(actor, recipe);
        public bool CanAcquireProjectInputs(global::System.Collections.Generic.IReadOnlyList<global::ClankerWorld.Simulation.Content.ContentQuantity> inputs, string? ownerId = null, string? residentId = null, global::System.Collections.Generic.Dictionary<(global::ClankerWorld.Simulation.World.ToolFamily Family, int Tier), global::ClankerWorld.Simulation.World.ToolDefinition?>? reachableToolCache = null, global::System.Lazy<global::ClankerWorld.Simulation.World.PlacedBuilding[]>? accessibleWarehouses = null) => Runtime.CanAcquireProjectInputs(inputs, ownerId, residentId, reachableToolCache, accessibleWarehouses);
        public bool HasIngredientsAtBuilding(global::System.Collections.Generic.IReadOnlyList<global::ClankerWorld.Simulation.Content.ContentQuantity> inputs, string ownerId, string buildingId) => Runtime.HasIngredientsAtBuilding(inputs, ownerId, buildingId);
        public global::ClankerWorld.Simulation.World.RecipeProjectActionPlan PlanRecipeProject(string actor, global::ClankerWorld.Simulation.Content.RecipeDefinition recipe, global::ClankerWorld.Simulation.World.PlacedBuilding workstation) => ProductionSystem.PlanRecipeProject(Runtime.RecipeWorld, actor, recipe, workstation);
        public int OutdoorExposure(global::ClankerWorld.Simulation.World.GridPoint position) => Runtime.OutdoorExposure(position);
        public global::ClankerWorld.Simulation.World.AnimalState? Animal(string id) => Runtime.Animal(id);
    }
}
