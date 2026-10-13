using ClankerWorld.GodotClient.Protocol;

namespace ClankerWorld.GodotClient.UI;

/// <summary>Shows the server's description without interpreting accepted history.</summary>
public static class WorldEventText
{
    public const string ContinuityRisk = "The world is at risk of dying out.";
    public static bool OffersNewcomer(OwnerWorldSnapshot? snapshot) =>
        snapshot is { ContinuityRuleActive: true, FounderSetup.Started: true };
    public static string Describe(OwnerWorldEvent worldEvent, OwnerWorldSnapshot? snapshot) =>
        snapshot?.EventTexts.TryGetValue(worldEvent.EventId, out var text) == true ? text : worldEvent.Text;
}
