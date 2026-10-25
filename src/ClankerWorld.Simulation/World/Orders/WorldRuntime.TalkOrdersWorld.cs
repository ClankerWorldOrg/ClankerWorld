namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private ITalkOrdersWorld TalkOrdersWorld => new TalkOrdersAdapter(this);

    private sealed class TalkOrdersAdapter(WorldRuntime runtime) : ITalkOrdersWorld
    {
        public global::ClankerWorld.Simulation.Society.SocietyCheckpoint Society => runtime.society.Checkpoint;
        public bool IsLinkedTalkOrder(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction) => WorldRuntime.IsLinkedTalkOrder(instruction);
        public global::ClankerWorld.Simulation.Cognition.AgentConversation? ConversationFor(string agentId) => runtime.ConversationFor(agentId);
        public string? TalkOrderBlocker(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person) => runtime.TalkOrderBlocker(instruction, person);
        public string? CurrentMarriageOrderPartner(string actor) => OrderSystem.CurrentMarriageOrderPartner(runtime.MarriageOrdersWorld, actor);
        public string? MarriageOrderBlocker(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction) => OrderSystem.MarriageOrderBlocker(runtime.MarriageOrdersWorld, instruction);
        public global::System.Collections.Generic.IReadOnlyDictionary<string, global::ClankerWorld.Simulation.World.AgentState> Agents => runtime.agents;
        public global::ClankerWorld.Simulation.World.BoatState? PassengerBoat(string actor) => TransportSystem.PassengerBoat(runtime.BoatWorld, actor);
        public bool NeedsUrgentFood(global::ClankerWorld.Simulation.World.AgentState person) => WorldRuntime.NeedsUrgentFood(person);
        public bool NeedsUrgentWarmth(global::ClankerWorld.Simulation.World.AgentState person) => runtime.NeedsUrgentWarmth(person);
        public bool HasExplicitConversationProvider(string agentId) => runtime.HasExplicitConversationProvider(agentId);
        public bool HasConversationAllowance(string agentId) => runtime.HasConversationAllowance(agentId);
        public global::System.Collections.ObjectModel.ReadOnlyCollection<global::ClankerWorld.Simulation.Cognition.AgentConversation> Conversations => runtime.conversations;
        public bool ActiveTalkOrderUses(string conversationId) => runtime.ActiveTalkOrderUses(conversationId);
        public bool IsWithinInteractionRange(global::ClankerWorld.Simulation.World.GridPoint origin, global::ClankerWorld.Simulation.World.GridPoint destination, int interactionRange) => runtime.IsWithinInteractionRange(origin, destination, interactionRange);
        public int ResourceInteractionRange => WorldRuntime.ResourceInteractionRange;
        public global::System.Collections.ObjectModel.ReadOnlyCollection<global::ClankerWorld.Simulation.World.GridPoint> Reach(string actor, global::ClankerWorld.Simulation.World.GridPoint target, global::ClankerWorld.Simulation.World.ReachPurpose purpose, int range, int load, global::ClankerWorld.Simulation.World.SwimmingPolicy swimming, global::ClankerWorld.Simulation.World.ReachObstacles obstacles = (global::ClankerWorld.Simulation.World.ReachObstacles)0) => runtime.Reach(actor, target, purpose, range, load, swimming, obstacles);
        public void CompleteClosedTalkOrders() => runtime.CompleteClosedTalkOrders();
        public string? UnacceptedMarriageOrderBlocker(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction) => OrderSystem.UnacceptedMarriageOrderBlocker(runtime.MarriageOrdersWorld, instruction);
        public void SetOrderStatus(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.OrderStatus status, string? reason, bool waitForDecision = false) => runtime.SetOrderStatus(instruction, status, reason, waitForDecision);
        public global::ClankerWorld.Simulation.Cognition.AgentConversation? ConversationForOrder(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction) => OrderSystem.ConversationForOrder(runtime.MarriageOrdersWorld, instruction);
        public global::System.Collections.ObjectModel.ReadOnlyCollection<global::ClankerWorld.Simulation.World.AgentMarriage> Marriages => runtime.marriages;
        public OrderStore Orders => runtime.orderStore;
        public void MoveToward(string agentId, global::ClankerWorld.Simulation.World.AgentState state, global::ClankerWorld.Simulation.World.GridPoint destination, string reason, int interactionRange = 0, int horseMovementBudget = 100) => runtime.MoveToward(agentId, state, destination, reason, interactionRange, horseMovementBudget);
        public bool ProposeConversation(string initiatorId, string inviteeId) => runtime.ProposeConversation(initiatorId, inviteeId);
        public bool IsSurvivalCandidate(string actor, string candidateId) => runtime.IsSurvivalCandidate(actor, candidateId);
        public bool IsMarketFoodCandidate(string actor, string candidateId) => MarketSystem.IsMarketFoodCandidate(runtime.MarketStallWorld, actor, candidateId);
        public bool ApplyMarketCandidate(string actor, global::ClankerWorld.Simulation.World.AgentState state, string id, global::ClankerWorld.Simulation.World.ActionPlan? originalPlan = null) => runtime.ApplyMarketCandidate(actor, state, id, originalPlan);
        public void ApplyCandidate(string agentId, global::ClankerWorld.Simulation.World.AgentState state, string candidateId, bool reportIdle) => runtime.ApplyCandidate(agentId, state, candidateId, reportIdle);
        public global::System.Collections.Generic.List<global::ClankerWorld.Simulation.Cognition.CognitionCandidate> ConversationCandidates(string agentId) => runtime.ConversationCandidates(agentId);
        public bool ApplyConversationCandidate(string agentId, string candidateId) => runtime.ApplyConversationCandidate(agentId, candidateId);
        public void RefreshMarriageOrderEligibilityStatus(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction) => OrderSystem.RefreshMarriageOrderEligibilityStatus(runtime.MarriageOrdersWorld, instruction);
        public bool IsActiveOrder(string status) => WorldRuntime.IsActiveOrder(status);
        public string? MarriageOrderOutcome(global::ClankerWorld.Simulation.Cognition.AgentConversation conversation, global::System.Collections.Generic.IEnumerable<global::ClankerWorld.Simulation.World.AgentMarriage> marriages) => WorldRuntime.MarriageOrderOutcome(conversation, marriages);
        public void CreditOrderEffect(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.Identity effectId, int units) => runtime.CreditOrderEffect(instruction, effectId, units);
        public string TalkOrderEffectId(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction) => WorldRuntime.TalkOrderEffectId(instruction);
    }

    private IMarriageOrdersWorld MarriageOrdersWorld => new MarriageOrdersAdapter(this);

    private sealed class MarriageOrdersAdapter(WorldRuntime runtime) : IMarriageOrdersWorld
    {
        public bool IsAdult(string actor) => runtime.IsAdult(actor);
        public string? CurrentMarriageOrderPartner(string actor) => OrderSystem.CurrentMarriageOrderPartner(runtime.MarriageOrdersWorld, actor);
        public global::ClankerWorld.Simulation.Society.SocietyCheckpoint Society => runtime.society.Checkpoint;
        public global::System.Collections.ObjectModel.ReadOnlyCollection<global::ClankerWorld.Simulation.World.AgentMarriage> Marriages => runtime.marriages;
        public string? MarriageOrderBlocker(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction) => OrderSystem.MarriageOrderBlocker(runtime.MarriageOrdersWorld, instruction);
        public OrderStore Orders => runtime.orderStore;
        public bool IsActiveOrder(string status) => WorldRuntime.IsActiveOrder(status);
        public string? UnacceptedMarriageOrderBlocker(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction) => OrderSystem.UnacceptedMarriageOrderBlocker(runtime.MarriageOrdersWorld, instruction);
        public void SetOrderStatus(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.OrderStatus status, string? reason, bool waitForDecision = false) => runtime.SetOrderStatus(instruction, status, reason, waitForDecision);
        public global::ClankerWorld.Simulation.Cognition.AgentConversation? ConversationForOrder(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction) => OrderSystem.ConversationForOrder(runtime.MarriageOrdersWorld, instruction);
        public global::System.Collections.ObjectModel.ReadOnlyCollection<global::ClankerWorld.Simulation.Cognition.AgentConversation> Conversations => runtime.conversations;
    }

    private IGuardianOrdersWorld GuardianOrdersWorld => new GuardianOrdersAdapter(this);

    private sealed class GuardianOrdersAdapter(WorldRuntime runtime) : GoodsRuntimeAdapter(runtime), IGuardianOrdersWorld
    {
        public bool NeedsCaregiver(string child) => FamilySystem.NeedsCaregiver(Runtime.FamilyCareWorld, child);
        public global::ClankerWorld.Simulation.World.DependentCareActionPlan? PlanDependentCare(string actor, global::ClankerWorld.Simulation.World.DependentCareChoice choice) => FamilySystem.PlanDependentCare(Runtime.FamilyCareWorld, actor, choice);
        public global::ClankerWorld.Simulation.World.ActionCheck CheckDependentCarePlan(global::ClankerWorld.Simulation.World.DependentCareActionPlan plan, global::ClankerWorld.Simulation.World.ActorCapabilityPolicy? policy = null) => FamilySystem.CheckDependentCarePlan(Runtime.FamilyCareWorld, plan, policy);
        public bool CanAcceptGuardian(string adult, string child) => FamilySystem.CanAcceptGuardian(Runtime.FamilyCareWorld, adult, child);
        public void ApplyDependentCareCandidate(string actor, string candidate, global::ClankerWorld.Simulation.World.ActionPlan? originalPlan = null) => Runtime.ApplyDependentCareCandidate(actor, candidate, originalPlan);
        public string GuardianOrderEffectId(string adult, string child) => WorldRuntime.GuardianOrderEffectId(adult, child);
    }

    private IKnowledgeCopyOrdersWorld KnowledgeCopyOrdersWorld => new KnowledgeCopyOrdersAdapter(this);

    private sealed class KnowledgeCopyOrdersAdapter(WorldRuntime runtime) : GoodsRuntimeAdapter(runtime), IKnowledgeCopyOrdersWorld
    {
        public global::ClankerWorld.Simulation.World.AgentKnowledgeArtifact[] HeldKnowledgeArtifacts(string ownerId) => Runtime.HeldKnowledgeArtifacts(ownerId);
        public bool HasCompleteCopyContents(string actor, global::ClankerWorld.Simulation.World.AgentKnowledgeArtifact source, global::ClankerWorld.Simulation.World.AgentKnowledgeFact[] facts, global::ClankerWorld.Simulation.World.AgentRecipeKnowledge[] recipes) => Runtime.HasCompleteCopyContents(actor, source, facts, recipes);
    }

    private IKnowledgeOrdersWorld KnowledgeOrdersWorld => new KnowledgeOrdersAdapter(this);

    private sealed class KnowledgeOrdersAdapter(WorldRuntime runtime) : GoodsRuntimeAdapter(runtime), IKnowledgeOrdersWorld
    {
        public global::ClankerWorld.Simulation.World.AgentKnowledgeWritingProject? KnowledgeWritingFor(string actor) => Runtime.KnowledgeWritingFor(actor);
        public bool MatchesKnowledgeSource(global::ClankerWorld.Simulation.World.OrderTask order, string? source, bool bound) => WorldRuntime.MatchesKnowledgeSource(order, source, bound);
        public bool IsCurrentKnowledgeOrderProject(global::ClankerWorld.Simulation.World.AgentKnowledgeWritingProject project) => OrderSystem.IsCurrentKnowledgeOrderProject(Runtime.KnowledgeOrdersWorld, project);
        public bool MayMakeKnowledgeArtifact(string actor) => Runtime.MayMakeKnowledgeArtifact(actor);
        public bool CanSupplyWriting(string actor, string kind) => Runtime.CanSupplyWriting(actor, kind);
        public string KnowledgeCopyBlockedReason(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction) => OrderSystem.KnowledgeCopyBlockedReason(Runtime.KnowledgeCopyOrdersWorld, instruction);
        public string KnowledgeWritePrefix => WorldRuntime.KnowledgeWritePrefix;
        public string KnowledgeCopyPrefix => WorldRuntime.KnowledgeCopyPrefix;
        public void ApplyKnowledgeWritingCandidate(string actor, global::ClankerWorld.Simulation.World.AgentState person, string candidate, global::ClankerWorld.Simulation.World.ActionPlan? originalPlan = null) => Runtime.ApplyKnowledgeWritingCandidate(actor, person, candidate, originalPlan);
        public void ReplaceKnowledgeWriting(global::ClankerWorld.Simulation.World.AgentKnowledgeWritingProject project) => Runtime.ReplaceKnowledgeWriting(project);
        public void ContinueKnowledgeWriting(global::ClankerWorld.Simulation.World.AgentKnowledgeWritingProject project) => Runtime.ContinueKnowledgeWriting(project);
        public bool IsActiveOrder(string status) => WorldRuntime.IsActiveOrder(status);
        public void ClearKnowledgeOrderBinding(global::ClankerWorld.Simulation.World.AgentKnowledgeWritingProject project) => OrderSystem.ClearKnowledgeOrderBinding(Runtime.KnowledgeOrdersWorld, project);
        public void CancelKnowledgeWriting(global::ClankerWorld.Simulation.World.AgentKnowledgeWritingProject project, string reason) => Runtime.CancelKnowledgeWriting(project, reason);
    }

    private IKnowledgeReadOrdersWorld KnowledgeReadOrdersWorld => new KnowledgeReadOrdersAdapter(this);

    private sealed class KnowledgeReadOrdersAdapter(WorldRuntime runtime) : IKnowledgeReadOrdersWorld
    {
        public global::ClankerWorld.Simulation.World.WorldKnowledgeState Knowledge => runtime.knowledge;
        public global::ClankerWorld.Simulation.World.AgentKnowledgeArtifact[] HeldKnowledgeArtifacts(string ownerId) => runtime.HeldKnowledgeArtifacts(ownerId);
        public bool HasUnknownArtifactContents(string actor, global::ClankerWorld.Simulation.World.AgentKnowledgeArtifact artifact) => runtime.HasUnknownArtifactContents(actor, artifact);
        public bool IsAdult(string actor) => runtime.IsAdult(actor);
        public global::ClankerWorld.Simulation.World.AgentKnowledgeWritingProject? KnowledgeWritingFor(string actor) => runtime.KnowledgeWritingFor(actor);
        public global::ClankerWorld.Simulation.World.AgentKnowledgeArtifact? KnowledgeReadOrderArtifact(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction) => OrderSystem.KnowledgeReadOrderArtifact(runtime.KnowledgeReadOrdersWorld, instruction);
        public bool KnowsMapFact(string agentId, global::ClankerWorld.Simulation.World.GridPoint position) => runtime.KnowsMapFact(agentId, position);
        public bool KnowsRecipe(string actor, string recipeId) => runtime.KnowsRecipe(actor, recipeId);
        public string? KnowledgeReadOrderBlocker(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction) => runtime.KnowledgeReadOrderBlocker(instruction);
        public void SetOrderStatus(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.OrderStatus status, string? reason, bool waitForDecision = false) => runtime.SetOrderStatus(instruction, status, reason, waitForDecision);
        public int ReadIfKnowledgeArtifact(string lotId, string sourceAgentId, string recipientId, global::ClankerWorld.Simulation.World.AgentKnowledgeArtifact? selectedArtifact = null) => runtime.ReadIfKnowledgeArtifact(lotId, sourceAgentId, recipientId, selectedArtifact);
        public long WorldTick => runtime.WorldTick;
        public OrderStore Orders => runtime.orderStore;
        public void CreditOrderEffect(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.Identity effectId, int units) => runtime.CreditOrderEffect(instruction, effectId, units);
        public string KnowledgeReadOrderEffectId(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction) => WorldRuntime.KnowledgeReadOrderEffectId(instruction);
    }

    private IShelterOrdersWorld ShelterOrdersWorld => new ShelterOrdersAdapter(this);

    private sealed class ShelterOrdersAdapter(WorldRuntime runtime) : IShelterOrdersWorld
    {
        public global::ClankerWorld.Simulation.Society.SocietyCheckpoint Society => runtime.society.Checkpoint;
        public bool HasHouseGuestInvitation(string actor, string houseId) => runtime.HasHouseGuestInvitation(actor, houseId);
        public bool IsTownHallStormRefuge(string actor, global::ClankerWorld.Simulation.World.PlacedBuilding building) => runtime.IsTownHallStormRefuge(actor, building);
        public bool IsWithinInteractionRange(global::ClankerWorld.Simulation.World.GridPoint origin, global::ClankerWorld.Simulation.World.GridPoint destination, int interactionRange) => runtime.IsWithinInteractionRange(origin, destination, interactionRange);
        public int ResourceInteractionRange => WorldRuntime.ResourceInteractionRange;
        public bool IsSwimming(string actor, global::ClankerWorld.Simulation.World.GridPoint position) => runtime.IsSwimming(actor, position);
        public bool IsTownHall(global::ClankerWorld.Simulation.World.PlacedBuilding building) => runtime.IsTownHall(building);
        public global::ClankerWorld.Simulation.Content.DeclarativeWorldContentState Content => runtime.worldContent;
        public global::System.Collections.Generic.IEnumerable<global::ClankerWorld.Simulation.World.PlacedBuilding> AccessibleHeatingBuildings(string actor) => runtime.AccessibleHeatingBuildings(actor);
        public global::System.Collections.Generic.IEnumerable<global::ClankerWorld.Simulation.World.PlacedBuilding> AccessibleShelters(string actor, bool includeStormRefuge = true) => runtime.AccessibleShelters(actor, includeStormRefuge);
        public global::ClankerWorld.Simulation.World.WeatherKind WeatherAt(global::ClankerWorld.Simulation.World.GridPoint position) => runtime.WeatherAt(position);
        public bool NaturalStormCover(global::ClankerWorld.Simulation.World.GridPoint point) => runtime.NaturalStormCover(point);
        public global::ClankerWorld.Simulation.World.PlacedBuilding? BoundShelterBuilding(string actor, global::ClankerWorld.Simulation.World.OrderTask order) => OrderSystem.BoundShelterBuilding(runtime.ShelterOrdersWorld, actor, order);
        public bool IsFireLit(global::ClankerWorld.Simulation.World.PlacedBuilding building) => runtime.IsFireLit(building);
        public bool ShelterBuildingCovers(string actor, global::ClankerWorld.Simulation.World.PlacedBuilding building, global::ClankerWorld.Simulation.World.GridPoint point) => OrderSystem.ShelterBuildingCovers(runtime.ShelterOrdersWorld, actor, building, point);
        public bool ShelterBuildingKnown(string actor, global::ClankerWorld.Simulation.World.AgentState person, global::ClankerWorld.Simulation.World.PlacedBuilding building) => OrderSystem.ShelterBuildingKnown(runtime.ShelterOrdersWorld, actor, person, building);
        public global::ClankerWorld.Simulation.World.WorldMap Map => runtime.map;
        public global::ClankerWorld.Simulation.World.GridPoint? ReachableHallShelterPoint(string actor, global::ClankerWorld.Simulation.World.GridPoint origin, global::ClankerWorld.Simulation.World.PlacedBuilding hall) => runtime.ReachableHallShelterPoint(actor, origin, hall);
        public bool CanReachLeg(string actor, global::ClankerWorld.Simulation.World.GridPoint origin, global::ClankerWorld.Simulation.World.GridPoint target, global::ClankerWorld.Simulation.World.ReachPurpose purpose, int range, int load, global::ClankerWorld.Simulation.World.SwimmingPolicy swimming, global::ClankerWorld.Simulation.World.ReachObstacles obstacles = (global::ClankerWorld.Simulation.World.ReachObstacles)0) => runtime.CanReachLeg(actor, origin, target, purpose, range, load, swimming, obstacles);
        public global::ClankerWorld.Simulation.World.GridPoint? NearbyNaturalStormCover(string actor, global::ClankerWorld.Simulation.World.GridPoint origin) => runtime.NearbyNaturalStormCover(actor, origin);
        public global::System.Collections.ObjectModel.ReadOnlyCollection<global::ClankerWorld.Simulation.World.GridPoint> Reach(string actor, global::ClankerWorld.Simulation.World.GridPoint target, global::ClankerWorld.Simulation.World.ReachPurpose purpose, int range, int load, global::ClankerWorld.Simulation.World.SwimmingPolicy swimming, global::ClankerWorld.Simulation.World.ReachObstacles obstacles = (global::ClankerWorld.Simulation.World.ReachObstacles)0) => runtime.Reach(actor, target, purpose, range, load, swimming, obstacles);
        public global::ClankerWorld.Simulation.World.WorldKnowledgeState Knowledge => runtime.knowledge;
        public global::System.Collections.Generic.Dictionary<string, global::ClankerWorld.Simulation.World.ResourceState> Resources => runtime.resources;
        public bool CanGatherFromSource(string actor, string itemKind, global::ClankerWorld.Simulation.World.MapResource source, global::System.Collections.Generic.Dictionary<(global::ClankerWorld.Simulation.World.ToolFamily Family, int Tier), global::ClankerWorld.Simulation.World.ToolDefinition?> reachableToolCache) => runtime.CanGatherFromSource(actor, itemKind, source, reachableToolCache);
        public int FreeCarryCapacity(string actor) => runtime.FreeCarryCapacity(actor);
        public global::ClankerWorld.Simulation.World.ToolGatheringPlan? ProjectMaterialHarvest(string actor, string itemKind, global::ClankerWorld.Simulation.World.MapResource source, bool useHarvestBonus = true) => runtime.ProjectMaterialHarvest(actor, itemKind, source, useHarvestBonus);
        public bool CanReturnWithHarvest(string actor, string itemKind, global::ClankerWorld.Simulation.World.MapResource source, global::ClankerWorld.Simulation.World.GridPoint destination, int destinationRange = 0, bool useHarvestBonus = true) => runtime.CanReturnWithHarvest(actor, itemKind, source, destination, destinationRange, useHarvestBonus);
        public bool HasCarriedOwnItem(string actor, string kind) => runtime.HasCarriedOwnItem(actor, kind);
        public global::ClankerWorld.Simulation.Kernel.InventoryLot? SharedItem(string kind, string actor, global::ClankerWorld.Simulation.World.GridPoint? returnTo = null, int returnRange = 0) => runtime.SharedItem(kind, actor, returnTo, returnRange);
        public global::ClankerWorld.Simulation.World.MapResource? KnownOrderFirewoodSource(string actor, global::ClankerWorld.Simulation.World.AgentState person, global::ClankerWorld.Simulation.World.GridPoint? returnTo = null) => OrderSystem.KnownOrderFirewoodSource(runtime.ShelterOrdersWorld, actor, person, returnTo);
        public global::ClankerWorld.Simulation.World.SurvivalState? SurvivalState => runtime.survivalState;
        public global::ClankerWorld.Simulation.World.OwnerShelterBinding? SelectShelterBinding(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person) => OrderSystem.SelectShelterBinding(runtime.ShelterOrdersWorld, instruction, person);
        public bool CanInspectShelterTarget(global::ClankerWorld.Simulation.World.OrderTask order, string actor, global::ClankerWorld.Simulation.World.AgentState person) => OrderSystem.CanInspectShelterTarget(runtime.ShelterOrdersWorld, order, actor, person);
        public bool ShelterBindingIsAvailable(string actor, global::ClankerWorld.Simulation.World.OrderTask order) => OrderSystem.ShelterBindingIsAvailable(runtime.ShelterOrdersWorld, actor, order);
        public bool CanObtainFirewood(string actor, global::ClankerWorld.Simulation.World.AgentState person, global::ClankerWorld.Simulation.World.GridPoint? returnTo = null) => OrderSystem.CanObtainFirewood(runtime.ShelterOrdersWorld, actor, person, returnTo);
        public OrderStore Orders => runtime.orderStore;
        public void MoveToward(string agentId, global::ClankerWorld.Simulation.World.AgentState state, global::ClankerWorld.Simulation.World.GridPoint destination, string reason, int interactionRange = 0, int horseMovementBudget = 100) => runtime.MoveToward(agentId, state, destination, reason, interactionRange, horseMovementBudget);
        public global::System.Collections.Generic.IReadOnlyDictionary<string, global::ClankerWorld.Simulation.World.AgentState> Agents => runtime.agents;
        public bool RecordKnowledgeFact(string actor, global::ClankerWorld.Simulation.World.GridPoint position) => runtime.RecordKnowledgeFact(actor, position);
        public void SetOrderStatus(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.OrderStatus status, string? reason, bool waitForDecision = false) => runtime.SetOrderStatus(instruction, status, reason, waitForDecision);
        public string ShelterOrderBlockedReason(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person) => runtime.ShelterOrderBlockedReason(instruction, person);
        public void CollectEquipment(string actor, global::ClankerWorld.Simulation.World.AgentState person, string kind, global::ClankerWorld.Simulation.World.GridPoint? returnTo = null, int returnRange = 0, string? plannedLotId = null) => runtime.CollectEquipment(actor, person, kind, returnTo, returnRange, plannedLotId);
        public global::ClankerWorld.Simulation.World.OrderSystem.MaterialGatherEffect? GatherProjectMaterial(string agentId, global::ClankerWorld.Simulation.World.AgentState state, string itemKind, global::ClankerWorld.Simulation.World.MapResource source, bool useHarvestBonus = true, string? deliveryBuildingId = null, global::ClankerWorld.Simulation.World.GridPoint? returnTo = null, int returnRange = 0) => runtime.GatherProjectMaterial(agentId, state, itemKind, source, useHarvestBonus, deliveryBuildingId, returnTo, returnRange);
        public global::ClankerWorld.Simulation.World.WorldRuntime.FireFuelEffect? TendFireAt(string actor, global::ClankerWorld.Simulation.World.AgentState person, global::ClankerWorld.Simulation.World.PlacedBuilding building) => runtime.TendFireAt(actor, person, building);
        public long WorldTick => runtime.WorldTick;
        public void CreditOrderEffect(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.Identity effectId, int units) => runtime.CreditOrderEffect(instruction, effectId, units);
        public string ShelterOrderReceipt(string instructionId, global::ClankerWorld.Simulation.World.OrderTask order) => WorldRuntime.ShelterOrderReceipt(instructionId, order);
        public bool MayFollowOwnerOrder(string actor, global::ClankerWorld.Simulation.World.OrderTask order) => runtime.MayFollowOwnerOrder(actor, order);
        public void MarkCheckpointCurrent() => runtime.checkpointSchemaVersion = StateSchemaVersion;
    }
}

