namespace ClankerWorld.Simulation.World;

public static partial class TownGovernmentValidation
{
    internal static void ValidateLocalLaws(string townId, TownGovernmentState state)
    {
        var recorded = new HashSet<string>(StringComparer.Ordinal);
        foreach (var law in state.Laws)
        {
            if (law is null || !ValidId(law.Id, townId + ":law:", state.Sequence) || law.Versions is not { Count: > 0 })
                throw new InvalidDataException("A Town's saved law is invalid.");
            for (var index = 0; index < law.Versions.Count; index++)
            {
                var version = law.Versions[index];
                var last = index == law.Versions.Count - 1;
                if (version is null || version.Version != index + 1 || version.SiteTiles is null ||
                    !TownLawRules.TryParse(TownLawRules.Text(version.Subject ?? "", version.Rule ?? ""), out var subject, out var rule) ||
                    subject != version.Subject || rule != version.Rule || !TownLawRules.IsScope(version.Scope) ||
                    version.Scope == TownLawRules.Site != version.SiteTiles.Count > 0 ||
                    !version.SiteTiles.SequenceEqual(TownLandRightsRules.OrderTiles(version.SiteTiles.Distinct())) ||
                    index > 0 && (version.Scope != law.Versions[0].Scope || !version.SiteTiles.SequenceEqual(law.Versions[0].SiteTiles)) ||
                    string.IsNullOrWhiteSpace(version.ProposalId) || version.AdoptedTick < 0 ||
                    index > 0 && law.Versions[index - 1].EndedByProposalId != version.ProposalId ||
                    !last && version.EndedTick != law.Versions[index + 1].AdoptedTick ||
                    (version.EndedTick is null) != (version.EndedByProposalId is null) || !last && version.EndedTick is null ||
                    version.EndedTick is { } ended && ended < version.AdoptedTick)
                    throw new InvalidDataException("A Town law's saved version history is invalid.");
                TownBoatAccessRules.ValidateLocal(version.BoatAccess, version.Subject, version.Rule, version.Scope);
                TownEstateDefaultRules.Validate(version.EstateDefault, version.Subject, version.Rule, version.Scope);
                if (version.BoatAccess is not null && version.EstateDefault is not null)
                    throw new InvalidDataException("One supported law cannot combine unrelated typed effects.");
                recorded.Add(version.ProposalId!);
                if (version.EndedByProposalId is { } endedBy) recorded.Add(endedBy);
            }
        }
        var estateVersions = state.Laws.SelectMany(law => law.Versions.Where(version => version.EstateDefault is not null))
            .OrderBy(version => version.AdoptedTick).ToArray();
        for (var index = 1; index < estateVersions.Length; index++)
            if (estateVersions[index - 1].EndedTick is not { } ended || ended > estateVersions[index].AdoptedTick)
                throw new InvalidDataException("Supported default estate laws cannot overlap in time.");
        if (state.LawDrafts.Any(draft => draft is null) || state.LawDrafts.Select(draft => draft.ProposalId).Distinct(StringComparer.Ordinal).Count() != state.LawDrafts.Count)
            throw new InvalidDataException("A Town's saved law proposals are invalid.");
        foreach (var draft in state.LawDrafts)
        {
            TownBoatAccessRules.ValidateLocal(draft.BoatAccess, draft.Subject, draft.Rule, draft.Scope);
            TownEstateDefaultRules.Validate(draft.EstateDefault, draft.Subject, draft.Rule, draft.Scope);
            var law = draft.LawId is null ? null : state.Laws.SingleOrDefault(item => item.Id == draft.LawId);
            if (string.IsNullOrWhiteSpace(draft.ProposalId) || draft.Action is not ("adopt" or "amend" or "repeal") || draft.SiteTiles is null ||
                draft.BoatAccess is not null && draft.EstateDefault is not null ||
                draft.Action == "amend" && (draft.BoatAccess is not null || law is not null && TownLawRules.Current(law).BoatAccess is not null ||
                    law is not null && draft.BaseVersion is { } estateVersion && estateVersion > 0 && estateVersion <= law.Versions.Count &&
                    (law.Versions[estateVersion - 1].EstateDefault is null) != (draft.EstateDefault is null)) ||
                !TownLawRules.TryParse(TownLawRules.Text(draft.Subject ?? "", draft.Rule ?? ""), out var subject, out var rule) ||
                subject != draft.Subject || rule != draft.Rule || !TownLawRules.IsScope(draft.Scope) ||
                draft.Scope == TownLawRules.Site != draft.SiteTiles.Count > 0 ||
                !draft.SiteTiles.SequenceEqual(TownLandRightsRules.OrderTiles(draft.SiteTiles.Distinct())) ||
                draft.Action == "adopt" && (draft.BaseVersion is not null || (draft.LawId is null) != (draft.Status != "enacted")) ||
                draft.Action != "adopt" && (law is null || draft.BaseVersion is not { } baseVersion || baseVersion < 1 || baseVersion > law.Versions.Count ||
                    draft.Scope != law.Versions[0].Scope || !draft.SiteTiles.SequenceEqual(law.Versions[0].SiteTiles)) ||
                draft.Status is not ("pending" or "not_passed" or "enacted" or "stale") ||
                draft.Status == "enacted" && (!recorded.Contains(draft.ProposalId) || draft.LawId is null ||
                    state.Laws.SingleOrDefault(item => item.Id == draft.LawId) is not { } enactedLaw ||
                    !enactedLaw.Versions.Any(version => version.ProposalId == draft.ProposalId || version.EndedByProposalId == draft.ProposalId)) ||
                draft.Status == "stale" && (recorded.Contains(draft.ProposalId) || draft.Action == "adopt" ||
                    law!.Versions[draft.BaseVersion!.Value - 1].EndedTick is null))
                throw new InvalidDataException("A Town's saved law proposal does not match its council vote.");
        }
        if (recorded.Any(id => !state.LawDrafts.Any(draft => draft.ProposalId == id && draft.Status == "enacted")))
            throw new InvalidDataException("A Town law changed without a recorded passed law proposal.");
        var drafts = state.LawDrafts.ToDictionary(draft => draft.ProposalId, StringComparer.Ordinal);
        foreach (var law in state.Laws)
            foreach (var version in law.Versions)
            {
                var source = drafts[version.ProposalId];
                if (source.LawId != law.Id || source.Subject != version.Subject || source.Rule != version.Rule || source.Scope != version.Scope ||
                    source.BoatAccess != version.BoatAccess || source.EstateDefault != version.EstateDefault || !source.SiteTiles.SequenceEqual(version.SiteTiles) ||
                    (version.Version == 1 ? source.Action != "adopt" : source.Action != "amend" || source.BaseVersion != version.Version - 1))
                    throw new InvalidDataException("A law version must match the exact wording and scope approved by its Council.");
                if (version.EndedByProposalId is not { } endId) continue;
                var ending = drafts[endId];
                if (ending.LawId != law.Id || ending.BaseVersion != version.Version || ending.Action is not ("amend" or "repeal") ||
                    ending.Action == "amend" && (version.Version >= law.Versions.Count || law.Versions[version.Version].ProposalId != endId) ||
                    ending.Action == "repeal" && (version.Version != law.Versions.Count || ending.Subject != version.Subject || ending.Rule != version.Rule ||
                        ending.BoatAccess != version.BoatAccess || ending.EstateDefault != version.EstateDefault))
                    throw new InvalidDataException("An ended law version needs the exact amendment or repeal of that version.");
            }
    }
}
