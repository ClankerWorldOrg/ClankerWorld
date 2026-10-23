using System.Collections.ObjectModel;
using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private abstract class GoodsRuntimeAdapter(WorldRuntime runtime)
    {
        protected WorldRuntime Runtime { get; } = runtime;

        public long WorldTick => Runtime.WorldTick;
        public void SetOrderStatus(OwnerQueuedInstruction instruction, OrderStatus status, string? reason, bool waitForDecision = false) => Runtime.SetOrderStatus(instruction, status, reason, waitForDecision);
        public void CreditOrderEffect(OwnerQueuedInstruction instruction, Identity effectId, int units) => Runtime.CreditOrderEffect(instruction, effectId, units);
        public IReadOnlyDictionary<string, AgentState> Agents => Runtime.agents;
        public SocietyCheckpoint Society => Runtime.society.Checkpoint;
        public bool IsAdult(string actor) => Runtime.IsAdult(actor);
        public OrderStore Orders => Runtime.orderStore;
        public void MoveToward(string agentId, AgentState state, GridPoint destination, string reason, int interactionRange = 0, int horseMovementBudget = 100) => Runtime.MoveToward(agentId, state, destination, reason, interactionRange, horseMovementBudget);
        public WorldContentSimulationState Simulation => Runtime.worldSimulation;
        public WorldMap Map => Runtime.map;
        public bool IsActiveOrder(string status) => WorldRuntime.IsActiveOrder(status);
        public DeclarativeWorldContentState Content => Runtime.worldContent;
        public void AppendEvent(WorldEventPayload payload, GridPoint? eventPosition = null) => Runtime.AppendEvent(payload, eventPosition);
        public int FreeCarryCapacity(string actor) => Runtime.FreeCarryCapacity(actor);
        public bool ReadyForBriefInteraction(string actor) => Runtime.ReadyForBriefInteraction(actor);
        public bool NeedsUrgentFood(AgentState person) => WorldRuntime.NeedsUrgentFood(person);
        public ReadOnlyCollection<GridPoint> Reach(string actor, GridPoint target, ReachPurpose purpose, int range, int load, SwimmingPolicy swimming, ReachObstacles obstacles = (ReachObstacles)0) => Runtime.Reach(actor, target, purpose, range, load, swimming, obstacles);
        public bool MayFollowOwnerOrder(string actor, OrderTask order) => Runtime.MayFollowOwnerOrder(actor, order);
        public bool IsWithinInteractionRange(GridPoint origin, GridPoint destination, int interactionRange) => Runtime.IsWithinInteractionRange(origin, destination, interactionRange);
        public void MarkCheckpointCurrent() => Runtime.checkpointSchemaVersion = StateSchemaVersion;
        public string HouseholdFor(string actor) => Runtime.HouseholdFor(actor);
        public int ResourceInteractionRange => WorldRuntime.ResourceInteractionRange;
        public bool NeedsUrgentWarmth(AgentState person) => Runtime.NeedsUrgentWarmth(person);
        public string FoodKnowledgeKind(MapResource resource) => WorldRuntime.FoodKnowledgeKind(resource);
        public PlacedBuilding? FarmhouseForHousehold(string householdId) => Runtime.FarmhouseForHousehold(householdId);
        public bool IsCartOrder(string action) => WorldRuntime.IsCartOrder(action);
        public bool IsAnimalOrder(string action) => WorldRuntime.IsAnimalOrder(action);
        public MapResource? KnownFoodSourceForOrder(OwnerQueuedInstruction instruction, AgentState person) => Runtime.KnownFoodSourceForOrder(instruction, person);
        public bool CanExploreForOrder(AgentState person) => Runtime.CanExploreForOrder(person);
        public PlacedBuilding? HouseholdBuildingWithTag(string householdId, string tag) => Runtime.HouseholdBuildingWithTag(householdId, tag);
        public AgentStore PhysicalAgents => Runtime.agentStore;
    }
}
