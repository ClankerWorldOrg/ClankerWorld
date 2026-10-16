namespace ClankerWorld.Contracts;

/// <summary>Authoritative need states; the server sends their labels in observation factors.</summary>
public static class NeedStates
{
    public const int ColdThreshold = 3_500;
    public const int IllnessThreshold = 1_500;
    public const int VeryHungryThreshold = 2_500;

    public static string FullnessState(int fullness) => fullness switch
    {
        >= 7_000 => "well fed",
        >= 3_500 => "fed",
        >= VeryHungryThreshold => "hungry",
        _ => "very hungry",
    };

    public static string WarmthState(int warmth) => warmth < ColdThreshold ? "cold" : "warm";
    public static string IllnessState(int illness) => illness >= IllnessThreshold ? "ill" : "healthy";
}
