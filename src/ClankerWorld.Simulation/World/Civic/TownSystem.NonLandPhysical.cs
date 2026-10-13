using System.Security.Cryptography;
using System.Text.Json;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

internal sealed partial class TownSystem
{
    internal const string NonLandRemedyPrefix = "nonviolent_remedy:";
    internal const string NonLandRelayPrefix = "nonviolent_relay:";
    internal const int UnfiledConductLimit = 256;
    internal static string NonLandDigest<T>(T value) =>
        DerivedId.Hash(JsonSerializer.Serialize(value));
    internal static string ConductLawDigest(TownLawVersion version) => NonLandDigest(new
    {
        version.Version,
        version.Subject,
        version.Rule,
        version.Scope,
        version.SiteTiles,
        version.ProposalId,
        version.AdoptedTick,
    });
    internal void RecordNonLandConduct(ITownWorld world, string actor, string kind, GridPoint position, string? targetId,
        string? itemKind, int quantity, string nativeActId)
    {
        if (!world.Agents.ContainsKey(actor)) return;
        nativeActId = DerivedId.Create(nativeActId);
        var actorTownId = TownForResident(world, actor);
        foreach (var town in world.Towns.ToArray())
        {
            if (town.Government is null) continue;
            var titles = world.TownLandTitles.Where(title => title.RecordedTick <= world.WorldTick).ToArray();
            var applicable = TownLawRules.Applicable(town.Government, town.Id, titles,
                actorTownId == town.Id, position, world.WorldTick);
            if (applicable.Count == 0 || town.NonLandHearings.ConductRecords.Any(record => record.Id == nativeActId)) continue;
            var laws = applicable.Select(pair => new TownConductLawSnapshot(pair.Law.Id, pair.Version.Version,
                ConductLawDigest(pair.Version), pair.Version.Scope,
                titles.Where(title => title.TownId == town.Id && title.Tiles.Contains(position))
                    .Select(title => title.Id).Order(StringComparer.Ordinal).ToArray(),
                (town.Council?.Knowledge ?? []).Where(receipt => receipt.AgentId == actor && receipt.LearnedTick <= world.WorldTick &&
                    town.Council!.Notices.Any(notice => notice.Id == receipt.NoticeId && notice.Kind == "law" &&
                        notice.SubjectId == pair.Law.Id && notice.PostedTick >= pair.Version.AdoptedTick && notice.PostedTick <= world.WorldTick))
                    .Select(receipt => receipt.NoticeId).Order(StringComparer.Ordinal).ToArray())).ToArray();
            var record = new TownConductRecord(nativeActId, actor, kind, position, world.WorldTick, actorTownId,
                targetId, itemKind, quantity, laws, "");
            record = record with { Version = NonLandDigest(record) };
            var observations = world.Agents.Values.Where(person => person.AgentId == actor ||
                    CanObserveNonLandAct(world, person.AgentId, actor, position))
                .OrderBy(person => person.AgentId, StringComparer.Ordinal)
                .Select(person => new TownConductAcquisition(WorldRuntime.CivicSharedRules.ConductSeenId(nativeActId, person.AgentId),
                    nativeActId, person.AgentId, "firsthand", world.WorldTick, person.Position,
                    DescribeNonLandConduct(record), ObserverHouseholdId: world.HouseholdFor(person.AgentId),
                    PrivateSiteHouseholdId: NonLandPrivateSite(world, position)?.HouseholdId)).ToArray();
            SetTown(world, town with
            {
                NonLandHearings = PruneNonLandConduct(town, town.NonLandHearings with
                {
                    ConductRecords = town.NonLandHearings.ConductRecords.Append(record).ToArray(),
                    Acquisitions = town.NonLandHearings.Acquisitions.Concat(observations).ToArray(),
                })
            });
        }
    }

    internal static TownNonLandHearingState PruneNonLandConduct(TownRuntimeState town, TownNonLandHearingState state)
    {
        var retained = state.Cases.Select(file => file.Allegation.IncidentId)
            .Concat((town.Council?.Proposals ?? []).Where(proposal => proposal.NonLandRequest is not null)
                .Select(proposal => proposal.NonLandRequest!.Allegation.IncidentId)).ToHashSet(StringComparer.Ordinal);
        foreach (var record in state.ConductRecords.Where(record => !retained.Contains(record.Id))
                     .OrderByDescending(record => record.Tick).ThenByDescending(record => record.Id, StringComparer.Ordinal)
                     .Take(UnfiledConductLimit)) retained.Add(record.Id);
        return state with
        {
            ConductRecords = state.ConductRecords.Where(record => retained.Contains(record.Id)).ToArray(),
            Acquisitions = state.Acquisitions.Where(item => retained.Contains(item.ConductId)).ToArray(),
        };
    }