internal interface IGuardianOrdersWorld
{
    bool IsAdult(string actor);
    global::ClankerWorld.Simulation.Society.SocietyCheckpoint Society { get; }
    bool NeedsCaregiver(string child);
    global::System.Collections.Generic.IReadOnlyDictionary<string, global::ClankerWorld.Simulation.World.AgentState> Agents { get; }
    global::ClankerWorld.Simulation.World.DependentCareActionPlan? PlanDependentCare(string actor, global::ClankerWorld.Simulation.World.DependentCareChoice choice);
    global::ClankerWorld.Simulation.World.ActionCheck CheckDependentCarePlan(global::ClankerWorld.Simulation.World.DependentCareActionPlan plan, global::ClankerWorld.Simulation.World.ActorCapabilityPolicy? policy = null);
    bool CanAcceptGuardian(string adult, string child);
    void SetOrderStatus(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.OrderStatus status, string? reason, bool waitForDecision = false);
    string GuardianOrderBlockedReason(string adult, string child);
    void ApplyDependentCareCandidate(string actor, string candidate, global::ClankerWorld.Simulation.World.ActionPlan? originalPlan = null);
    void CreditOrderEffect(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.Identity effectId, int units);
    string GuardianOrderEffectId(string adult, string child);
    bool ReadyForBriefInteraction(string actor);
}

