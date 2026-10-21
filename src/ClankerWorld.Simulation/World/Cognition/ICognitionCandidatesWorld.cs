namespace ClankerWorld.Simulation.World;

internal interface ICognitionCandidatesWorld
{
    global::System.Collections.Generic.List<global::ClankerWorld.Simulation.Cognition.CognitionCandidate> CreateRegisteredCandidates(string agentId, global::ClankerWorld.Simulation.World.AgentState state, bool restrictForOrder);
    global::ClankerWorld.Simulation.World.BoatState? PassengerBoat(string actor);
    global::ClankerWorld.Simulation.Cognition.CognitionCandidate WaitingCandidate(string actor, string text);
    int ComfortableFullness { get; }
    void AddFoodPlanCandidate(global::System.Collections.Generic.List<global::ClankerWorld.Simulation.Cognition.CognitionCandidate> candidates, global::ClankerWorld.Simulation.World.FoodActionPlan? plan, string description, int priority, string? destination = null);
    global::ClankerWorld.Simulation.World.ConsumeFoodActionPlan? PlanFoodConsumption(string actor, global::ClankerWorld.Simulation.Kernel.InventoryLot? selected = null);
    void AddMilkCandidate(global::System.Collections.Generic.List<global::ClankerWorld.Simulation.Cognition.CognitionCandidate> candidates, string actor);
    global::ClankerWorld.Simulation.Cognition.AgentConversation? ConversationFor(string agentId);
    global::System.Collections.Generic.List<global::ClankerWorld.Simulation.Cognition.CognitionCandidate> ConversationCandidates(string agentId);
    void AddRegisteredChoices(global::ClankerWorld.Simulation.World.WorldRuntime.ChoiceGroup group, global::ClankerWorld.Simulation.World.WorldRuntime.ChoiceContext context);
    bool IsAdult(string actor);
    bool NeedsUrgentWarmth(global::ClankerWorld.Simulation.World.AgentState person);
    bool IsChild(string actor);
    bool NeedsUrgentFood(global::ClankerWorld.Simulation.World.AgentState person);
    global::ClankerWorld.Simulation.Society.SocietyCheckpoint Society { get; }
    global::System.Collections.Generic.IEnumerable<global::ClankerWorld.Simulation.World.PlacedBuilding> WarehousesAccessibleTo(string actor);
    global::ClankerWorld.Simulation.World.OwnerQueuedInstruction? PendingInstructionFor(string agentId);
    global::System.Collections.Generic.List<global::ClankerWorld.Simulation.Cognition.CognitionCandidate> RestrictCandidatesForOrder(global::System.Collections.Generic.List<global::ClankerWorld.Simulation.Cognition.CognitionCandidate> candidates, global::ClankerWorld.Simulation.World.OwnerQueuedInstruction order, global::ClankerWorld.Simulation.World.AgentState state);
    global::ClankerWorld.Simulation.Cognition.CognitionCandidate? SelectOrderSurvivalCandidate(global::System.Collections.Generic.IEnumerable<global::ClankerWorld.Simulation.Cognition.CognitionCandidate> candidates, global::ClankerWorld.Simulation.World.AgentState person, global::ClankerWorld.Simulation.World.OwnerQueuedInstruction? instruction);
    bool TalkOrderHasConversation(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction);
    bool IsSurvivalCandidate(string actor, string candidateId);
    global::ClankerWorld.Simulation.Cognition.CognitionCandidate? OrderCandidateFor(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person);
    bool ShouldInterruptOrder(global::ClankerWorld.Simulation.World.AgentState person, global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.Cognition.CognitionCandidate? orderCandidate, global::ClankerWorld.Simulation.Cognition.CognitionCandidate? urgentCandidate);
    bool IsShelterOrder(string action);
    void AddHouseholdBuildingPlans(global::System.Collections.Generic.List<global::ClankerWorld.Simulation.Cognition.CognitionCandidate> candidates, global::ClankerWorld.Simulation.Society.SocietyAgent agent, bool needsWarmth, string householdId, global::System.Collections.Generic.List<string> buildingsWithoutSites);
    global::ClankerWorld.Simulation.World.WorldContentSimulationState Simulation { get; }
    global::ClankerWorld.Simulation.Content.DeclarativeWorldContentState Content { get; }
    bool IsGenericFoodRecipe(global::ClankerWorld.Simulation.Content.RecipeDefinition recipe);
    bool IsHouseholdLayout(global::ClankerWorld.Simulation.Content.BuildingDefinition? building);
    bool IsHandcartRecipe(global::ClankerWorld.Simulation.Content.RecipeDefinition recipe);
    string ProductionOwnerFor(global::ClankerWorld.Simulation.World.PlacedBuilding? building, string workerId);
    bool HasCarriedUnreservedQuantities(string actor, global::System.Collections.Generic.IReadOnlyList<global::ClankerWorld.Simulation.Content.ContentQuantity> quantities);
    bool NeedsRecipeOutput(global::ClankerWorld.Simulation.Content.RecipeDefinition recipe, string? ownerId = null, string? requestWorker = null);
    bool AnotherAgentWaitsForWorkSite(string actor, global::ClankerWorld.Simulation.Content.RecipeDefinition recipe);
    bool CanAcquireProjectInputs(global::System.Collections.Generic.IReadOnlyList<global::ClankerWorld.Simulation.Content.ContentQuantity> inputs, string? ownerId = null, string? residentId = null, global::System.Collections.Generic.Dictionary<(global::ClankerWorld.Simulation.World.ToolFamily Family, int Tier), global::ClankerWorld.Simulation.World.ToolDefinition?>? reachableToolCache = null, global::System.Lazy<global::ClankerWorld.Simulation.World.PlacedBuilding[]>? accessibleWarehouses = null);
    bool TryFindRecipeSite(global::ClankerWorld.Simulation.Content.RecipeDefinition recipe, out string siteId, out global::ClankerWorld.Simulation.World.GridPoint position, string? actorId = null);
    bool HasIngredientsAtBuilding(global::System.Collections.Generic.IReadOnlyList<global::ClankerWorld.Simulation.Content.ContentQuantity> inputs, string ownerId, string buildingId);
    global::ClankerWorld.Simulation.World.RecipeProjectActionPlan PlanRecipeProject(string actor, global::ClankerWorld.Simulation.Content.RecipeDefinition recipe, global::ClankerWorld.Simulation.World.PlacedBuilding workstation);
    global::ClankerWorld.Simulation.World.ActionCheck CheckActionPlan(global::ClankerWorld.Simulation.World.ActionPlan plan);
    int OutdoorExposure(global::ClankerWorld.Simulation.World.GridPoint position);
    global::ClankerWorld.Simulation.World.AnimalState? Animal(string id);
    global::System.Collections.Generic.IReadOnlyList<global::ClankerWorld.Simulation.World.TownRuntimeState> Towns { get; }
    global::ClankerWorld.Simulation.World.WorldMap Map { get; }
}
