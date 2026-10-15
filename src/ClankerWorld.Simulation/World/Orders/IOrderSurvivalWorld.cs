using System.Collections.ObjectModel;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

internal interface IOrderSurvivalWorld
{
    string TownProjectReturnPrefix { get; }
    IReadOnlyDictionary<string, AgentState> Agents { get; }
    bool IsShelterOrder(string action);
    bool NeedsUrgentFood(AgentState person);
    bool NeedsUrgentWarmth(AgentState person);
    System.Collections.Generic.List<ClankerWorld.Simulation.Cognition.CognitionCandidate> CreateCandidates(string agentId, AgentState state, bool restrictForOrder = true);
    bool IsShopFoodCandidate(string actor, string candidateId);
    bool IsMarketFoodCandidate(string actor, string candidateId);
}
