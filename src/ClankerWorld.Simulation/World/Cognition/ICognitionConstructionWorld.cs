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

internal interface ICognitionConstructionWorld
{
    DeclarativeWorldContentState Content { get; }
    SocietyCheckpoint Society { get; }
    int ProjectWorkTicks { get; }
    void AppendEvent(string kind, string detail, GridPoint? eventPosition = null);
    ConstructionActionPlan? RetainedConstruction(string actor, AgentProject project);
    ActionCheck CheckConstruction(ConstructionActionPlan plan, bool checkDestination = true, ActorCapabilityPolicy? policy = null);
    void RefuseChoice(ActionPlan plan, ActionRefusal refusal);
    TownLayoutContext CreateTownLayoutContext(string actor, GridPoint? selectedSite = null, BuildingDefinition? building = null, bool forTownProject = false, string? townProjectId = null, WorldRuntime.TownLayoutBasis? basis = null);
    string? TownForResident(string residentId);
    bool IsHouseholdBuildingTag(string tag);
    PlacedBuilding? HouseForHousehold(string householdId);
    void MoveToward(string agentId, AgentState state, GridPoint destination, string reason, int interactionRange = 0, int horseMovementBudget = 100);
    OwnerQueuedInstruction? ConstructionInstructionForProject(string actor, AgentProject? project);
    bool TryConstructionOrderSite(OwnerQueuedInstruction instruction, AgentState person, out BuildingDefinition? definition, out string? household, out GridPoint position, out string failure);
    string BuildingConstructionOwner(string actor, BuildingDefinition definition);
    BuildingPlacementResult PlaceBuildingCore(string instanceId, string definitionId, GridPoint position, string eventKind, string? assignedTownId = null, string? householdId = null, string? constructionOwnerId = null, string? constructionInstructionId = null, IReadOnlyList<ConstructionMaterialInput>? selectedMaterials = null);
    string BuildInstanceId(string agentId, BuildingDefinition definition);
    void CreditCompletedWork(string actor, string domain, SkillKind? skill = null);
    void RecordConstructionOrderCompletion(OwnerQueuedInstruction instruction, string materialOwner);
    RecipeProjectActionPlan? RetainedRecipeProject(string actor, AgentProject project);
    bool TryFindRecipeSite(RecipeDefinition recipe, out string siteId, out GridPoint position, string? actorId = null);
    OwnerQueuedInstruction? PendingInstructionFor(string agentId);
    bool IsProductionOrderProject(OwnerQueuedInstruction instruction, AgentProject? project);
    ProductionStartResult StartProductionCore(string recipeId, string buildingInstanceId, string workerId, string eventKind, string? orderInstructionId = null, RecipeProjectActionPlan? originalPlan = null);

}
