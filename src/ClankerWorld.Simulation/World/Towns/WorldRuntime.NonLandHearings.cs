using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private static string NonLandReportToken(TownViolationAllegation report) => TownSystem.NonLandReportToken(report);
    private static string NonLandOfferToken(TownRemedyOffer offer) => TownSystem.NonLandOfferToken(offer);
    private static string NonLandCaseToken(TownNonLandCase item) => TownSystem.NonLandCaseToken(item);
    private static string NonLandActionToken(TownNonLandCase item) => TownSystem.NonLandActionToken(item);
}