    internal static bool HasPriorLawNotice(TownRuntimeState town, TownViolationAllegation allegation, string subject) =>
        town.NonLandHearings.ConductRecords.Any(record => record.Id == allegation.IncidentId && record.ActorId == subject &&
            record.Tick == allegation.ConductTick && record.Laws.Any(law => law.LawId == allegation.LawId &&
                law.Version == allegation.LawVersion && law.PriorNoticeIds.Count > 0));
    internal static bool KnowsConductLaw(TownRuntimeState town, string actor, TownConductLawSnapshot snapshot)
    {
        var version = town.Government?.Laws.FirstOrDefault(law => law.Id == snapshot.LawId)?.Versions
            .FirstOrDefault(item => item.Version == snapshot.Version);
        return version is not null && (town.Council?.Knowledge ?? []).Any(receipt => receipt.AgentId == actor &&
            town.Council!.Notices.Any(notice => notice.Id == receipt.NoticeId && notice.Kind == "law" &&
                notice.SubjectId == snapshot.LawId && notice.PostedTick >= version.AdoptedTick &&
                (version.EndedTick is null || notice.PostedTick < version.EndedTick)));
    }

    internal static bool CanObserveNonLandAct(ITownWorld world, string observer, string actor, GridPoint position)
    {
        if (!world.Agents.TryGetValue(observer, out var person) ||
            !world.IsWithinInteractionRange(person.Position, position, WorldRuntime.CivicSharedRules.ResourceInteractionRange)) return false;
        // Being beside a private worksite does not grant access to its contents or work inside it.
        var privateSite = NonLandPrivateSite(world, position);
        return privateSite is null || world.HouseholdFor(observer) == privateSite.HouseholdId || observer == actor;
    }

    internal static PlacedBuilding? NonLandPrivateSite(ITownWorld world, GridPoint position) => world.WorldSimulation.Buildings.FirstOrDefault(building =>
        building.HouseholdId is not null && world.WorldContent.Buildings.FirstOrDefault(item => item.CanonicalId == building.DefinitionId) is { } definition &&
        WorldContentSimulationRules.Footprint(definition, building).Contains(position));
    internal static string DescribeNonLandConduct(TownConductRecord record) =>
        $"Observed {record.ConductKind.Replace('_', ' ')} at ({record.Position.X},{record.Position.Y})" +
        (record.ItemKind is null ? "." : $", involving {record.Quantity} {record.ItemKind.Replace('_', ' ')}.");
    internal static (TownViolationAllegation Allegation, TownCaseEvidence Evidence)[] NonLandKnownReports(ITownWorld world, TownRuntimeState town, string actor)
    {
        var result = new List<(TownViolationAllegation, TownCaseEvidence)>();
        foreach (var acquisition in town.NonLandHearings.Acquisitions.Where(item => item.AgentId == actor)
                     .OrderBy(item => item.Id, StringComparer.Ordinal))
        {
            var record = town.NonLandHearings.ConductRecords.Single(item => item.Id == acquisition.ConductId);
            foreach (var law in record.Laws.Where(law => KnowsConductLaw(town, actor, law) ||
                         acquisition.Kind == "record_inspection" && town.NonLandHearings.Cases.Any(file => file.Id == acquisition.PublicCaseId &&
                             file.Allegation.LawId == law.LawId && file.Allegation.LawVersion == law.Version)))
                result.Add((new(record.Id, record.ActorId, record.ConductKind, record.Position, record.Tick,
                    law.LawId, law.Version, acquisition.Text, [acquisition.Id]),
                    NonLandEvidence(world, record, acquisition, actor, 1)));
        }
        return result.GroupBy(item => (item.Item1.IncidentId, item.Item1.LawId, item.Item1.LawVersion))
            .Select(group => group.OrderBy(item => item.Item2.Kind == "observation" ? 0 : item.Item2.Kind == "record" ? 1 : 2)
                .ThenBy(item => item.Item2.Id, StringComparer.Ordinal).First()).ToArray();
    }

