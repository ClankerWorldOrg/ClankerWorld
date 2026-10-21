namespace ClankerWorld.Simulation.World;

internal interface ICognitionConstructionWorld
{
    void AppendEvent(global::ClankerWorld.Simulation.World.WorldEventPayload payload, global::ClankerWorld.Simulation.World.GridPoint? eventPosition = null);
    global::ClankerWorld.Simulation.World.ConstructionActionPlan? RetainedConstruction(string actor, global::ClankerWorld.Simulation.World.AgentProject project);
    global::ClankerWorld.Simulation.World.ActionCheck CheckConstruction(global::ClankerWorld.Simulation.World.ConstructionActionPlan plan, bool checkDestination = true, global::ClankerWorld.Simulation.World.ActorCapabilityPolicy? policy = null);
    void RefuseChoice(global::ClankerWorld.Simulation.World.ActionPlan plan, global::ClankerWorld.Simulation.World.ActionRefusal refusal);
    global::ClankerWorld.Simulation.Content.DeclarativeWorldContentState Content { get; }
    global::ClankerWorld.Simulation.World.TownLayoutContext CreateTownLayoutContext(string actor, global::ClankerWorld.Simulation.World.GridPoint? selectedSite = null, global::ClankerWorld.Simulation.Content.BuildingDefinition? building = null, bool forTownProject = false, string? townProjectId = null);
    string? TownForResident(string residentId);
    bool IsHouseholdBuildingTag(string tag);
    global::ClankerWorld.Simulation.Society.SocietyCheckpoint Society { get; }
    global::ClankerWorld.Simulation.World.PlacedBuilding? HouseForHousehold(string householdId);
    void MoveToward(string agentId, global::ClankerWorld.Simulation.World.AgentState state, global::ClankerWorld.Simulation.World.GridPoint destination, string reason, int interactionRange = 0, int horseMovementBudget = 100);
    global::ClankerWorld.Simulation.World.OwnerQueuedInstruction? ConstructionInstructionForProject(string actor, global::ClankerWorld.Simulation.World.AgentProject? project);
    int ProjectWorkTicks { get; }
    bool TryConstructionOrderSite(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person, out global::ClankerWorld.Simulation.Content.BuildingDefinition? definition, out string? household, out global::ClankerWorld.Simulation.World.GridPoint position, out string failure);
    string BuildingConstructionOwner(string actor, global::ClankerWorld.Simulation.Content.BuildingDefinition definition);
    global::ClankerWorld.Simulation.World.BuildingPlacementResult PlaceBuildingCore(string instanceId, string definitionId, global::ClankerWorld.Simulation.World.GridPoint position, string eventKind, string? assignedTownId = null, string? householdId = null, string? constructionOwnerId = null, string? constructionInstructionId = null, global::System.Collections.Generic.IReadOnlyList<global::ClankerWorld.Simulation.World.ConstructionMaterialInput>? selectedMaterials = null);
    string BuildInstanceId(string agentId, global::ClankerWorld.Simulation.Content.BuildingDefinition definition);
    void CreditCompletedWork(string actor, string domain, global::ClankerWorld.Simulation.World.SkillKind? skill = null);
    void RecordConstructionOrderCompletion(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, string materialOwner);
    global::ClankerWorld.Simulation.World.RecipeProjectActionPlan? RetainedRecipeProject(string actor, global::ClankerWorld.Simulation.World.AgentProject project);
    bool TryFindRecipeSite(global::ClankerWorld.Simulation.Content.RecipeDefinition recipe, out string siteId, out global::ClankerWorld.Simulation.World.GridPoint position, string? actorId = null);
    global::ClankerWorld.Simulation.World.OwnerQueuedInstruction? PendingInstructionFor(string agentId);
    bool IsProductionOrderProject(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentProject? project);
    global::ClankerWorld.Simulation.World.ProductionStartResult StartProductionCore(string recipeId, string buildingInstanceId, string workerId, string eventKind, string? orderInstructionId = null, global::ClankerWorld.Simulation.World.RecipeProjectActionPlan? originalPlan = null);
}
