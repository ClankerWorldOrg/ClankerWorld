namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private ICognitionCandidatesWorld CognitionCandidatesWorld => new CognitionCandidatesAdapter(this);

    private sealed class CognitionCandidatesAdapter(WorldRuntime runtime) : ICognitionCandidatesWorld
    {
        public global::System.Collections.Generic.List<global::ClankerWorld.Simulation.Cognition.CognitionCandidate> CreateRegisteredCandidates(string agentId, global::ClankerWorld.Simulation.World.AgentState state, bool restrictForOrder) => runtime.CreateRegisteredCandidates(agentId, state, restrictForOrder);
        public global::ClankerWorld.Simulation.World.BoatState? PassengerBoat(string actor) => TransportSystem.PassengerBoat(runtime.BoatWorld, actor);
        public global::ClankerWorld.Simulation.Cognition.CognitionCandidate WaitingCandidate(string actor, string text) => runtime.WaitingCandidate(actor, text);
        public int ComfortableFullness => WorldRuntime.ComfortableFullness;
        public void AddFoodPlanCandidate(global::System.Collections.Generic.List<global::ClankerWorld.Simulation.Cognition.CognitionCandidate> candidates, global::ClankerWorld.Simulation.World.FoodActionPlan? plan, string description, int priority, string? destination = null) => runtime.AddFoodPlanCandidate(candidates, plan, description, priority, destination);
        public global::ClankerWorld.Simulation.World.ConsumeFoodActionPlan? PlanFoodConsumption(string actor, global::ClankerWorld.Simulation.Kernel.InventoryLot? selected = null) => runtime.PlanFoodConsumption(actor, selected);
        public void AddMilkCandidate(global::System.Collections.Generic.List<global::ClankerWorld.Simulation.Cognition.CognitionCandidate> candidates, string actor) => runtime.AddMilkCandidate(candidates, actor);
        public global::ClankerWorld.Simulation.Cognition.AgentConversation? ConversationFor(string agentId) => runtime.ConversationFor(agentId);
        public global::System.Collections.Generic.List<global::ClankerWorld.Simulation.Cognition.CognitionCandidate> ConversationCandidates(string agentId) => runtime.ConversationCandidates(agentId);
        public void AddRegisteredChoices(global::ClankerWorld.Simulation.World.WorldRuntime.ChoiceGroup group, global::ClankerWorld.Simulation.World.WorldRuntime.ChoiceContext context) => runtime.AddRegisteredChoices(group, context);
        public bool IsAdult(string actor) => runtime.IsAdult(actor);
        public bool NeedsUrgentWarmth(global::ClankerWorld.Simulation.World.AgentState person) => runtime.NeedsUrgentWarmth(person);
        public bool IsChild(string actor) => runtime.IsChild(actor);
        public bool NeedsUrgentFood(global::ClankerWorld.Simulation.World.AgentState person) => WorldRuntime.NeedsUrgentFood(person);
        public global::ClankerWorld.Simulation.Society.SocietyCheckpoint Society => runtime.society.Checkpoint;
        public global::System.Collections.Generic.IEnumerable<global::ClankerWorld.Simulation.World.PlacedBuilding> WarehousesAccessibleTo(string actor) => runtime.WarehousesAccessibleTo(actor);
        public global::ClankerWorld.Simulation.World.OwnerQueuedInstruction? PendingInstructionFor(string agentId) => runtime.PendingInstructionFor(agentId);
        public global::System.Collections.Generic.List<global::ClankerWorld.Simulation.Cognition.CognitionCandidate> RestrictCandidatesForOrder(global::System.Collections.Generic.List<global::ClankerWorld.Simulation.Cognition.CognitionCandidate> candidates, global::ClankerWorld.Simulation.World.OwnerQueuedInstruction order, global::ClankerWorld.Simulation.World.AgentState state) => runtime.RestrictCandidatesForOrder(candidates, order, state);
        public global::ClankerWorld.Simulation.Cognition.CognitionCandidate? SelectOrderSurvivalCandidate(global::System.Collections.Generic.IEnumerable<global::ClankerWorld.Simulation.Cognition.CognitionCandidate> candidates, global::ClankerWorld.Simulation.World.AgentState person, global::ClankerWorld.Simulation.World.OwnerQueuedInstruction? instruction) => runtime.SelectOrderSurvivalCandidate(candidates, person, instruction);
        public bool TalkOrderHasConversation(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction) => runtime.TalkOrderHasConversation(instruction);
        public bool IsSurvivalCandidate(string actor, string candidateId) => runtime.IsSurvivalCandidate(actor, candidateId);
        public global::ClankerWorld.Simulation.Cognition.CognitionCandidate? OrderCandidateFor(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person) => runtime.OrderCandidateFor(instruction, person);
        public bool ShouldInterruptOrder(global::ClankerWorld.Simulation.World.AgentState person, global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.Cognition.CognitionCandidate? orderCandidate, global::ClankerWorld.Simulation.Cognition.CognitionCandidate? urgentCandidate) => runtime.ShouldInterruptOrder(person, instruction, orderCandidate, urgentCandidate);
        public bool IsShelterOrder(string action) => WorldRuntime.IsShelterOrder(action);
        public void AddHouseholdBuildingPlans(global::System.Collections.Generic.List<global::ClankerWorld.Simulation.Cognition.CognitionCandidate> candidates, global::ClankerWorld.Simulation.Society.SocietyAgent agent, bool needsWarmth, string householdId, global::System.Collections.Generic.List<string> buildingsWithoutSites) => runtime.AddHouseholdBuildingPlans(candidates, agent, needsWarmth, householdId, buildingsWithoutSites);
        public global::ClankerWorld.Simulation.World.WorldContentSimulationState Simulation => runtime.worldSimulation;
        public global::ClankerWorld.Simulation.Content.DeclarativeWorldContentState Content => runtime.worldContent;
        public bool IsGenericFoodRecipe(global::ClankerWorld.Simulation.Content.RecipeDefinition recipe) => WorldRuntime.IsGenericFoodRecipe(recipe);
        public bool IsHouseholdLayout(global::ClankerWorld.Simulation.Content.BuildingDefinition? building) => WorldRuntime.IsHouseholdLayout(building);
        public bool IsHandcartRecipe(global::ClankerWorld.Simulation.Content.RecipeDefinition recipe) => ProductionSystem.IsHandcartRecipe(recipe);
        public string ProductionOwnerFor(global::ClankerWorld.Simulation.World.PlacedBuilding? building, string workerId) => runtime.ProductionOwnerFor(building, workerId);
        public bool HasCarriedUnreservedQuantities(string actor, global::System.Collections.Generic.IReadOnlyList<global::ClankerWorld.Simulation.Content.ContentQuantity> quantities) => runtime.HasCarriedUnreservedQuantities(actor, quantities);
        public bool NeedsRecipeOutput(global::ClankerWorld.Simulation.Content.RecipeDefinition recipe, string? ownerId = null, string? requestWorker = null) => runtime.NeedsRecipeOutput(recipe, ownerId, requestWorker);
        public bool AnotherAgentWaitsForWorkSite(string actor, global::ClankerWorld.Simulation.Content.RecipeDefinition recipe) => runtime.AnotherAgentWaitsForWorkSite(actor, recipe);
        public bool CanAcquireProjectInputs(global::System.Collections.Generic.IReadOnlyList<global::ClankerWorld.Simulation.Content.ContentQuantity> inputs, string? ownerId = null, string? residentId = null, global::System.Collections.Generic.Dictionary<(global::ClankerWorld.Simulation.World.ToolFamily Family, int Tier), global::ClankerWorld.Simulation.World.ToolDefinition?>? reachableToolCache = null, global::System.Lazy<global::ClankerWorld.Simulation.World.PlacedBuilding[]>? accessibleWarehouses = null) => runtime.CanAcquireProjectInputs(inputs, ownerId, residentId, reachableToolCache, accessibleWarehouses);
        public bool TryFindRecipeSite(global::ClankerWorld.Simulation.Content.RecipeDefinition recipe, out string siteId, out global::ClankerWorld.Simulation.World.GridPoint position, string? actorId = null) => runtime.TryFindRecipeSite(recipe, out siteId, out position, actorId);
        public bool HasIngredientsAtBuilding(global::System.Collections.Generic.IReadOnlyList<global::ClankerWorld.Simulation.Content.ContentQuantity> inputs, string ownerId, string buildingId) => runtime.HasIngredientsAtBuilding(inputs, ownerId, buildingId);
        public global::ClankerWorld.Simulation.World.RecipeProjectActionPlan PlanRecipeProject(string actor, global::ClankerWorld.Simulation.Content.RecipeDefinition recipe, global::ClankerWorld.Simulation.World.PlacedBuilding workstation) => ProductionSystem.PlanRecipeProject(runtime.RecipeWorld, actor, recipe, workstation);
        public global::ClankerWorld.Simulation.World.ActionCheck CheckActionPlan(global::ClankerWorld.Simulation.World.ActionPlan plan) => runtime.CheckActionPlan(plan);
        public int OutdoorExposure(global::ClankerWorld.Simulation.World.GridPoint position) => runtime.OutdoorExposure(position);
        public global::ClankerWorld.Simulation.World.AnimalState? Animal(string id) => runtime.Animal(id);
        public global::System.Collections.Generic.IReadOnlyList<global::ClankerWorld.Simulation.World.TownRuntimeState> Towns => runtime.towns;
        public global::ClankerWorld.Simulation.World.WorldMap Map => runtime.map;
    }
}