    internal static TownCaseEvidence[] NonLandKnownEvidence(ITownWorld world, TownRuntimeState town,
        TownNonLandCase file, string actor) => town.NonLandHearings.Acquisitions
        .Where(item => item.AgentId == actor && item.ConductId == file.Allegation.IncidentId)
        .OrderBy(item => item.Id, StringComparer.Ordinal)
        .Select(item => NonLandEvidence(world, town.NonLandHearings.ConductRecords.Single(record => record.Id == item.ConductId),
            item, actor, file.Revisions[^1].Number))
        .Concat(NonLandKnownPublicRecords(town, world.HouseholdLandUseRights, world.TownLandTitles, actor,
            file.Allegation.Position, file.Revisions[^1].Number, world.WorldTick)).ToArray();
    internal static TownCaseEvidence NonLandEvidence(ITownWorld world, TownConductRecord record, TownConductAcquisition acquired,
        string actor, int revision) => new(acquired.Id, revision,
        acquired.Kind == "firsthand" ? "observation" : acquired.Kind == "record_inspection" ? "record" : "allegation",
        acquired.Kind, acquired.SourceAgentId ?? actor,
        record.Id, record.Version, acquired.Tick, actor, world.WorldTick, acquired.Text);
    internal static TownNonLandHearingState AcquireNonLandInspectedSources(ITownWorld world, TownRuntimeState town, TownNonLandCase file, string actor)
    {
        if (!world.Agents.TryGetValue(actor, out var reader) || !TownNonLandRules.ReadCurrent(file, actor, world.WorldTick))
            return town.NonLandHearings;
        var state = town.NonLandHearings;
        foreach (var evidence in file.Evidence.Where(item => item.Kind is "observation" or "record" &&
                     item.SourceRecordId == file.Allegation.IncidentId && item.SubmittedTick <= world.WorldTick))
        {
            var original = state.Acquisitions.FirstOrDefault(item => item.Id == evidence.Id);
            if (original is null || state.Acquisitions.Any(item => item.AgentId == actor && item.ConductId == original.ConductId &&
                    item.Kind == "record_inspection" && item.PublicCaseId == file.Id)) continue;
            var acquired = new TownConductAcquisition(WorldRuntime.CivicSharedRules.ConductInspectionId(original.ConductId, file.Id, actor),
                original.ConductId, actor, "record_inspection", world.WorldTick, reader.Position, evidence.Text,
                actor, original.Id, ObserverHouseholdId: world.HouseholdFor(actor), PublicCaseId: file.Id);
            state = state with { Acquisitions = state.Acquisitions.Append(acquired).ToArray() };
        }
        return state;
    }

    internal bool RelayNonLandKnownReport(ITownWorld world, TownRuntimeState town, string source, string recipient,
        string incidentId, string statement)
    {
        if (!world.Agents.TryGetValue(source, out var speaker) || !world.Agents.TryGetValue(recipient, out var listener) ||
            source == recipient || !world.IsWithinInteractionRange(speaker.Position, listener.Position, WorldRuntime.CivicSharedRules.ResourceInteractionRange) ||
            string.IsNullOrWhiteSpace(statement) || statement.Length > TownCouncilRules.MaximumProposalText || statement.Any(char.IsControl)) return false;
        town = world.Towns.Single(item => item.Id == town.Id);
        if (town.NonLandHearings.Acquisitions.Any(item => item.AgentId == recipient && item.ConductId == incidentId)) return true;
        var original = town.NonLandHearings.Acquisitions.Where(item => item.AgentId == source && item.ConductId == incidentId)
            .OrderBy(item => item.Tick).ThenBy(item => item.Id, StringComparer.Ordinal).FirstOrDefault();
        if (original is null) return false;
        var id = WorldRuntime.CivicSharedRules.ConductRelayId(incidentId, world.WorldTick, source, recipient);
        if (town.NonLandHearings.Acquisitions.Any(item => item.Id == id)) return true;
        var acquired = new TownConductAcquisition(id, incidentId, recipient, "relay", world.WorldTick,
            listener.Position, statement.Trim(), source, original.Id, speaker.Position, world.HouseholdFor(recipient));
        SetTown(world, town with { NonLandHearings = town.NonLandHearings with { Acquisitions = town.NonLandHearings.Acquisitions.Append(acquired).ToArray() } });
        return true;
    }

    internal bool NonLandRemedyTermKnownTo(ITownWorld world, TownRuntimeState town, TownRemedyTerm term, string actor)
    {
        if (term.TargetId is null) return true;
        if (term.Kind == "public_service_goods") return world.WorldSimulation.Buildings.Any(building =>
            building.InstanceId == term.TargetId && building.TownId == town.Id &&
            (TownForResident(world, actor) == town.Id || world.Agents.TryGetValue(actor, out var person) && world.IsWithinInteractionRange(person.Position, building.Position, WorldRuntime.CivicSharedRules.ResourceInteractionRange)));
        var lot = world.Society.Inventory.Lots.FirstOrDefault(item => item.Id == term.TargetId);
        if (lot is null) return false;
        if (lot.OwnerId == actor || lot.OwnerId == world.HouseholdFor(actor)) return true;
        return world.Agents.TryGetValue(actor, out var observer) && world.Agents.TryGetValue(term.ContributorId, out var contributor) && world.IsWithinInteractionRange(observer.Position, contributor.Position, WorldRuntime.CivicSharedRules.ResourceInteractionRange) &&
            ToolProgressionRules.IsTopLevelCarriedLot(lot, term.ContributorId) &&
            CanObserveNonLandAct(world, actor, term.ContributorId, contributor.Position);
    }