internal interface IKnowledgeCopyOrdersWorld
{
    global::ClankerWorld.Simulation.World.AgentKnowledgeArtifact[] HeldKnowledgeArtifacts(string ownerId);
    global::ClankerWorld.Simulation.World.AgentKnowledgeFact[] FactsToWrite(string actor, string kind, global::ClankerWorld.Simulation.World.AgentKnowledgeArtifact? source = null);
    global::ClankerWorld.Simulation.World.AgentRecipeKnowledge[] RecipesToWrite(string actor, string kind, global::ClankerWorld.Simulation.World.AgentKnowledgeArtifact? source = null);
    bool HasCompleteCopyContents(string actor, global::ClankerWorld.Simulation.World.AgentKnowledgeArtifact source, global::ClankerWorld.Simulation.World.AgentKnowledgeFact[] facts, global::ClankerWorld.Simulation.World.AgentRecipeKnowledge[] recipes);
    bool AlreadyWrote(string actor, string kind, global::ClankerWorld.Simulation.World.AgentKnowledgeFact[] facts, global::ClankerWorld.Simulation.World.AgentRecipeKnowledge[] recipes);
    global::ClankerWorld.Simulation.World.AgentKnowledgeArtifact? KnowledgeCopySourceFor(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction);
}

internal interface IKnowledgeOrdersWorld
{
    bool IsAdult(string actor);
    global::ClankerWorld.Simulation.World.AgentKnowledgeWritingProject? KnowledgeWritingFor(string actor);
    bool MatchesKnowledgeSource(global::ClankerWorld.Simulation.World.OrderTask order, string? source, bool bound);
    bool IsCurrentKnowledgeOrderProject(global::ClankerWorld.Simulation.World.AgentKnowledgeWritingProject project);
    bool MayMakeKnowledgeArtifact(string actor);
    global::ClankerWorld.Simulation.World.AgentKnowledgeArtifact? KnowledgeCopySourceFor(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction);
    bool CanSupplyWriting(string actor, string kind);
    global::ClankerWorld.Simulation.World.AgentKnowledgeFact[] FactsToWrite(string actor, string kind, global::ClankerWorld.Simulation.World.AgentKnowledgeArtifact? source = null);
    global::ClankerWorld.Simulation.World.AgentRecipeKnowledge[] RecipesToWrite(string actor, string kind, global::ClankerWorld.Simulation.World.AgentKnowledgeArtifact? source = null);
    bool AlreadyWrote(string actor, string kind, global::ClankerWorld.Simulation.World.AgentKnowledgeFact[] facts, global::ClankerWorld.Simulation.World.AgentRecipeKnowledge[] recipes);
    string KnowledgeCopyBlockedReason(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction);
    global::ClankerWorld.Simulation.Cognition.CognitionCandidate? KnowledgeOrderCandidateFor(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction);
    void SetOrderStatus(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.OrderStatus status, string? reason, bool waitForDecision = false);
    string KnowledgeOrderBlockedReason(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction);
    global::ClankerWorld.Simulation.Society.SocietyCheckpoint Society { get; }
    string KnowledgeWritePrefix { get; }
    OrderStore Orders { get; }
    string KnowledgeCopyPrefix { get; }
    void ApplyKnowledgeWritingCandidate(string actor, global::ClankerWorld.Simulation.World.AgentState person, string candidate, global::ClankerWorld.Simulation.World.ActionPlan? originalPlan = null);
    global::System.Collections.Generic.IReadOnlyDictionary<string, global::ClankerWorld.Simulation.World.AgentState> Agents { get; }
    void ReplaceKnowledgeWriting(global::ClankerWorld.Simulation.World.AgentKnowledgeWritingProject project);
    void ContinueKnowledgeWriting(global::ClankerWorld.Simulation.World.AgentKnowledgeWritingProject project);
    bool IsActiveOrder(string status);
    global::ClankerWorld.Simulation.World.OwnerQueuedInstruction? PendingInstructionFor(string agentId);
    void ClearKnowledgeOrderBinding(global::ClankerWorld.Simulation.World.AgentKnowledgeWritingProject project);
    void CreditOrderEffect(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.Identity effectId, int units);
    void CancelKnowledgeWriting(global::ClankerWorld.Simulation.World.AgentKnowledgeWritingProject project, string reason);
}

