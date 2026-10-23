using ClankerWorld.Simulation.Cognition;

namespace ClankerWorld.Simulation.World;

public sealed record FoodPolicyBallot(string Policy, long ProposedTick, long ExpiryTick,
    IReadOnlyList<string> Electorate, IReadOnlyList<string> Approvals, IReadOnlyList<string> Rejections);

public sealed record FoodStewardship(string? StewardId, string FoodPolicy, long LastResolutionTick,
    FoodPolicyBallot? Ballot = null);

public sealed partial class WorldRuntime
{
    private FoodStewardship? foodStewardship
    {
        get => townSystem.State.FoodStewardship;
        set => townSystem.RestoreFoodStewardship(value);
    }

    private string[] FoodPolicyVoters() => TownSystem.FoodPolicyVoters(civicWorld);
    private int ContributionScore(string actor) => TownSystem.ContributionScore(civicWorld, actor);
    private int SharedFoodQuantity() => TownSystem.SharedFoodQuantity(civicWorld);
    private string? ProposedFoodPolicy(string actor) => townSystem.ProposedFoodPolicy(civicWorld, actor);
    private bool HasFoodPolicyDecision(string actor) => townSystem.HasFoodPolicyDecision(civicWorld, actor);
    private int SharedFoodCollectionAllowance(string actor) => townSystem.SharedFoodCollectionAllowance(civicWorld, actor);
    private bool MayCollectSharedFood(string actor) => townSystem.MayCollectSharedFood(civicWorld, actor);
    private void AdvanceFoodStewardship() => townSystem.AdvanceFoodStewardship(civicWorld);
    private void ResolveFoodPolicyBallot() => townSystem.ResolveFoodPolicyBallot(civicWorld);
    private void AddFoodPolicyCandidates(List<CognitionCandidate> candidates, string actor) => townSystem.AddFoodPolicyCandidates(civicWorld, candidates, actor);
    private static void ValidateFoodStewardship(WorldRuntimeState state) => TownSystem.ValidateFoodStewardship(state);
}