    internal static bool NonLandWorkAllowed(ITownWorld world, string actor) => world.IsAdult(actor) && world.Agents.TryGetValue(actor, out var person) &&
        !WorldRuntime.CivicSharedRules.NeedsUrgentFood(person) && !world.NeedsUrgentWarmth(person) && !world.IllDependentsNeedingCare(actor).Any() &&
        !world.ChildrenNeedingCare(actor).Any() &&
        IllnessRules.AllowsWork(actor, world.WorldTick, person.Survival?.IllnessBasisPoints ?? 0);
    internal static bool NonLandEquipmentRepairPaused(ITownWorld world, string actor) => world.Agents[actor].Equipment?.Repair is { } repair && world.Towns.Any(town => town.NonLandHearings.Agreements.Any(agreement => agreement.Status is "pending" or "overdue" &&
            !TownRemedyRules.IsSuperseded(town.NonLandHearings, agreement.Id) && agreement.Terms.Any(term =>
                term.Kind == "repair_equipment" && term.ContributorId == actor && term.TargetId == repair.LotId &&
                NonLandRemaining(town.NonLandHearings, agreement, term) > 0))) &&
        (world.IllDependentsNeedingCare(actor).Any() || world.ChildrenNeedingCare(actor).Any());
    internal static InventoryLot? NonLandReturnLot(ITownWorld world, TownRemedyTerm term) => world.Society.Inventory.Lots
        .Where(lot => lot.OwnerId == term.ContributorId && ToolProgressionRules.IsTopLevelCarriedLot(lot, term.ContributorId) &&
            lot.DeliveryBuildingId is null && lot.ItemKind == term.ItemKind && (term.TargetId is null || lot.Id == term.TargetId) &&
            !InventoryContainerRules.IsContainer(lot.ItemKind) &&
            !PersonalEquipmentRules.IsSelected(world.Agents[term.ContributorId].Equipment, lot.Id) && world.AvailableLotQuantity(lot) >= term.Quantity)
        .OrderBy(lot => lot.Id, StringComparer.Ordinal).FirstOrDefault();
    internal static bool NonLandRemedyFeasible(ITownWorld world, TownRuntimeState town, TownRemedyTerm term)
    {
        if (term.Quantity <= 0 || !NonLandWorkAllowed(world, term.ContributorId)) return false;
        var actor = term.ContributorId;
        var person = world.Agents[actor];
        if (term.Kind == "return_goods")
            return term.BeneficiaryId is { } recipient && recipient != actor && world.Agents.TryGetValue(recipient, out var beneficiary) && world.IsWithinInteractionRange(person.Position, beneficiary.Position, WorldRuntime.CivicSharedRules.ResourceInteractionRange) &&
                CanObserveNonLandAct(world, actor, recipient, beneficiary.Position) &&
                NonLandReturnLot(world, term) is { } lot && world.AvailableLotQuantity(lot) >= term.Quantity &&
                (!WorldRuntime.CivicSharedRules.IsEdibleFood(lot.ItemKind) || person.FullnessBasisPoints >= 6_500 && world.PreferredFood(actor, actor).Sum(world.AvailableLotQuantity) - term.Quantity >= 2) && world.FreeCarryCapacity(recipient) >= term.Quantity;
        if (term.Kind == "public_service_goods")
        {
            var warehouse = world.WarehouseForResident(actor);
            return term.BeneficiaryId == town.Id && warehouse is not null && warehouse.TownId == town.Id &&
                (term.TargetId is null || term.TargetId == warehouse.InstanceId) && WorldRuntime.CivicSharedRules.WarehouseResourceKinds.Contains(term.ItemKind ?? "") && world.DestinationRoom(warehouse.InstanceId) >= term.Quantity && world.PersonalWarehouseAvailableQuantity(actor, term.ItemKind!) - WorldRuntime.CivicSharedRules.WarehouseLoadQuantity >= term.Quantity &&
                (person.Position == warehouse.Position || world.FindUnoccupiedRoute(actor, person.Position, warehouse.Position, 0).Count > 0);
        }
        if (term.Kind != "repair_equipment" || term.Quantity != 1 || term.TargetId is null) return false;
        var predecessor = town.NonLandHearings.Offers.Where(offer => offer.Terms.Any(item => item.Id == term.Id))
            .Select(offer => offer.ReplacesOfferId is { } previous ? town.NonLandHearings.Offers.FirstOrDefault(item => item.Id == previous)?.AgreementId : null)
            .FirstOrDefault(id => id is not null);
        if (world.Towns.Select(other => other.Id == town.Id ? town : other).Any(other => other.NonLandHearings.Agreements.Any(agreement =>
                agreement.Status is "pending" or "overdue" && !TownRemedyRules.IsSuperseded(other.NonLandHearings, agreement.Id) &&
                !(other.Id == town.Id && agreement.Id == predecessor) && agreement.Terms.Any(existing =>
                    existing.Kind == "repair_equipment" && existing.ContributorId == term.ContributorId && existing.TargetId == term.TargetId &&
                    NonLandRemaining(other.NonLandHearings, agreement, existing) > 0 &&
                    (other.Id != town.Id || existing.Id != term.Id))))) return false;
        var target = world.Society.Inventory.Lots.FirstOrDefault(lot => lot.Id == term.TargetId && lot.ItemKind == term.ItemKind &&
            lot.ConditionBasisPoints < 10_000 && (lot.OwnerId == actor || lot.OwnerId == world.HouseholdFor(actor)));
        if (target is null || term.BeneficiaryId != target.OwnerId) return false;
        if (person.Equipment?.Repair?.LotId == target.Id && world.CanContinueEquipmentRepair(actor)) return true;
        if (!NonLandRepairMaterials(target.ItemKind).All(input => world.HasCarriedMaterial(actor, input.ResourceId, input.Amount))) return false;
        var native = new List<CognitionCandidate>();
        world.AddToolRepairCandidates(native, actor);
        world.AddEquipmentCandidates(native, actor, person);
        world.AddHandcartCandidates(native, actor, person);
        return native.Any(candidate => candidate.Id == WorldRuntime.CivicSharedRules.RepairToolPrefix + target.Id || candidate.Id == WorldRuntime.CivicSharedRules.RepairCartPrefix + target.Id ||
            candidate.Id == "repair_equipment" && world.WornEquipmentItems(actor).Any(lot => lot.Id == target.Id) && world.CanPrepareEquipmentRepair(actor, person, target));
    }

