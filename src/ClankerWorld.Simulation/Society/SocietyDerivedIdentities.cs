using System.Globalization;

namespace ClankerWorld.Simulation.Society;

public static partial class SocietyRules
{
    private static string GuardianCareId(string child, string adult, long tick) => DerivedId.Create($"guardian-care:{child}:{adult}:{tick}");
    private static string GuardianMembershipId(string household, string child, long tick, int suffix) =>
        DerivedId.Create($"{household}:guardian-membership:{child}:{tick}" + (suffix == 0 ? "" : $":{suffix}"));
    private static string DependentCareId(string adult, string child, long tick) => DerivedId.Create($"dependent-care:{adult}:{child}:{tick}");
    private static string ReplacementCareId(string adult, string child, long tick) => DerivedId.Create($"replacement-care:{adult}:{child}:{tick}");
    private static string DomesticPartnershipId(string relationship) => DerivedId.Create($"domestic:partnership:{relationship}");
    private static string DomesticSeparatedId(string relationship, string person) => DerivedId.Create($"domestic:separate:{relationship}:{person}");
    private static string EstateId(string person, long tick) => DerivedId.Create($"estate:{person}:{tick.ToString(CultureInfo.InvariantCulture)}");
    private static string EstateSplitLotId(string lot, string estate, string owner, string? storage) =>
        DerivedId.Create($"{lot}#estate:{estate}:{owner}" + (storage is null ? "" : ":storage:" + storage));
    private static string FinalWordsMemoryId(string estate, string listener) => DerivedId.Create($"final-words:{estate}:{listener}", "final-words:sha256:");
}
