using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.Playtest;

public sealed record SettlementBallot(string Policy, long ProposedTick, long ExpiryTick,
    IReadOnlyList<string> Electorate, IReadOnlyList<string> Approvals, IReadOnlyList<string> Rejections);

public sealed record SettlementCouncil(string? StewardId, string FoodPolicy, long LastResolutionTick,
    SettlementBallot? Ballot = null);

public sealed partial class PrivateWorldRuntime
{
    private SettlementCouncil? council
    {
        get => civicSystem.State.Council;
        set => civicSystem.RestoreCouncil(value);
    }

    private string[] CouncilMembers() => CivicSystem.CouncilMembers(civicWorld);
    private int ContributionScore(string actor) => CivicSystem.ContributionScore(civicWorld, actor);
    private int SharedFoodQuantity() => CivicSystem.SharedFoodQuantity(civicWorld);
    private string? ProposedFoodPolicy(string actor) => civicSystem.ProposedFoodPolicy(civicWorld, actor);
    private bool HasCouncilDecision(string actor) => civicSystem.HasCouncilDecision(civicWorld, actor);
    private int SharedFoodCollectionAllowance(string actor) => civicSystem.SharedFoodCollectionAllowance(civicWorld, actor);
    private bool MayCollectSharedFood(string actor) => civicSystem.MayCollectSharedFood(civicWorld, actor);
    private void AdvanceSettlementCouncil() => civicSystem.AdvanceSettlementCouncil(civicWorld);
    private void ResolveCouncilBallot() => civicSystem.ResolveCouncilBallot(civicWorld);
    private void AddCouncilCandidates(List<CognitionCandidate> candidates, string actor) => civicSystem.AddCouncilCandidates(civicWorld, candidates, actor);
    private void ApplyCouncilCandidate(string actor, string candidate) => civicSystem.ApplyCouncilCandidate(civicWorld, actor, candidate);
    private static void ValidateCouncil(PrivateWorldRuntimeState state) => CivicSystem.ValidateCouncil(state);
}