    internal static bool NonLandRemedyDurationFeasible(ITownWorld world, TownRuntimeState town, TownRemedyTerm[] terms, long ticks)
    {
        if (ticks <= 0 || terms.Length == 0 || terms.Any(term => !NonLandRemedyFeasible(world, town, term)) ||
            !NonLandRemedyCapacityFeasible(world, terms)) return false;
        foreach (var group in terms.GroupBy(term => term.ContributorId, StringComparer.Ordinal))
        {
            var actor = group.Key;
            var person = world.Agents[actor];
            var position = person.Position;
            long needed = 0;
            var used = new Dictionary<string, long>(StringComparer.Ordinal);
            var namedReturns = new Dictionary<string, long>(StringComparer.Ordinal);
            var repaired = new HashSet<string>(StringComparer.Ordinal);
            foreach (var term in group)
            {
                GridPoint destination;
                long work;
                if (term.Kind == "repair_equipment")
                {
                    if (!repaired.Add(term.TargetId!)) return false;
                    var target = world.Society.Inventory.GetLot(term.TargetId!);
                    var inProgress = person.Equipment?.Repair?.LotId == target.Id ? person.Equipment.Repair : null;
                    var materials = inProgress is null ? NonLandRepairMaterials(target.ItemKind) : [];
                    foreach (var input in materials)
                        used[input.ResourceId] = used.GetValueOrDefault(input.ResourceId) + input.Amount;
                    if (target.ItemKind == InventoryContainerRules.Handcart)
                    {
                        destination = new(target.GroundPosition!.Value.X, target.GroundPosition.Value.Y);
                        work = 1;
                    }
                    else if (ToolProgressionRules.Find(target.ItemKind) is not null)
                    {
                        destination = world.HouseholdBuildingWithTag(world.HouseholdFor(actor), "blacksmith")!.Position;
                        work = 1;
                    }
                    else
                    {
                        destination = world.EquipmentRepairSite(actor, target)!.Position;
                        var repairTicks = world.SkilledWorkTicks(actor, SkillKind.Crafting, PersonalEquipmentRules.RepairWorkTicks);
                        work = inProgress is null ? 1 + repairTicks : Math.Max(1, repairTicks - inProgress.WorkDone);
                    }
                }
                else
                {
                    used[term.ItemKind!] = used.GetValueOrDefault(term.ItemKind!) + term.Quantity;
                    if (term.Kind == "return_goods" && term.TargetId is { } source)
                        namedReturns[source] = namedReturns.GetValueOrDefault(source) + term.Quantity;
                    destination = term.Kind == "return_goods" ? world.Agents[term.BeneficiaryId!].Position : world.WarehouseForResident(actor)!.Position;
                    work = term.Kind == "return_goods" ? 1 : ((long)term.Quantity + WorldRuntime.CivicSharedRules.WarehouseLoadQuantity - 1) / WorldRuntime.CivicSharedRules.WarehouseLoadQuantity;
                }
                var range = term.Kind == "return_goods" ? WorldRuntime.CivicSharedRules.ResourceInteractionRange : 0;
                if (!world.IsWithinInteractionRange(position, destination, range))
                {
                    if (position == person.Position) needed += person.TravelCooldownTicks;
                    var route = world.FindUnoccupiedRoute(actor, position, destination, range);
                    if (route.Count < 2) return false;
                    for (var step = 1; step < route.Count; step++)
                    {
                        // The last arrival needs one tick; its cooldown delays later travel, not onsite work.
                        needed += step == route.Count - 1 ? 1 : (world.TravelStepCost(actor, route[step - 1], route[step]) + 99) / 100 +
                            IllnessRules.TravelDelayTicks(person.Survival?.IllnessBasisPoints ?? 0);
                    }
                    position = route[^1];
                }
                needed += work;
                if (needed > ticks) return false;
            }
            foreach (var promise in namedReturns)
                if (world.AvailableLotQuantity(world.Society.Inventory.GetLot(promise.Key)) < promise.Value) return false;
            foreach (var expenditure in used)
            {
                var stock = world.Society.Inventory.Lots.Where(lot => lot.OwnerId == actor &&
                    ToolProgressionRules.IsTopLevelCarriedLot(lot, actor) && lot.DeliveryBuildingId is null &&
                    lot.ItemKind == expenditure.Key).Sum(lot => (long)world.AvailableLotQuantity(lot));
                var reserve = group.Any(term => term.Kind == "public_service_goods" && term.ItemKind == expenditure.Key)
                    ? WorldRuntime.CivicSharedRules.WarehouseLoadQuantity : WorldRuntime.CivicSharedRules.IsEdibleFood(expenditure.Key) ? 2 : 0;
                if (stock - reserve < expenditure.Value) return false;
            }
        }
        return true;
    }

