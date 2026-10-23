namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private ICognitionConstructionWorld CognitionConstructionWorld => new CognitionConstructionAdapter(this);

    private sealed class CognitionConstructionAdapter(WorldRuntime runtime) : ICognitionConstructionWorld
    {
        public void AppendEvent(global::ClankerWorld.Simulation.World.WorldEventPayload payload, global::ClankerWorld.Simulation.World.GridPoint? eventPosition = null) => runtime.AppendEvent(payload, eventPosition);
        public global::ClankerWorld.Simulation.World.ConstructionActionPlan? RetainedConstruction(string actor, global::ClankerWorld.Simulation.World.AgentProject project) => runtime.RetainedConstruction(actor, project);
        public global::ClankerWorld.Simulation.World.ActionCheck CheckConstruction(global::ClankerWorld.Simulation.World.ConstructionActionPlan plan, bool checkDestination = true, global::ClankerWorld.Simulation.World.ActorCapabilityPolicy? policy = null) => runtime.CheckConstruction(plan, checkDestination, policy);
        public void RefuseChoice(global::ClankerWorld.Simulation.World.ActionPlan plan, global::ClankerWorld.Simulation.World.ActionRefusal refusal) => runtime.RefuseChoice(plan, refusal);
        public global::ClankerWorld.Simulation.Content.DeclarativeWorldContentState Content => runtime.worldContent;
        public global::ClankerWorld.Simulation.World.TownLayoutContext CreateTownLayoutContext(string actor, global::ClankerWorld.Simulation.World.GridPoint? selectedSite = null, global::ClankerWorld.Simulation.Content.BuildingDefinition? building = null, bool forTownProject = false, string? townProjectId = null) => runtime.CreateTownLayoutContext(actor, selectedSite, building, forTownProject, townProjectId);
        public string? TownForResident(string residentId) => runtime.TownForResident(residentId);
        public bool IsHouseholdBuildingTag(string tag) => WorldRuntime.IsHouseholdBuildingTag(tag);
        public global::ClankerWorld.Simulation.Society.SocietyCheckpoint Society => runtime.society.Checkpoint;
        public global::ClankerWorld.Simulation.World.PlacedBuilding? HouseForHousehold(string householdId) => runtime.HouseForHousehold(householdId);
        public void MoveToward(string agentId, global::ClankerWorld.Simulation.World.AgentState state, global::ClankerWorld.Simulation.World.GridPoint destination, string reason, int interactionRange = 0, int horseMovementBudget = 100) => runtime.MoveToward(agentId, state, destination, reason, interactionRange, horseMovementBudget);
        public global::ClankerWorld.Simulation.World.OwnerQueuedInstruction? ConstructionInstructionForProject(string actor, global::ClankerWorld.Simulation.World.AgentProject? project) => OrderSystem.ConstructionInstructionForProject(runtime.ConstructionOrdersWorld, actor, project);
        public int ProjectWorkTicks => WorldRuntime.ProjectWorkTicks;
        public bool TryConstructionOrderSite(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person, out global::ClankerWorld.Simulation.Content.BuildingDefinition? definition, out string? household, out global::ClankerWorld.Simulation.World.GridPoint position, out string failure) => OrderSystem.TryConstructionOrderSite(runtime.ConstructionOrdersWorld, instruction, person, out definition, out household, out position, out failure);
        public string BuildingConstructionOwner(string actor, global::ClankerWorld.Simulation.Content.BuildingDefinition definition) => runtime.BuildingConstructionOwner(actor, definition);
        public global::ClankerWorld.Simulation.World.BuildingPlacementResult PlaceBuildingCore(string instanceId, string definitionId, global::ClankerWorld.Simulation.World.GridPoint position, string eventKind, string? assignedTownId = null, string? householdId = null, string? constructionOwnerId = null, string? constructionInstructionId = null, global::System.Collections.Generic.IReadOnlyList<global::ClankerWorld.Simulation.World.ConstructionMaterialInput>? selectedMaterials = null) => runtime.PlaceBuildingCore(instanceId, definitionId, position, eventKind, assignedTownId, householdId, constructionOwnerId, constructionInstructionId, selectedMaterials);
        public string BuildInstanceId(string agentId, global::ClankerWorld.Simulation.Content.BuildingDefinition definition) => runtime.BuildInstanceId(agentId, definition);
        public void CreditCompletedWork(string actor, string domain, global::ClankerWorld.Simulation.World.SkillKind? skill = null) => runtime.CreditCompletedWork(actor, domain, skill);
        public void RecordConstructionOrderCompletion(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, string materialOwner) => OrderSystem.RecordConstructionOrderCompletion(runtime.ConstructionOrdersWorld, instruction, materialOwner);
        public global::ClankerWorld.Simulation.World.RecipeProjectActionPlan? RetainedRecipeProject(string actor, global::ClankerWorld.Simulation.World.AgentProject project) => runtime.RetainedRecipeProject(actor, project);
        public bool TryFindRecipeSite(global::ClankerWorld.Simulation.Content.RecipeDefinition recipe, out string siteId, out global::ClankerWorld.Simulation.World.GridPoint position, string? actorId = null) => runtime.TryFindRecipeSite(recipe, out siteId, out position, actorId);
        public global::ClankerWorld.Simulation.World.OwnerQueuedInstruction? PendingInstructionFor(string agentId) => runtime.PendingInstructionFor(agentId);
        public bool IsProductionOrderProject(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentProject? project) => WorldRuntime.IsProductionOrderProject(instruction, project);
        public global::ClankerWorld.Simulation.World.ProductionStartResult StartProductionCore(string recipeId, string buildingInstanceId, string workerId, string eventKind, string? orderInstructionId = null, global::ClankerWorld.Simulation.World.RecipeProjectActionPlan? originalPlan = null) => runtime.StartProductionCore(recipeId, buildingInstanceId, workerId, eventKind, orderInstructionId, originalPlan);
    }
}
