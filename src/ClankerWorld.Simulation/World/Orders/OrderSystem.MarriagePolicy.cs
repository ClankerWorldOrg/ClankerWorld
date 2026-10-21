namespace ClankerWorld.Simulation.World;

internal sealed partial class OrderSystem
{
    internal const string MarriageAdultBlocker = "Only an adult can propose marriage.";
    internal const string MarriagePartnerBlocker = "Waiting for an eligible current partner before attempting marriage.";
    internal const string MarriageChangedPartnerBlocker = "The selected person is no longer your current partner; this order will not choose someone else.";
    internal const string MarriageEligibilityBlocker = "This partnership is not currently eligible for marriage; both adults need chosen full names and must be free to marry.";

    internal const string MarriageOrderTask = "attempt marriage with your current partner; both people keep their consent and surname choices";
}
