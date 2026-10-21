namespace ClankerWorld.Simulation.World;

internal interface IOrderSurvivalWorld
{
    bool NeedsUrgentFood(global::ClankerWorld.Simulation.World.AgentState person);
    bool IsFoodSurvivalCandidate(string actor, string candidateId);
    bool NeedsUrgentWarmth(global::ClankerWorld.Simulation.World.AgentState person);
    global::ClankerWorld.Simulation.Cognition.CognitionCandidate? SelectOrderSurvivalCandidate(global::System.Collections.Generic.IEnumerable<global::ClankerWorld.Simulation.Cognition.CognitionCandidate> candidates, global::ClankerWorld.Simulation.World.AgentState person, global::ClankerWorld.Simulation.World.OwnerQueuedInstruction? instruction);
    global::System.Collections.Generic.List<global::ClankerWorld.Simulation.Cognition.CognitionCandidate> CreateCandidates(string agentId, global::ClankerWorld.Simulation.World.AgentState state, bool restrictForOrder = true);
    bool IsSurvivalCandidate(string actor, string candidateId);
    string TownProjectReturnPrefix { get; }
    global::System.Collections.Generic.IReadOnlyDictionary<string, global::ClankerWorld.Simulation.World.AgentState> Agents { get; }
    bool IsShopFoodCandidate(string actor, string candidateId, global::ClankerWorld.Simulation.World.ShopBuyActionPlan? selectedPlan = null);
    bool IsMarketFoodCandidate(string actor, string candidateId);
}