internal interface IKnowledgeReadOrdersWorld
{
    global::ClankerWorld.Simulation.World.WorldKnowledgeState Knowledge { get; }
    global::ClankerWorld.Simulation.World.AgentKnowledgeArtifact[] HeldKnowledgeArtifacts(string ownerId);
    bool HasUnknownArtifactContents(string actor, global::ClankerWorld.Simulation.World.AgentKnowledgeArtifact artifact);
    bool IsAdult(string actor);
    global::ClankerWorld.Simulation.World.AgentKnowledgeWritingProject? KnowledgeWritingFor(string actor);
    global::ClankerWorld.Simulation.World.AgentKnowledgeArtifact? KnowledgeReadOrderArtifact(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction);
    bool KnowsMapFact(string agentId, global::ClankerWorld.Simulation.World.GridPoint position);
    bool KnowsRecipe(string actor, string recipeId);
    string? KnowledgeReadOrderBlocker(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction);
    void SetOrderStatus(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.OrderStatus status, string? reason, bool waitForDecision = false);
    int ReadIfKnowledgeArtifact(string lotId, string sourceAgentId, string recipientId, global::ClankerWorld.Simulation.World.AgentKnowledgeArtifact? selectedArtifact = null);
    long WorldTick { get; }
    OrderStore Orders { get; }
    void CreditOrderEffect(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.Identity effectId, int units);
    string KnowledgeReadOrderEffectId(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction);
}

