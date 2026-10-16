namespace ClankerWorld.Simulation.World;

public static class NeedThresholds
{
    public const int NonLandReturnFoodFullness = 6_500;
    public const int MedicalCareIllness = 2_500;
    public const int ComfortableWarmth = 6_000;
    public const int UrgentWarmth = ClankerWorld.Contracts.NeedStates.ColdThreshold;
    public const int FoodPickupFullness = 7_000;
    public const int ComfortableFullness = 4_000;
    public const int RoutineFoodSeekFullness = 4_500;
    public const int UrgentFullness = 2_000;
    public const int ChildHelpingFullness = 6_000;
    public const int SpareFoodStorageFullness = ChildHelpingFullness;
}
