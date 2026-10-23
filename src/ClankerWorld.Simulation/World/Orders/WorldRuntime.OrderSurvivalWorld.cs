namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private IOrderSurvivalWorld OrderSurvivalWorld => new OrderSurvivalAdapter(this);

    private sealed class OrderSurvivalAdapter(WorldRuntime runtime) : IOrderSurvivalWorld
    {
        public bool NeedsUrgentFood(global::ClankerWorld.Simulation.World.AgentState person) => WorldRuntime.NeedsUrgentFood(person);
        public bool IsFoodSurvivalCandidate(string actor, string candidateId) => OrderSystem.IsFoodSurvivalCandidate(runtime.OrderSurvivalWorld, actor, candidateId);
        public bool NeedsUrgentWarmth(global::ClankerWorld.Simulation.World.AgentState person) => runtime.NeedsUrgentWarmth(person);
        public global::ClankerWorld.Simulation.Cognition.CognitionCandidate? SelectOrderSurvivalCandidate(global::System.Collections.Generic.IEnumerable<global::ClankerWorld.Simulation.Cognition.CognitionCandidate> candidates, global::ClankerWorld.Simulation.World.AgentState person, global::ClankerWorld.Simulation.World.OwnerQueuedInstruction? instruction) => runtime.SelectOrderSurvivalCandidate(candidates, person, instruction);
        public global::System.Collections.Generic.List<global::ClankerWorld.Simulation.Cognition.CognitionCandidate> CreateCandidates(string agentId, global::ClankerWorld.Simulation.World.AgentState state, bool restrictForOrder = true) => runtime.CreateCandidates(agentId, state, restrictForOrder);
        public bool IsSurvivalCandidate(string actor, string candidateId) => runtime.IsSurvivalCandidate(actor, candidateId);
        public string TownProjectReturnPrefix => WorldRuntime.TownProjectReturnPrefix;
        public global::System.Collections.Generic.IReadOnlyDictionary<string, global::ClankerWorld.Simulation.World.AgentState> Agents => runtime.agents;
        public bool IsShopFoodCandidate(string actor, string candidateId, global::ClankerWorld.Simulation.World.ShopBuyActionPlan? selectedPlan = null) => runtime.IsShopFoodCandidate(actor, candidateId, selectedPlan);
        public bool IsMarketFoodCandidate(string actor, string candidateId) => runtime.IsMarketFoodCandidate(actor, candidateId);
    }
}