internal interface IMarriageOrdersWorld
{
    bool IsAdult(string actor);
    string? CurrentMarriageOrderPartner(string actor);
    global::ClankerWorld.Simulation.Society.SocietyCheckpoint Society { get; }
    global::System.Collections.ObjectModel.ReadOnlyCollection<global::ClankerWorld.Simulation.World.AgentMarriage> Marriages { get; }
    string? MarriageOrderBlocker(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction);
    OrderStore Orders { get; }
    bool IsActiveOrder(string status);
    string? UnacceptedMarriageOrderBlocker(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction);
    void SetOrderStatus(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.OrderStatus status, string? reason, bool waitForDecision = false);
    global::ClankerWorld.Simulation.Cognition.AgentConversation? ConversationForOrder(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction);
    global::System.Collections.ObjectModel.ReadOnlyCollection<global::ClankerWorld.Simulation.Cognition.AgentConversation> Conversations { get; }
}

internal interface IShelterOrdersWorld
{
    global::ClankerWorld.Simulation.Society.SocietyCheckpoint Society { get; }
    bool HasHouseGuestInvitation(string actor, string houseId);
    bool IsTownHallStormRefuge(string actor, global::ClankerWorld.Simulation.World.PlacedBuilding building);
    bool IsWithinInteractionRange(global::ClankerWorld.Simulation.World.GridPoint origin, global::ClankerWorld.Simulation.World.GridPoint destination, int interactionRange);
    int ResourceInteractionRange { get; }
    bool IsSwimming(string actor, global::ClankerWorld.Simulation.World.GridPoint position);
    bool IsTownHall(global::ClankerWorld.Simulation.World.PlacedBuilding building);
    global::ClankerWorld.Simulation.Content.DeclarativeWorldContentState Content { get; }
    global::System.Collections.Generic.IEnumerable<global::ClankerWorld.Simulation.World.PlacedBuilding> AccessibleHeatingBuildings(string actor);
    global::System.Collections.Generic.IEnumerable<global::ClankerWorld.Simulation.World.PlacedBuilding> AccessibleShelters(string actor, bool includeStormRefuge = true);
    global::ClankerWorld.Simulation.World.WeatherKind WeatherAt(global::ClankerWorld.Simulation.World.GridPoint position);
    bool NaturalStormCover(global::ClankerWorld.Simulation.World.GridPoint point);
    global::ClankerWorld.Simulation.World.PlacedBuilding? BoundShelterBuilding(string actor, global::ClankerWorld.Simulation.World.OrderTask order);
    bool IsFireLit(global::ClankerWorld.Simulation.World.PlacedBuilding building);
    bool ShelterBuildingCovers(string actor, global::ClankerWorld.Simulation.World.PlacedBuilding building, global::ClankerWorld.Simulation.World.GridPoint point);
    bool ShelterBuildingKnown(string actor, global::ClankerWorld.Simulation.World.AgentState person, global::ClankerWorld.Simulation.World.PlacedBuilding building);
    global::ClankerWorld.Simulation.World.WorldMap Map { get; }
    global::ClankerWorld.Simulation.World.GridPoint? ReachableHallShelterPoint(string actor, global::ClankerWorld.Simulation.World.GridPoint origin, global::ClankerWorld.Simulation.World.PlacedBuilding hall);
    bool CanReachLeg(string actor, global::ClankerWorld.Simulation.World.GridPoint origin, global::ClankerWorld.Simulation.World.GridPoint target, global::ClankerWorld.Simulation.World.ReachPurpose purpose, int range, int load, global::ClankerWorld.Simulation.World.SwimmingPolicy swimming, global::ClankerWorld.Simulation.World.ReachObstacles obstacles = (global::ClankerWorld.Simulation.World.ReachObstacles)0);
    global::ClankerWorld.Simulation.World.GridPoint? NearbyNaturalStormCover(string actor, global::ClankerWorld.Simulation.World.GridPoint origin);
    global::System.Collections.ObjectModel.ReadOnlyCollection<global::ClankerWorld.Simulation.World.GridPoint> Reach(string actor, global::ClankerWorld.Simulation.World.GridPoint target, global::ClankerWorld.Simulation.World.ReachPurpose purpose, int range, int load, global::ClankerWorld.Simulation.World.SwimmingPolicy swimming, global::ClankerWorld.Simulation.World.ReachObstacles obstacles = (global::ClankerWorld.Simulation.World.ReachObstacles)0);
    global::ClankerWorld.Simulation.World.WorldKnowledgeState Knowledge { get; }
    global::System.Collections.Generic.Dictionary<string, global::ClankerWorld.Simulation.World.ResourceState> Resources { get; }
    bool CanGatherFromSource(string actor, string itemKind, global::ClankerWorld.Simulation.World.MapResource source, global::System.Collections.Generic.Dictionary<(global::ClankerWorld.Simulation.World.ToolFamily Family, int Tier), global::ClankerWorld.Simulation.World.ToolDefinition?> reachableToolCache);
    int FreeCarryCapacity(string actor);
    global::ClankerWorld.Simulation.World.ToolGatheringPlan? ProjectMaterialHarvest(string actor, string itemKind, global::ClankerWorld.Simulation.World.MapResource source, bool useHarvestBonus = true);
    bool CanReturnWithHarvest(string actor, string itemKind, global::ClankerWorld.Simulation.World.MapResource source, global::ClankerWorld.Simulation.World.GridPoint destination, int destinationRange = 0, bool useHarvestBonus = true);
    bool HasCarriedOwnItem(string actor, string kind);
    global::ClankerWorld.Simulation.Kernel.InventoryLot? SharedItem(string kind, string actor, global::ClankerWorld.Simulation.World.GridPoint? returnTo = null, int returnRange = 0);
    global::ClankerWorld.Simulation.World.MapResource? KnownOrderFirewoodSource(string actor, global::ClankerWorld.Simulation.World.AgentState person, global::ClankerWorld.Simulation.World.GridPoint? returnTo = null);
    global::ClankerWorld.Simulation.World.SurvivalState? SurvivalState { get; }
    global::ClankerWorld.Simulation.World.OwnerShelterBinding? SelectShelterBinding(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person);
    bool CanInspectShelterTarget(global::ClankerWorld.Simulation.World.OrderTask order, string actor, global::ClankerWorld.Simulation.World.AgentState person);
    bool ShelterBindingIsAvailable(string actor, global::ClankerWorld.Simulation.World.OrderTask order);
    bool CanObtainFirewood(string actor, global::ClankerWorld.Simulation.World.AgentState person, global::ClankerWorld.Simulation.World.GridPoint? returnTo = null);
    OrderStore Orders { get; }
    void MoveToward(string agentId, global::ClankerWorld.Simulation.World.AgentState state, global::ClankerWorld.Simulation.World.GridPoint destination, string reason, int interactionRange = 0, int horseMovementBudget = 100);
    global::System.Collections.Generic.IReadOnlyDictionary<string, global::ClankerWorld.Simulation.World.AgentState> Agents { get; }
    bool RecordKnowledgeFact(string actor, global::ClankerWorld.Simulation.World.GridPoint position);
    void SetOrderStatus(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.OrderStatus status, string? reason, bool waitForDecision = false);
    string ShelterOrderBlockedReason(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person);
    void CollectEquipment(string actor, global::ClankerWorld.Simulation.World.AgentState person, string kind, global::ClankerWorld.Simulation.World.GridPoint? returnTo = null, int returnRange = 0, string? plannedLotId = null);
    global::ClankerWorld.Simulation.World.OrderSystem.MaterialGatherEffect? GatherProjectMaterial(string agentId, global::ClankerWorld.Simulation.World.AgentState state, string itemKind, global::ClankerWorld.Simulation.World.MapResource source, bool useHarvestBonus = true, string? deliveryBuildingId = null, global::ClankerWorld.Simulation.World.GridPoint? returnTo = null, int returnRange = 0);
    global::ClankerWorld.Simulation.World.WorldRuntime.FireFuelEffect? TendFireAt(string actor, global::ClankerWorld.Simulation.World.AgentState person, global::ClankerWorld.Simulation.World.PlacedBuilding building);
    long WorldTick { get; }
    void CreditOrderEffect(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.Identity effectId, int units);
    string ShelterOrderReceipt(string instructionId, global::ClankerWorld.Simulation.World.OrderTask order);
    bool MayFollowOwnerOrder(string actor, global::ClankerWorld.Simulation.World.OrderTask order);
    void MarkCheckpointCurrent();
}

