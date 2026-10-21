namespace ClankerWorld.Simulation.World;

internal sealed partial class TownSystem
{
    private void Commit(CivicState next)
    {
        if (SystemInvariantChecks.Enabled)
        {
            ValidateLocalTowns(next.Towns);
            ValidateLocalFounderSetup(next.FounderSetup);
            ValidateLocalFoodStewardship(next.FoodStewardship);
            ValidateLocalLandRecords(next.TownLandTitles, next.HouseholdLandUseRights, next.HouseholdLandUseRequests);
        }
        State = next;
    }

    internal static void ValidateLocalTowns(IReadOnlyList<TownRuntimeState> towns)
    {
        if (towns.Any(town => town is null) || towns.Select(town => town.Id).Distinct(StringComparer.Ordinal).Count() != towns.Count)
            throw new InvalidDataException("A founder-setup world must keep its original first Town and unique Town identities.");
        foreach (var town in towns)
        {
            if (town.Council is { } council) ValidateLocalCouncil(town.Id, council);
            if (!IdentityRules.Reference(town.Id, DerivedId.MaximumLength, requireTrimmed: true) ||
                string.IsNullOrWhiteSpace(town.Name) || town.Name != town.Name.Trim() || !BoundedText.IsWithinLimit(town.Name, 120) || town.FoundedTick < 0 ||
                town.ResidentIds is null || town.AssignedBuildingIds is null || town.BorderTiles is null ||
                town.FirstBuildingCompletedTick is { } completed && completed < town.FoundedTick ||
                town.AbandonedSinceTick is { } abandoned && (!town.IsAbandoned || abandoned < town.FoundedTick) ||
                town.AssignedBuildingIds.Count > 0 && town.FirstBuildingCompletedTick is null ||
                town.ResidentIds.Distinct(StringComparer.Ordinal).Count() != town.ResidentIds.Count ||
                town.AssignedBuildingIds.Distinct(StringComparer.Ordinal).Count() != town.AssignedBuildingIds.Count ||
                town.BorderTiles.Distinct().Count() != town.BorderTiles.Count || town.BorderTiles.Count == 0)
                throw new InvalidDataException("A Town identity, founding record, membership, or footprint is invalid.");
            if (town.Id == TownBorderRules.FirstTownId)
            {
                if (town.Name != TownBorderRules.FirstTownName || town.FoundedTick != 0)
                    throw new InvalidDataException("The original first Town's identity and founding history are invalid.");
            }
            else if (town.FoundingState != "founded")
                throw new InvalidDataException("An additional recorded Town must have a completed founding record.");
            if (town.OriginSite is { } site && !town.BorderTiles.Contains(site))
                throw new InvalidDataException("The saved Town border does not cover its founding site.");
        }
    }

    internal static void ValidateLocalFounderSetup(FounderSetupState? setup)
    {
        if (setup is null) return;
        if (setup.FounderIds is null || setup.FounderIds.Count > WorldRuntime.RequiredFounders ||
            setup.FounderIds.Distinct(StringComparer.Ordinal).Count() != setup.FounderIds.Count || setup.FounderIds.Any(string.IsNullOrWhiteSpace))
            throw new InvalidDataException("Founder setup references invalid or duplicate agents.");
        if (setup.Started && setup.FounderIds.Count != WorldRuntime.RequiredFounders)
            throw new InvalidDataException("A started world requires four configured founders.");
    }

    internal static void ValidateLocalFoodStewardship(FoodStewardship? saved)
    {
        if (saved is null) return;
        if (saved.FoodPolicy is not ("open" or "essential_first") || saved.LastResolutionTick < 0)
            throw new InvalidDataException("The saved household council is invalid.");
        if (saved.Ballot is { } ballot && (ballot.Policy is not ("open" or "essential_first") || ballot.Policy == saved.FoodPolicy ||
            ballot.Electorate is null || ballot.Approvals is null || ballot.Rejections is null || ballot.ProposedTick < 0 ||
            ballot.ExpiryTick < ballot.ProposedTick || ballot.ExpiryTick - ballot.ProposedTick != 120 || ballot.Electorate.Count == 0 ||
            ballot.Electorate.Distinct(StringComparer.Ordinal).Count() != ballot.Electorate.Count ||
            ballot.Approvals.Concat(ballot.Rejections).Distinct(StringComparer.Ordinal).Count() != ballot.Approvals.Count + ballot.Rejections.Count ||
            ballot.Approvals.Concat(ballot.Rejections).Any(id => !ballot.Electorate.Contains(id, StringComparer.Ordinal))))
            throw new InvalidDataException("The saved household ballot is invalid.");
    }

