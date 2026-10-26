using System.Text.Json;
using System.Text.Json.Serialization;

namespace ClankerWorld.Simulation.World;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ActionRefusedEventPayload(string? ActorId, [property: JsonRequired] string CandidateId, [property: JsonRequired] long RefusedTick, [property: JsonRequired] long RetryAfterTick, [property: JsonRequired] string Check) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string DiagnosticDetail => JsonSerializer.Serialize(new { AgentId = ActorId, CandidateId, RefusedTick, RetryAfterTick, Check });
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => new string?[] { CandidateId }.Where(id => !string.IsNullOrEmpty(id)).Select(id => id!).ToArray();
    [JsonIgnore] public override bool FieldsAreValid => CandidateId is not null && Check is not null;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record AgeActionRejectedEventPayload(string? ActorId, [property: JsonRequired] string CandidateId) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string DiagnosticDetail => $"{ActorId}:{CandidateId}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => new string?[] { CandidateId }.Where(id => !string.IsNullOrEmpty(id)).Select(id => id!).ToArray();
    [JsonIgnore] public override bool FieldsAreValid => CandidateId is not null;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record AgingOverrideChangedEventPayload(string? ActorId, [property: JsonRequired] int Rate) : ScalarWorldEventPayload(ActorId)
{
    [JsonIgnore] public override string DiagnosticDetail => $"{Rate}";
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record AnimalAddedEventPayload(string? ActorId, [property: JsonRequired] string AnimalSequence) : SubjectlessWorldEventPayload(ActorId)
{
    [JsonIgnore] public override string DiagnosticDetail => $"{ActorId}:animal-{AnimalSequence}";
    [JsonIgnore] public override bool FieldsAreValid => AnimalSequence is not null;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record AnimalBornEventPayload(string? ActorId, [property: JsonRequired] string ChildId, [property: JsonRequired] string AnimalId) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string DiagnosticDetail => $"{ChildId}:{AnimalId}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => new string?[] { ChildId, AnimalId }.Where(id => !string.IsNullOrEmpty(id)).Select(id => id!).ToArray();
    [JsonIgnore] public override bool FieldsAreValid => ChildId is not null && AnimalId is not null;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record AnimalBreedingStartedEventPayload(string? ActorId, [property: JsonRequired] string AnimalId, [property: JsonRequired] string MaleId) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string DiagnosticDetail => $"{AnimalId}:{MaleId}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => new string?[] { AnimalId, MaleId }.Where(id => !string.IsNullOrEmpty(id)).Select(id => id!).ToArray();
    [JsonIgnore] public override bool FieldsAreValid => AnimalId is not null && MaleId is not null;
}

/// <summary>Animal actions with the same actor and animal references.</summary>
public abstract record AnimalActionWorldEventPayload(string? ActorId, [property: JsonRequired] string AnimalId) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string DiagnosticDetail => $"{ActorId}:{AnimalId}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => new string?[] { AnimalId }.Where(id => !string.IsNullOrEmpty(id)).Select(id => id!).ToArray();
    [JsonIgnore] public override bool FieldsAreValid => AnimalId is not null;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record AnimalCaredEventPayload(string? ActorId, string AnimalId) : AnimalActionWorldEventPayload(ActorId, AnimalId);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record AnimalDiedEventPayload(string? ActorId, [property: JsonRequired] string AnimalId) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string DiagnosticDetail => $"{AnimalId}:old_age";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => new string?[] { AnimalId }.Where(id => !string.IsNullOrEmpty(id)).Select(id => id!).ToArray();
    [JsonIgnore] public override bool FieldsAreValid => AnimalId is not null;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record AnimalHideCollectedEventPayload(string? ActorId, string AnimalId) : AnimalActionWorldEventPayload(ActorId, AnimalId);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record AnimalPermissionChangedEventPayload(string? ActorId, [property: JsonRequired] string AnimalId, [property: JsonRequired] string Action, [property: JsonRequired] string OtherId) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string DiagnosticDetail => $"{ActorId}:{AnimalId}:{Action}:{OtherId}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => new string?[] { AnimalId, OtherId }.Where(id => !string.IsNullOrEmpty(id)).Select(id => id!).ToArray();
    [JsonIgnore] public override bool FieldsAreValid => AnimalId is not null && Action is not null && OtherId is not null;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record AnimalProductCollectedEventPayload(string? ActorId, [property: JsonRequired] string AnimalId, [property: JsonRequired] string ItemKind, [property: JsonRequired] int Quantity) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string DiagnosticDetail => $"{ActorId}:{AnimalId}:{ItemKind}:{Quantity}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => new string?[] { AnimalId }.Where(id => !string.IsNullOrEmpty(id)).Select(id => id!).ToArray();
    [JsonIgnore] public override bool FieldsAreValid => AnimalId is not null && ItemKind is not null;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record AnimalProductReadyEventPayload(string? ActorId, [property: JsonRequired] string AnimalId, [property: JsonRequired] string? Product) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string DiagnosticDetail => $"{AnimalId}:{Product}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => new string?[] { AnimalId }.Where(id => !string.IsNullOrEmpty(id)).Select(id => id!).ToArray();
    [JsonIgnore] public override bool FieldsAreValid => AnimalId is not null;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record AnimalSaddledEventPayload(string? ActorId, string AnimalId) : AnimalActionWorldEventPayload(ActorId, AnimalId);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record AnimalTamedEventPayload(string? ActorId, string AnimalId) : AnimalActionWorldEventPayload(ActorId, AnimalId);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record AnimalTradeOfferedEventPayload(string? ActorId, [property: JsonRequired] string OfferId, [property: JsonRequired] string AnimalId, [property: JsonRequired] string PaymentId) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string DiagnosticDetail => $"{OfferId}:{AnimalId}:{PaymentId}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => new string?[] { OfferId, AnimalId, PaymentId }.Where(id => !string.IsNullOrEmpty(id)).Select(id => id!).ToArray();
    [JsonIgnore] public override bool FieldsAreValid => OfferId is not null && AnimalId is not null && PaymentId is not null;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record AnimalTransferredEventPayload(string? ActorId, [property: JsonRequired] string OfferId, [property: JsonRequired] string AnimalId, [property: JsonRequired] string ReceivingHouseholdId) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string DiagnosticDetail => $"{OfferId}:{AnimalId}:{ReceivingHouseholdId}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => new string?[] { OfferId, AnimalId, ReceivingHouseholdId }.Where(id => !string.IsNullOrEmpty(id)).Select(id => id!).ToArray();
    [JsonIgnore] public override bool FieldsAreValid => OfferId is not null && AnimalId is not null && ReceivingHouseholdId is not null;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record AnimalYardSuppliedEventPayload(string? ActorId, [property: JsonRequired] string YardInstanceId) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string DiagnosticDetail => $"{ActorId}:{YardInstanceId}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => new string?[] { YardInstanceId }.Where(id => !string.IsNullOrEmpty(id)).Select(id => id!).ToArray();
    [JsonIgnore] public override bool FieldsAreValid => YardInstanceId is not null;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record AnimalsArrivedEventPayload(string? ActorId, [property: JsonRequired] int AnimalCount) : ScalarWorldEventPayload(ActorId)
{
    [JsonIgnore] public override string DiagnosticDetail => $"wild:{AnimalCount}";
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record BarterCancelledEventPayload(string? ActorId) : ActorOnlyWorldEventPayload(ActorId);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record BarterCompletedEventPayload(string? ActorId) : ActorOnlyWorldEventPayload(ActorId);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record BarterDeclinedEventPayload(string? ActorId) : ActorOnlyWorldEventPayload(ActorId);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record BarterOfferedEventPayload(string? ActorId, [property: JsonRequired] string OtherAgentId) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string DiagnosticDetail => $"{ActorId}:{OtherAgentId}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => new string?[] { OtherAgentId }.Where(id => !string.IsNullOrEmpty(id)).Select(id => id!).ToArray();
    [JsonIgnore] public override bool FieldsAreValid => OtherAgentId is not null;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record BlacksmithContentStagedEventPayload(string? ActorId, string PackageId) : FilteredPackageWorldEventPayload(ActorId, PackageId);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record BoatActionBlockedEventPayload(string? ActorId) : ScalarWorldEventPayload(ActorId)
{
    [JsonIgnore] public override string DiagnosticDetail => $"{ActorId}:stay_aboard";
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record BoatArrivedEventPayload(string? ActorId, [property: JsonRequired] string BoatId, [property: JsonRequired] string PortId) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string DiagnosticDetail => $"{ActorId}:{BoatId}:{PortId}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => new string?[] { BoatId, PortId }.Where(id => !string.IsNullOrEmpty(id)).Select(id => id!).ToArray();
    [JsonIgnore] public override bool FieldsAreValid => BoatId is not null && PortId is not null;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record BoatDepartedEventPayload(string? ActorId, [property: JsonRequired] string BoatId, [property: JsonRequired] string OriginInstanceId, [property: JsonRequired] string DestinationInstanceId) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string DiagnosticDetail => $"{ActorId}:{BoatId}:{OriginInstanceId}:{DestinationInstanceId}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => new string?[] { BoatId, OriginInstanceId, DestinationInstanceId }.Where(id => !string.IsNullOrEmpty(id)).Select(id => id!).ToArray();
    [JsonIgnore] public override bool FieldsAreValid => BoatId is not null && OriginInstanceId is not null && DestinationInstanceId is not null;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record BoatRecoveryStartedEventPayload(string? ActorId, [property: JsonRequired] string BoatId, [property: JsonRequired] string RecoveryInstanceId) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string DiagnosticDetail => $"{ActorId}:{BoatId}:{RecoveryInstanceId}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => new string?[] { BoatId, RecoveryInstanceId }.Where(id => !string.IsNullOrEmpty(id)).Select(id => id!).ToArray();
    [JsonIgnore] public override bool FieldsAreValid => BoatId is not null && RecoveryInstanceId is not null;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record BoatReturnedEventPayload(string? ActorId, [property: JsonRequired] string BoatId, [property: JsonRequired] string PortId) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string DiagnosticDetail => $"{ActorId}:{BoatId}:{PortId}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => new string?[] { BoatId, PortId }.Where(id => !string.IsNullOrEmpty(id)).Select(id => id!).ToArray();
    [JsonIgnore] public override bool FieldsAreValid => BoatId is not null && PortId is not null;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record BoatTripCancelledEventPayload(string? ActorId, [property: JsonRequired] string RequestId, [property: JsonRequired] string Reason) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string DiagnosticDetail => $"{ActorId}:{RequestId}:{Reason}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => new string?[] { RequestId }.Where(id => !string.IsNullOrEmpty(id)).Select(id => id!).ToArray();
    [JsonIgnore] public override bool FieldsAreValid => RequestId is not null && Reason is not null;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record BoatTripRequestedEventPayload(string? ActorId, [property: JsonRequired] string QueuedRequestId, [property: JsonRequired] string OriginInstanceId, [property: JsonRequired] string DestinationInstanceId) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string DiagnosticDetail => $"{ActorId}:{QueuedRequestId}:{OriginInstanceId}:{DestinationInstanceId}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => new string?[] { QueuedRequestId, OriginInstanceId, DestinationInstanceId }.Where(id => !string.IsNullOrEmpty(id)).Select(id => id!).ToArray();
    [JsonIgnore] public override bool FieldsAreValid => QueuedRequestId is not null && OriginInstanceId is not null && DestinationInstanceId is not null;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record BoatWaitingEventPayload(string? ActorId, [property: JsonRequired] string BoatId) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string DiagnosticDetail => $"{ActorId}:{BoatId}:arrival_blocked";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => new string?[] { BoatId }.Where(id => !string.IsNullOrEmpty(id)).Select(id => id!).ToArray();
    [JsonIgnore] public override bool FieldsAreValid => BoatId is not null;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record BridgeBuiltEventPayload(string? ActorId, [property: JsonRequired] string Trigger, [property: JsonRequired] string BridgeId, [property: JsonRequired] string? RoadId) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string DiagnosticDetail => RoadId is null ? $"{Trigger}:{BridgeId}" : $"{Trigger}:{BridgeId}:{RoadId}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => new string?[] { BridgeId, RoadId }.Where(id => !string.IsNullOrEmpty(id)).Select(id => id!).ToArray();
    [JsonIgnore] public override bool FieldsAreValid => Trigger is not null && BridgeId is not null;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CarryingFullEventPayload(string? ActorId) : ActorOnlyWorldEventPayload(ActorId);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CognitionBackpressureEventPayload(string? ActorId, [property: JsonRequired] string AgentId) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string DiagnosticDetail => $"{AgentId}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => new string?[] { AgentId }.Where(id => !string.IsNullOrEmpty(id)).Select(id => id!).ToArray();
    [JsonIgnore] public override bool FieldsAreValid => AgentId is not null;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CognitionRejectedEventPayload(string? ActorId, [property: JsonRequired] string Outcome) : SubjectlessWorldEventPayload(ActorId)
{
    [JsonIgnore] public override string DiagnosticDetail => $"{ActorId}:{Outcome}";
    [JsonIgnore] public override bool FieldsAreValid => Outcome is not null;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CommunalBoatLaunchedEventPayload(string? ActorId, [property: JsonRequired] string TownId, [property: JsonRequired] string BoatId, [property: JsonRequired] string PortInstanceId, [property: JsonRequired] string ProjectName) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string DiagnosticDetail => $"{TownId}:{BoatId}:{PortInstanceId}:{ProjectName}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => new string?[] { TownId, BoatId, PortInstanceId }.Where(id => !string.IsNullOrEmpty(id)).Select(id => id!).ToArray();
    [JsonIgnore] public override bool FieldsAreValid => TownId is not null && BoatId is not null && PortInstanceId is not null && ProjectName is not null;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ConsumptionFailedEventPayload(string? ActorId) : ScalarWorldEventPayload(ActorId)
{
    [JsonIgnore] public override string DiagnosticDetail => $"{ActorId}:no_food";
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ContentActivatedEventPayload(string? ActorId, string PackageId) : FilteredPackageWorldEventPayload(ActorId, PackageId);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ContentApprovedEventPayload(string? ActorId, string PackageId) : FilteredPackageWorldEventPayload(ActorId, PackageId);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ContentDefinitionsActivatedEventPayload(string? ActorId, [property: JsonRequired] string PackageId, [property: JsonRequired] int BuildingsCount, [property: JsonRequired] int RecipesCount) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string DiagnosticDetail => $"{PackageId}:buildings={BuildingsCount}:recipes={RecipesCount}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => new string?[] { PackageId }.Where(id => !string.IsNullOrEmpty(id)).Select(id => id!).ToArray();
    [JsonIgnore] public override bool FieldsAreValid => PackageId is not null;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ContentProposedEventPayload(string? ActorId, string PackageId) : FilteredPackageWorldEventPayload(ActorId, PackageId);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ContentRolledBackEventPayload(string? ActorId, [property: JsonRequired] string PackageId, [property: JsonRequired] string Reason) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string DiagnosticDetail => $"{PackageId}:{Reason}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => new string?[] { PackageId }.Where(id => !string.IsNullOrEmpty(id)).Select(id => id!).ToArray();
    [JsonIgnore] public override bool FieldsAreValid => PackageId is not null && Reason is not null;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ContentStagedEventPayload(string? ActorId, string PackageId) : FilteredPackageWorldEventPayload(ActorId, PackageId);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ContentValidatedEventPayload(string? ActorId, string PackageId) : FilteredPackageWorldEventPayload(ActorId, PackageId);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CropMoistureEffectEventPayload(string? ActorId, [property: JsonRequired] string FieldId, [property: JsonRequired] int Cycle, [property: JsonRequired] string Effect, [property: JsonRequired] int Moisture) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string DiagnosticDetail => $"{FieldId}:harvest:{Cycle}:{Effect}:{Moisture}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => new string?[] { FieldId }.Where(id => !string.IsNullOrEmpty(id)).Select(id => id!).ToArray();
    [JsonIgnore] public override bool FieldsAreValid => FieldId is not null && Effect is not null;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CropWeatherLossEventPayload(string? ActorId, [property: JsonRequired] string FieldId, [property: JsonRequired] int Cycle, [property: JsonRequired] string Weather) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string DiagnosticDetail => $"{FieldId}:harvest:{Cycle}:{Weather}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => new string?[] { FieldId }.Where(id => !string.IsNullOrEmpty(id)).Select(id => id!).ToArray();
    [JsonIgnore] public override bool FieldsAreValid => FieldId is not null && Weather is not null;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record DependentCaredForEventPayload(string? ActorId) : ActorOnlyWorldEventPayload(ActorId);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record DestinationReachedEventPayload(string? ActorId, [property: JsonRequired] string Reason) : SubjectlessWorldEventPayload(ActorId)
{
    [JsonIgnore] public override string DiagnosticDetail => $"{ActorId}:{Reason}";
    [JsonIgnore] public override bool FieldsAreValid => Reason is not null;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record DeveloperEditEventPayload(string? ActorId, [property: JsonRequired] string WorldId, [property: JsonRequired] long ExpectedEventId, [property: JsonRequired] string Operation, [property: JsonRequired] string Value, [property: JsonRequired] int Amount, [property: JsonRequired] string? OtherAgentId) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string DiagnosticDetail => JsonSerializer.Serialize(new { WorldId, ExpectedEventId, AgentId = ActorId, Operation, Value, Amount, OtherAgentId });
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => new string?[] { WorldId, OtherAgentId }.Where(id => !string.IsNullOrEmpty(id)).Select(id => id!).ToArray();
    [JsonIgnore] public override bool FieldsAreValid => WorldId is not null && Operation is not null && Value is not null;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record EmptyVesselPickedUpEventPayload(string? ActorId, [property: JsonRequired] string VesselId, [property: JsonRequired] string SourceInstanceId, [property: JsonRequired] string HouseInstanceId) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string DiagnosticDetail => $"{ActorId}:{VesselId}:{SourceInstanceId}:{HouseInstanceId}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => new string?[] { VesselId, SourceInstanceId, HouseInstanceId }.Where(id => !string.IsNullOrEmpty(id)).Select(id => id!).ToArray();
    [JsonIgnore] public override bool FieldsAreValid => VesselId is not null && SourceInstanceId is not null && HouseInstanceId is not null;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ExpansionOrderPausedEventPayload(string? ActorId, [property: JsonRequired] string InstructionId, [property: JsonRequired] string JobId) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string DiagnosticDetail => $"{InstructionId}:{JobId}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => new string?[] { InstructionId, JobId }.Where(id => !string.IsNullOrEmpty(id)).Select(id => id!).ToArray();
    [JsonIgnore] public override bool FieldsAreValid => InstructionId is not null && JobId is not null;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ExpansionOrderResumedEventPayload(string? ActorId, [property: JsonRequired] string InstructionId, [property: JsonRequired] string JobId) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string DiagnosticDetail => $"{InstructionId}:{JobId}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => new string?[] { InstructionId, JobId }.Where(id => !string.IsNullOrEmpty(id)).Select(id => id!).ToArray();
    [JsonIgnore] public override bool FieldsAreValid => InstructionId is not null && JobId is not null;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ExplorationAbortedEventPayload(string? ActorId, [property: JsonRequired] string Reason) : SubjectlessWorldEventPayload(ActorId)
{
    [JsonIgnore] public override string DiagnosticDetail => $"{ActorId}:{Reason}";
    [JsonIgnore] public override bool FieldsAreValid => Reason is not null;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ExplorationCompletedEventPayload(string? ActorId, [property: JsonRequired] int VisitedTilesCount) : ScalarWorldEventPayload(ActorId)
{
    [JsonIgnore] public override string DiagnosticDetail => $"{ActorId}:visited={VisitedTilesCount}";
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ExplorationDiscoveredEventPayload(string? ActorId, [property: JsonRequired] int PositionX, [property: JsonRequired] int PositionY, [property: JsonRequired] string Terrain, [property: JsonRequired] string Resources) : SubjectlessWorldEventPayload(ActorId)
{
    [JsonIgnore] public override string DiagnosticDetail => $"{ActorId}:{PositionX},{PositionY}:{Terrain}:{Resources}";
    [JsonIgnore] public override bool FieldsAreValid => Terrain is not null && Resources is not null;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ExplorationGoalFoundEventPayload(string? ActorId, [property: JsonRequired] string GoalKind, [property: JsonRequired] string Target) : SubjectlessWorldEventPayload(ActorId)
{
    [JsonIgnore] public override string DiagnosticDetail => $"{ActorId}:{GoalKind}:{Target}";
    [JsonIgnore] public override bool FieldsAreValid => GoalKind is not null && Target is not null;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ExplorationReturnStartedEventPayload(string? ActorId) : ActorOnlyWorldEventPayload(ActorId);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ExplorationStartedEventPayload(string? ActorId, [property: JsonRequired] int PositionX, [property: JsonRequired] int PositionY) : ScalarWorldEventPayload(ActorId)
{
    [JsonIgnore] public override string DiagnosticDetail => $"{ActorId}:{PositionX},{PositionY}";
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record FieldHarvestedEventPayload(string? ActorId, [property: JsonRequired] string FieldId, [property: JsonRequired] string? Crop) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string DiagnosticDetail => $"{ActorId}:{FieldId}:{Crop}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => new string?[] { FieldId }.Where(id => !string.IsNullOrEmpty(id)).Select(id => id!).ToArray();
    [JsonIgnore] public override bool FieldsAreValid => FieldId is not null;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record FieldPlantedEventPayload(string? ActorId, [property: JsonRequired] string FieldId, [property: JsonRequired] string? Crop) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string DiagnosticDetail => $"{ActorId}:{FieldId}:{Crop}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => new string?[] { FieldId }.Where(id => !string.IsNullOrEmpty(id)).Select(id => id!).ToArray();
    [JsonIgnore] public override bool FieldsAreValid => FieldId is not null;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record FieldPreparedEventPayload(string? ActorId, [property: JsonRequired] string FieldId, [property: JsonRequired] string? Crop) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string DiagnosticDetail => $"{ActorId}:{FieldId}:{Crop}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => new string?[] { FieldId }.Where(id => !string.IsNullOrEmpty(id)).Select(id => id!).ToArray();
    [JsonIgnore] public override bool FieldsAreValid => FieldId is not null;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record FieldReadyEventPayload(string? ActorId, [property: JsonRequired] string FieldId) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string DiagnosticDetail => $"{FieldId}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => new string?[] { FieldId }.Where(id => !string.IsNullOrEmpty(id)).Select(id => id!).ToArray();
    [JsonIgnore] public override bool FieldsAreValid => FieldId is not null;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record FieldReturnedToGrassEventPayload(string? ActorId, [property: JsonRequired] string FieldId, [property: JsonRequired] string HouseholdId) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string DiagnosticDetail => $"{FieldId}:{HouseholdId}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => new string?[] { FieldId, HouseholdId }.Where(id => !string.IsNullOrEmpty(id)).Select(id => id!).ToArray();
    [JsonIgnore] public override bool FieldsAreValid => FieldId is not null && HouseholdId is not null;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record FieldTendedEventPayload(string? ActorId, [property: JsonRequired] string FieldId, [property: JsonRequired] string? Crop) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string DiagnosticDetail => $"{ActorId}:{FieldId}:{Crop}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => new string?[] { FieldId }.Where(id => !string.IsNullOrEmpty(id)).Select(id => id!).ToArray();
    [JsonIgnore] public override bool FieldsAreValid => FieldId is not null;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record FieldWorkInterruptedEventPayload(string? ActorId, [property: JsonRequired] string FieldId) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string DiagnosticDetail => $"{FieldId}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => new string?[] { FieldId }.Where(id => !string.IsNullOrEmpty(id)).Select(id => id!).ToArray();
    [JsonIgnore] public override bool FieldsAreValid => FieldId is not null;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record FieldWorkStartedEventPayload(string? ActorId, [property: JsonRequired] string FieldId, [property: JsonRequired] string ItemKind) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string DiagnosticDetail => $"{ActorId}:{FieldId}:{ItemKind}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => new string?[] { FieldId }.Where(id => !string.IsNullOrEmpty(id)).Select(id => id!).ToArray();
    [JsonIgnore] public override bool FieldsAreValid => FieldId is not null && ItemKind is not null;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record FireExtinguishedEventPayload(string? ActorId, [property: JsonRequired] string InstanceId) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string DiagnosticDetail => $"{InstanceId}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => new string?[] { InstanceId }.Where(id => !string.IsNullOrEmpty(id)).Select(id => id!).ToArray();
    [JsonIgnore] public override bool FieldsAreValid => InstanceId is not null;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record FireFuelledEventPayload(string? ActorId, [property: JsonRequired] string BuildingInstanceId) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string DiagnosticDetail => $"{BuildingInstanceId}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => new string?[] { BuildingInstanceId }.Where(id => !string.IsNullOrEmpty(id)).Select(id => id!).ToArray();
    [JsonIgnore] public override bool FieldsAreValid => BuildingInstanceId is not null;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record FirstTownLayoutAcceptedEventPayload(string? ActorId, [property: JsonRequired] int SiteX, [property: JsonRequired] int SiteY, [property: JsonRequired] int BuildingCount, [property: JsonRequired] int RoadCount) : ScalarWorldEventPayload(ActorId)
{
    [JsonIgnore] public override string DiagnosticDetail => $"{SiteX},{SiteY}:buildings:{BuildingCount}:roads:{RoadCount}";
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record FirstTownLayoutRedoneEventPayload(string? ActorId, [property: JsonRequired] int SiteX, [property: JsonRequired] int SiteY, [property: JsonRequired] int BuildingCount, [property: JsonRequired] int RoadCount) : ScalarWorldEventPayload(ActorId)
{
    [JsonIgnore] public override string DiagnosticDetail => $"{SiteX},{SiteY}:buildings:{BuildingCount}:roads:{RoadCount}";
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record FoodConsumedEventPayload(string? ActorId) : ActorOnlyWorldEventPayload(ActorId);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record FoodHarvestedEventPayload(string? ActorId, [property: JsonRequired] int HarvestYield) : ScalarWorldEventPayload(ActorId)
{
    [JsonIgnore] public override string DiagnosticDetail => $"{ActorId}:{HarvestYield}";
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record FoodPolicyAdoptedEventPayload(string? ActorId, [property: JsonRequired] string Policy) : SubjectlessWorldEventPayload(ActorId)
{
    [JsonIgnore] public override string DiagnosticDetail => $"{Policy}";
    [JsonIgnore] public override bool FieldsAreValid => Policy is not null;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record FoodPolicyProposedEventPayload(string? ActorId, [property: JsonRequired] string Policy) : SubjectlessWorldEventPayload(ActorId)
{
    [JsonIgnore] public override string DiagnosticDetail => $"{Policy}";
    [JsonIgnore] public override bool FieldsAreValid => Policy is not null;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record FoodPolicyRejectedEventPayload(string? ActorId, [property: JsonRequired] string Policy) : SubjectlessWorldEventPayload(ActorId)
{
    [JsonIgnore] public override string DiagnosticDetail => $"{Policy}";
    [JsonIgnore] public override bool FieldsAreValid => Policy is not null;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record FoodPolicyVoteRecordedEventPayload(string? ActorId) : ActorOnlyWorldEventPayload(ActorId);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record FoodStewardChangedEventPayload(string? ActorId, [property: JsonRequired] string Successor) : SubjectlessWorldEventPayload(ActorId)
{
    [JsonIgnore] public override string DiagnosticDetail => $"{Successor}";
    [JsonIgnore] public override bool FieldsAreValid => Successor is not null;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record FoodStoredInPotEventPayload(string? ActorId, [property: JsonRequired] string PotId, [property: JsonRequired] string FoodId, [property: JsonRequired] int Quantity) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string DiagnosticDetail => $"{ActorId}:{PotId}:{FoodId}:{Quantity}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => new string?[] { PotId, FoodId }.Where(id => !string.IsNullOrEmpty(id)).Select(id => id!).ToArray();
    [JsonIgnore] public override bool FieldsAreValid => PotId is not null && FoodId is not null;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record FoodTakenFromPotEventPayload(string? ActorId, [property: JsonRequired] string PotId, [property: JsonRequired] string FoodId, [property: JsonRequired] int Quantity) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string DiagnosticDetail => $"{ActorId}:{PotId}:{FoodId}:{Quantity}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => new string?[] { PotId, FoodId }.Where(id => !string.IsNullOrEmpty(id)).Select(id => id!).ToArray();
    [JsonIgnore] public override bool FieldsAreValid => PotId is not null && FoodId is not null;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record FounderMovedEventPayload(string? ActorId, [property: JsonRequired] int PositionX, [property: JsonRequired] int PositionY, [property: JsonRequired] int PositionXValue, [property: JsonRequired] int PositionYValue) : ScalarWorldEventPayload(ActorId)
{
    [JsonIgnore] public override string DiagnosticDetail => $"{ActorId}:{PositionX},{PositionY}->{PositionXValue},{PositionYValue}";
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record FounderPlacedEventPayload(string? ActorId, string HouseholdId) : HouseholdWorldEventPayload(ActorId, HouseholdId);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record FounderPlacementUndoneEventPayload(string? ActorId) : ActorOnlyWorldEventPayload(ActorId);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record FruitHarvestedEventPayload(string? ActorId, [property: JsonRequired] string SourceId, [property: JsonRequired] int HarvestYield) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string DiagnosticDetail => $"{ActorId}:{SourceId}:{HarvestYield}:picked";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => new string?[] { SourceId }.Where(id => !string.IsNullOrEmpty(id)).Select(id => id!).ToArray();
    [JsonIgnore] public override bool FieldsAreValid => SourceId is not null;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record HandcartAttachedEventPayload(string? ActorId, [property: JsonRequired] string CartId) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string DiagnosticDetail => $"{ActorId}:{CartId}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => new string?[] { CartId }.Where(id => !string.IsNullOrEmpty(id)).Select(id => id!).ToArray();
    [JsonIgnore] public override bool FieldsAreValid => CartId is not null;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record HandcartBlockedEventPayload(string? ActorId, [property: JsonRequired] string Reason) : SubjectlessWorldEventPayload(ActorId)
{
    [JsonIgnore] public override string DiagnosticDetail => $"{ActorId}:{Reason}";
    [JsonIgnore] public override bool FieldsAreValid => Reason is not null;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record HandcartLoadedEventPayload(string? ActorId, [property: JsonRequired] string CartHereId, [property: JsonRequired] string ItemKind, [property: JsonRequired] int Quantity) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string DiagnosticDetail => $"{ActorId}:{CartHereId}:{ItemKind}:{Quantity}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => new string?[] { CartHereId }.Where(id => !string.IsNullOrEmpty(id)).Select(id => id!).ToArray();
    [JsonIgnore] public override bool FieldsAreValid => CartHereId is not null && ItemKind is not null;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record HandcartMovedEventPayload(string? ActorId, [property: JsonRequired] string CartId) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string DiagnosticDetail => $"{ActorId}:{CartId}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => new string?[] { CartId }.Where(id => !string.IsNullOrEmpty(id)).Select(id => id!).ToArray();
    [JsonIgnore] public override bool FieldsAreValid => CartId is not null;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record HandcartParkedEventPayload(string? ActorId, [property: JsonRequired] string CartLotId, [property: JsonRequired] string Reason) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string DiagnosticDetail => $"{ActorId}:{CartLotId}:{Reason}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => new string?[] { CartLotId }.Where(id => !string.IsNullOrEmpty(id)).Select(id => id!).ToArray();
    [JsonIgnore] public override bool FieldsAreValid => CartLotId is not null && Reason is not null;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record HandcartRepairedEventPayload(string? ActorId, [property: JsonRequired] string CartHereId) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string DiagnosticDetail => $"{ActorId}:{CartHereId}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => new string?[] { CartHereId }.Where(id => !string.IsNullOrEmpty(id)).Select(id => id!).ToArray();
    [JsonIgnore] public override bool FieldsAreValid => CartHereId is not null;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record HandcartTransferredEventPayload(string? ActorId, [property: JsonRequired] string CartHereId, [property: JsonRequired] string RecipientId) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string DiagnosticDetail => $"{ActorId}:{CartHereId}:{RecipientId}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => new string?[] { CartHereId, RecipientId }.Where(id => !string.IsNullOrEmpty(id)).Select(id => id!).ToArray();
    [JsonIgnore] public override bool FieldsAreValid => CartHereId is not null && RecipientId is not null;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record HandcartUnloadedEventPayload(string? ActorId, [property: JsonRequired] string CartHereId, [property: JsonRequired] string ItemKind, [property: JsonRequired] int Quantity) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string DiagnosticDetail => $"{ActorId}:{CartHereId}:{ItemKind}:{Quantity}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => new string?[] { CartHereId }.Where(id => !string.IsNullOrEmpty(id)).Select(id => id!).ToArray();
    [JsonIgnore] public override bool FieldsAreValid => CartHereId is not null && ItemKind is not null;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record HarvestFailedEventPayload(string? ActorId, [property: JsonRequired] string Reason) : SubjectlessWorldEventPayload(ActorId)
{
    [JsonIgnore] public override string DiagnosticDetail => $"{ActorId}:{Reason}";
    [JsonIgnore] public override bool FieldsAreValid => Reason is not null;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record HearthContentStagedEventPayload(string? ActorId, string PackageId) : FilteredPackageWorldEventPayload(ActorId, PackageId);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record HorseDismountedEventPayload(string? ActorId, [property: JsonRequired] string AnimalId, [property: JsonRequired] string Reason) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string DiagnosticDetail => $"{ActorId}:{AnimalId}:{Reason}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => new string?[] { AnimalId }.Where(id => !string.IsNullOrEmpty(id)).Select(id => id!).ToArray();
    [JsonIgnore] public override bool FieldsAreValid => AnimalId is not null && Reason is not null;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record HorseMountedEventPayload(string? ActorId, string AnimalId) : AnimalActionWorldEventPayload(ActorId, AnimalId);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record HostedDecisionCompletedEventPayload(string? ActorId, [property: JsonRequired] string Outcome) : SubjectlessWorldEventPayload(ActorId)
{
    [JsonIgnore] public override string DiagnosticDetail => $"{ActorId}:{Outcome}";
    [JsonIgnore] public override bool FieldsAreValid => Outcome is not null;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record HostedDecisionDiscardedEventPayload(string? ActorId) : ActorOnlyWorldEventPayload(ActorId);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record HostedDecisionStartedEventPayload(string? ActorId) : ActorOnlyWorldEventPayload(ActorId);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record HouseContentStagedEventPayload(string? ActorId, string PackageId) : FilteredPackageWorldEventPayload(ActorId, PackageId);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record HouseCookingContentStagedEventPayload(string? ActorId, string PackageId) : FilteredPackageWorldEventPayload(ActorId, PackageId);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record HouseGuestInvitedEventPayload(string? ActorId, [property: JsonRequired] string HouseId, [property: JsonRequired] string GuestId) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string DiagnosticDetail => $"{HouseId}:{ActorId}:{GuestId}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => new string?[] { HouseId, GuestId }.Where(id => !string.IsNullOrEmpty(id)).Select(id => id!).ToArray();
    [JsonIgnore] public override bool FieldsAreValid => HouseId is not null && GuestId is not null;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record HouseGuestRevokedEventPayload(string? ActorId, [property: JsonRequired] string HouseId, [property: JsonRequired] string GuestId) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string DiagnosticDetail => $"{HouseId}:{ActorId}:{GuestId}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => new string?[] { HouseId, GuestId }.Where(id => !string.IsNullOrEmpty(id)).Select(id => id!).ToArray();
    [JsonIgnore] public override bool FieldsAreValid => HouseId is not null && GuestId is not null;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record HouseToolMadeEventPayload(string? ActorId, [property: JsonRequired] string ItemKind) : SubjectlessWorldEventPayload(ActorId)
{
    [JsonIgnore] public override string DiagnosticDetail => $"{ActorId}|{ItemKind}";
    [JsonIgnore] public override bool FieldsAreValid => ItemKind is not null;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record InitialContentActivatedEventPayload(string? ActorId, string PackageId) : FilteredPackageWorldEventPayload(ActorId, PackageId);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record MaterialGatheredEventPayload(string? ActorId, [property: JsonRequired] string ItemKind, [property: JsonRequired] int Quantity) : SubjectlessWorldEventPayload(ActorId)
{
    [JsonIgnore] public override string DiagnosticDetail => $"{ActorId}:{ItemKind}:{Quantity}";
    [JsonIgnore] public override bool FieldsAreValid => ItemKind is not null;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record MealEatenEventPayload(string? ActorId, [property: JsonRequired] string ItemKind, [property: JsonRequired] int Nutrition) : SubjectlessWorldEventPayload(ActorId)
{
    [JsonIgnore] public override string DiagnosticDetail => $"{ActorId}:{ItemKind}:nutrition={Nutrition}";
    [JsonIgnore] public override bool FieldsAreValid => ItemKind is not null;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record MilkDrunkEventPayload(string? ActorId) : ActorOnlyWorldEventPayload(ActorId);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ModelAttemptStatusEventPayload(string? ActorId, [property: JsonRequired] string Status) : SubjectlessWorldEventPayload(ActorId)
{
    [JsonIgnore] public override string DiagnosticDetail => $"{ActorId}:{Status}";
    [JsonIgnore] public override bool FieldsAreValid => Status is not null;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ModelCallWarningEventPayload(string? ActorId, [property: JsonRequired] long Used, [property: JsonRequired] long Limit) : ScalarWorldEventPayload(ActorId)
{
    [JsonIgnore] public override string DiagnosticDetail => $"used:{Used}:limit:{Limit}";
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record MovementBlockedEventPayload(string? ActorId, [property: JsonRequired] string Reason, [property: JsonRequired] int WaitTicks) : SubjectlessWorldEventPayload(ActorId)
{
    [JsonIgnore] public override string DiagnosticDetail => $"{ActorId}:{Reason}:wait={WaitTicks}";
    [JsonIgnore] public override bool FieldsAreValid => Reason is not null;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record OrnamentGivenEventPayload(string? ActorId, [property: JsonRequired] string RecipientId, [property: JsonRequired] string LotId, [property: JsonRequired] string ReceivedId) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string DiagnosticDetail => $"{ActorId}:ornament_gift:{RecipientId}:{LotId}:{ReceivedId}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => new string?[] { RecipientId, LotId, ReceivedId }.Where(id => !string.IsNullOrEmpty(id)).Select(id => id!).ToArray();
    [JsonIgnore] public override bool FieldsAreValid => RecipientId is not null && LotId is not null && ReceivedId is not null;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record OrnamentRemovedEventPayload(string? ActorId, [property: JsonRequired] string LotId) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string DiagnosticDetail => $"{ActorId}:ornament:{LotId}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => new string?[] { LotId }.Where(id => !string.IsNullOrEmpty(id)).Select(id => id!).ToArray();
    [JsonIgnore] public override bool FieldsAreValid => LotId is not null;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record OrnamentWornEventPayload(string? ActorId, [property: JsonRequired] string LotId) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string DiagnosticDetail => $"{ActorId}:ornament:{LotId}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => new string?[] { LotId }.Where(id => !string.IsNullOrEmpty(id)).Select(id => id!).ToArray();
    [JsonIgnore] public override bool FieldsAreValid => LotId is not null;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record PausedEventPayload(string? ActorId) : ScalarWorldEventPayload(ActorId)
{
    [JsonIgnore] public override string DiagnosticDetail => $"owner_request";
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record PotteryContentStagedEventPayload(string? ActorId, string PackageId) : FilteredPackageWorldEventPayload(ActorId, PackageId);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record PrimaryCaregiverAssignedEventPayload(string? ActorId) : ActorOnlyWorldEventPayload(ActorId);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ReplacementCareAcceptedEventPayload(string? ActorId, [property: JsonRequired] string ChildId) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string DiagnosticDetail => $"{ActorId}|{ChildId}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => new string?[] { ChildId }.Where(id => !string.IsNullOrEmpty(id)).Select(id => id!).ToArray();
    [JsonIgnore] public override bool FieldsAreValid => ChildId is not null;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record RestaurantContentStagedEventPayload(string? ActorId, string PackageId) : FilteredPackageWorldEventPayload(ActorId, PackageId);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ResumedEventPayload(string? ActorId, [property: JsonRequired] long RunEpoch) : ScalarWorldEventPayload(ActorId)
{
    [JsonIgnore] public override string DiagnosticDetail => $"epoch:{RunEpoch}";
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record RoutineHelperChangedEventPayload(string? ActorId, [property: JsonRequired] string Provider, [property: JsonRequired] string Model) : SubjectlessWorldEventPayload(ActorId)
{
    [JsonIgnore] public override string DiagnosticDetail => $"{Provider}:{Model}";
    [JsonIgnore] public override bool FieldsAreValid => Provider is not null && Model is not null;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record SavedRoadFootprintsRepairedEventPayload(string? ActorId, [property: JsonRequired] int Removed) : ScalarWorldEventPayload(ActorId)
{
    [JsonIgnore] public override string DiagnosticDetail => $"removed:{Removed}";
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record SiloContentStagedEventPayload(string? ActorId, string PackageId) : FilteredPackageWorldEventPayload(ActorId, PackageId);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record SpareCargoStoredEventPayload(string? ActorId, [property: JsonRequired] string ItemKind, [property: JsonRequired] int Quantity, [property: JsonRequired] string StorageBuildingId) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string DiagnosticDetail => $"{ActorId}:{ItemKind}:{Quantity}:{StorageBuildingId}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => new string?[] { StorageBuildingId }.Where(id => !string.IsNullOrEmpty(id)).Select(id => id!).ToArray();
    [JsonIgnore] public override bool FieldsAreValid => ItemKind is not null && StorageBuildingId is not null;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record StarterContentStagedEventPayload(string? ActorId, string PackageId) : FilteredPackageWorldEventPayload(ActorId, PackageId);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record StartingResourceBlockedEventPayload(string? ActorId, [property: JsonRequired] string ItemKind) : SubjectlessWorldEventPayload(ActorId)
{
    [JsonIgnore] public override string DiagnosticDetail => $"{ItemKind}";
    [JsonIgnore] public override bool FieldsAreValid => ItemKind is not null;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record StartingResourcesAddedEventPayload(string? ActorId, [property: JsonRequired] string Resources) : SubjectlessWorldEventPayload(ActorId)
{
    [JsonIgnore] public override string DiagnosticDetail => $"{Resources}";
    [JsonIgnore] public override bool FieldsAreValid => Resources is not null;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record SurvivalActivatedEventPayload(string? ActorId) : ScalarWorldEventPayload(ActorId)
{
    [JsonIgnore] public override string DiagnosticDetail => $"weather_equipment_and_food";
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record SurvivalConditionChangedEventPayload(string? ActorId, [property: JsonRequired] int Warmth, [property: JsonRequired] int Illness) : ScalarWorldEventPayload(ActorId)
{
    [JsonIgnore] public override string DiagnosticDetail => $"{ActorId}:warmth={Warmth}:illness={Illness}";
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record TailorContentStagedEventPayload(string? ActorId, string PackageId) : FilteredPackageWorldEventPayload(ActorId, PackageId);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record TickAdvancedEventPayload(string? ActorId, [property: JsonRequired] long TargetTick) : ScalarWorldEventPayload(ActorId)
{
    [JsonIgnore] public override string DiagnosticDetail => $"{TargetTick}";
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record TrafficBridgeNotBuiltEventPayload(string? ActorId, [property: JsonRequired] string BridgeId, [property: JsonRequired] string Reason) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string DiagnosticDetail => $"{BridgeId}:{Reason}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => new string?[] { BridgeId }.Where(id => !string.IsNullOrEmpty(id)).Select(id => id!).ToArray();
    [JsonIgnore] public override bool FieldsAreValid => BridgeId is not null && Reason is not null;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record TreeHarvestedEventPayload(string? ActorId, [property: JsonRequired] string SourceId, [property: JsonRequired] string? TreeKind) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string DiagnosticDetail => $"{ActorId}:{SourceId}:{TreeKind}:stump";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => new string?[] { SourceId }.Where(id => !string.IsNullOrEmpty(id)).Select(id => id!).ToArray();
    [JsonIgnore] public override bool FieldsAreValid => SourceId is not null;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record TreePlantedEventPayload(string? ActorId, [property: JsonRequired] string TreeId, [property: JsonRequired] string Species) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string DiagnosticDetail => $"{ActorId}:{TreeId}:{Species}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => new string?[] { TreeId }.Where(id => !string.IsNullOrEmpty(id)).Select(id => id!).ToArray();
    [JsonIgnore] public override bool FieldsAreValid => TreeId is not null && Species is not null;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record TreePlantingRefusedEventPayload(string? ActorId, [property: JsonRequired] string Refusal) : SubjectlessWorldEventPayload(ActorId)
{
    [JsonIgnore] public override string DiagnosticDetail => $"{ActorId}:{Refusal}";
    [JsonIgnore] public override bool FieldsAreValid => Refusal is not null;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record TreeReplantedEventPayload(string? ActorId, [property: JsonRequired] string TreeId, [property: JsonRequired] string? TreeKind) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string DiagnosticDetail => $"{ActorId}:{TreeId}:{TreeKind}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => new string?[] { TreeId }.Where(id => !string.IsNullOrEmpty(id)).Select(id => id!).ToArray();
    [JsonIgnore] public override bool FieldsAreValid => TreeId is not null;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record TreeSeedCollectedEventPayload(string? ActorId, [property: JsonRequired] string SourceId, [property: JsonRequired] int TreeSeedQuantity) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string DiagnosticDetail => $"{ActorId}:{SourceId}:{TreeSeedQuantity}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => new string?[] { SourceId }.Where(id => !string.IsNullOrEmpty(id)).Select(id => id!).ToArray();
    [JsonIgnore] public override bool FieldsAreValid => SourceId is not null;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record VoluntaryGoodsReturnedEventPayload(string? ActorId, [property: JsonRequired] string? BeneficiaryId, [property: JsonRequired] string LotId, [property: JsonRequired] int Remaining) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string DiagnosticDetail => $"{ActorId}:{BeneficiaryId}:{LotId}:{Remaining}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => new string?[] { BeneficiaryId, LotId }.Where(id => !string.IsNullOrEmpty(id)).Select(id => id!).ToArray();
    [JsonIgnore] public override bool FieldsAreValid => LotId is not null;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record WarehouseContentStagedEventPayload(string? ActorId, string PackageId) : FilteredPackageWorldEventPayload(ActorId, PackageId);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record WaterJugCollectedEventPayload(string? ActorId, [property: JsonRequired] string JugId) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string DiagnosticDetail => $"{ActorId}:{JugId}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => new string?[] { JugId }.Where(id => !string.IsNullOrEmpty(id)).Select(id => id!).ToArray();
    [JsonIgnore] public override bool FieldsAreValid => JugId is not null;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record WaterJugFilledEventPayload(string? ActorId, [property: JsonRequired] string JugId, [property: JsonRequired] int Remaining, [property: JsonRequired] int ShoreX, [property: JsonRequired] int ShoreY) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string DiagnosticDetail => $"{ActorId}:{JugId}:{Remaining}:{ShoreX},{ShoreY}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => new string?[] { JugId }.Where(id => !string.IsNullOrEmpty(id)).Select(id => id!).ToArray();
    [JsonIgnore] public override bool FieldsAreValid => JugId is not null;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record WaterJugReturnedEventPayload(string? ActorId, [property: JsonRequired] string JugId, [property: JsonRequired] string HouseInstanceId) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string DiagnosticDetail => $"{ActorId}:{JugId}:{HouseInstanceId}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => new string?[] { JugId, HouseInstanceId }.Where(id => !string.IsNullOrEmpty(id)).Select(id => id!).ToArray();
    [JsonIgnore] public override bool FieldsAreValid => JugId is not null && HouseInstanceId is not null;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record WeatherChangedEventPayload(string? ActorId, [property: JsonRequired] string Season, [property: JsonRequired] string AnchorWeather) : SubjectlessWorldEventPayload(ActorId)
{
    [JsonIgnore] public override string DiagnosticDetail => $"{Season}:{AnchorWeather}";
    [JsonIgnore] public override bool FieldsAreValid => Season is not null && AnchorWeather is not null;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record WorkPracticeEarnedEventPayload(string? ActorId) : ActorOnlyWorldEventPayload(ActorId);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record WorldCreatedEventPayload(string? ActorId, [property: JsonRequired] string WorldSeed, [property: JsonRequired] int AgentsCount) : SubjectlessWorldEventPayload(ActorId)
{
    [JsonIgnore] public override string DiagnosticDetail => $"{WorldSeed}:agents:{AgentsCount}";
    [JsonIgnore] public override bool FieldsAreValid => WorldSeed is not null;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record WorldStartedEventPayload(string? ActorId) : ScalarWorldEventPayload(ActorId)
{
    [JsonIgnore] public override string DiagnosticDetail => $"four_founders_ready";
}

/// <summary>Package events that retain their package reference as the subject.</summary>
public abstract record PackageWorldEventPayload(string? ActorId, [property: JsonRequired] string PackageId) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string DiagnosticDetail => PackageId;
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => [PackageId];
    [JsonIgnore] public override bool FieldsAreValid => PackageId is not null;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record AnimalContentStagedEventPayload(string? ActorId, string PackageId) : PackageWorldEventPayload(ActorId, PackageId);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CareContentStagedEventPayload(string? ActorId, string PackageId) : PackageWorldEventPayload(ActorId, PackageId);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record HouseToolsContentStagedEventPayload(string? ActorId, string PackageId) : PackageWorldEventPayload(ActorId, PackageId);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record KnowledgeContentStagedEventPayload(string? ActorId, string PackageId) : PackageWorldEventPayload(ActorId, PackageId);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record MarketContentStagedEventPayload(string? ActorId, string PackageId) : PackageWorldEventPayload(ActorId, PackageId);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record OrnamentContentStagedEventPayload(string? ActorId, string PackageId) : PackageWorldEventPayload(ActorId, PackageId);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record PortContentStagedEventPayload(string? ActorId, string PackageId) : PackageWorldEventPayload(ActorId, PackageId);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record StoreContentStagedEventPayload(string? ActorId, string PackageId) : PackageWorldEventPayload(ActorId, PackageId);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record StreetLanternContentStagedEventPayload(string? ActorId, string PackageId) : PackageWorldEventPayload(ActorId, PackageId);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record TownHallContentStagedEventPayload(string? ActorId, string PackageId) : PackageWorldEventPayload(ActorId, PackageId);
