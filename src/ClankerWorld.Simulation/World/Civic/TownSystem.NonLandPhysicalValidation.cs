using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

internal sealed partial class TownSystem
{
    internal static void ValidateNonLandPhysicalState(WorldMap map, SocietyCheckpoint society,
        IReadOnlyList<TownRuntimeState> towns, IReadOnlyList<TownLandTitleRecord> titles,
        IReadOnlyList<HouseholdLandUseRight> rights)
    {
        var tick = society.WorldTick;
        var agents = society.Agents.Select(person => person.Id).ToHashSet(StringComparer.Ordinal);
        var households = society.Households.Select(household => household.Id).ToHashSet(StringComparer.Ordinal);
        var townIds = towns.Select(town => town.Id).ToHashSet(StringComparer.Ordinal);
        var nativeReceipts = new HashSet<string>(StringComparer.Ordinal);
        var creditedReceipts = new HashSet<string>(StringComparer.Ordinal);
        var consumedRepairInputs = new HashSet<string>(StringComparer.Ordinal);
        var sharedActs = new Dictionary<string, TownConductRecord>(StringComparer.Ordinal);
        foreach (var town in towns)
        {
            var state = town.NonLandHearings;
            if (state is null || state.ConductRecords is null || state.Acquisitions is null || state.NativeReceipts is null ||
                state.ConductRecords.Any(record => record is null || !IdentityRules.Reference(record.Id, DerivedId.MaximumLength, requireTrimmed: true) ||
                    !agents.Contains(record.ActorId) || record.ActorTownId is not null && !townIds.Contains(record.ActorTownId) ||
                    record.ConductKind is not ("travel" or "gather_food" or "gather_material" or "fell_tree" or
                        "repair_equipment" or "return_goods" or "public_service_goods") ||
                    !map.Contains(record.Position) || record.Tick < 0 || record.Tick > tick || record.Quantity <= 0 ||
                    record.TargetId is not null && !IdentityRules.Reference(record.TargetId, int.MaxValue, requireTrimmed: true) ||
                    record.ItemKind is not null && !IdentityRules.Reference(record.ItemKind, int.MaxValue, requireTrimmed: true) ||
                    record.Laws is not { Count: > 0 } || record.Laws.Any(law => law is null ||
                        !IdentityRules.Reference(law.LawId, int.MaxValue, requireTrimmed: true) || law.Version <= 0 || law.TitleIds is null || law.PriorNoticeIds is null ||
                        !law.TitleIds.All(id => IdentityRules.Reference(id, requireTrimmed: true)) || !law.PriorNoticeIds.All(id => IdentityRules.Reference(id, requireTrimmed: true))) ||
                    record.Version != NonLandDigest(record with { Version = "" })) ||
                state.ConductRecords.Select(record => record.Id).Distinct(StringComparer.Ordinal).Count() != state.ConductRecords.Count ||
                state.Acquisitions.Any(item => item is null || !IdentityRules.Reference(item.Id, DerivedId.MaximumLength, requireTrimmed: true) || !IdentityRules.Reference(item.ConductId, int.MaxValue, requireTrimmed: true) ||
                    !agents.Contains(item.AgentId) || item.Kind is not ("firsthand" or "relay" or "record_inspection") ||
                    item.Tick < 0 || item.Tick > tick || !map.Contains(item.Position) ||
                    item.ObserverHouseholdId is not null && !households.Contains(item.ObserverHouseholdId) ||
                    item.PrivateSiteHouseholdId is not null && !households.Contains(item.PrivateSiteHouseholdId) ||
                    !BoundedText.IsValid(item.Text, 1024, requireTrimmed: true)) ||
                state.Acquisitions.Select(item => item.Id).Distinct(StringComparer.Ordinal).Count() != state.Acquisitions.Count)
                throw new InvalidDataException("Saved physical conduct and personal acquisition history is invalid.");

            var records = state.ConductRecords.ToDictionary(record => record.Id, StringComparer.Ordinal);
            var acquisitions = state.Acquisitions.ToDictionary(item => item.Id, StringComparer.Ordinal);
            foreach (var record in state.ConductRecords)
            {
                if (sharedActs.TryGetValue(record.Id, out var other) &&
                    (other.ActorId != record.ActorId || other.ActorTownId != record.ActorTownId || other.Tick != record.Tick ||
                     other.ConductKind != record.ConductKind || other.Position != record.Position || other.TargetId != record.TargetId ||
                     other.ItemKind != record.ItemKind || other.Quantity != record.Quantity))
                    throw new InvalidDataException("One native act cannot identify different physical conduct in different Towns.");
                sharedActs[record.Id] = record;
                if (record.Laws.Select(law => law.LawId).Distinct(StringComparer.Ordinal).Count() != record.Laws.Count)
                    throw new InvalidDataException("An act cannot carry competing versions of the same Town law.");
                foreach (var law in record.Laws)
                {
                    var version = town.Government?.Laws.FirstOrDefault(item => item.Id == law.LawId)?.Versions
                        .FirstOrDefault(item => item.Version == law.Version);
                    // A later action in this same tick can amend or repeal a law. The native snapshot
                    // retains the wording observed before that change, rather than reinterpreting it.
                    if (version is null || version.AdoptedTick > record.Tick || version.EndedTick < record.Tick ||
                        version.Scope != law.Scope || ConductLawDigest(version) != law.WordingDigest ||
                        law.TitleIds.Distinct(StringComparer.Ordinal).Count() != law.TitleIds.Count ||
                        law.PriorNoticeIds.Distinct(StringComparer.Ordinal).Count() != law.PriorNoticeIds.Count ||
                        law.TitleIds.Any(id => !titles.Any(title => title.Id == id && title.TownId == town.Id &&
                            title.RecordedTick <= record.Tick && title.Tiles.Contains(record.Position))) ||
                        law.Scope == TownLawRules.ResidentDuty && record.ActorTownId != town.Id ||
                        law.Scope is TownLawRules.Jurisdiction or TownLawRules.Site && law.TitleIds.Count == 0 ||
                        law.Scope == TownLawRules.Site && !version.SiteTiles.Contains(record.Position) ||
                        law.PriorNoticeIds.Any(id => !(town.Council?.Knowledge ?? []).Any(receipt =>
                            receipt.AgentId == record.ActorId && receipt.NoticeId == id && receipt.LearnedTick <= record.Tick &&
                            town.Council!.Notices.Any(notice => notice.Id == id && notice.Kind == "law" &&
                                notice.SubjectId == law.LawId && notice.PostedTick >= version.AdoptedTick && notice.PostedTick <= record.Tick))))
                        throw new InvalidDataException("Saved conduct lacks its actual historical jurisdiction, wording or prior notice.");
                }
            }
            foreach (var acquired in state.Acquisitions)
            {
                if (!records.TryGetValue(acquired.ConductId, out var record) || acquired.Tick < record.Tick)
                    throw new InvalidDataException("Personal conduct knowledge must reference an existing earlier act.");
                if (acquired.Kind == "firsthand")
                {
                    if (acquired.Tick != record.Tick || acquired.SourceAgentId is not null || acquired.ParentId is not null ||
                        acquired.SourcePosition is not null || acquired.PublicCaseId is not null ||
                        acquired.AgentId != record.ActorId && map.FootDistance(acquired.Position, record.Position) > WorldRuntime.CivicSharedRules.ResourceInteractionRange ||
                        acquired.AgentId != record.ActorId && acquired.PrivateSiteHouseholdId is not null &&
                            acquired.ObserverHouseholdId != acquired.PrivateSiteHouseholdId ||
                        acquired.Text != DescribeNonLandConduct(record))
                        throw new InvalidDataException("A firsthand observation must match the native act and observation range.");
                }
                else if (acquired.Kind == "relay" && (acquired.SourceAgentId is null || acquired.SourceAgentId == acquired.AgentId ||
                    acquired.ParentId is null || !acquisitions.TryGetValue(acquired.ParentId, out var parent) ||
                    parent.AgentId != acquired.SourceAgentId || parent.ConductId != acquired.ConductId || parent.Tick > acquired.Tick ||
                    acquired.SourcePosition is not { } sourcePosition || !map.Contains(sourcePosition) ||
                    map.FootDistance(sourcePosition, acquired.Position) > WorldRuntime.CivicSharedRules.ResourceInteractionRange || acquired.PublicCaseId is not null))
                    throw new InvalidDataException("A communicated claim must retain its source's earlier acquisition.");
                else if (acquired.Kind == "record_inspection" && (acquired.SourceAgentId != acquired.AgentId ||
                    acquired.ParentId is null || !acquisitions.TryGetValue(acquired.ParentId, out var inspected) ||
                    inspected.ConductId != acquired.ConductId || inspected.Tick > acquired.Tick ||
                    acquired.SourcePosition is not null || acquired.PrivateSiteHouseholdId is not null ||
                    !state.Cases.Any(file => file.Id == acquired.PublicCaseId && file.Evidence.Any(evidence =>
                        evidence.Id == acquired.ParentId && evidence.Kind is "observation" or "record" &&
                        evidence.SourceRecordId == acquired.ConductId && evidence.Text == acquired.Text &&
                        evidence.SubmittedTick <= acquired.Tick) && file.Reads.Any(read => read.AgentId == acquired.AgentId &&
                            read.ReadTick == acquired.Tick && read.EvidenceIds.Contains(acquired.ParentId, StringComparer.Ordinal)))))
                    throw new InvalidDataException("Record inspection requires the actual public case read and its admitted source evidence.");
                var seen = new HashSet<string>(StringComparer.Ordinal) { acquired.Id };
                var cursor = acquired;
                while (cursor.ParentId is { } parentId)
                {
                    if (!seen.Add(parentId) || !acquisitions.TryGetValue(parentId, out cursor!))
                        throw new InvalidDataException("Conduct communication provenance cannot cycle or lose its source.");
                }
            }
            foreach (var file in state.Cases)
            {
                ValidatePhysicalAllegation(file.Allegation, records);
                ValidatePhysicalAcquisitions(file.Allegation.IncidentId, file.Filings[0].AgentId, file.FiledTick,
                    file.Allegation.SourceEvidenceIds, acquisitions);
                foreach (var filing in file.Filings)
                    ValidatePhysicalAcquisitions(file.Allegation.IncidentId, filing.AgentId, filing.Tick, filing.EvidenceIds, acquisitions);
                foreach (var evidence in file.Evidence)
                {
                    if (evidence.Kind == "allegation" && evidence.Acquisition == "statement" &&
                        evidence.SourceRecordId is null && evidence.SourceVersion is null &&
                        evidence.SourceAgentId == evidence.SubmittedByAgentId && evidence.ObservedTick == evidence.SubmittedTick)
                        continue;
                    if (evidence.Kind == "record" && evidence.Acquisition == "record_inspection" &&
                        NonLandKnownPublicRecords(town, rights, titles, evidence.SubmittedByAgentId,
                            file.Allegation.Position, evidence.Revision, evidence.SubmittedTick).Any(record =>
                            (record with
                            {
                                Id = "case-evidence:" + NonLandToken(file.Id + "|" + record.Id + "|" +
                                evidence.SubmittedByAgentId + "|" + evidence.Revision)
                            }) == evidence))
                        continue;
                    var law = town.Government?.Laws.FirstOrDefault(item => item.Id == file.Allegation.LawId);
                    var wording = law?.Versions.FirstOrDefault(version => version.Version == file.Allegation.LawVersion);
                    if (law is not null && wording is not null && evidence.SourceRecordId == LandHearingLawToken(law, wording))
                    {
                        var text = "Applicable law at the alleged act: " + TownLawRules.Text(wording.Subject, wording.Rule);
                        if (evidence.Kind != "record" || evidence.Acquisition != "record_inspection" ||
                            evidence.SourceAgentId != evidence.SubmittedByAgentId || evidence.SourceVersion != TownLandHearingRules.LawVersion(wording) ||
                            evidence.ObservedTick != file.Allegation.ConductTick || evidence.Text != BoundedText.Excerpt(text, 256) ||
                            !file.Reads.Any(read => read.AgentId == evidence.SubmittedByAgentId && read.ReadTick == evidence.SubmittedTick &&
                                read.EvidenceIds.Contains(evidence.Id, StringComparer.Ordinal)))
                            throw new InvalidDataException("A cited law record requires its exact historical wording and actual public file inspection.");
                        continue;
                    }
                    if (!acquisitions.TryGetValue(evidence.Id, out var acquired) || acquired.AgentId != evidence.SubmittedByAgentId ||
                        acquired.Tick > evidence.SubmittedTick || evidence.ObservedTick != acquired.Tick || evidence.Text != acquired.Text ||
                        !records.TryGetValue(acquired.ConductId, out var record) || evidence.SourceRecordId != record.Id ||
                        evidence.SourceVersion != record.Version || evidence.SourceAgentId != (acquired.SourceAgentId ?? acquired.AgentId) ||
                        evidence.Kind != (acquired.Kind == "firsthand" ? "observation" : acquired.Kind == "record_inspection" ? "record" : "allegation") ||
                        evidence.Acquisition != acquired.Kind)
                        throw new InvalidDataException("Case evidence cannot invent or upgrade a person's actual source knowledge.");
                }
            }
            foreach (var proposal in town.Council?.Proposals.Where(item => item.NonLandRequest is not null) ?? [])
            {
                var allegation = proposal.NonLandRequest!.Allegation;
                ValidatePhysicalAllegation(allegation, records);
                ValidatePhysicalAcquisitions(allegation.IncidentId, proposal.AuthorId, proposal.OpenedTick,
                    allegation.SourceEvidenceIds, acquisitions);
            }
            foreach (var receipt in state.NativeReceipts)
            {
                if (receipt is null || !IdentityRules.Reference(receipt.Id, DerivedId.MaximumLength, requireTrimmed: true) || !nativeReceipts.Add(receipt.Id) ||
                    receipt.Kind is not ("return_goods" or "repair_equipment" or "public_service_goods") ||
                    !agents.Contains(receipt.ActorId) || receipt.Tick < 0 || receipt.Tick > tick || receipt.Quantity <= 0 ||
                    !IdentityRules.Reference(receipt.ItemKind, int.MaxValue, requireTrimmed: true) || !IdentityRules.Reference(receipt.SourceLotId, int.MaxValue, requireTrimmed: true) ||
                    !IdentityRules.Reference(receipt.ResultLotId, int.MaxValue, requireTrimmed: true) || !map.Contains(receipt.Position) ||
                    receipt.ActorHouseholdId is not null && !households.Contains(receipt.ActorHouseholdId) ||
                    receipt.ActorTownId is not null && !townIds.Contains(receipt.ActorTownId) ||
                    receipt.SourceQuantityBefore <= 0 || receipt.AvailableQuantityBefore <= 0 ||
                    receipt.AvailableQuantityBefore > receipt.SourceQuantityBefore || receipt.RepairInputs is null ||
                    receipt.BeforeCondition is < 0 or > 10_000 || receipt.AfterCondition is < 0 or > 10_000 ||
                    receipt.Version != NonLandDigest(receipt with { Version = "" }) ||
                    receipt.Kind == "repair_equipment" && (receipt.Quantity != 1 || receipt.TargetId != receipt.SourceLotId ||
                        receipt.BeforeCondition >= receipt.AfterCondition || receipt.ResultOwnerId != receipt.PreviousOwnerId ||
                        receipt.BeneficiaryId != receipt.PreviousOwnerId ||
                        receipt.PreviousOwnerId != receipt.ActorId && receipt.PreviousOwnerId != receipt.ActorHouseholdId) ||
                    receipt.Kind != "repair_equipment" && (receipt.PreviousOwnerId != receipt.ActorId ||
                        receipt.ResultOwnerId != receipt.BeneficiaryId || receipt.BeforeCondition != receipt.AfterCondition ||
                        receipt.Quantity > receipt.AvailableQuantityBefore || receipt.RepairInputs.Count != 0 ||
                        receipt.ResultLotId != (receipt.Quantity == receipt.SourceQuantityBefore ? receipt.SourceLotId :
                            DerivedId.Create(receipt.SourceLotId + "#transfer:" + receipt.Id))) ||
                    receipt.Kind == "return_goods" && (!agents.Contains(receipt.BeneficiaryId ?? "") ||
                        receipt.BeneficiaryId == receipt.ActorId || receipt.TargetId != receipt.SourceLotId) ||
                    receipt.Kind == "public_service_goods" && (receipt.BeneficiaryId != town.Id || receipt.ActorTownId != town.Id ||
                        !WorldRuntime.CivicSharedRules.WarehouseResourceKinds.Contains(receipt.ItemKind) || receipt.Quantity > WorldRuntime.CivicSharedRules.WarehouseLoadQuantity ||
                        receipt.CarriedAvailableQuantityBefore is not { } carriedAvailable ||
                        carriedAvailable < receipt.AvailableQuantityBefore || carriedAvailable - receipt.Quantity < WorldRuntime.CivicSharedRules.WarehouseLoadQuantity ||
                        !IdentityRules.Reference(receipt.TargetId, int.MaxValue, requireTrimmed: true)) ||
                    receipt.Kind != "public_service_goods" && receipt.CarriedAvailableQuantityBefore is not null)
                    throw new InvalidDataException("A saved native remedy receipt is inconsistent with a supported physical effect.");
                if (receipt.Kind == "repair_equipment")
                {
                    var materials = NonLandRepairMaterials(receipt.ItemKind);
                    if (materials.Count == 0 || receipt.RepairInputs.Count != materials.Count ||
                        receipt.RepairInputs.Any(input => input is null || input.Reservation is null) ||
                        receipt.RepairInputs.Select(input => (string?)input.Reservation.Id).Distinct(StringComparer.Ordinal).Count() != receipt.RepairInputs.Count ||
                        receipt.RepairInputs.Any(input => input.Reservation.State != InventoryReservationState.Completed ||
                            !consumedRepairInputs.Add(input.Reservation.Id) ||
                            input.Reservation.Purpose != "equipment_repair" || input.Reservation.OwnerId != receipt.PreviousOwnerId ||
                            input.Reservation.ExpiryTick < receipt.Tick ||
                            !society.Inventory.Reservations.Contains(input.Reservation) ||
                            !materials.Any(material => material.ResourceId == input.ItemKind && material.Amount == input.Reservation.Quantity)) ||
                        materials.Any(material => receipt.RepairInputs.Count(input => input.ItemKind == material.ResourceId &&
                            input.Reservation.Quantity == material.Amount) != 1))
                        throw new InvalidDataException("A completed repair must retain its exact consumed native material reservations.");
                }
                var effects = state.Effects.Where(effect => effect.NativeReceiptId == receipt.Id).ToArray();
                if (effects.Length != 1) throw new InvalidDataException("Each retained native receipt must back exactly one remedy effect.");
            }
            foreach (var effect in state.Effects)
            {
                var receipt = state.NativeReceipts.SingleOrDefault(item => item.Id == effect.NativeReceiptId);
                if (!creditedReceipts.Add(effect.NativeReceiptId) || receipt is null || receipt.Version != effect.NativeReceiptVersion ||
                    receipt.ActorId != effect.ActorId || receipt.Tick != effect.Tick || receipt.Kind != effect.Kind ||
                    receipt.BeneficiaryId != effect.BeneficiaryId || receipt.ItemKind != effect.ItemKind ||
                    effect.Quantity > receipt.Quantity || effect.TargetId is not null && effect.TargetId != receipt.TargetId)
                    throw new InvalidDataException("A remedy effect needs one matching, unspent native completion receipt.");
            }
        }
    }

    internal static void ValidatePhysicalAllegation(TownViolationAllegation allegation,
        Dictionary<string, TownConductRecord> records)
    {
        if (!records.TryGetValue(allegation.IncidentId, out var record) || record.ActorId != allegation.SubjectId ||
            record.ConductKind != allegation.ConductKind || record.Position != allegation.Position || record.Tick != allegation.ConductTick ||
            !record.Laws.Any(law => law.LawId == allegation.LawId && law.Version == allegation.LawVersion))
            throw new InvalidDataException("A reported act must retain its native identity and conduct-time law scope.");
    }

    internal static void ValidatePhysicalAcquisitions(string conductId, string actor, long tick,
        IReadOnlyList<string> evidenceIds, Dictionary<string, TownConductAcquisition> acquisitions)
    {
        if (evidenceIds.Count == 0 || evidenceIds.Any(id => !acquisitions.TryGetValue(id, out var item) ||
                item.ConductId != conductId || item.AgentId != actor || item.Tick > tick))
            throw new InvalidDataException("A report requires the reporter's actual knowledge before filing.");
    }
}
