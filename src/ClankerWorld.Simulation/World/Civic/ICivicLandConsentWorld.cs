namespace ClankerWorld.Simulation.World;

internal interface ICivicLandConsentWorld : ICivicWorld
{
    string[] HouseholdAdults(string householdId);
}
