using ClankerWorld.Simulation.Cognition;

namespace ClankerWorld.Simulation.World;

internal sealed partial class CognitionSystem
{
    internal static bool IsMatchingIdentityMomentResponse(CognitionDecisionRequest request, CognitionDecisionResponse? response)
    {
        try
        {
            response?.Validate();
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch (InvalidDataException)
        {
            return false;
        }
        return response is not null && response.RequestId == request.RequestId &&
            response.AgentId == request.Observation.AgentId && response.Provider == DecisionProviderKind.LargeLanguageModel &&
            response.ProviderEpoch == request.ProviderEpoch && response.RunEpoch == request.Observation.RunEpoch &&
            response.DecisionGeneration == request.Observation.DecisionGeneration &&
            response.ObservationDigest == request.Observation.ObservationDigest && response.SelectedCandidateId == "identity_optional";
    }
}
