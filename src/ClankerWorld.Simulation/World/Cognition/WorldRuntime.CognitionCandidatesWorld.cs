using System.Collections.ObjectModel;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private ICognitionCandidatesWorld CognitionCandidatesWorld => new CognitionCandidatesAdapter(this);

    private sealed class CognitionCandidatesAdapter(WorldRuntime runtime) : ICognitionCandidatesWorld
    {
        public int ComfortableFullness => WorldRuntime.ComfortableFullness;
        public SocietyCheckpoint Society => runtime.society.Checkpoint;
        public DeclarativeWorldContentState Content => runtime.worldContent;
        public WorldContentSimulationState Simulation => runtime.worldSimulation;
        public IReadOnlyList<TownRuntimeState> Towns => runtime.towns;
        public WorldMap Map => runtime.map;
        public List<CognitionCandidate> CreateRegisteredCandidates(string agentId, AgentState state, bool restrictForOrder) => runtime.CreateRegisteredCandidates(agentId, state, restrictForOrder);
        public BoatState? PassengerBoat(string actor) => runtime.PassengerBoat(actor);
        public CognitionCandidate WaitingCandidate(string actor, string text) => runtime.WaitingCandidate(actor, text);
        public void AddFoodPlanCandidate(List<CognitionCandidate> candidates, FoodActionPlan? plan, string description, int priority, string? destination = null) => runtime.AddFoodPlanCandidate(candidates, plan, description, priority, destination);
        public ConsumeFoodActionPlan? PlanFoodConsumption(string actor, InventoryLot? selected = null) => runtime.PlanFoodConsumption(actor, selected);
        public void AddMilkCandidate(List<CognitionCandidate> candidates, string actor) => runtime.AddMilkCandidate(candidates, actor);
        public AgentConversation? ConversationFor(string agentId) => runtime.ConversationFor(agentId);
        public List<CognitionCandidate> ConversationCandidates(string agentId) => runtime.ConversationCandidates(agentId);
        public void AddRegisteredChoices(WorldRuntime.ChoiceGroup group, WorldRuntime.ChoiceContext context) => runtime.AddRegisteredChoices(group, context);
        public bool IsAdult(string actor) => runtime.IsAdult(actor);
        public bool NeedsUrgentWarmth(AgentState person) => runtime.NeedsUrgentWarmth(person);
        public bool IsChild(string actor) => runtime.IsChild(actor);
        public bool NeedsUrgentFood(AgentState person) => WorldRuntime.NeedsUrgentFood(person);
        public IEnumerable<PlacedBuilding> WarehousesAccessibleTo(string actor) => runtime.WarehousesAccessibleTo(actor);
        public OwnerQueuedInstruction? PendingInstructionFor(string agentId) => runtime.PendingInstructionFor(agentId);
        public List<CognitionCandidate> RestrictCandidatesForOrder(List<CognitionCandidate> candidates, OwnerQueuedInstruction order, AgentState state) => runtime.RestrictCandidatesForOrder(candidates, order, state);
        public CognitionCandidate? SelectOrderSurvivalCandidate(IEnumerable<CognitionCandidate> candidates, AgentState person, OwnerQueuedInstruction? instruction) => runtime.SelectOrderSurvivalCandidate(candidates, person, instruction);
        public bool TalkOrderHasConversation(OwnerQueuedInstruction instruction) => runtime.TalkOrderHasConversation(instruction);
        public bool IsSurvivalCandidate(string actor, string candidateId) => runtime.IsSurvivalCandidate(actor, candidateId);
        public CognitionCandidate? OrderCandidateFor(OwnerQueuedInstruction instruction, AgentState person) => runtime.OrderCandidateFor(instruction, person);
        public bool ShouldInterruptOrder(AgentState person, OwnerQueuedInstruction instruction, CognitionCandidate? orderCandidate, CognitionCandidate? urgentCandidate) => runtime.ShouldInterruptOrder(person, instruction, orderCandidate, urgentCandidate);
        public bool IsShelterOrder(string action) => WorldRuntime.IsShelterOrder(action);
        public void AddHouseholdBuildingPlans(List<CognitionCandidate> candidates, SocietyAgent agent, AgentState state, string householdId, List<string> buildingsWithoutSites) => runtime.AddHouseholdBuildingPlans(candidates, agent, state, householdId, buildingsWithoutSites);
        public bool IsGenericFoodRecipe(RecipeDefinition recipe) => WorldRuntime.IsGenericFoodRecipe(recipe);
        public bool IsHouseholdBuildingTag(string tag) => WorldRuntime.IsHouseholdBuildingTag(tag);
        public bool IsHandcartRecipe(RecipeDefinition recipe) => WorldRuntime.IsHandcartRecipe(recipe);
        public string ProductionOwnerFor(PlacedBuilding? building, string workerId) => runtime.ProductionOwnerFor(building, workerId);
        public bool HasCarriedUnreservedQuantities(string actor, IReadOnlyList<ContentQuantity> quantities) => runtime.HasCarriedUnreservedQuantities(actor, quantities);
        public bool NeedsRecipeOutput(RecipeDefinition recipe, string? ownerId = null, string? requestWorker = null) => runtime.NeedsRecipeOutput(recipe, ownerId, requestWorker);
        public bool AnotherAgentWaitsForWorkSite(string actor, RecipeDefinition recipe) => runtime.AnotherAgentWaitsForWorkSite(actor, recipe);
        public bool CanAcquireProjectInputs(IReadOnlyList<ContentQuantity> inputs, string? ownerId = null, string? residentId = null, Dictionary<(ToolFamily Family, int Tier), ToolDefinition?>? reachableToolCache = null, Lazy<PlacedBuilding[]>? accessibleWarehouses = null) => runtime.CanAcquireProjectInputs(inputs, ownerId, residentId, reachableToolCache, accessibleWarehouses);
        public bool TryFindRecipeSite(RecipeDefinition recipe, out string siteId, out GridPoint position, string? actorId = null) => runtime.TryFindRecipeSite(recipe, out siteId, out position, actorId);
        public bool HasIngredientsAtBuilding(IReadOnlyList<ContentQuantity> inputs, string ownerId, string buildingId) => runtime.HasIngredientsAtBuilding(inputs, ownerId, buildingId);
        public RecipeProjectActionPlan PlanRecipeProject(string actor, RecipeDefinition recipe, PlacedBuilding workstation) => runtime.PlanRecipeProject(actor, recipe, workstation);
        public ActionCheck CheckActionPlan(ActionPlan plan) => runtime.CheckActionPlan(plan);
        public int OutdoorExposure(GridPoint position) => runtime.OutdoorExposure(position);
        public AnimalState? Animal(string id) => runtime.Animal(id);

    }
}
