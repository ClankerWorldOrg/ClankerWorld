using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

internal sealed partial class TownSystem
{
    internal void RefreshTownLandHearings(ITownWorld world)
    {
        foreach (var town in State.Towns.ToArray())
        {
            if (town.Council is { } council && (town.LandRecords.Cases.Any(landCase => landCase.Status != "settled" ||
                    landCase.ReopenRequests.Any(request => request.Status == "pending")) ||
                town.LandRecords.Transfers.Any(transfer => transfer.Status == "pending") ||
                town.NonLandHearings.Cases.Any(item => item.Status == "pending" || item.ReopenRequests.Any(request => request.Status == "pending")) ||
                town.NonLandHearings.Offers.Any(offer => offer.Status == "pending") ||
                town.NonLandHearings.Agreements.Any(agreement => agreement.Status != "completed" && !TownRemedyRules.IsSuperseded(town.NonLandHearings, agreement.Id))))
                SaveTownCouncil(world, town, council, town.Government);
        }
    }

    internal static void ValidateLandHearings(WorldMap map, SocietyCheckpoint checkpoint,
            IReadOnlyList<TownRuntimeState> savedTowns, IReadOnlyList<HouseholdLandUseRight> rights,
            IReadOnlyList<HouseholdLandUseRequest> requests, IReadOnlyList<TownLandTitleRecord> titles, int day)
    {
        var knownAgents = checkpoint.Agents.Select(person => person.Id).ToHashSet(StringComparer.Ordinal);
        var knownHouseholds = checkpoint.Households.Select(household => household.Id).ToHashSet(StringComparer.Ordinal);
        var households = checkpoint.Agents.ToDictionary(person => person.Id, person => person.HouseholdId, StringComparer.Ordinal);
        foreach (var town in savedTowns)
        {
            if (town.LandRecords is null)
                throw new InvalidDataException("A saved Town is missing its land-hearing history.");
            var adults = checkpoint.Agents.Where(person => town.ResidentIds.Contains(person.Id, StringComparer.Ordinal) &&
                    person.Status == SocietyAgentStatus.Active && LifeStageRules.IsAdult(person.AgeBand))
                .Select(person => person.Id).Order(StringComparer.Ordinal).ToArray();
            TownLandHearingValidation.Validate(map, checkpoint.WorldTick, town.Id, town.LandRecords,
                rights.Where(right => right.TownId == town.Id).ToArray(), titles, knownAgents, knownHouseholds,
                town.Council, day, town.Government, adults, households,
                currentPartiesResolver: item => TownLandCasePartyRules.CurrentParties(town,
                    TownLandHearingRules.CurrentRevision(item).Tiles, rights, requests, checkpoint.Agents,
                    checkpoint.WorldTick, item));
            TownLandTransferValidation.ValidateSalePayments(map, town.LandRecords, checkpoint.Inventory);
            foreach (var proposal in town.Council?.Proposals.Where(proposal => proposal.Kind == "land_hearing") ?? [])
                TownLandGovernmentFilingRules.Validate(proposal, town, map, titles, knownHouseholds);
        }
    }

}