    internal static bool NonLandRemedyCapacityFeasible(ITownWorld world, IEnumerable<TownRemedyTerm> terms)
    {
        foreach (var returns in terms.Where(term => term.Kind == "return_goods")
                     .GroupBy(term => term.BeneficiaryId!, StringComparer.Ordinal))
            if (!world.Agents.ContainsKey(returns.Key) || returns.Sum(term => (long)term.Quantity) > world.FreeCarryCapacity(returns.Key)) return false;
        var deliveries = terms.Where(term => term.Kind == "public_service_goods")
            .Select(term => (Term: term, Warehouse: world.WarehouseForResident(term.ContributorId))).ToArray();
        if (deliveries.Any(delivery => delivery.Warehouse is null)) return false;
        foreach (var storage in deliveries.GroupBy(delivery => delivery.Warehouse!.InstanceId, StringComparer.Ordinal))
            if (storage.Sum(delivery => (long)delivery.Term.Quantity) > world.DestinationRoom(storage.Key)) return false;
        return true;
    }

    internal static string NonLandRemedyCandidateId(TownRuntimeState town, TownRestorativeAgreement agreement, TownRemedyTerm term) =>
        WorldRuntime.CivicSharedRules.NonLandRemedyChoiceId(town.Id, agreement.Id, term.Id);
    internal static int NonLandRemaining(TownNonLandHearingState state, TownRestorativeAgreement agreement, TownRemedyTerm term) =>
        term.Quantity - state.Effects.Where(effect => effect.AgreementId == agreement.Id && effect.TermId == term.Id).Sum(effect => effect.Quantity);
    internal static void AddNonLandRemedyCandidates(ITownWorld world, List<CognitionCandidate> candidates, string actor, TownRuntimeState town)
    {
        if (world.Agents.TryGetValue(actor, out var speaker))
            foreach (var record in town.NonLandHearings.ConductRecords.Where(record =>
                         town.NonLandHearings.Acquisitions.Any(item => item.ConductId == record.Id && item.AgentId == actor)))
                foreach (var listener in world.Agents.Values.Where(person => person.AgentId != actor && world.IsWithinInteractionRange(speaker.Position, person.Position, WorldRuntime.CivicSharedRules.ResourceInteractionRange) &&
                             !town.NonLandHearings.Acquisitions.Any(item => item.ConductId == record.Id && item.AgentId == person.AgentId)))
                    candidates.Add(new(NonLandRelayCandidateId(town, record, listener.AgentId),
                        "Tell this nearby person what you observed or were told about this act; it remains a report.",
                        185, listener.AgentId));
        if (!NonLandWorkAllowed(world, actor)) return;
        foreach (var agreement in town.NonLandHearings.Agreements.Where(item => item.Status is "pending" or "overdue" &&
                     !TownRemedyRules.IsSuperseded(town.NonLandHearings, item.Id))
                     .OrderBy(item => item.Id, StringComparer.Ordinal))
            foreach (var term in agreement.Terms.Where(item => item.ContributorId == actor))
            {
                var remaining = NonLandRemaining(town.NonLandHearings, agreement, term);
                if (remaining <= 0 || !NonLandRemedyFeasible(world, town, term with { Quantity = remaining })) continue;
                candidates.Add(new(NonLandRemedyCandidateId(town, agreement, term),
                    $"Voluntarily complete agreed {term.Kind.Replace('_', ' ')} using real goods or repair work.", 185, term.TargetId));
            }
    }