    internal static bool IsLocallyValidPlot([System.Diagnostics.CodeAnalysis.NotNullWhen(true)] IReadOnlyList<GridPoint>? tiles, long recordedTick, long? agreedEndTick = null) =>
        tiles is { Count: > 0 } && tiles.Distinct().Count() == tiles.Count &&
        tiles.SequenceEqual(TownLandRightsRules.OrderTiles(tiles)) && recordedTick >= 0 &&
        (agreedEndTick is null || agreedEndTick >= recordedTick);

    internal static void ValidateLocalLandRecords(
        [System.Diagnostics.CodeAnalysis.NotNull] IReadOnlyList<TownLandTitleRecord>? titles,
        [System.Diagnostics.CodeAnalysis.NotNull] IReadOnlyList<HouseholdLandUseRight>? rights,
        [System.Diagnostics.CodeAnalysis.NotNull] IReadOnlyList<HouseholdLandUseRequest>? requests)
    {
        if (titles is null || rights is null || requests is null || titles.Any(item => item is null) || rights.Any(item => item is null) || requests.Any(item => item is null))
            throw new InvalidDataException("Saved Town land titles, use rights and requests must be present and cannot contain null entries.");
        if (titles.Select(item => item.Id).Distinct(StringComparer.Ordinal).Count() != titles.Count ||
            rights.Select(item => item.Id).Distinct(StringComparer.Ordinal).Count() != rights.Count ||
            requests.Select(item => item.Id).Distinct(StringComparer.Ordinal).Count() != requests.Count)
            throw new InvalidDataException("Saved Town land record identities must be unique within each record type.");
        foreach (var title in titles)
            if (!IdentityRules.Reference(title.Id, DerivedId.MaximumLength, requireTrimmed: true) || !IdentityRules.Reference(title.TownId, DerivedId.MaximumLength, requireTrimmed: true) || !IsLocallyValidPlot(title.Tiles, title.RecordedTick))
                throw new InvalidDataException("A saved Town title is invalid or outside its recorded Town border.");
        foreach (var right in rights)
            if (!IdentityRules.Reference(right.Id, DerivedId.MaximumLength, requireTrimmed: true) || !IdentityRules.Reference(right.TownId, DerivedId.MaximumLength, requireTrimmed: true) || !IdentityRules.Reference(right.HouseholdId, int.MaxValue, requireTrimmed: true) ||
                !BoundedText.IsValid(right.GrantSource, 64, requireTrimmed: true) || !IsLocallyValidPlot(right.Tiles, right.GrantedTick, right.AgreedEndTick))
                throw new InvalidDataException("A saved household land-use right is invalid or not covered by Town title.");
        foreach (var request in requests)
            if (!IdentityRules.Reference(request.Id, DerivedId.MaximumLength, requireTrimmed: true) || !IdentityRules.Reference(request.TownId, DerivedId.MaximumLength, requireTrimmed: true) || !IdentityRules.Reference(request.HouseholdId, int.MaxValue, requireTrimmed: true) ||
                !IdentityRules.Reference(request.RequestedByAgentId, int.MaxValue, requireTrimmed: true) || request.Tiles is not { Count: <= ClankerWorld.Simulation.Cognition.CognitionDecisionResponse.MaximumCivicLandTiles } ||
                !IsLocallyValidPlot(request.Tiles, request.RequestedTick, request.AgreedEndTick))
                throw new InvalidDataException("A saved household land-use request is invalid or not covered by Town title.");
    }
}