internal interface ITalkOrdersWorld
{
    global::ClankerWorld.Simulation.Society.SocietyCheckpoint Society { get; }
    bool IsLinkedTalkOrder(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction);
    global::ClankerWorld.Simulation.Cognition.AgentConversation? ConversationFor(string agentId);
    string? TalkOrderBlocker(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person);
    string? CurrentMarriageOrderPartner(string actor);
    string? MarriageOrderBlocker(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction);
    global::System.Collections.Generic.IReadOnlyDictionary<string, global::ClankerWorld.Simulation.World.AgentState> Agents { get; }
    global::ClankerWorld.Simulation.World.BoatState? PassengerBoat(string actor);
    bool NeedsUrgentFood(global::ClankerWorld.Simulation.World.AgentState person);
    bool NeedsUrgentWarmth(global::ClankerWorld.Simulation.World.AgentState person);
    bool HasExplicitConversationProvider(string agentId);
    bool HasConversationAllowance(string agentId);
    global::System.Collections.ObjectModel.ReadOnlyCollection<global::ClankerWorld.Simulation.Cognition.AgentConversation> Conversations { get; }
    bool ActiveTalkOrderUses(string conversationId);
    bool IsWithinInteractionRange(global::ClankerWorld.Simulation.World.GridPoint origin, global::ClankerWorld.Simulation.World.GridPoint destination, int interactionRange);
    int ResourceInteractionRange { get; }
    global::System.Collections.ObjectModel.ReadOnlyCollection<global::ClankerWorld.Simulation.World.GridPoint> Reach(string actor, global::ClankerWorld.Simulation.World.GridPoint target, global::ClankerWorld.Simulation.World.ReachPurpose purpose, int range, int load, global::ClankerWorld.Simulation.World.SwimmingPolicy swimming, global::ClankerWorld.Simulation.World.ReachObstacles obstacles = (global::ClankerWorld.Simulation.World.ReachObstacles)0);
    void CompleteClosedTalkOrders();
    string? UnacceptedMarriageOrderBlocker(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction);
    void SetOrderStatus(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.OrderStatus status, string? reason, bool waitForDecision = false);
    global::ClankerWorld.Simulation.Cognition.AgentConversation? ConversationForOrder(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction);
    global::System.Collections.ObjectModel.ReadOnlyCollection<global::ClankerWorld.Simulation.World.AgentMarriage> Marriages { get; }
    OrderStore Orders { get; }
    void MoveToward(string agentId, global::ClankerWorld.Simulation.World.AgentState state, global::ClankerWorld.Simulation.World.GridPoint destination, string reason, int interactionRange = 0, int horseMovementBudget = 100);
    bool ProposeConversation(string initiatorId, string inviteeId);
    bool IsSurvivalCandidate(string actor, string candidateId);
    bool IsMarketFoodCandidate(string actor, string candidateId);
    bool ApplyMarketCandidate(string actor, global::ClankerWorld.Simulation.World.AgentState state, string id, global::ClankerWorld.Simulation.World.ActionPlan? originalPlan = null);
    void ApplyCandidate(string agentId, global::ClankerWorld.Simulation.World.AgentState state, string candidateId, bool reportIdle);
    global::System.Collections.Generic.List<global::ClankerWorld.Simulation.Cognition.CognitionCandidate> ConversationCandidates(string agentId);
    bool ApplyConversationCandidate(string agentId, string candidateId);
    void RefreshMarriageOrderEligibilityStatus(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction);
    bool IsActiveOrder(string status);
    string? MarriageOrderOutcome(global::ClankerWorld.Simulation.Cognition.AgentConversation conversation, global::System.Collections.Generic.IEnumerable<global::ClankerWorld.Simulation.World.AgentMarriage> marriages);
    void CreditOrderEffect(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.Identity effectId, int units);
    string TalkOrderEffectId(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction);
}
