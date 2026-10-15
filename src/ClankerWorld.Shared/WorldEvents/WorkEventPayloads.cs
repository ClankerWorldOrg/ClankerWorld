using System.Text.Json;
using System.Text.Json.Serialization;

namespace ClankerWorld.Simulation.World;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record BuildCompletedEventPayload(string? ActorId, [property: JsonRequired] string SubjectId, [property: JsonRequired] string DefinitionId, [property: JsonRequired] string? BuildingId, [property: JsonRequired] int? SiteX, [property: JsonRequired] int? SiteY, [property: JsonRequired] string? HouseholdId) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string Kind => "build_completed";
    [JsonIgnore] public override string DiagnosticDetail => $"{SubjectId}:{DefinitionId}" + (SiteX is null ? (BuildingId is null ? "" : $":{BuildingId}") : $":{SiteX},{SiteY}") + (HouseholdId is null ? "" : $":household={HouseholdId}");
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => new string?[] { SubjectId, DefinitionId, BuildingId, HouseholdId }.Where(id => !string.IsNullOrEmpty(id)).Select(id => id!).ToArray();
    [JsonIgnore] public override bool FieldsAreValid => SubjectId is not null && DefinitionId is not null;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record BuildRejectedEventPayload(string? ActorId, [property: JsonRequired] string CandidateId, [property: JsonRequired] string? Reason) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string Kind => "build_rejected";
    [JsonIgnore] public override string DiagnosticDetail => $"{ActorId}:{CandidateId}:{Reason}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => new string?[] { CandidateId }.Where(id => !string.IsNullOrEmpty(id)).Select(id => id!).ToArray();
    [JsonIgnore] public override bool FieldsAreValid => CandidateId is not null;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record BuildStartedEventPayload(string? ActorId, [property: JsonRequired] string SubjectId, [property: JsonRequired] string DefinitionId, [property: JsonRequired] string? BuildingId, [property: JsonRequired] int? SiteX, [property: JsonRequired] int? SiteY, [property: JsonRequired] string? HouseholdId) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string Kind => "build_started";
    [JsonIgnore] public override string DiagnosticDetail => $"{SubjectId}:{DefinitionId}" + (SiteX is null ? (BuildingId is null ? "" : $":{BuildingId}") : $":{SiteX},{SiteY}") + (HouseholdId is null ? "" : $":household={HouseholdId}");
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => new string?[] { SubjectId, DefinitionId, BuildingId, HouseholdId }.Where(id => !string.IsNullOrEmpty(id)).Select(id => id!).ToArray();
    [JsonIgnore] public override bool FieldsAreValid => SubjectId is not null && DefinitionId is not null;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record BuildingExpandedEventPayload(string? ActorId, [property: JsonRequired] string BuildingId, [property: JsonRequired] string JobId) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string Kind => "building_expanded";
    [JsonIgnore] public override string DiagnosticDetail => $"{BuildingId}:{JobId}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => new string?[] { BuildingId, JobId }.Where(id => !string.IsNullOrEmpty(id)).Select(id => id!).ToArray();
    [JsonIgnore] public override bool FieldsAreValid => BuildingId is not null && JobId is not null;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record BuildingExpansionCancelledEventPayload(string? ActorId, [property: JsonRequired] string BuildingId, [property: JsonRequired] string JobId, [property: JsonRequired] string? Reason) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string Kind => "building_expansion_cancelled";
    [JsonIgnore] public override string DiagnosticDetail => Reason is null ? $"{BuildingId}:{JobId}" : $"{BuildingId}:{JobId}:{Reason}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => new string?[] { BuildingId, JobId }.Where(id => !string.IsNullOrEmpty(id)).Select(id => id!).ToArray();
    [JsonIgnore] public override bool FieldsAreValid => BuildingId is not null && JobId is not null;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record BuildingExpansionMaterialDeliveredEventPayload(string? ActorId, [property: JsonRequired] string ItemKind, [property: JsonRequired] int Quantity, [property: JsonRequired] string BuildingId, [property: JsonRequired] string Location) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string Kind => "building_expansion_material_delivered";
    [JsonIgnore] public override string DiagnosticDetail => $"{ActorId}:{ItemKind}:{Quantity}:{BuildingId}:{Location}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => new string?[] { BuildingId }.Where(id => !string.IsNullOrEmpty(id)).Select(id => id!).ToArray();
    [JsonIgnore] public override bool FieldsAreValid => ItemKind is not null && BuildingId is not null && Location is not null;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record BuildingExpansionMaterialPickedUpEventPayload(string? ActorId, [property: JsonRequired] string SharedId, [property: JsonRequired] int Quantity, [property: JsonRequired] string BuildingInstanceId) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string Kind => "building_expansion_material_picked_up";
    [JsonIgnore] public override string DiagnosticDetail => $"{ActorId}:{SharedId}:{Quantity}:{BuildingInstanceId}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => new string?[] { SharedId, BuildingInstanceId }.Where(id => !string.IsNullOrEmpty(id)).Select(id => id!).ToArray();
    [JsonIgnore] public override bool FieldsAreValid => SharedId is not null && BuildingInstanceId is not null;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record BuildingExpansionStartedEventPayload(string? ActorId, [property: JsonRequired] string BuildingId, [property: JsonRequired] string JobId) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string Kind => "building_expansion_started";
    [JsonIgnore] public override string DiagnosticDetail => $"{BuildingId}:{JobId}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => new string?[] { BuildingId, JobId }.Where(id => !string.IsNullOrEmpty(id)).Select(id => id!).ToArray();
    [JsonIgnore] public override bool FieldsAreValid => BuildingId is not null && JobId is not null;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record BuildingPlacedEventPayload(string? ActorId, [property: JsonRequired] string SubjectId, [property: JsonRequired] string DefinitionId, [property: JsonRequired] string? BuildingId, [property: JsonRequired] int? SiteX, [property: JsonRequired] int? SiteY, [property: JsonRequired] string? HouseholdId) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string Kind => "building_placed";
    [JsonIgnore] public override string DiagnosticDetail => $"{SubjectId}:{DefinitionId}" + (SiteX is null ? (BuildingId is null ? "" : $":{BuildingId}") : $":{SiteX},{SiteY}") + (HouseholdId is null ? "" : $":household={HouseholdId}");
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => new string?[] { SubjectId, DefinitionId, BuildingId, HouseholdId }.Where(id => !string.IsNullOrEmpty(id)).Select(id => id!).ToArray();
    [JsonIgnore] public override bool FieldsAreValid => SubjectId is not null && DefinitionId is not null;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record BuildingReassignedEventPayload(string? ActorId, [property: JsonRequired] string InstanceId, [property: JsonRequired] string NextTownId, [property: JsonRequired] string NextHouseholdId) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string Kind => "building_reassigned";
    [JsonIgnore] public override string DiagnosticDetail => $"{InstanceId}:town={NextTownId}:household={NextHouseholdId}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => new string?[] { InstanceId, NextTownId, NextHouseholdId }.Where(id => !string.IsNullOrEmpty(id)).Select(id => id!).ToArray();
    [JsonIgnore] public override bool FieldsAreValid => InstanceId is not null && NextTownId is not null && NextHouseholdId is not null;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record BuildingRemovedEventPayload(string? ActorId, [property: JsonRequired] string InstanceId, [property: JsonRequired] string DefinitionId) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string Kind => "building_removed";
    [JsonIgnore] public override string DiagnosticDetail => $"{InstanceId}:{DefinitionId}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => new string?[] { InstanceId, DefinitionId }.Where(id => !string.IsNullOrEmpty(id)).Select(id => id!).ToArray();
    [JsonIgnore] public override bool FieldsAreValid => InstanceId is not null && DefinitionId is not null;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record BuildingVariantContentStagedEventPayload(string? ActorId, [property: JsonRequired] string PackageId) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string Kind => "building_variant_content_staged";
    [JsonIgnore] public override string DiagnosticDetail => $"{PackageId}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => new string?[] { PackageId }.Where(id => !string.IsNullOrEmpty(id)).Select(id => id!).ToArray();
    [JsonIgnore] public override bool FieldsAreValid => PackageId is not null;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record EquipmentCollectedEventPayload(string? ActorId, [property: JsonRequired] string ItemKind) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string Kind => "equipment_collected";
    [JsonIgnore] public override string DiagnosticDetail => $"{ActorId}:{ItemKind}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => [];
    [JsonIgnore] public override bool FieldsAreValid => ItemKind is not null;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record EquipmentEquippedEventPayload(string? ActorId, [property: JsonRequired] string EquippedId, [property: JsonRequired] string ItemKind) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string Kind => "equipment_equipped";
    [JsonIgnore] public override string DiagnosticDetail => $"{ActorId}|{EquippedId}|{ItemKind}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => new string?[] { EquippedId }.Where(id => !string.IsNullOrEmpty(id)).Select(id => id!).ToArray();
    [JsonIgnore] public override bool FieldsAreValid => EquippedId is not null && ItemKind is not null;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record EquipmentRepairInterruptedEventPayload(string? ActorId, [property: JsonRequired] string LotId) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string Kind => "equipment_repair_interrupted";
    [JsonIgnore] public override string DiagnosticDetail => $"{ActorId}|{LotId}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => new string?[] { LotId }.Where(id => !string.IsNullOrEmpty(id)).Select(id => id!).ToArray();
    [JsonIgnore] public override bool FieldsAreValid => LotId is not null;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record EquipmentRepairStartedEventPayload(string? ActorId, [property: JsonRequired] string TargetId, [property: JsonRequired] string SiteInstanceId) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string Kind => "equipment_repair_started";
    [JsonIgnore] public override string DiagnosticDetail => $"{ActorId}|{TargetId}|{SiteInstanceId}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => new string?[] { TargetId, SiteInstanceId }.Where(id => !string.IsNullOrEmpty(id)).Select(id => id!).ToArray();
    [JsonIgnore] public override bool FieldsAreValid => TargetId is not null && SiteInstanceId is not null;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record EquipmentRepairedEventPayload(string? ActorId, [property: JsonRequired] string LotId) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string Kind => "equipment_repaired";
    [JsonIgnore] public override string DiagnosticDetail => $"{ActorId}|{LotId}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => new string?[] { LotId }.Where(id => !string.IsNullOrEmpty(id)).Select(id => id!).ToArray();
    [JsonIgnore] public override bool FieldsAreValid => LotId is not null;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record FarmContentStagedEventPayload(string? ActorId, [property: JsonRequired] string PackageId) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string Kind => "farm_content_staged";
    [JsonIgnore] public override string DiagnosticDetail => $"{PackageId}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => new string?[] { PackageId }.Where(id => !string.IsNullOrEmpty(id)).Select(id => id!).ToArray();
    [JsonIgnore] public override bool FieldsAreValid => PackageId is not null;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record FarmFlourPickedUpEventPayload(string? ActorId, [property: JsonRequired] string ResourceId, [property: JsonRequired] int Quantity, [property: JsonRequired] string DestinationId) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string Kind => "farm_flour_picked_up";
    [JsonIgnore] public override string DiagnosticDetail => $"{ActorId}:{ResourceId}:{Quantity}:{DestinationId}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => new string?[] { ResourceId, DestinationId }.Where(id => !string.IsNullOrEmpty(id)).Select(id => id!).ToArray();
    [JsonIgnore] public override bool FieldsAreValid => ResourceId is not null && DestinationId is not null;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record FarmGrainPickedUpEventPayload(string? ActorId, [property: JsonRequired] string ResourceId, [property: JsonRequired] int Quantity, [property: JsonRequired] string DestinationId) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string Kind => "farm_grain_picked_up";
    [JsonIgnore] public override string DiagnosticDetail => $"{ActorId}:{ResourceId}:{Quantity}:{DestinationId}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => new string?[] { ResourceId, DestinationId }.Where(id => !string.IsNullOrEmpty(id)).Select(id => id!).ToArray();
    [JsonIgnore] public override bool FieldsAreValid => ResourceId is not null && DestinationId is not null;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record InstructionAppliedEventPayload(string? ActorId, [property: JsonRequired] string InstructionId, [property: JsonRequired] string Action) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string Kind => "instruction_applied";
    [JsonIgnore] public override string DiagnosticDetail => $"{InstructionId}:{Action}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => new string?[] { InstructionId }.Where(id => !string.IsNullOrEmpty(id)).Select(id => id!).ToArray();
    [JsonIgnore] public override bool FieldsAreValid => InstructionId is not null && Action is not null;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record InstructionNotUnderstoodEventPayload(string? ActorId, [property: JsonRequired] string InstructionId) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string Kind => "instruction_not_understood";
    [JsonIgnore] public override string DiagnosticDetail => $"{ActorId}:{InstructionId}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => new string?[] { InstructionId }.Where(id => !string.IsNullOrEmpty(id)).Select(id => id!).ToArray();
    [JsonIgnore] public override bool FieldsAreValid => InstructionId is not null;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record InstructionOrderCancelledEventPayload(string? ActorId, [property: JsonRequired] string InstructionId, [property: JsonRequired] string Cause) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string Kind => "instruction_order_cancelled";
    [JsonIgnore] public override string DiagnosticDetail => $"{ActorId}:{InstructionId}:{Cause}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => new string?[] { InstructionId }.Where(id => !string.IsNullOrEmpty(id)).Select(id => id!).ToArray();
    [JsonIgnore] public override bool FieldsAreValid => InstructionId is not null && Cause is not null;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record InstructionOrderFinishedEventPayload(string? ActorId, [property: JsonRequired] string InstructionId, [property: JsonRequired] string EffectId) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string Kind => "instruction_order_finished";
    [JsonIgnore] public override string DiagnosticDetail => $"{ActorId}:{InstructionId}:{EffectId}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => new string?[] { InstructionId, EffectId }.Where(id => !string.IsNullOrEmpty(id)).Select(id => id!).ToArray();
    [JsonIgnore] public override bool FieldsAreValid => InstructionId is not null && EffectId is not null;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record InstructionOrderProgressEventPayload(string? ActorId, [property: JsonRequired] string InstructionId, [property: JsonRequired] int Completed) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string Kind => "instruction_order_progress";
    [JsonIgnore] public override string DiagnosticDetail => $"{ActorId}:{InstructionId}:{Completed}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => new string?[] { InstructionId }.Where(id => !string.IsNullOrEmpty(id)).Select(id => id!).ToArray();
    [JsonIgnore] public override bool FieldsAreValid => InstructionId is not null;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record InstructionOrderStaleDecisionEventPayload(string? ActorId) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string Kind => "instruction_order_stale_decision";
    [JsonIgnore] public override string DiagnosticDetail => $"{ActorId}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => [];
    [JsonIgnore] public override bool FieldsAreValid => true;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record InstructionOrderStatusEventPayload(string? ActorId, [property: JsonRequired] string InstructionId, [property: JsonRequired] string Status) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string Kind => "instruction_order_status";
    [JsonIgnore] public override string DiagnosticDetail => $"{ActorId}:{InstructionId}:{Status}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => new string?[] { InstructionId }.Where(id => !string.IsNullOrEmpty(id)).Select(id => id!).ToArray();
    [JsonIgnore] public override bool FieldsAreValid => InstructionId is not null && Status is not null;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record InstructionQueuedEventPayload(string? ActorId, [property: JsonRequired] string InstructionId, [property: JsonRequired] string InstructionKind) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string Kind => "instruction_queued";
    [JsonIgnore] public override string DiagnosticDetail => $"{InstructionId}:{InstructionKind}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => new string?[] { InstructionId }.Where(id => !string.IsNullOrEmpty(id)).Select(id => id!).ToArray();
    [JsonIgnore] public override bool FieldsAreValid => InstructionId is not null && InstructionKind is not null;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record OwnerStockDeliveredEventPayload(string? ActorId, [property: JsonRequired] string SourceLotId, [property: JsonRequired] int Quantity, [property: JsonRequired] string DestinationId) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string Kind => "owner_stock_delivered";
    [JsonIgnore] public override string DiagnosticDetail => $"{ActorId}:{SourceLotId}:{Quantity}:{DestinationId}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => new string?[] { SourceLotId, DestinationId }.Where(id => !string.IsNullOrEmpty(id)).Select(id => id!).ToArray();
    [JsonIgnore] public override bool FieldsAreValid => SourceLotId is not null && DestinationId is not null;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record OwnerStockPickedUpEventPayload(string? ActorId, [property: JsonRequired] string SourceLotId, [property: JsonRequired] int Quantity, [property: JsonRequired] string DestinationId) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string Kind => "owner_stock_picked_up";
    [JsonIgnore] public override string DiagnosticDetail => $"{ActorId}:{SourceLotId}:{Quantity}:{DestinationId}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => new string?[] { SourceLotId, DestinationId }.Where(id => !string.IsNullOrEmpty(id)).Select(id => id!).ToArray();
    [JsonIgnore] public override bool FieldsAreValid => SourceLotId is not null && DestinationId is not null;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ProductionActionRefusedEventPayload(string? ActorId, [property: JsonRequired] string JobId) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string Kind => "production_action_refused";
    [JsonIgnore] public override string DiagnosticDetail => $"{JobId}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => new string?[] { JobId }.Where(id => !string.IsNullOrEmpty(id)).Select(id => id!).ToArray();
    [JsonIgnore] public override bool FieldsAreValid => JobId is not null;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ProductionInputUnusableEventPayload(string? ActorId, [property: JsonRequired] string JobId) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string Kind => "production_input_unusable";
    [JsonIgnore] public override string DiagnosticDetail => $"{JobId}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => new string?[] { JobId }.Where(id => !string.IsNullOrEmpty(id)).Select(id => id!).ToArray();
    [JsonIgnore] public override bool FieldsAreValid => JobId is not null;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ProductionOrderPausedEventPayload(string? ActorId, [property: JsonRequired] string InstructionId, [property: JsonRequired] string JobId) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string Kind => "production_order_paused";
    [JsonIgnore] public override string DiagnosticDetail => $"{InstructionId}:{JobId}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => new string?[] { InstructionId, JobId }.Where(id => !string.IsNullOrEmpty(id)).Select(id => id!).ToArray();
    [JsonIgnore] public override bool FieldsAreValid => InstructionId is not null && JobId is not null;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ProductionOrderResumedEventPayload(string? ActorId, [property: JsonRequired] string InstructionId, [property: JsonRequired] string JobId) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string Kind => "production_order_resumed";
    [JsonIgnore] public override string DiagnosticDetail => $"{InstructionId}:{JobId}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => new string?[] { InstructionId, JobId }.Where(id => !string.IsNullOrEmpty(id)).Select(id => id!).ToArray();
    [JsonIgnore] public override bool FieldsAreValid => InstructionId is not null && JobId is not null;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ProductionOutputUnsupportedEventPayload(string? ActorId, [property: JsonRequired] string JobId) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string Kind => "production_output_unsupported";
    [JsonIgnore] public override string DiagnosticDetail => $"{JobId}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => new string?[] { JobId }.Where(id => !string.IsNullOrEmpty(id)).Select(id => id!).ToArray();
    [JsonIgnore] public override bool FieldsAreValid => JobId is not null;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ProductionToolUnusableEventPayload(string? ActorId, [property: JsonRequired] string JobId) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string Kind => "production_tool_unusable";
    [JsonIgnore] public override string DiagnosticDetail => $"{JobId}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => new string?[] { JobId }.Where(id => !string.IsNullOrEmpty(id)).Select(id => id!).ToArray();
    [JsonIgnore] public override bool FieldsAreValid => JobId is not null;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ProductionWorkerUnavailableEventPayload(string? ActorId, [property: JsonRequired] string JobId) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string Kind => "production_worker_unavailable";
    [JsonIgnore] public override string DiagnosticDetail => $"{JobId}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => new string?[] { JobId }.Where(id => !string.IsNullOrEmpty(id)).Select(id => id!).ToArray();
    [JsonIgnore] public override bool FieldsAreValid => JobId is not null;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ProjectChosenEventPayload(string? ActorId, [property: JsonRequired] string CandidateId) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string Kind => "project_chosen";
    [JsonIgnore] public override string DiagnosticDetail => $"{ActorId}:{CandidateId}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => new string?[] { CandidateId }.Where(id => !string.IsNullOrEmpty(id)).Select(id => id!).ToArray();
    [JsonIgnore] public override bool FieldsAreValid => CandidateId is not null;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ProjectFreshChoiceResumedEventPayload(string? ActorId, [property: JsonRequired] string CandidateId) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string Kind => "project_fresh_choice_resumed";
    [JsonIgnore] public override string DiagnosticDetail => $"{ActorId}:{CandidateId}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => new string?[] { CandidateId }.Where(id => !string.IsNullOrEmpty(id)).Select(id => id!).ToArray();
    [JsonIgnore] public override bool FieldsAreValid => CandidateId is not null;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ProjectMaterialDeliveredEventPayload(string? ActorId, [property: JsonRequired] string ResourceId) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string Kind => "project_material_delivered";
    [JsonIgnore] public override string DiagnosticDetail => $"{ActorId}:{ResourceId}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => new string?[] { ResourceId }.Where(id => !string.IsNullOrEmpty(id)).Select(id => id!).ToArray();
    [JsonIgnore] public override bool FieldsAreValid => ResourceId is not null;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ProjectProgressEventPayload(string? ActorId, [property: JsonRequired] string Stage, [property: JsonRequired] string Blocker) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string Kind => "project_progress";
    [JsonIgnore] public override string DiagnosticDetail => $"{ActorId}:{Stage}:{Blocker}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => [];
    [JsonIgnore] public override bool FieldsAreValid => Stage is not null && Blocker is not null;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ProjectRequestFulfilledEventPayload(string? ActorId, [property: JsonRequired] string RequesterId, [property: JsonRequired] string ItemKind, [property: JsonRequired] int Quantity) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string Kind => "project_request_fulfilled";
    [JsonIgnore] public override string DiagnosticDetail => $"{ActorId}:{RequesterId}:{ItemKind}:{Quantity}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => new string?[] { RequesterId }.Where(id => !string.IsNullOrEmpty(id)).Select(id => id!).ToArray();
    [JsonIgnore] public override bool FieldsAreValid => RequesterId is not null && ItemKind is not null;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ProjectSiteReselectedEventPayload(string? ActorId, [property: JsonRequired] string DefinitionId) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string Kind => "project_site_reselected";
    [JsonIgnore] public override string DiagnosticDetail => $"{ActorId}:{DefinitionId}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => new string?[] { DefinitionId }.Where(id => !string.IsNullOrEmpty(id)).Select(id => id!).ToArray();
    [JsonIgnore] public override bool FieldsAreValid => DefinitionId is not null;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record RecipeCancelledEventPayload(string? ActorId, [property: JsonRequired] string JobId, [property: JsonRequired] string RecipeId, [property: JsonRequired] string? BuildingId) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string Kind => "recipe_cancelled";
    [JsonIgnore] public override string DiagnosticDetail => $"{JobId}:{RecipeId}" + (BuildingId is null ? "" : $":{BuildingId}");
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => new string?[] { JobId, RecipeId, BuildingId }.Where(id => !string.IsNullOrEmpty(id)).Select(id => id!).ToArray();
    [JsonIgnore] public override bool FieldsAreValid => JobId is not null && RecipeId is not null;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record RecipeCompletedEventPayload(string? ActorId, [property: JsonRequired] string JobId, [property: JsonRequired] string RecipeId, [property: JsonRequired] string? BuildingId) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string Kind => "recipe_completed";
    [JsonIgnore] public override string DiagnosticDetail => $"{JobId}:{RecipeId}" + (BuildingId is null ? "" : $":{BuildingId}");
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => new string?[] { JobId, RecipeId, BuildingId }.Where(id => !string.IsNullOrEmpty(id)).Select(id => id!).ToArray();
    [JsonIgnore] public override bool FieldsAreValid => JobId is not null && RecipeId is not null;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record RecipeStartedEventPayload(string? ActorId, [property: JsonRequired] string JobId, [property: JsonRequired] string RecipeId, [property: JsonRequired] string? BuildingId) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string Kind => "recipe_started";
    [JsonIgnore] public override string DiagnosticDetail => $"{JobId}:{RecipeId}" + (BuildingId is null ? "" : $":{BuildingId}");
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => new string?[] { JobId, RecipeId, BuildingId }.Where(id => !string.IsNullOrEmpty(id)).Select(id => id!).ToArray();
    [JsonIgnore] public override bool FieldsAreValid => JobId is not null && RecipeId is not null;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ShopTradeCancelledEventPayload(string? ActorId, [property: JsonRequired] string BuyerId, [property: JsonRequired] string OfferId) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string Kind => "shop_trade_cancelled";
    [JsonIgnore] public override string DiagnosticDetail => $"{BuyerId}:{OfferId}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => new string?[] { BuyerId, OfferId }.Where(id => !string.IsNullOrEmpty(id)).Select(id => id!).ToArray();
    [JsonIgnore] public override bool FieldsAreValid => BuyerId is not null && OfferId is not null;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ShopTradeCompletedEventPayload(string? ActorId, [property: JsonRequired] string OfferId) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string Kind => "shop_trade_completed";
    [JsonIgnore] public override string DiagnosticDetail => $"{ActorId}:{OfferId}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => new string?[] { OfferId }.Where(id => !string.IsNullOrEmpty(id)).Select(id => id!).ToArray();
    [JsonIgnore] public override bool FieldsAreValid => OfferId is not null;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ShopTradeOfferedEventPayload(string? ActorId, [property: JsonRequired] string OfferId) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string Kind => "shop_trade_offered";
    [JsonIgnore] public override string DiagnosticDetail => $"{ActorId}:{OfferId}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => new string?[] { OfferId }.Where(id => !string.IsNullOrEmpty(id)).Select(id => id!).ToArray();
    [JsonIgnore] public override bool FieldsAreValid => OfferId is not null;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record SmithInputDeliveredEventPayload(string? ActorId, [property: JsonRequired] string InputId, [property: JsonRequired] int PersonalQuantity, [property: JsonRequired] string BlacksmithInstanceId) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string Kind => "smith_input_delivered";
    [JsonIgnore] public override string DiagnosticDetail => $"{ActorId}:{InputId}:{PersonalQuantity}:{BlacksmithInstanceId}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => new string?[] { InputId, BlacksmithInstanceId }.Where(id => !string.IsNullOrEmpty(id)).Select(id => id!).ToArray();
    [JsonIgnore] public override bool FieldsAreValid => InputId is not null && BlacksmithInstanceId is not null;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record SmithInputPickedUpEventPayload(string? ActorId, [property: JsonRequired] string InputId, [property: JsonRequired] int Quantity, [property: JsonRequired] string BlacksmithInstanceId) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string Kind => "smith_input_picked_up";
    [JsonIgnore] public override string DiagnosticDetail => $"{ActorId}:{InputId}:{Quantity}:{BlacksmithInstanceId}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => new string?[] { InputId, BlacksmithInstanceId }.Where(id => !string.IsNullOrEmpty(id)).Select(id => id!).ToArray();
    [JsonIgnore] public override bool FieldsAreValid => InputId is not null && BlacksmithInstanceId is not null;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record SmithOreDeliveredEventPayload(string? ActorId, [property: JsonRequired] string OreId, [property: JsonRequired] int Quantity, [property: JsonRequired] string BlacksmithInstanceId) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string Kind => "smith_ore_delivered";
    [JsonIgnore] public override string DiagnosticDetail => $"{ActorId}:{OreId}:{Quantity}:{BlacksmithInstanceId}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => new string?[] { OreId, BlacksmithInstanceId }.Where(id => !string.IsNullOrEmpty(id)).Select(id => id!).ToArray();
    [JsonIgnore] public override bool FieldsAreValid => OreId is not null && BlacksmithInstanceId is not null;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record StoreStockCollectedEventPayload(string? ActorId, [property: JsonRequired] string GoodsId, [property: JsonRequired] int Quantity, [property: JsonRequired] string StoreInstanceId) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string Kind => "store_stock_collected";
    [JsonIgnore] public override string DiagnosticDetail => $"{ActorId}:{GoodsId}:{Quantity}:{StoreInstanceId}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => new string?[] { GoodsId, StoreInstanceId }.Where(id => !string.IsNullOrEmpty(id)).Select(id => id!).ToArray();
    [JsonIgnore] public override bool FieldsAreValid => GoodsId is not null && StoreInstanceId is not null;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record StoreStockDeliveredEventPayload(string? ActorId, [property: JsonRequired] string GoodsId, [property: JsonRequired] int Quantity, [property: JsonRequired] string StoreInstanceId) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string Kind => "store_stock_delivered";
    [JsonIgnore] public override string DiagnosticDetail => $"{ActorId}:{GoodsId}:{Quantity}:{StoreInstanceId}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => new string?[] { GoodsId, StoreInstanceId }.Where(id => !string.IsNullOrEmpty(id)).Select(id => id!).ToArray();
    [JsonIgnore] public override bool FieldsAreValid => GoodsId is not null && StoreInstanceId is not null;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ToolRepairedEventPayload(string? ActorId, [property: JsonRequired] string RepairId, [property: JsonRequired] string BlacksmithInstanceId) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string Kind => "tool_repaired";
    [JsonIgnore] public override string DiagnosticDetail => $"{ActorId}:{RepairId}:{BlacksmithInstanceId}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => new string?[] { RepairId, BlacksmithInstanceId }.Where(id => !string.IsNullOrEmpty(id)).Select(id => id!).ToArray();
    [JsonIgnore] public override bool FieldsAreValid => RepairId is not null && BlacksmithInstanceId is not null;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ToolRequestAcceptedEventPayload(string? ActorId, [property: JsonRequired] string RequestId) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string Kind => "tool_request_accepted";
    [JsonIgnore] public override string DiagnosticDetail => $"{ActorId}:{RequestId}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => new string?[] { RequestId }.Where(id => !string.IsNullOrEmpty(id)).Select(id => id!).ToArray();
    [JsonIgnore] public override bool FieldsAreValid => RequestId is not null;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ToolRequestCompletedEventPayload(string? ActorId, [property: JsonRequired] string RequestId) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string Kind => "tool_request_completed";
    [JsonIgnore] public override string DiagnosticDetail => $"{ActorId}:{RequestId}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => new string?[] { RequestId }.Where(id => !string.IsNullOrEmpty(id)).Select(id => id!).ToArray();
    [JsonIgnore] public override bool FieldsAreValid => RequestId is not null;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ToolRequestInterruptedEventPayload(string? ActorId, [property: JsonRequired] string RequestId) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string Kind => "tool_request_interrupted";
    [JsonIgnore] public override string DiagnosticDetail => $"{ActorId}:{RequestId}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => new string?[] { RequestId }.Where(id => !string.IsNullOrEmpty(id)).Select(id => id!).ToArray();
    [JsonIgnore] public override bool FieldsAreValid => RequestId is not null;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ToolRequestPlacedEventPayload(string? ActorId, [property: JsonRequired] string RequestId) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string Kind => "tool_request_placed";
    [JsonIgnore] public override string DiagnosticDetail => $"{ActorId}:{RequestId}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => new string?[] { RequestId }.Where(id => !string.IsNullOrEmpty(id)).Select(id => id!).ToArray();
    [JsonIgnore] public override bool FieldsAreValid => RequestId is not null;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ToolRequestReadyEventPayload(string? ActorId, [property: JsonRequired] string RequestId) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string Kind => "tool_request_ready";
    [JsonIgnore] public override string DiagnosticDetail => $"{ActorId}:{RequestId}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => new string?[] { RequestId }.Where(id => !string.IsNullOrEmpty(id)).Select(id => id!).ToArray();
    [JsonIgnore] public override bool FieldsAreValid => RequestId is not null;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ToolRequestRefusedEventPayload(string? ActorId, [property: JsonRequired] string RequestId) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string Kind => "tool_request_refused";
    [JsonIgnore] public override string DiagnosticDetail => $"{ActorId}:{RequestId}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => new string?[] { RequestId }.Where(id => !string.IsNullOrEmpty(id)).Select(id => id!).ToArray();
    [JsonIgnore] public override bool FieldsAreValid => RequestId is not null;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ToolRequestWithdrawnEventPayload(string? ActorId, [property: JsonRequired] string RequestId) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string Kind => "tool_request_withdrawn";
    [JsonIgnore] public override string DiagnosticDetail => $"{ActorId}:{RequestId}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => new string?[] { RequestId }.Where(id => !string.IsNullOrEmpty(id)).Select(id => id!).ToArray();
    [JsonIgnore] public override bool FieldsAreValid => RequestId is not null;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record WorkstationInputPickedUpEventPayload(string? ActorId, [property: JsonRequired] string StockId, [property: JsonRequired] int Quantity, [property: JsonRequired] string BuildingInstanceId) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string Kind => "workstation_input_picked_up";
    [JsonIgnore] public override string DiagnosticDetail => $"{ActorId}:{StockId}:{Quantity}:{BuildingInstanceId}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => new string?[] { StockId, BuildingInstanceId }.Where(id => !string.IsNullOrEmpty(id)).Select(id => id!).ToArray();
    [JsonIgnore] public override bool FieldsAreValid => StockId is not null && BuildingInstanceId is not null;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record WorkstationSuppliedEventPayload(string? ActorId, [property: JsonRequired] string CarriedId, [property: JsonRequired] int Quantity, [property: JsonRequired] string BuildingInstanceId) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string Kind => "workstation_supplied";
    [JsonIgnore] public override string DiagnosticDetail => $"{ActorId}:{CarriedId}:{Quantity}:{BuildingInstanceId}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => new string?[] { CarriedId, BuildingInstanceId }.Where(id => !string.IsNullOrEmpty(id)).Select(id => id!).ToArray();
    [JsonIgnore] public override bool FieldsAreValid => CarriedId is not null && BuildingInstanceId is not null;
}
