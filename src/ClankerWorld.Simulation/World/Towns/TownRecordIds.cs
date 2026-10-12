namespace ClankerWorld.Simulation.World;

internal static class TownRecordIds
{
    internal static string Nomination(string actor, string nominee) => DerivedId.Create("nomination:" + actor + "->" + nominee);
    internal static string Admission(string subject) => DerivedId.Create("admission:" + subject);
    internal static string LandRequest(string town, int ordinal) => DerivedId.Create("land-request:" + town + ":" + ordinal);
    internal static string UnopenedHearing(string proposal) => DerivedId.Create(proposal + ":unopened");
    internal static string LawEvidence(string law, int version) => DerivedId.Create("law:" + law + ":" + version);
    internal static string PersonParty(string role, string person) => DerivedId.Create(role + ":" + person);
    internal static string TownParty(string town) => DerivedId.Create("town:" + town);
    internal static string DeliveryInput(string delivery) => DerivedId.Create(delivery + ":input");
    internal static string DeliveryFinal(string delivery) => DerivedId.Create(delivery + ":deliver");
    internal static string DepositOperation(string receipt) => DerivedId.Create(receipt + ":deposit");
    internal static string Election(string town, long sequence) => DerivedId.Numbered(town + ":election:", sequence);
    internal static string Proposal(string town, long sequence) => DerivedId.Numbered(town + ":proposal:", sequence);
    internal static string Government(string town, long sequence) => DerivedId.Numbered(town + ":government:", sequence);
    internal static string MayoralContest(string town, long sequence) => DerivedId.Numbered(town + ":mayor:", sequence);
    internal static string Law(string town, long sequence) => DerivedId.Numbered(town + ":law:", sequence);
    internal static string LandTransfer(string town, long sequence) => DerivedId.Numbered("land-transfer:" + town + ":", sequence);
    internal static string LandCase(string town, long sequence) => DerivedId.Numbered("land-case:" + town + ":", sequence);
    internal static string LandRuling(string town, long sequence) => DerivedId.Numbered("land-ruling:" + town + ":", sequence);
    internal static string LandAdjustment(string town, long sequence) => DerivedId.Numbered("land-adjustment:" + town + ":", sequence);
    internal static string LandReopen(string town, long sequence) => DerivedId.Numbered("land-reopen:" + town + ":", sequence);
}
