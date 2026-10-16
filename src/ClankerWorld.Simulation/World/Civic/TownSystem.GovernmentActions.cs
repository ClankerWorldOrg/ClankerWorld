using ClankerWorld.Simulation.Cognition;

namespace ClankerWorld.Simulation.World;

internal sealed partial class TownSystem
{
    internal static void AddTownGovernmentCandidates(ITownWorld world, List<CognitionCandidate> candidates, string actor, TownRuntimeState town)
    {
        var government = town.Government!;
        var history = CivicHistory(town);
        // Each choice costs prompt space on every model call, so protected proposals are
        // made at the notice place, like admission requests.
        foreach (var target in TownArrangementRules.Supported)
            OfferGovernmentProposal(world, candidates, actor, town, GovernmentProposalKind.Propose, TownArrangementRules.Key(target),
                $"Initiate a protected resident vote in {town.Name}: {TownArrangementRules.Declaration(target)} " +
                "More than half the eligible adult residents must approve; incumbent permission is not needed. No power changes before a valid handover.", 195);
        foreach (var office in government.Offices.Where(office => office.HolderId is not null))
            OfferGovernmentOffice(world, candidates, actor, town, GovernmentOfficeKind.ExtendNonLand,
                TownArrangementRules.Key(government.Arrangement with { NonLand = TownArrangementRules.Mayor }), NonLandExtensionToken(office),
                "Propose adding nonviolent civil adjudication to " + world.Society.GetAgent(office.HolderId!).Name +
                "'s existing " + TownArrangementRules.MandateLabel(office.Mandates) + " term. Resident majority and their separate personal consent are required; the existing term deadline stays unchanged.", 195);
        foreach (var change in government.Changes.Where(change => change.Status is "queued" or "voting" or "handover" &&
                     change.NonLandExtension is { ConsentTick: null } extension && extension.HolderId == actor))
            OfferGovernmentOffice(world, candidates, actor, town, GovernmentOfficeKind.AcceptNonLand, change.Id, "",
                "Personally accept these proposed additional nonviolent adjudication duties for your existing term. This supplies no resident vote, and does not restart your term.", 185);
        OfferGovernmentProposal(world, candidates, actor, town, GovernmentProposalKind.ReplaceMayor, TownArrangementRules.Key(government.Arrangement),
                $"Ask {town.Name}'s adult residents to approve early replacement of the elected mayoral mandates. Approval requires a resident majority and a valid successor election.", 196);
        foreach (var change in government.Changes.Where(change => change.Status is "queued" or "voting"))
        {
            OfferGovernmentProposal(world, candidates, actor, town, GovernmentProposalKind.Withdraw, change.Id,
                "Withdraw your unfinished protected government proposal.", 197);
            OfferGovernmentProposal(world, candidates, actor, town, GovernmentProposalKind.Yes, change.Id,
                $"Cast your final yes vote on the protected resident proposal in {town.Name}. {TownArrangementRules.Declaration(change.Target)}", 165);
            OfferGovernmentProposal(world, candidates, actor, town, GovernmentProposalKind.No, change.Id,
                $"Cast your final no vote on the protected resident proposal in {town.Name}. {TownArrangementRules.Declaration(change.Target)}", 166);
        }
        foreach (var mandates in TownArrangementRules.SupportedMandates)
        {
            OfferGovernmentOffice(world, candidates, actor, town, GovernmentOfficeKind.Register, mandates, "",
                $"Personally agree to seek election for {TownArrangementRules.MandateLabel(mandates)} in {town.Name}, if residents authorize that office. This grants no authority or Council candidacy. To add duties to a current office, use its protected extension; this registration cannot restart that term.", 190);
            OfferGovernmentOffice(world, candidates, actor, town, GovernmentOfficeKind.Withdraw, mandates, "",
                $"Withdraw willingness to seek election for {TownArrangementRules.MandateLabel(mandates)} in {town.Name}; this does not resign a held office.", 190);
        }
        foreach (var office in government.Offices)
            OfferGovernmentOffice(world, candidates, actor, town, GovernmentOfficeKind.Resign, office.Mandates, "",
                $"Resign only your mandate for {TownArrangementRules.MandateLabel(office.Mandates)} in {town.Name}. Any separate mandate continues.", 198);
        if (government.Contest is { Stage: "voting", RoundDeadlineTick: { } closes } contest && contest.Voters.Contains(actor, StringComparer.Ordinal) &&
            history.Knows(actor, TownGovernmentRules.RoundToken(contest)))
            foreach (var id in contest.Candidates)
                candidates.Add(new(CivicAction(world, town.Id, "mayor_vote", TownGovernmentRules.RoundToken(contest), id),
                    $"Submit or revise your mayoral ballot for {world.Society.GetAgent(id).Name} in {town.Name}, for {TownArrangementRules.MandateLabel(contest.Mandates)}. " +
                    $"Round {contest.Round} closes on world day {CivicDayNumber(world, closes)}. A tie requires another vote, never a draw.", 167));
    }

    internal static string NonLandExtensionToken(TownOffice office) => NonLandToken(office.Mandates + "|" + office.ElectionId);
    internal static TownOffice NonLandExtensionOffice(TownGovernmentState government, string token) =>
        government.Offices.SingleOrDefault(office => NonLandExtensionToken(office) == token) ??
            throw new InvalidOperationException("The proposed added duties refer to an earlier office term.");
    internal static (TownCouncilState Council, TownGovernmentState Government) ApplyTownGovernmentAction(ITownWorld world, TownRuntimeState town, string actor, string action, string subject, string choice,
        TownCouncilState council, TownGovernmentState government) => action switch
        {
            "government_propose" or "government_replace" => TownGovernmentRules.Propose(council, government, town.Id, actor,
                TownArrangementRules.Parse(subject) ?? throw new InvalidOperationException("Unsupported government arrangement."),
                action == "government_replace", TownAdults(world, town), world.WorldTick, CivicDay(world)),
            "government_extend_non_land" => TownGovernmentRules.Propose(council, government, town.Id, actor,
                TownArrangementRules.Parse(subject) ?? throw new InvalidOperationException("Unsupported government arrangement."),
                false, TownAdults(world, town), world.WorldTick, CivicDay(world),
                nonLandBaseMandate: NonLandExtensionOffice(government, choice).Mandates,
                expectedNonLandBaseElectionId: NonLandExtensionOffice(government, choice).ElectionId),
            "government_accept_non_land" => TownGovernmentRules.AcceptNonLandDuties(council, government, subject, actor, TownAdults(world, town), world.WorldTick),
            "government_yes" or "government_no" => (council, TownGovernmentRules.Vote(government, subject, actor, action == "government_yes", world.WorldTick)),
            "government_withdraw" => TownGovernmentRules.Withdraw(council, government, subject, actor, world.WorldTick),
            "mayor_register" => TownGovernmentRules.RegisterMayor(council, government, actor, subject, TownAdults(world, town), world.WorldTick),
            "mayor_withdraw" => TownGovernmentRules.WithdrawMayor(council, government, actor, subject, world.WorldTick),
            "mayor_resign" => TownGovernmentRules.Resign(council, government, actor, subject, world.WorldTick),
            "mayor_vote" => (council, TownGovernmentRules.VoteMayor(government, subject, actor, ResolveCivicAgentToken(world, choice), world.WorldTick)),
            _ => throw new InvalidOperationException("Unsupported government action."),
        };
}
