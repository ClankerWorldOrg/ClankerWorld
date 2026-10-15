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
    private ICognitionConstructionWorld CognitionConstructionWorld => new CognitionConstructionAdapter(this);

    private sealed class CognitionConstructionAdapter(WorldRuntime runtime) : ICognitionConstructionWorld
    {
        public DeclarativeWorldContentState Content => runtime.worldContent;
        public SocietyCheckpoint Society => runtime.society.Checkpoint;
        public int ProjectWorkTicks => WorldRuntime.ProjectWorkTicks;
        public void AppendEvent(string kind, string detail, GridPoint? eventPosition = null) => runtime.AppendEvent(kind, detail, eventPosition);
        public ConstructionActionPlan? RetainedConstruction(string actor, AgentProject project) => runtime.RetainedConstruction(actor, project);
        public ActionCheck CheckConstruction(ConstructionActionPlan plan, bool checkDestination = true, ActorCapabilityPolicy? policy = null) => runtime.CheckConstruction(plan, checkDestination, policy);
        public void RefuseChoice(ActionPlan plan, ActionRefusal refusal) => runtime.RefuseChoice(plan, refusal);
        public TownLayoutContext CreateTownLayoutContext(string actor, GridPoint? selectedSite = null, BuildingDefinition? building = null, bool forTownProject = false, string? townProjectId = null, WorldRuntime.TownLayoutBasis? basis = null) => runtime.CreateTownLayoutContext(actor, selectedSite, building, forTownProject, townProjectId, basis);
        public string? TownForResident(string residentId) => runtime.TownForResident(residentId);
        public bool IsHouseholdBuildingTag(string tag) => WorldRuntime.IsHouseholdBuildingTag(tag);
        public PlacedBuilding? HouseForHousehold(string householdId) => runtime.HouseForHousehold(householdId);
        public void MoveToward(string agentId, AgentState state, GridPoint destination, string reason, int interactionRange = 0, int horseMovementBudget = 100) => runtime.MoveToward(agentId, state, destination, reason, interactionRange, horseMovementBudget);
        public OwnerQueuedInstruction? ConstructionInstructionForProject(string actor, AgentProject? project) => runtime.ConstructionInstructionForProject(actor, project);
        public bool TryConstructionOrderSite(OwnerQueuedInstruction instruction, AgentState person, out BuildingDefinition? definition, out string? household, out GridPoint position, out string failure) => runtime.TryConstructionOrderSite(instruction, person, out definition, out household, out position, out failure);
        public string BuildingConstructionOwner(string actor, BuildingDefinition definition) => runtime.BuildingConstructionOwner(actor, definition);
        public BuildingPlacementResult PlaceBuildingCore(string instanceId, string definitionId, GridPoint position, string eventKind, string? assignedTownId = null, string? householdId = null, string? constructionOwnerId = null, string? constructionInstructionId = null, IReadOnlyList<ConstructionMaterialInput>? selectedMaterials = null) => runtime.PlaceBuildingCore(instanceId, definitionId, position, eventKind, assignedTownId, householdId, constructionOwnerId, constructionInstructionId, selectedMaterials);
        public string BuildInstanceId(string agentId, BuildingDefinition definition) => runtime.BuildInstanceId(agentId, definition);
        public void CreditCompletedWork(string actor, string domain, SkillKind? skill = null) => runtime.CreditCompletedWork(actor, domain, skill);
        public void RecordConstructionOrderCompletion(OwnerQueuedInstruction instruction, string materialOwner) => runtime.RecordConstructionOrderCompletion(instruction, materialOwner);
        public RecipeProjectActionPlan? RetainedRecipeProject(string actor, AgentProject project) => runtime.RetainedRecipeProject(actor, project);
        public bool TryFindRecipeSite(RecipeDefinition recipe, out string siteId, out GridPoint position, string? actorId = null) => runtime.TryFindRecipeSite(recipe, out siteId, out position, actorId);
        public OwnerQueuedInstruction? PendingInstructionFor(string agentId) => runtime.PendingInstructionFor(agentId);
        public bool IsProductionOrderProject(OwnerQueuedInstruction instruction, AgentProject? project) => WorldRuntime.IsProductionOrderProject(instruction, project);
        public ProductionStartResult StartProductionCore(string recipeId, string buildingInstanceId, string workerId, string eventKind, string? orderInstructionId = null, RecipeProjectActionPlan? originalPlan = null) => runtime.StartProductionCore(recipeId, buildingInstanceId, workerId, eventKind, orderInstructionId, originalPlan);

    }
}
