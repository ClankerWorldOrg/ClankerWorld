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
}
