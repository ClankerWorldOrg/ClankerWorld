namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private ICognitionConstructionWorld CognitionConstructionWorld => new CognitionConstructionAdapter(this);

    private sealed class CognitionConstructionAdapter(WorldRuntime runtime) : AgentRuntimeAdapter(runtime), ICognitionConstructionWorld
    {
        public void AppendEvent(global::ClankerWorld.Simulation.World.WorldEventPayload payload, global::ClankerWorld.Simulation.World.GridPoint? eventPosition = null) => Runtime.AppendEvent(payload, eventPosition);
        public global::ClankerWorld.Simulation.World.ConstructionActionPlan? RetainedConstruction(string actor, global::ClankerWorld.Simulation.World.AgentProject project) => Runtime.RetainedConstruction(actor, project);
        public global::ClankerWorld.Simulation.World.ActionCheck CheckConstruction(global::ClankerWorld.Simulation.World.ConstructionActionPlan plan, bool checkDestination = true, global::ClankerWorld.Simulation.World.ActorCapabilityPolicy? policy = null) => Runtime.CheckConstruction(plan, checkDestination, policy);
        public global::ClankerWorld.Simulation.World.TownLayoutContext CreateTownLayoutContext(string actor, global::ClankerWorld.Simulation.World.GridPoint? selectedSite = null, global::ClankerWorld.Simulation.Content.BuildingDefinition? building = null, bool forTownProject = false, string? townProjectId = null) => Runtime.CreateTownLayoutContext(actor, selectedSite, building, forTownProject, townProjectId);
        public string? TownForResident(string residentId) => Runtime.TownForResident(residentId);
        public bool IsHouseholdBuildingTag(string tag) => WorldRuntime.IsHouseholdBuildingTag(tag);
        public global::ClankerWorld.Simulation.World.PlacedBuilding? HouseForHousehold(string householdId) => Runtime.HouseForHousehold(householdId);
        public void MoveToward(string agentId, global::ClankerWorld.Simulation.World.AgentState state, global::ClankerWorld.Simulation.World.GridPoint destination, string reason, int interactionRange = 0, int horseMovementBudget = 100) => Runtime.MoveToward(agentId, state, destination, reason, interactionRange, horseMovementBudget);
        public global::ClankerWorld.Simulation.World.OwnerQueuedInstruction? ConstructionInstructionForProject(string actor, global::ClankerWorld.Simulation.World.AgentProject? project) => OrderSystem.ConstructionInstructionForProject(Runtime.ConstructionOrdersWorld, actor, project);
        public int ProjectWorkTicks => WorldRuntime.ProjectWorkTicks;
        public bool TryConstructionOrderSite(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person, out global::ClankerWorld.Simulation.Content.BuildingDefinition? definition, out string? household, out global::ClankerWorld.Simulation.World.GridPoint position, out string failure) => OrderSystem.TryConstructionOrderSite(Runtime.ConstructionOrdersWorld, instruction, person, out definition, out household, out position, out failure);
        public string BuildingConstructionOwner(string actor, global::ClankerWorld.Simulation.Content.BuildingDefinition definition) => Runtime.BuildingConstructionOwner(actor, definition);
        public global::ClankerWorld.Simulation.World.BuildingPlacementResult PlaceBuildingCore(string instanceId, string definitionId, global::ClankerWorld.Simulation.World.GridPoint position, string eventKind, string? assignedTownId = null, string? householdId = null, string? constructionOwnerId = null, string? constructionInstructionId = null, global::System.Collections.Generic.IReadOnlyList<global::ClankerWorld.Simulation.World.ConstructionMaterialInput>? selectedMaterials = null) => Runtime.PlaceBuildingCore(instanceId, definitionId, position, eventKind, assignedTownId, householdId, constructionOwnerId, constructionInstructionId, selectedMaterials);
        public string BuildInstanceId(string agentId, global::ClankerWorld.Simulation.Content.BuildingDefinition definition) => Runtime.BuildInstanceId(agentId, definition);
        public void CreditCompletedWork(string actor, string domain, global::ClankerWorld.Simulation.World.SkillKind? skill = null) => Runtime.CreditCompletedWork(actor, domain, skill);
        public void RecordConstructionOrderCompletion(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, string materialOwner) => OrderSystem.RecordConstructionOrderCompletion(Runtime.ConstructionOrdersWorld, instruction, materialOwner);
        public global::ClankerWorld.Simulation.World.RecipeProjectActionPlan? RetainedRecipeProject(string actor, global::ClankerWorld.Simulation.World.AgentProject project) => Runtime.RetainedRecipeProject(actor, project);
        public global::ClankerWorld.Simulation.World.OwnerQueuedInstruction? PendingInstructionFor(string agentId) => Runtime.PendingInstructionFor(agentId);
        public bool IsProductionOrderProject(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentProject? project) => WorldRuntime.IsProductionOrderProject(instruction, project);
        public global::ClankerWorld.Simulation.World.ProductionStartResult StartProductionCore(string recipeId, string buildingInstanceId, string workerId, string eventKind, string? orderInstructionId = null, global::ClankerWorld.Simulation.World.RecipeProjectActionPlan? originalPlan = null) => Runtime.StartProductionCore(recipeId, buildingInstanceId, workerId, eventKind, orderInstructionId, originalPlan);
    }
}
