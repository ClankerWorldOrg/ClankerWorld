using System.Globalization;
using ClankerWorld.Simulation.Cognition;

namespace ClankerWorld.Simulation.World;

internal sealed partial class CivicSystem
{
    internal static (TownGovernanceState Council, TownGovernmentState Government) AdvanceCivic(ICivicTownWorld world, TownRuntimeState town,
        TownGovernanceState council, TownGovernmentState government)
    {
        var adults = TownAdults(world, town);
        return TownGovernmentRules.Advance(council, government, town.Id, town.Name, world.WorldSeed, adults, world.WorldTick, CivicDay(world), TownLandHearingElectionBusy(town) || TownNonviolentElectionBusy(town));
    }

    internal static void AddTownLawCandidates(ICivicTownWorld world, List<CognitionCandidate> candidates, string actor, TownRuntimeState town)
    {
        const string format = "Write civic_proposal as 'subject: rule'. It needs the current council's votes, applies only from adoption " +
            "and records a social rule; it does not block actions, change ownership or create offices.";
        candidates.Add(new(CivicAction(world, town.Id, "propose", TownLawRules.Jurisdiction),
            $"Propose a law for everyone on {town.Name}'s claimed land, visitors included. {format}", 190));
        candidates.Add(new(CivicAction(world, town.Id, "propose", TownLawRules.ResidentDuty),
            $"Propose a duty for {town.Name}'s residents that applies wherever they are. {format}", 191));
        var here = world.Agents[actor].Position;
        if (TownLawRules.SiteAround(world.Map, here, town.Id, world.TownLandTitles).Length > 0)
            candidates.Add(new(CivicAction(world, town.Id, "propose", SiteToken(here)),
                $"Propose a law for the claimed land within {TownLawRules.SiteRadius} tiles of where you stand in {town.Name}, visitors included. {format}", 192));
        var history = CivicHistory(town);
        if (town.Government is { } government && !TownEstateDefaultRules.HasPending(government))
        {
            var estateLaw = government.Laws.SingleOrDefault(law => TownLawRules.IsInForce(law) && TownLawRules.Current(law).EstateDefault is not null);
            var knowsEstateLaw = estateLaw is null || town.Governance!.Notices.LastOrDefault(notice => notice.SubjectId == estateLaw.Id) is { } notice &&
                history.Known(actor).Contains(notice.Id);
            if (knowsEstateLaw)
                foreach (var share in new[] { 0, 25, 50, 75, 100 })
                {
                    if (estateLaw is not null && TownLawRules.Current(estateLaw).EstateDefault!.TownSharePercent == share) continue;
                    var target = estateLaw is null ? "adopt" : estateLaw.Id + ":" + TownLawRules.Current(estateLaw).Version.ToString(CultureInfo.InvariantCulture);
                    candidates.Add(new(CivicAction(world, town.Id, "estate_default", target, share.ToString(CultureInfo.InvariantCulture)),
                        $"Propose a supported default estate law: {TownEstateDefaultRules.Text(new(share))} " +
                        "It needs the ordinary Council vote. No living person's belongings move; a valid will always wins. Whole vessels stay together, and undeliverable Town shares follow the household default.", 195));
                }
        }
        foreach (var law in town.Government?.Laws.Where(l => TownLawRules.IsInForce(l) && town.Governance!.Notices.LastOrDefault(n => n.SubjectId == l.Id) is { } latest &&
                history.Known(actor).Contains(latest.Id))
            ?? [])
        {
            var number = TownLawRules.Number(law.Id).ToString(CultureInfo.InvariantCulture);
            var current = TownLawRules.Current(law);
            if (current.BoatAccess is null && current.EstateDefault is null) candidates.Add(new(CivicAction(world, town.Id, "amend", law.Id, current.Version.ToString(CultureInfo.InvariantCulture)),
                $"Propose amending {town.Name}'s law {number} ({current.Subject}); write the complete new wording in civic_proposal as 'subject: rule'. " +
                "It keeps the law's scope, needs the council's votes and applies only from adoption.", 193));
            candidates.Add(new(CivicAction(world, town.Id, "repeal", law.Id, current.Version.ToString(CultureInfo.InvariantCulture)),
                $"Propose repealing {town.Name}'s law {number} ({current.Subject}). It needs the council's votes; earlier conduct stays under the law.", 194));
        }
    }

    internal static string SiteToken(GridPoint center) =>
        "site:" + center.X.ToString(CultureInfo.InvariantCulture) + "." + center.Y.ToString(CultureInfo.InvariantCulture);
    internal static GridPoint[] SiteFromToken(ICivicTownWorld world, string token, string townId)
    {
        var parts = token.Split(':', '.');
        if (parts is not ["site", var x, var y] ||
            !int.TryParse(x, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var column) ||
            !int.TryParse(y, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var row))
            throw new InvalidOperationException("The law site is not a recognized claimed-land site.");
        return TownLawRules.SiteAround(world.Map, new GridPoint(column, row), townId, world.TownLandTitles);
    }

    internal static (TownGovernanceState, TownGovernmentState) ApplyTownLawAction(ICivicTownWorld world, TownRuntimeState town, string actor, string kind,
        string target, string versionToken, string? text, TownGovernanceState council, TownGovernmentState government)
    {
        var adults = TownAdults(world, town);
        int? expectedVersion = null;
        if (kind is "amend" or "repeal")
        {
            if (!int.TryParse(versionToken, NumberStyles.None, CultureInfo.InvariantCulture, out var version) || version < 1)
                throw new InvalidOperationException("A law change must identify the version that was chosen.");
            expectedVersion = version;
        }
        switch (kind)
        {
            case "propose":
                if (text is null) throw new InvalidOperationException("A law proposal needs civic_proposal text.");
                var scope = target.StartsWith("site:", StringComparison.Ordinal) ? TownLawRules.Site : target;
                var site = scope == TownLawRules.Site ? SiteFromToken(world, target, town.Id) : [];
                return TownLawRules.ProposeAdoption(council, government, town.Id, actor, text, scope, site, adults, world.WorldTick, CivicDay(world));
            case "amend":
                if (text is null) throw new InvalidOperationException("An amendment needs civic_proposal text.");
                return TownLawRules.ProposeAmendment(council, government, town.Id, actor, target, text, adults, world.WorldTick, CivicDay(world), expectedVersion);
            case "repeal":
                return TownLawRules.ProposeRepeal(council, government, town.Id, actor, target, adults, world.WorldTick, CivicDay(world), expectedVersion);
            default:
                throw new InvalidOperationException("Unknown Town law action.");
        }
    }
}