    internal bool ApplyNonLandRemedyAction(ITownWorld world, TownRuntimeState town, string actor, string candidateId)
    {
        if (candidateId.StartsWith(NonLandRelayPrefix, StringComparison.Ordinal))
        {
            foreach (var record in town.NonLandHearings.ConductRecords)
                foreach (var listener in world.Agents.Keys)
                    if (candidateId == NonLandRelayCandidateId(town, record, listener))
                    {
                        var known = town.NonLandHearings.Acquisitions.FirstOrDefault(item => item.ConductId == record.Id && item.AgentId == actor);
                        if (known is not null) RelayNonLandKnownReport(world, town, actor, listener, record.Id, known.Text);
                        return true;
                    }
            return false;
        }
        if (!candidateId.StartsWith(NonLandRemedyPrefix, StringComparison.Ordinal)) return false;
        town = world.Towns.Single(item => item.Id == town.Id);
        foreach (var agreement in town.NonLandHearings.Agreements.Where(item => item.Status is "pending" or "overdue" &&
                     !TownRemedyRules.IsSuperseded(town.NonLandHearings, item.Id)))
            foreach (var term in agreement.Terms.Where(item => item.ContributorId == actor))
            {
                if (candidateId != NonLandRemedyCandidateId(town, agreement, term)) continue;
                var remaining = NonLandRemaining(town.NonLandHearings, agreement, term);
                if (remaining <= 0 || !NonLandRemedyFeasible(world, town, term with { Quantity = remaining })) return true;
                var previousSelection = world.NonLandRemedySelection;
                world.NonLandRemedySelection = (town.Id, agreement.Id, term.Id);
                try
                {
                    if (term.Kind == "return_goods")
                    {
                        var lot = NonLandReturnLot(world, term with { Quantity = remaining })!;
                        var operation = WorldRuntime.CivicSharedRules.NativeReturnId(world.WorldTick, actor, world.NextEventId);
                        world.ApplyInventoryTransition(inventory => InventoryAuthority.Transfer(inventory, operation, actor,
                            term.BeneficiaryId!, lot.Id, remaining, "voluntary_restitution"));
                        RecordNonLandGoodsCompletion(world, actor, lot, term.BeneficiaryId!, remaining, null, operation, "return_goods");
                        world.AppendEvent(new VoluntaryGoodsReturnedEventPayload(actor, term.BeneficiaryId, lot.Id, remaining));
                    }
                    else if (term.Kind == "public_service_goods")
                        DeliverNonLandTownGoods(world, actor, term, remaining);
                    else
                    {
                        var person = world.Agents[actor];
                        var target = world.Society.Inventory.GetLot(term.TargetId!);
                        if (target.ItemKind == InventoryContainerRules.Handcart)
                            world.ApplyHandcartCandidate(actor, person, WorldRuntime.CivicSharedRules.RepairCartPrefix + target.Id);
                        else if (ToolProgressionRules.Find(target.ItemKind) is not null)
                            world.RepairTool(actor, person, target.Id);
                        else if (person.Equipment?.Repair?.LotId == target.Id)
                            world.ContinueEquipmentRepair(actor);
                        else
                            world.RepairEquipment(actor, person, target.Id);
                    }
                }
                finally
                {
                    world.NonLandRemedySelection = previousSelection;
                }
                return true;
            }
        return false;
    }

    internal static string NonLandRelayCandidateId(TownRuntimeState town, TownConductRecord record, string listener) =>
        WorldRuntime.CivicSharedRules.NonLandRelayChoiceId(town.Id, record.Id, listener);
    internal void DeliverNonLandTownGoods(ITownWorld world, string actor, TownRemedyTerm term, int remaining)
    {
        var warehouse = world.WarehouseForResident(actor)!;
        if (world.Agents[actor].Position != warehouse.Position)
        {
            world.MoveToward(actor, world.Agents[actor], warehouse.Position, "voluntary_town_service", 0);
            return;
        }
        if (world.PersonalWarehouseSurplus(actor, term.ItemKind) is not { } surplus) return;
        var lot = surplus.Lot;
        var quantity = Math.Min(remaining, Math.Min(WorldRuntime.CivicSharedRules.WarehouseLoadQuantity,
            Math.Min(world.DestinationRoom(warehouse.InstanceId), surplus.Quantity)));
        if (quantity <= 0) return;
        var availableBefore = world.PersonalWarehouseAvailableQuantity(actor, lot.ItemKind);
        var operation = WorldRuntime.CivicSharedRules.WarehouseStockId(world.WorldTick, actor);
        world.ApplyInventoryTransition(inventory => InventoryAuthority.Transfer(inventory, operation, actor, warehouse.TownId!,
            lot.Id, quantity, "town_resources_stored", warehouse.InstanceId));
        RecordNonLandGoodsCompletion(world, actor, lot, warehouse.TownId!, quantity, warehouse.InstanceId, operation, "public_service_goods", availableBefore);
        world.AppendEvent(new TownResourcesStoredEventPayload(actor, lot.Id, quantity, warehouse.InstanceId));
    }

