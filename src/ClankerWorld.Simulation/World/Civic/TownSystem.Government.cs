using System.Globalization;
using ClankerWorld.Simulation.Cognition;

namespace ClankerWorld.Simulation.World;

internal sealed partial class TownSystem
{
    internal static (TownCouncilState Council, TownGovernmentState Government) AdvanceCivic(ITownWorld world, TownRuntimeState town,
        TownCouncilState council, TownGovernmentState government)
    {
        var adults = TownAdults(world, town);
        var result = TownGovernmentRules.Advance(council, government, town.Id, town.Name, world.WorldSeed, adults, world.WorldTick, CivicDay(world), TownLandHearingElectionBusy(town) || TownNonLandElectionBusy(town));
        if (SystemInvariantChecks.Enabled)
        {
            TownGovernmentStateValidation.ValidateLocal(town.Id, result.Government, allowIntermediateVotes: false);
            ValidateCompletedGovernmentAuthority(result.Council, result.Government);
        }
        return result;
    }

    internal static void AddTownLawCandidates(ITownWorld world, List<CognitionCandidate> candidates, string actor, TownRuntimeState town)
    {
        const string format = "Write civic_proposal as 'subject: rule'. It needs the current council's votes, applies only from adoption " +
            "and records a social rule; it does not block actions, change ownership or create offices.";
        OfferTownLaw(world, candidates, actor, town, TownLawChoiceKind.Adopt, TownLawRules.Jurisdiction, "",
            $"Propose a law for everyone on {town.Name}'s claimed land, visitors included. {format}", 190);
        OfferTownLaw(world, candidates, actor, town, TownLawChoiceKind.Adopt, TownLawRules.ResidentDuty, "",
            $"Propose a duty for {town.Name}'s residents that applies wherever they are. {format}", 191);
        var here = world.Agents[actor].Position;
        if (TownLawRules.SiteAround(world.Map, here, town.Id, world.TownLandTitles).Length > 0)
            OfferTownLaw(world, candidates, actor, town, TownLawChoiceKind.Adopt, SiteToken(here), "",
                $"Propose a law for the claimed land within {TownLawRules.SiteRadius} tiles of where you stand in {town.Name}, visitors included. {format}", 192);
        var history = CivicHistory(town);
        var estateLaw = town.Government?.Laws.SingleOrDefault(law => TownLawRules.IsInForce(law) && TownLawRules.Current(law).EstateDefault is not null);
        var estateTarget = estateLaw is null ? "adopt" : estateLaw.Id + ":" + TownLawRules.Current(estateLaw).Version.ToString(CultureInfo.InvariantCulture);
        foreach (var share in new[] { 0, 25, 50, 75, 100 })
            OfferTownPolicy(world, candidates, actor, new(town.Id, TownPolicyKind.EstateDefault, estateTarget, share.ToString(CultureInfo.InvariantCulture)),
                $"Propose a supported default estate law: {TownEstateDefaultRules.Text(new(share))} " +
                "It needs the ordinary Council vote. No living person's belongings move; a valid will always wins. Whole vessels stay together, and undeliverable Town shares follow the household default.", 195);
        foreach (var law in town.Government?.Laws.Where(l => TownLawRules.IsInForce(l) && town.Council!.Notices.LastOrDefault(n => n.SubjectId == l.Id) is { } latest &&
                history.Known(actor).Contains(latest.Id))
            ?? [])
        {
            var number = TownLawRules.Number(law.Id).ToString(CultureInfo.InvariantCulture);
            var current = TownLawRules.Current(law);
            OfferTownLaw(world, candidates, actor, town, TownLawChoiceKind.Amend, law.Id, current.Version.ToString(CultureInfo.InvariantCulture),
                $"Propose amending {town.Name}'s law {number} ({current.Subject}); write the complete new wording in civic_proposal as 'subject: rule'. " +
                    "It keeps the law's scope, needs the council's votes and applies only from adoption.", 193);
            OfferTownLaw(world, candidates, actor, town, TownLawChoiceKind.Repeal, law.Id, current.Version.ToString(CultureInfo.InvariantCulture),
                $"Propose repealing {town.Name}'s law {number} ({current.Subject}). It needs the council's votes; earlier conduct stays under the law.", 194);
        }
    }

    internal static string SiteToken(GridPoint center) =>
        "site:" + center.X.ToString(CultureInfo.InvariantCulture) + "." + center.Y.ToString(CultureInfo.InvariantCulture);
}
