using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

/// <summary>Checks a Town's saved laws, government changes and mayor's office against its council and land records.</summary>
public static partial class TownGovernmentValidation
{
    public static void Validate(TownRuntimeState town, SocietyCheckpoint society,
        IReadOnlyList<TownLandTitleRecord> titles, int day)
    {
        if (town.Government is not { } state)
        {
            if (town.FoundingState == "founded")
                throw new InvalidDataException("A founded Town is missing its saved laws and government record.");
            return;
        }
        if (town.Council is not { } council)
            throw new InvalidDataException("A Town's government record needs its saved council.");
        TownGovernmentStateValidation.ValidateLocal(town.Id, state, allowIntermediateVotes: false);
        TownSystem.ValidateCompletedGovernmentAuthority(council, state);
        var tick = society.WorldTick;
        ValidateLaws(town, state, council, titles, tick, society.Agents.Select(person => person.Id).ToHashSet(StringComparer.Ordinal));
        TownGovernmentStateValidation.Validate(town, state, council, society, day);
    }

    private static void ValidateLaws(TownRuntimeState town, TownGovernmentState state, TownCouncilState council,
        IReadOnlyList<TownLandTitleRecord> titles, long tick, IReadOnlySet<string> known)
    {
        var proposals = council.Proposals.ToDictionary(proposal => proposal.Id, StringComparer.Ordinal);
        foreach (var law in state.Laws)
            foreach (var version in law.Versions)
            {
                if (version.SiteTiles.Any(tile => !TownLandRightsRules.IsCoveredByTownTitle(tile, town.Id, titles)) ||
                    !proposals.TryGetValue(version.ProposalId, out var adopted) || adopted.Kind != "law" || adopted.Status != "passed" ||
                    adopted.SettledTick != version.AdoptedTick || version.AdoptedTick > tick ||
                    version.EndedTick is { } ended && (ended > tick || !proposals.TryGetValue(version.EndedByProposalId!, out var ending) ||
                        ending.Kind != "law" || ending.Status != "passed" || ending.SettledTick != ended))
                    throw new InvalidDataException("A Town law's saved version history is invalid.");
                TownBoatAccessRules.ValidateReferences(version.BoatAccess, known);
            }
        foreach (var draft in state.LawDrafts)
        {
            TownBoatAccessRules.ValidateReferences(draft.BoatAccess, known);
            var law = draft.LawId is null ? null : state.Laws.SingleOrDefault(item => item.Id == draft.LawId);
            if (!proposals.TryGetValue(draft.ProposalId, out var proposal) || proposal.Kind != "law" ||
                draft.SiteTiles.Any(tile => !TownLandRightsRules.IsCoveredByTownTitle(tile, town.Id, titles)) ||
                proposal.Text != TownLawRules.ProposalText(draft) || proposal.RequestKey != TownLawRules.RequestKey(draft) ||
                draft.Status switch
                {
                    "pending" => proposal.Status != "pending",
                    "not_passed" => proposal.Status is "pending" or "passed",
                    "enacted" => proposal.Status != "passed",
                    "stale" => proposal.Status != "passed" || law!.Versions[draft.BaseVersion!.Value - 1].EndedTick > proposal.SettledTick,
                    _ => true,
                })
                throw new InvalidDataException("A Town's saved law proposal does not match its council vote.");
        }
        var drafts = state.LawDrafts.ToDictionary(draft => draft.ProposalId, StringComparer.Ordinal);
        if (council.Proposals.Any(proposal => TownLawRules.IsStructuredRequest(proposal.RequestKey) && !drafts.ContainsKey(proposal.Id)))
            throw new InvalidDataException("Every structured law proposal needs its full saved wording and scope.");
    }

    internal static bool ValidId(string? id, string prefix, long sequence) =>
        DerivedId.MatchesNumbered(id, prefix, sequence);
}
