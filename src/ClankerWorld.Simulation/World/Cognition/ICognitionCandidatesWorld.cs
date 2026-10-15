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

internal interface ICognitionCandidatesWorld
{
    int ComfortableFullness { get; }
    SocietyCheckpoint Society { get; }
    DeclarativeWorldContentState Content { get; }
    WorldContentSimulationState Simulation { get; }
    IReadOnlyList<TownRuntimeState> Towns { get; }
    WorldMap Map { get; }
    List<CognitionCandidate> CreateRegisteredCandidates(string agentId, AgentState state, bool restrictForOrder);
    BoatState? PassengerBoat(string actor);
    CognitionCandidate WaitingCandidate(string actor, string text);
    void AddFoodPlanCandidate(List<CognitionCandidate> candidates, FoodActionPlan? plan, string description, int priority, string? destination = null);
    ConsumeFoodActionPlan? PlanFoodConsumption(string actor, InventoryLot? selected = null);
    void AddMilkCandidate(List<CognitionCandidate> candidates, string actor);
    AgentConversation? ConversationFor(string agentId);
    List<CognitionCandidate> ConversationCandidates(string agentId);
    void AddRegisteredChoices(WorldRuntime.ChoiceGroup group, WorldRuntime.ChoiceContext context);
    bool IsAdult(string actor);
    bool NeedsUrgentWarmth(AgentState person);
    bool IsChild(string actor);
    bool NeedsUrgentFood(AgentState person);
    IEnumerable<PlacedBuilding> WarehousesAccessibleTo(string actor);
    OwnerQueuedInstruction? PendingInstructionFor(string agentId);
    List<CognitionCandidate> RestrictCandidatesForOrder(List<CognitionCandidate> candidates, OwnerQueuedInstruction order, AgentState state);
    CognitionCandidate? SelectOrderSurvivalCandidate(IEnumerable<CognitionCandidate> candidates, AgentState person, OwnerQueuedInstruction? instruction);
    bool TalkOrderHasConversation(OwnerQueuedInstruction instruction);
    bool IsSurvivalCandidate(string actor, string candidateId);
    CognitionCandidate? OrderCandidateFor(OwnerQueuedInstruction instruction, AgentState person);
    bool ShouldInterruptOrder(AgentState person, OwnerQueuedInstruction instruction, CognitionCandidate? orderCandidate, CognitionCandidate? urgentCandidate);
    bool IsShelterOrder(string action);
    void AddHouseholdBuildingPlans(List<CognitionCandidate> candidates, SocietyAgent agent, AgentState state, string householdId, List<string> buildingsWithoutSites);
    bool IsGenericFoodRecipe(RecipeDefinition recipe);
    bool IsHouseholdBuildingTag(string tag);
    bool IsHandcartRecipe(RecipeDefinition recipe);
    string ProductionOwnerFor(PlacedBuilding? building, string workerId);
    bool HasCarriedUnreservedQuantities(string actor, IReadOnlyList<ContentQuantity> quantities);
    bool NeedsRecipeOutput(RecipeDefinition recipe, string? ownerId = null, string? requestWorker = null);
    bool AnotherAgentWaitsForWorkSite(string actor, RecipeDefinition recipe);
    bool CanAcquireProjectInputs(IReadOnlyList<ContentQuantity> inputs, string? ownerId = null, string? residentId = null, Dictionary<(ToolFamily Family, int Tier), ToolDefinition?>? reachableToolCache = null, Lazy<PlacedBuilding[]>? accessibleWarehouses = null);
    bool TryFindRecipeSite(RecipeDefinition recipe, out string siteId, out GridPoint position, string? actorId = null);
    bool HasIngredientsAtBuilding(IReadOnlyList<ContentQuantity> inputs, string ownerId, string buildingId);
    RecipeProjectActionPlan PlanRecipeProject(string actor, RecipeDefinition recipe, PlacedBuilding workstation);
    ActionCheck CheckActionPlan(ActionPlan plan);
    int OutdoorExposure(GridPoint position);
    AnimalState? Animal(string id);

}