    internal void RecordNonLandRepairCompletion(ITownWorld world, string actor, InventoryLot originalTarget, string repairedLotId,
        string nativeReceiptId, GridPoint position, IReadOnlyList<string> reservationIds)
    {
        var result = world.Society.Inventory.GetLot(repairedLotId);
        if (result.ConditionBasisPoints <= originalTarget.ConditionBasisPoints) return;
        nativeReceiptId = DerivedId.Create(nativeReceiptId);
        var receipt = new TownNativeRemedyReceipt(nativeReceiptId, "repair_equipment", actor, world.WorldTick,
            originalTarget.OwnerId, originalTarget.ItemKind, 1, originalTarget.Id, originalTarget.Id,
            repairedLotId, originalTarget.OwnerId, result.OwnerId, position,
            originalTarget.ConditionBasisPoints, result.ConditionBasisPoints, "", world.HouseholdFor(actor), TownForResident(world, actor),
            originalTarget.Quantity, 1, NonLandRepairMaterials(originalTarget.ItemKind).Zip(reservationIds,
                (input, id) => new TownNativeRepairInput(input.ResourceId, world.Society.Inventory.GetReservation(id))).ToArray());
        RecordNonLandNativeReceipt(world, receipt);
        RecordNonLandConduct(world, actor, "repair_equipment", position, originalTarget.Id, originalTarget.ItemKind, 1, nativeReceiptId);
    }

    internal void RecordNonLandGoodsCompletion(ITownWorld world, string actor, InventoryLot sourceLot, string recipientId,
        int quantity, string? storageBuildingId, string nativeReceiptId, string kind, long? carriedAvailableBefore = null)
    {
        var resultId = quantity == sourceLot.Quantity ? sourceLot.Id : DerivedId.Create(sourceLot.Id + "#transfer:" + nativeReceiptId);
        var result = world.Society.Inventory.GetLot(resultId);
        var receipt = new TownNativeRemedyReceipt(nativeReceiptId, kind, actor, world.WorldTick, recipientId,
            sourceLot.ItemKind, quantity, storageBuildingId ?? sourceLot.Id, sourceLot.Id, resultId,
            sourceLot.OwnerId, result.OwnerId, world.Agents[actor].Position,
            sourceLot.ConditionBasisPoints, result.ConditionBasisPoints, "", world.HouseholdFor(actor), TownForResident(world, actor),
            sourceLot.Quantity, world.AvailableLotQuantity(sourceLot), [], carriedAvailableBefore);
        RecordNonLandNativeReceipt(world, receipt);
        RecordNonLandConduct(world, actor, kind, receipt.Position, receipt.TargetId, receipt.ItemKind, quantity, nativeReceiptId);
    }

    internal void RecordNonLandNativeReceipt(ITownWorld world, TownNativeRemedyReceipt receipt)
    {
        if (world.Towns.Any(town => town.NonLandHearings.NativeReceipts.Any(item => item.Id == receipt.Id))) return;
        // One physical completion cannot be spent against two agreements or two Towns.
        foreach (var town in world.Towns.OrderBy(item => item.Id, StringComparer.Ordinal).ToArray())
            foreach (var agreement in town.NonLandHearings.Agreements.Where(item => item.Status is "pending" or "overdue" &&
                     !TownRemedyRules.IsSuperseded(town.NonLandHearings, item.Id))
                         .OrderBy(item => item.AcceptedTick).ThenBy(item => item.Id, StringComparer.Ordinal))
                foreach (var term in agreement.Terms)
                {
                    if (world.NonLandRemedySelection is { } selected &&
                        (selected.TownId != town.Id || selected.AgreementId != agreement.Id || selected.TermId != term.Id)) continue;
                    if (agreement.AcceptedTick > receipt.Tick || term.Kind != receipt.Kind || term.ContributorId != receipt.ActorId ||
                        term.BeneficiaryId != receipt.BeneficiaryId || term.ItemKind != receipt.ItemKind ||
                        term.TargetId is not null && term.TargetId != receipt.TargetId) continue;
                    var remaining = NonLandRemaining(town.NonLandHearings, agreement, term);
                    if (remaining <= 0) continue;
                    receipt = receipt with { Version = NonLandDigest(receipt) };
                    var effect = new TownRemedyEffect(WorldRuntime.CivicSharedRules.RemedyEffectId(receipt.Id), agreement.Id, term.Id, receipt.ActorId,
                        receipt.Tick, receipt.Kind, receipt.BeneficiaryId, receipt.ItemKind, Math.Min(remaining, receipt.Quantity),
                        term.TargetId, receipt.Id, receipt.Version);
                    var state = town.NonLandHearings with { NativeReceipts = town.NonLandHearings.NativeReceipts.Append(receipt).ToArray() };
                    SetTown(world, town with { NonLandHearings = TownRemedyRules.RecordEffect(state, effect) });
                    world.AppendEvent(new LawCaseRemedyEffectEventPayload(receipt.ActorId, town.Id, agreement.Id), receipt.Position);
                    return;
                }
    }

    internal static IReadOnlyList<ContentQuantity> NonLandRepairMaterials(string itemKind) =>
        itemKind == InventoryContainerRules.Handcart ? [new("wood", 1), new("iron_fittings", 1), new("rope", 1)] :
        ToolProgressionRules.Find(itemKind) is not null ? ToolProgressionRules.RepairMaterials(itemKind) :
        PersonalEquipmentRules.RepairMaterials(itemKind);
}
