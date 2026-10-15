using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;
using System.Diagnostics.CodeAnalysis;

namespace ClankerWorld.Simulation.World;

/// <summary>Durable ownership proof, noticed consent and exact physical transfer receipts.</summary>
public static partial class TownPropertyValidation
{
    public static void ValidateFiles(TownLandRecords state, string townId, long tick,
        IReadOnlySet<string> agents, IReadOnlySet<string> households, TownCouncilState? council, TownGovernmentState? government)
    {
        ValidateLocalFiles(state, townId);
        foreach (var item in state.Cases)
        {
            if (item.Property is not { } property) continue;
            Check(households.Contains(property.Request.SourceHouseholdId ?? property.Request.TargetHouseholdId!),
                "A property file needs its complete exact request, snapshots and personal choices.");
            foreach (var snapshot in property.Snapshots)
                Check(snapshot.Tick <= tick && snapshot.LivingFormerMemberIds.All(agents.Contains) && snapshot.RecipientAdultIds.All(agents.Contains),
                    "A noticed property snapshot has inconsistent owners or claimants.");
            foreach (var consent in property.Consents)
            {
                var revision = item.Revisions.Single(revision => revision.Number == consent.Revision);
                Check(consent.Tick <= tick && TownLandHearingRules.HasNoticeReceipt(revision, consent.AgentId, consent.Tick, council!.Knowledge),
                    "Property consent must follow the person's actual noticed asset-file read.");
            }
            foreach (var filing in item.Filings)
            {
                Check(council?.Proposals.Any(proposal => proposal.Id == filing.AuthorityId && proposal.AuthorId == filing.AgentId && proposal.Status == "passed" &&
                        proposal.LandHearingRequest is { } request && request.Property == property.Request && request.RequestedOutcome == filing.RequestedOutcome &&
                        request.Tiles.SequenceEqual(item.Revisions[0].Tiles)) == true ||
                    government?.Offices.Any(office => office.Mandates == "ordinary" && office.HolderId == filing.AgentId && office.ElectionId == filing.AuthorityId &&
                        office.TermStartTick <= filing.Tick && office.TermEndTick > filing.Tick) == true ||
                    government?.OfficeHistory.Any(office => office.Mandates == "ordinary" && office.HolderId == filing.AgentId && office.ElectionId == filing.AuthorityId &&
                        office.StartTick <= filing.Tick && office.EndTick >= filing.Tick) == true, "A property case must keep the actual Town filing authority.");
            }
        }
    }

    public static void ValidateWorld(WorldMap map, SocietyCheckpoint society, IReadOnlyList<TownRuntimeState> towns,
        WorldContentSimulationState simulation, DeclarativeWorldContentState content)
    {
        foreach (var town in towns)
            foreach (var item in town.LandRecords.Cases.Where(item => item.Property is not null))
            {
                var property = item.Property!;
                foreach (var snapshot in property.Snapshots)
                {
                    var definition = content.Buildings.SingleOrDefault(definition => definition.CanonicalId == snapshot.Building.DefinitionId);
                    Check(definition is not null && WorldContentSimulationRules.Footprint(definition, snapshot.Building).OrderBy(tile => tile.Y).ThenBy(tile => tile.X)
                        .SequenceEqual(item.Revisions.Single(revision => revision.Number == snapshot.Revision).Tiles), "A property notice must cover its whole existing building footprint.");
                    if (property.Request.SourceHouseholdId is { } source)
                        Check(!TownPropertyRules.HadMembers(society, source, snapshot.Tick) &&
                            snapshot.LivingFormerMemberIds.SequenceEqual(TownPropertyRules.FormerMembers(society, source, snapshot.Tick)),
                            "A property case cannot forget living former household members.");
                }
                if (property.Request.SourceHouseholdId is null)
                    Check(town.LandRecords.Cases.Any(previous => previous.Id != item.Id && previous.Property?.Transfer is { ResultBuilding.HouseholdId: null } receipt &&
                        receipt.ResultBuilding.InstanceId == property.Request.BuildingId && receipt.Tick <= item.FiledTick),
                        "An onward grant needs the Town's prior recovery ruling.");
            }
    }

    public static HashSet<string> RecoveredBuildings(IReadOnlyList<TownRuntimeState>? towns) =>
        (towns ?? []).SelectMany(town => town?.LandRecords?.Cases ?? []).Where(item => item?.Property is { Transfer: not null, Request: not null })
            .Select(item => item.Property!.Request.BuildingId).ToHashSet(StringComparer.Ordinal);

    private static bool Canonical(IReadOnlyList<string> values) => values.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).SequenceEqual(values);
    private static void Check([DoesNotReturnIf(false)] bool valid, string message)
    {
        if (!valid) throw new InvalidDataException(message);
    }
}
