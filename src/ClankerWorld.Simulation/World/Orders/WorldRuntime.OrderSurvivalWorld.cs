using System.Collections.ObjectModel;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private IOrderSurvivalWorld OrderSurvivalWorld => new OrderSurvivalAdapter(this);

    private sealed class OrderSurvivalAdapter(WorldRuntime runtime) : IOrderSurvivalWorld
    {
        public string TownProjectReturnPrefix => WorldRuntime.TownProjectReturnPrefix;
        public IReadOnlyDictionary<string, AgentState> Agents => runtime.agents;
        public bool IsShelterOrder(string action) => WorldRuntime.IsShelterOrder(action);
        public bool NeedsUrgentFood(AgentState person) => WorldRuntime.NeedsUrgentFood(person);
        public bool NeedsUrgentWarmth(AgentState person) => runtime.NeedsUrgentWarmth(person);
        public System.Collections.Generic.List<ClankerWorld.Simulation.Cognition.CognitionCandidate> CreateCandidates(string agentId, AgentState state, bool restrictForOrder = true) => runtime.CreateCandidates(agentId, state, restrictForOrder);
        public bool IsShopFoodCandidate(string actor, string candidateId) => runtime.IsShopFoodCandidate(actor, candidateId);
        public bool IsMarketFoodCandidate(string actor, string candidateId) => runtime.IsMarketFoodCandidate(actor, candidateId);
    }
}
