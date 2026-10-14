using System.Text.Json;
using System.Text.Json.Serialization;

namespace ClankerWorld.Simulation.World;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record AgentAddedEventPayload(string? ActorId) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string Kind => "agent_added";
    [JsonIgnore] public override string DiagnosticDetail => $"{ActorId}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => [];
    [JsonIgnore] public override bool FieldsAreValid => true;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record AgentIdentityChosenEventPayload(string? ActorId) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string Kind => "agent_identity_chosen";
    [JsonIgnore] public override string DiagnosticDetail => $"{ActorId}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => [];
    [JsonIgnore] public override bool FieldsAreValid => true;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record AgentIdentityKeptEventPayload(string? ActorId) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string Kind => "agent_identity_kept";
    [JsonIgnore] public override string DiagnosticDetail => $"{ActorId}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => [];
    [JsonIgnore] public override bool FieldsAreValid => true;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record AgentIdentityMomentKeptEventPayload(string? ActorId, [property: JsonRequired] string MomentKind) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string Kind => "agent_identity_moment_kept";
    [JsonIgnore] public override string DiagnosticDetail => $"{ActorId}:{MomentKind}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => [];
    [JsonIgnore] public override bool FieldsAreValid => MomentKind is not null;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record AgentIdentityMomentStartedEventPayload(string? ActorId, [property: JsonRequired] string MomentKind) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string Kind => "agent_identity_moment_started";
    [JsonIgnore] public override string DiagnosticDetail => $"{ActorId}:{MomentKind}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => [];
    [JsonIgnore] public override bool FieldsAreValid => MomentKind is not null;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record AgentIdentityRevisedEventPayload(string? ActorId, [property: JsonRequired] string MomentKind) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string Kind => "agent_identity_revised";
    [JsonIgnore] public override string DiagnosticDetail => $"{ActorId}:{MomentKind}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => [];
    [JsonIgnore] public override bool FieldsAreValid => MomentKind is not null;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record AgentIdleEventPayload(string? ActorId) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string Kind => "agent_idle";
    [JsonIgnore] public override string DiagnosticDetail => $"{ActorId}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => [];
    [JsonIgnore] public override bool FieldsAreValid => true;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record AgentKnowledgeArtifactCollectedEventPayload(string? ActorId, [property: JsonRequired] string ArtifactId) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string Kind => "agent_knowledge_artifact_collected";
    [JsonIgnore] public override string DiagnosticDetail => $"{ActorId}|{ArtifactId}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => new string?[] { ArtifactId }.Where(id => !string.IsNullOrEmpty(id)).Select(id => id!).ToArray();
    [JsonIgnore] public override bool FieldsAreValid => ArtifactId is not null;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record AgentKnowledgeArtifactCreatedEventPayload(string? ActorId, [property: JsonRequired] string ArtifactId, [property: JsonRequired] string ProjectKind, [property: JsonRequired] int FactCount) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string Kind => "agent_knowledge_artifact_created";
    [JsonIgnore] public override string DiagnosticDetail => $"{ActorId}|{ArtifactId}|{ProjectKind}|{FactCount}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => new string?[] { ArtifactId }.Where(id => !string.IsNullOrEmpty(id)).Select(id => id!).ToArray();
    [JsonIgnore] public override bool FieldsAreValid => ArtifactId is not null && ProjectKind is not null;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record AgentKnowledgeArtifactReadEventPayload(string? ActorId, [property: JsonRequired] string SourceAgentId, [property: JsonRequired] string RecipientId, [property: JsonRequired] string ArtifactId, [property: JsonRequired] int FactCount) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string Kind => "agent_knowledge_artifact_read";
    [JsonIgnore] public override string DiagnosticDetail => $"{SourceAgentId}|{RecipientId}|{ArtifactId}|{FactCount}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => new string?[] { SourceAgentId, RecipientId, ArtifactId }.Where(id => !string.IsNullOrEmpty(id)).Select(id => id!).ToArray();
    [JsonIgnore] public override bool FieldsAreValid => SourceAgentId is not null && RecipientId is not null && ArtifactId is not null;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record AgentKnowledgeArtifactStoredEventPayload(string? ActorId, [property: JsonRequired] string ArtifactId, [property: JsonRequired] string HouseInstanceId) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string Kind => "agent_knowledge_artifact_stored";
    [JsonIgnore] public override string DiagnosticDetail => $"{ActorId}|{ArtifactId}|{HouseInstanceId}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => new string?[] { ArtifactId, HouseInstanceId }.Where(id => !string.IsNullOrEmpty(id)).Select(id => id!).ToArray();
    [JsonIgnore] public override bool FieldsAreValid => ArtifactId is not null && HouseInstanceId is not null;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record AgentKnowledgeLearnedEventPayload(string? ActorId) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string Kind => "agent_knowledge_learned";
    [JsonIgnore] public override string DiagnosticDetail => $"{ActorId}|firsthand|1";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => [];
    [JsonIgnore] public override bool FieldsAreValid => true;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record AgentKnowledgeMaterialCollectedEventPayload(string? ActorId, [property: JsonRequired] string ItemKind, [property: JsonRequired] int Quantity) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string Kind => "agent_knowledge_material_collected";
    [JsonIgnore] public override string DiagnosticDetail => $"{ActorId}|{ItemKind}|{Quantity}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => [];
    [JsonIgnore] public override bool FieldsAreValid => ItemKind is not null;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record AgentKnowledgeSharedEventPayload(string? ActorId, [property: JsonRequired] string RecipientId, [property: JsonRequired] string ArtifactId, [property: JsonRequired] int FactCount) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string Kind => "agent_knowledge_shared";
    [JsonIgnore] public override string DiagnosticDetail => $"{ActorId}|{RecipientId}|{ArtifactId}|{FactCount}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => new string?[] { RecipientId, ArtifactId }.Where(id => !string.IsNullOrEmpty(id)).Select(id => id!).ToArray();
    [JsonIgnore] public override bool FieldsAreValid => RecipientId is not null && ArtifactId is not null;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record AgentKnowledgeWritingCancelledEventPayload(string? ActorId, [property: JsonRequired] string ProjectKind, [property: JsonRequired] string Reason) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string Kind => "agent_knowledge_writing_cancelled";
    [JsonIgnore] public override string DiagnosticDetail => $"{ActorId}|{ProjectKind}|{Reason}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => [];
    [JsonIgnore] public override bool FieldsAreValid => ProjectKind is not null && Reason is not null;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record AgentKnowledgeWritingStartedEventPayload(string? ActorId, [property: JsonRequired] string ItemKind, [property: JsonRequired] string ProjectId) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string Kind => "agent_knowledge_writing_started";
    [JsonIgnore] public override string DiagnosticDetail => $"{ActorId}|{ItemKind}|{ProjectId}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => new string?[] { ProjectId }.Where(id => !string.IsNullOrEmpty(id)).Select(id => id!).ToArray();
    [JsonIgnore] public override bool FieldsAreValid => ItemKind is not null && ProjectId is not null;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record AgentMemoriesArchivedEventPayload(string? ActorId, [property: JsonRequired] int ArchivedMemoriesCount, [property: JsonRequired] int ArchivedBeliefsCount) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string Kind => "agent_memories_archived";
    [JsonIgnore] public override string DiagnosticDetail => $"{ArchivedMemoriesCount}:{ArchivedBeliefsCount}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => [];
    [JsonIgnore] public override bool FieldsAreValid => true;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record AgentMemoriesSummarizedEventPayload(string? ActorId, [property: JsonRequired] int MemoryCount) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string Kind => "agent_memories_summarized";
    [JsonIgnore] public override string DiagnosticDetail => $"{ActorId}:{MemoryCount}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => [];
    [JsonIgnore] public override bool FieldsAreValid => true;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record AgentMovedEventPayload(string? ActorId, [property: JsonRequired] int PositionX, [property: JsonRequired] int PositionY, [property: JsonRequired] int NextX, [property: JsonRequired] int NextY, [property: JsonRequired] string Reason) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string Kind => "agent_moved";
    [JsonIgnore] public override string DiagnosticDetail => $"{ActorId}:{PositionX},{PositionY}->{NextX},{NextY}:{Reason}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => [];
    [JsonIgnore] public override bool FieldsAreValid => Reason is not null;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record AgentNameRejectedEventPayload(string? ActorId) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string Kind => "agent_name_rejected";
    [JsonIgnore] public override string DiagnosticDetail => $"{ActorId}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => [];
    [JsonIgnore] public override bool FieldsAreValid => true;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record AgentNameRetryExhaustedEventPayload(string? ActorId) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string Kind => "agent_name_retry_exhausted";
    [JsonIgnore] public override string DiagnosticDetail => $"{ActorId}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => [];
    [JsonIgnore] public override bool FieldsAreValid => true;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record AgentNameRetryRequestedEventPayload(string? ActorId) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string Kind => "agent_name_retry_requested";
    [JsonIgnore] public override string DiagnosticDetail => $"{ActorId}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => [];
    [JsonIgnore] public override bool FieldsAreValid => true;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record AgentNameRetryUnavailableEventPayload(string? ActorId) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string Kind => "agent_name_retry_unavailable";
    [JsonIgnore] public override string DiagnosticDetail => $"{ActorId}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => [];
    [JsonIgnore] public override bool FieldsAreValid => true;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record AgentNameRetryUnusableEventPayload(string? ActorId) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string Kind => "agent_name_retry_unusable";
    [JsonIgnore] public override string DiagnosticDetail => $"{ActorId}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => [];
    [JsonIgnore] public override bool FieldsAreValid => true;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record AgentNameUnavailableEventPayload(string? ActorId) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string Kind => "agent_name_unavailable";
    [JsonIgnore] public override string DiagnosticDetail => $"{ActorId}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => [];
    [JsonIgnore] public override bool FieldsAreValid => true;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record AgentNamedEventPayload(string? ActorId) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string Kind => "agent_named";
    [JsonIgnore] public override string DiagnosticDetail => $"{ActorId}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => [];
    [JsonIgnore] public override bool FieldsAreValid => true;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record AgentRecipeLearnedEventPayload(string? ActorId, [property: JsonRequired] string RecipeId) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string Kind => "agent_recipe_learned";
    [JsonIgnore] public override string DiagnosticDetail => $"{ActorId}|{RecipeId}|practice";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => new string?[] { RecipeId }.Where(id => !string.IsNullOrEmpty(id)).Select(id => id!).ToArray();
    [JsonIgnore] public override bool FieldsAreValid => RecipeId is not null;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record AgentRemovedEventPayload(string? ActorId) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string Kind => "agent_removed";
    [JsonIgnore] public override string DiagnosticDetail => $"{ActorId}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => [];
    [JsonIgnore] public override bool FieldsAreValid => true;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record AgentRenamedEventPayload(string? ActorId) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string Kind => "agent_renamed";
    [JsonIgnore] public override string DiagnosticDetail => $"{ActorId}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => [];
    [JsonIgnore] public override bool FieldsAreValid => true;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record BorrowedGoodsReturnedEventPayload(string? ActorId, [property: JsonRequired] string ItemKind, [property: JsonRequired] int Quantity, [property: JsonRequired] string OwnerId) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string Kind => "borrowed_goods_returned";
    [JsonIgnore] public override string DiagnosticDetail => $"{ActorId}|{ItemKind}|{Quantity}|{OwnerId}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => new string?[] { OwnerId }.Where(id => !string.IsNullOrEmpty(id)).Select(id => id!).ToArray();
    [JsonIgnore] public override bool FieldsAreValid => ItemKind is not null && OwnerId is not null;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CaregiverAcceptedEventPayload(string? ActorId) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string Kind => "caregiver_accepted";
    [JsonIgnore] public override string DiagnosticDetail => $"{ActorId}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => [];
    [JsonIgnore] public override bool FieldsAreValid => true;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CaregiverAssignedEventPayload(string? ActorId) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string Kind => "caregiver_assigned";
    [JsonIgnore] public override string DiagnosticDetail => $"{ActorId}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => [];
    [JsonIgnore] public override bool FieldsAreValid => true;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CaregiverEndedEventPayload(string? ActorId) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string Kind => "caregiver_ended";
    [JsonIgnore] public override string DiagnosticDetail => $"{ActorId}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => [];
    [JsonIgnore] public override bool FieldsAreValid => true;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CaregiverProposalExpiredEventPayload(string? ActorId, [property: JsonRequired] string TargetId) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string Kind => "caregiver_proposal_expired";
    [JsonIgnore] public override string DiagnosticDetail => $"{TargetId}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => new string?[] { TargetId }.Where(id => !string.IsNullOrEmpty(id)).Select(id => id!).ToArray();
    [JsonIgnore] public override bool FieldsAreValid => TargetId is not null;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CaregiverProposedEventPayload(string? ActorId) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string Kind => "caregiver_proposed";
    [JsonIgnore] public override string DiagnosticDetail => $"{ActorId}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => [];
    [JsonIgnore] public override bool FieldsAreValid => true;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CaregiverRefusedEventPayload(string? ActorId) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string Kind => "caregiver_refused";
    [JsonIgnore] public override string DiagnosticDetail => $"{ActorId}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => [];
    [JsonIgnore] public override bool FieldsAreValid => true;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ChildBornEventPayload(string? ActorId) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string Kind => "child_born";
    [JsonIgnore] public override string DiagnosticDetail => $"{ActorId}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => [];
    [JsonIgnore] public override bool FieldsAreValid => true;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ChildCaredForEventPayload(string? ActorId) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string Kind => "child_cared_for";
    [JsonIgnore] public override string DiagnosticDetail => $"{ActorId}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => [];
    [JsonIgnore] public override bool FieldsAreValid => true;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ChildCollectedHouseholdEventPayload(string? ActorId, [property: JsonRequired] string ItemKind, [property: JsonRequired] int Quantity) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string Kind => "child_collected_household";
    [JsonIgnore] public override string DiagnosticDetail => $"{ActorId}:{ItemKind}:{Quantity}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => [];
    [JsonIgnore] public override bool FieldsAreValid => ItemKind is not null;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ChildConverseEventPayload(string? ActorId, [property: JsonRequired] string TargetId) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string Kind => "child_converse";
    [JsonIgnore] public override string DiagnosticDetail => $"{ActorId}:{TargetId}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => new string?[] { TargetId }.Where(id => !string.IsNullOrEmpty(id)).Select(id => id!).ToArray();
    [JsonIgnore] public override bool FieldsAreValid => TargetId is not null;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ChildDeliveredHouseholdEventPayload(string? ActorId, [property: JsonRequired] string ItemKind, [property: JsonRequired] int Quantity, [property: JsonRequired] string HouseInstanceId) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string Kind => "child_delivered_household";
    [JsonIgnore] public override string DiagnosticDetail => $"{ActorId}:{ItemKind}:{Quantity}:{HouseInstanceId}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => new string?[] { HouseInstanceId }.Where(id => !string.IsNullOrEmpty(id)).Select(id => id!).ToArray();
    [JsonIgnore] public override bool FieldsAreValid => ItemKind is not null && HouseInstanceId is not null;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ChildHelpedHouseholdEventPayload(string? ActorId) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string Kind => "child_helped_household";
    [JsonIgnore] public override string DiagnosticDetail => $"{ActorId}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => [];
    [JsonIgnore] public override bool FieldsAreValid => true;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ChildLearnEventPayload(string? ActorId, [property: JsonRequired] string TargetId) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string Kind => "child_learn";
    [JsonIgnore] public override string DiagnosticDetail => $"{ActorId}:{TargetId}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => new string?[] { TargetId }.Where(id => !string.IsNullOrEmpty(id)).Select(id => id!).ToArray();
    [JsonIgnore] public override bool FieldsAreValid => TargetId is not null;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ChildModelSelectedEventPayload(string? ActorId, [property: JsonRequired] string ChoiceReason) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string Kind => "child_model_selected";
    [JsonIgnore] public override string DiagnosticDetail => $"{ActorId}:{ChoiceReason}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => [];
    [JsonIgnore] public override bool FieldsAreValid => ChoiceReason is not null;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ChildPlayEventPayload(string? ActorId, [property: JsonRequired] string TargetId) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string Kind => "child_play";
    [JsonIgnore] public override string DiagnosticDetail => $"{ActorId}:{TargetId}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => new string?[] { TargetId }.Where(id => !string.IsNullOrEmpty(id)).Select(id => id!).ToArray();
    [JsonIgnore] public override bool FieldsAreValid => TargetId is not null;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ContinuityPlanProceededEventPayload(string? ActorId) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string Kind => "continuity_plan_proceeded";
    [JsonIgnore] public override string DiagnosticDetail => $"{ActorId}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => [];
    [JsonIgnore] public override bool FieldsAreValid => true;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ContinuityRuleOffEventPayload(string? ActorId, [property: JsonRequired] int EligibleCouples, [property: JsonRequired] int Threshold) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string Kind => "continuity_rule_off";
    [JsonIgnore] public override string DiagnosticDetail => $"eligible_couples|{EligibleCouples}|threshold|{Threshold}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => [];
    [JsonIgnore] public override bool FieldsAreValid => true;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ContinuityRuleOnEventPayload(string? ActorId, [property: JsonRequired] int EligibleCouples, [property: JsonRequired] int Threshold) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string Kind => "continuity_rule_on";
    [JsonIgnore] public override string DiagnosticDetail => $"eligible_couples|{EligibleCouples}|threshold|{Threshold}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => [];
    [JsonIgnore] public override bool FieldsAreValid => true;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ConversationAcceptedEventPayload(string? ActorId, [property: JsonRequired] string ConversationId) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string Kind => "conversation_accepted";
    [JsonIgnore] public override string DiagnosticDetail => $"{ConversationId}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => new string?[] { ConversationId }.Where(id => !string.IsNullOrEmpty(id)).Select(id => id!).ToArray();
    [JsonIgnore] public override bool FieldsAreValid => ConversationId is not null;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ConversationActionRejectedEventPayload(string? ActorId, [property: JsonRequired] string Action) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string Kind => "conversation_action_rejected";
    [JsonIgnore] public override string DiagnosticDetail => $"{ActorId}:{Action}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => [];
    [JsonIgnore] public override bool FieldsAreValid => Action is not null;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ConversationAgreementAcceptedEventPayload(string? ActorId, [property: JsonRequired] string ConversationId) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string Kind => "conversation_agreement_accepted";
    [JsonIgnore] public override string DiagnosticDetail => $"{ConversationId}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => new string?[] { ConversationId }.Where(id => !string.IsNullOrEmpty(id)).Select(id => id!).ToArray();
    [JsonIgnore] public override bool FieldsAreValid => ConversationId is not null;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ConversationAgreementPendingEventPayload(string? ActorId, [property: JsonRequired] string ConversationId) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string Kind => "conversation_agreement_pending";
    [JsonIgnore] public override string DiagnosticDetail => $"{ConversationId}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => new string?[] { ConversationId }.Where(id => !string.IsNullOrEmpty(id)).Select(id => id!).ToArray();
    [JsonIgnore] public override bool FieldsAreValid => ConversationId is not null;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ConversationClosedEventPayload(string? ActorId, [property: JsonRequired] string ConversationId, [property: JsonRequired] string Reason) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string Kind => "conversation_closed";
    [JsonIgnore] public override string DiagnosticDetail => $"{ConversationId}:{Reason}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => new string?[] { ConversationId }.Where(id => !string.IsNullOrEmpty(id)).Select(id => id!).ToArray();
    [JsonIgnore] public override bool FieldsAreValid => ConversationId is not null && Reason is not null;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ConversationInterruptedEventPayload(string? ActorId, [property: JsonRequired] string ConversationId, [property: JsonRequired] string Reason) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string Kind => "conversation_interrupted";
    [JsonIgnore] public override string DiagnosticDetail => $"{ConversationId}:{Reason}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => new string?[] { ConversationId }.Where(id => !string.IsNullOrEmpty(id)).Select(id => id!).ToArray();
    [JsonIgnore] public override bool FieldsAreValid => ConversationId is not null && Reason is not null;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ConversationProposedEventPayload(string? ActorId, [property: JsonRequired] string ConversationId) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string Kind => "conversation_proposed";
    [JsonIgnore] public override string DiagnosticDetail => $"{ConversationId}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => new string?[] { ConversationId }.Where(id => !string.IsNullOrEmpty(id)).Select(id => id!).ToArray();
    [JsonIgnore] public override bool FieldsAreValid => ConversationId is not null;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ConversationResumeRequestedEventPayload(string? ActorId, [property: JsonRequired] string ConversationId) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string Kind => "conversation_resume_requested";
    [JsonIgnore] public override string DiagnosticDetail => $"{ConversationId}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => new string?[] { ConversationId }.Where(id => !string.IsNullOrEmpty(id)).Select(id => id!).ToArray();
    [JsonIgnore] public override bool FieldsAreValid => ConversationId is not null;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ConversationResumedEventPayload(string? ActorId, [property: JsonRequired] string ConversationId) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string Kind => "conversation_resumed";
    [JsonIgnore] public override string DiagnosticDetail => $"{ConversationId}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => new string?[] { ConversationId }.Where(id => !string.IsNullOrEmpty(id)).Select(id => id!).ToArray();
    [JsonIgnore] public override bool FieldsAreValid => ConversationId is not null;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ConversationTurnAdmittedEventPayload(string? ActorId, [property: JsonRequired] string ConversationId, [property: JsonRequired] string TurnId) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string Kind => "conversation_turn_admitted";
    [JsonIgnore] public override string DiagnosticDetail => $"{ConversationId}:{TurnId}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => new string?[] { ConversationId, TurnId }.Where(id => !string.IsNullOrEmpty(id)).Select(id => id!).ToArray();
    [JsonIgnore] public override bool FieldsAreValid => ConversationId is not null && TurnId is not null;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ConversationTurnStartedEventPayload(string? ActorId, [property: JsonRequired] string ConversationId, [property: JsonRequired] string SpeakerId) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string Kind => "conversation_turn_started";
    [JsonIgnore] public override string DiagnosticDetail => $"{ConversationId}:{SpeakerId}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => new string?[] { ConversationId, SpeakerId }.Where(id => !string.IsNullOrEmpty(id)).Select(id => id!).ToArray();
    [JsonIgnore] public override bool FieldsAreValid => ConversationId is not null && SpeakerId is not null;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record EstateWillAcceptedEventPayload(string? ActorId, [property: JsonRequired] string EstateId, [property: JsonRequired] string Split, [property: JsonRequired] int HeirCount) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string Kind => "estate_will_accepted";
    [JsonIgnore] public override string DiagnosticDetail => $"{EstateId}:{Split}:{HeirCount}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => new string?[] { EstateId }.Where(id => !string.IsNullOrEmpty(id)).Select(id => id!).ToArray();
    [JsonIgnore] public override bool FieldsAreValid => EstateId is not null && Split is not null;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record EstateWillDefaultEventPayload(string? ActorId, [property: JsonRequired] string EstateId, [property: JsonRequired] string Reason) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string Kind => "estate_will_default";
    [JsonIgnore] public override string DiagnosticDetail => $"{EstateId}:{Reason}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => new string?[] { EstateId }.Where(id => !string.IsNullOrEmpty(id)).Select(id => id!).ToArray();
    [JsonIgnore] public override bool FieldsAreValid => EstateId is not null && Reason is not null;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record EstateWillStartedEventPayload(string? ActorId, [property: JsonRequired] string EstateId) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string Kind => "estate_will_started";
    [JsonIgnore] public override string DiagnosticDetail => $"{EstateId}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => new string?[] { EstateId }.Where(id => !string.IsNullOrEmpty(id)).Select(id => id!).ToArray();
    [JsonIgnore] public override bool FieldsAreValid => EstateId is not null;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record GuardianAssignedEventPayload(string? ActorId) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string Kind => "guardian_assigned";
    [JsonIgnore] public override string DiagnosticDetail => $"{ActorId}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => [];
    [JsonIgnore] public override bool FieldsAreValid => true;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record GuardianNeededEventPayload(string? ActorId) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string Kind => "guardian_needed";
    [JsonIgnore] public override string DiagnosticDetail => $"{ActorId}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => [];
    [JsonIgnore] public override bool FieldsAreValid => true;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record GuardianPlacementCancelledEventPayload(string? ActorId) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string Kind => "guardian_placement_cancelled";
    [JsonIgnore] public override string DiagnosticDetail => $"{ActorId}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => [];
    [JsonIgnore] public override bool FieldsAreValid => true;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record GuardianPlacementCompletedEventPayload(string? ActorId, [property: JsonRequired] string CaregiverId) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string Kind => "guardian_placement_completed";
    [JsonIgnore] public override string DiagnosticDetail => $"{ActorId}:{CaregiverId}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => new string?[] { CaregiverId }.Where(id => !string.IsNullOrEmpty(id)).Select(id => id!).ToArray();
    [JsonIgnore] public override bool FieldsAreValid => CaregiverId is not null;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record GuardianPlacementPendingEventPayload(string? ActorId) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string Kind => "guardian_placement_pending";
    [JsonIgnore] public override string DiagnosticDetail => $"{ActorId}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => [];
    [JsonIgnore] public override bool FieldsAreValid => true;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record HouseholdDeliveryRecoveredEventPayload(string? ActorId, [property: JsonRequired] string StockId, [property: JsonRequired] int PhysicalQuantity) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string Kind => "household_delivery_recovered";
    [JsonIgnore] public override string DiagnosticDetail => $"{ActorId}:{StockId}:{PhysicalQuantity}:pile";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => new string?[] { StockId }.Where(id => !string.IsNullOrEmpty(id)).Select(id => id!).ToArray();
    [JsonIgnore] public override bool FieldsAreValid => StockId is not null;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record HouseholdFoodCollectedEventPayload(string? ActorId, [property: JsonRequired] string LotId) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string Kind => "household_food_collected";
    [JsonIgnore] public override string DiagnosticDetail => $"{ActorId}:{LotId}:1";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => new string?[] { LotId }.Where(id => !string.IsNullOrEmpty(id)).Select(id => id!).ToArray();
    [JsonIgnore] public override bool FieldsAreValid => LotId is not null;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record HouseholdFoodStoredEventPayload(string? ActorId, [property: JsonRequired] string FoodId, [property: JsonRequired] int Quantity, [property: JsonRequired] string HouseInstanceId) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string Kind => "household_food_stored";
    [JsonIgnore] public override string DiagnosticDetail => $"{ActorId}:{FoodId}:{Quantity}:{HouseInstanceId}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => new string?[] { FoodId, HouseInstanceId }.Where(id => !string.IsNullOrEmpty(id)).Select(id => id!).ToArray();
    [JsonIgnore] public override bool FieldsAreValid => FoodId is not null && HouseInstanceId is not null;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record HouseholdFoundedEventPayload(string? ActorId, [property: JsonRequired] string HouseholdId) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string Kind => "household_founded";
    [JsonIgnore] public override string DiagnosticDetail => $"{ActorId}|{HouseholdId}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => new string?[] { HouseholdId }.Where(id => !string.IsNullOrEmpty(id)).Select(id => id!).ToArray();
    [JsonIgnore] public override bool FieldsAreValid => HouseholdId is not null;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record HouseholdJoinedEventPayload(string? ActorId, [property: JsonRequired] string HouseholdId) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string Kind => "household_joined";
    [JsonIgnore] public override string DiagnosticDetail => $"{ActorId}:{HouseholdId}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => new string?[] { HouseholdId }.Where(id => !string.IsNullOrEmpty(id)).Select(id => id!).ToArray();
    [JsonIgnore] public override bool FieldsAreValid => HouseholdId is not null;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record HouseholdLeftEventPayload(string? ActorId, [property: JsonRequired] string HouseholdId, [property: JsonRequired] string Cause, [property: JsonRequired] int Allowance) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string Kind => "household_left";
    [JsonIgnore] public override string DiagnosticDetail => $"{ActorId}|{HouseholdId}|{Cause}|{Allowance}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => new string?[] { HouseholdId }.Where(id => !string.IsNullOrEmpty(id)).Select(id => id!).ToArray();
    [JsonIgnore] public override bool FieldsAreValid => HouseholdId is not null && Cause is not null;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record HouseholdPreparationToolStoredEventPayload(string? ActorId, [property: JsonRequired] string ItemKind, [property: JsonRequired] int StoredQuantity, [property: JsonRequired] string HouseForPickupInstanceId) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string Kind => "household_preparation_tool_stored";
    [JsonIgnore] public override string DiagnosticDetail => $"{ActorId}:{ItemKind}:{StoredQuantity}:{HouseForPickupInstanceId}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => new string?[] { HouseForPickupInstanceId }.Where(id => !string.IsNullOrEmpty(id)).Select(id => id!).ToArray();
    [JsonIgnore] public override bool FieldsAreValid => ItemKind is not null && HouseForPickupInstanceId is not null;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record HouseholdStockDeliveredEventPayload(string? ActorId, [property: JsonRequired] string CarriedId, [property: JsonRequired] int DeliveredQuantity, [property: JsonRequired] string HouseInstanceId) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string Kind => "household_stock_delivered";
    [JsonIgnore] public override string DiagnosticDetail => $"{ActorId}:{CarriedId}:{DeliveredQuantity}:{HouseInstanceId}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => new string?[] { CarriedId, HouseInstanceId }.Where(id => !string.IsNullOrEmpty(id)).Select(id => id!).ToArray();
    [JsonIgnore] public override bool FieldsAreValid => CarriedId is not null && HouseInstanceId is not null;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record HouseholdStockPickedUpEventPayload(string? ActorId, [property: JsonRequired] string StockId, [property: JsonRequired] int Quantity, [property: JsonRequired] string HouseForPickupInstanceId) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string Kind => "household_stock_picked_up";
    [JsonIgnore] public override string DiagnosticDetail => $"{ActorId}:{StockId}:{Quantity}:{HouseForPickupInstanceId}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => new string?[] { StockId, HouseForPickupInstanceId }.Where(id => !string.IsNullOrEmpty(id)).Select(id => id!).ToArray();
    [JsonIgnore] public override bool FieldsAreValid => StockId is not null && HouseForPickupInstanceId is not null;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record HouseholdWorkResumedEventPayload(string? ActorId, [property: JsonRequired] string JobId, [property: JsonRequired] string? HouseholdId) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string Kind => "household_work_resumed";
    [JsonIgnore] public override string DiagnosticDetail => $"{ActorId}|{JobId}|{HouseholdId}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => new string?[] { JobId, HouseholdId }.Where(id => !string.IsNullOrEmpty(id)).Select(id => id!).ToArray();
    [JsonIgnore] public override bool FieldsAreValid => JobId is not null;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record HousingAnswerRecordedEventPayload(string? ActorId, [property: JsonRequired] string ApplicantId) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string Kind => "housing_answer_recorded";
    [JsonIgnore] public override string DiagnosticDetail => $"{ActorId}:{ApplicantId}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => new string?[] { ApplicantId }.Where(id => !string.IsNullOrEmpty(id)).Select(id => id!).ToArray();
    [JsonIgnore] public override bool FieldsAreValid => ApplicantId is not null;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record HousingBlockedEventPayload(string? ActorId, [property: JsonRequired] string Blocker) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string Kind => "housing_blocked";
    [JsonIgnore] public override string DiagnosticDetail => $"{ActorId}:{Blocker}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => [];
    [JsonIgnore] public override bool FieldsAreValid => Blocker is not null;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record HousingRequestExpiredEventPayload(string? ActorId, [property: JsonRequired] string HouseholdId) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string Kind => "housing_request_expired";
    [JsonIgnore] public override string DiagnosticDetail => $"{ActorId}:{HouseholdId}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => new string?[] { HouseholdId }.Where(id => !string.IsNullOrEmpty(id)).Select(id => id!).ToArray();
    [JsonIgnore] public override bool FieldsAreValid => HouseholdId is not null;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record HousingRequestMadeEventPayload(string? ActorId, [property: JsonRequired] string HouseholdId) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string Kind => "housing_request_made";
    [JsonIgnore] public override string DiagnosticDetail => $"{ActorId}:{HouseholdId}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => new string?[] { HouseholdId }.Where(id => !string.IsNullOrEmpty(id)).Select(id => id!).ToArray();
    [JsonIgnore] public override bool FieldsAreValid => HouseholdId is not null;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record HousingRequestRefusedEventPayload(string? ActorId, [property: JsonRequired] string HouseholdId) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string Kind => "housing_request_refused";
    [JsonIgnore] public override string DiagnosticDetail => $"{ActorId}:{HouseholdId}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => new string?[] { HouseholdId }.Where(id => !string.IsNullOrEmpty(id)).Select(id => id!).ToArray();
    [JsonIgnore] public override bool FieldsAreValid => HouseholdId is not null;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record LessonAcceptedEventPayload(string? ActorId) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string Kind => "lesson_accepted";
    [JsonIgnore] public override string DiagnosticDetail => $"{ActorId}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => [];
    [JsonIgnore] public override bool FieldsAreValid => true;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record LessonCancelledEventPayload(string? ActorId) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string Kind => "lesson_cancelled";
    [JsonIgnore] public override string DiagnosticDetail => $"{ActorId}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => [];
    [JsonIgnore] public override bool FieldsAreValid => true;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record LessonCompletedEventPayload(string? ActorId) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string Kind => "lesson_completed";
    [JsonIgnore] public override string DiagnosticDetail => $"{ActorId}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => [];
    [JsonIgnore] public override bool FieldsAreValid => true;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record LessonDeclinedEventPayload(string? ActorId) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string Kind => "lesson_declined";
    [JsonIgnore] public override string DiagnosticDetail => $"{ActorId}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => [];
    [JsonIgnore] public override bool FieldsAreValid => true;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record LessonPausedEventPayload(string? ActorId) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string Kind => "lesson_paused";
    [JsonIgnore] public override string DiagnosticDetail => $"{ActorId}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => [];
    [JsonIgnore] public override bool FieldsAreValid => true;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record LessonRequestedEventPayload(string? ActorId) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string Kind => "lesson_requested";
    [JsonIgnore] public override string DiagnosticDetail => $"{ActorId}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => [];
    [JsonIgnore] public override bool FieldsAreValid => true;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record LessonTrainingEventPayload(string? ActorId) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string Kind => "lesson_training";
    [JsonIgnore] public override string DiagnosticDetail => $"{ActorId}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => [];
    [JsonIgnore] public override bool FieldsAreValid => true;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record MarriageAcceptedEventPayload(string? ActorId, [property: JsonRequired] string OtherAgentId, [property: JsonRequired] string InitiatorName, [property: JsonRequired] string InviteeName) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string Kind => "marriage_accepted";
    [JsonIgnore] public override string DiagnosticDetail => $"{InitiatorName} and {InviteeName} agreed to marry; their shared surname is still undecided.";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => new string?[] { OtherAgentId }.Where(id => !string.IsNullOrEmpty(id)).Select(id => id!).ToArray();
    [JsonIgnore] public override bool FieldsAreValid => OtherAgentId is not null && InitiatorName is not null && InviteeName is not null;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record MarriageEndedEventPayload(string? ActorId, [property: JsonRequired] string OtherAgentId, [property: JsonRequired] string FirstName, [property: JsonRequired] string SecondName, [property: JsonRequired] bool EndedByDeath) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string Kind => "marriage_ended";
    [JsonIgnore] public override string DiagnosticDetail => EndedByDeath ? $"The marriage between {FirstName} and {SecondName} ended after a partner died." : $"The marriage between {FirstName} and {SecondName} ended by separation.";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => new string?[] { OtherAgentId }.Where(id => !string.IsNullOrEmpty(id)).Select(id => id!).ToArray();
    [JsonIgnore] public override bool FieldsAreValid => OtherAgentId is not null && FirstName is not null && SecondName is not null;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record MarriageSurnameAgreedEventPayload(string? ActorId, [property: JsonRequired] string OtherAgentId, [property: JsonRequired] string CoupleName, [property: JsonRequired] string Surname) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string Kind => "marriage_surname_agreed";
    [JsonIgnore] public override string DiagnosticDetail => $"{CoupleName} completed their marriage and agreed to share the surname {Surname}.";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => new string?[] { OtherAgentId }.Where(id => !string.IsNullOrEmpty(id)).Select(id => id!).ToArray();
    [JsonIgnore] public override bool FieldsAreValid => OtherAgentId is not null && CoupleName is not null && Surname is not null;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record MarriageSurnameBlockedEventPayload(string? ActorId, [property: JsonRequired] string ConversationId) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string Kind => "marriage_surname_blocked";
    [JsonIgnore] public override string DiagnosticDetail => $"{ConversationId}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => new string?[] { ConversationId }.Where(id => !string.IsNullOrEmpty(id)).Select(id => id!).ToArray();
    [JsonIgnore] public override bool FieldsAreValid => ConversationId is not null;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record MarriageSurnameDrawEventPayload(string? ActorId, [property: JsonRequired] string OtherAgentId, [property: JsonRequired] string CoupleName, [property: JsonRequired] string Surname) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string Kind => "marriage_surname_draw";
    [JsonIgnore] public override string DiagnosticDetail => $"{CoupleName} completed their marriage. A draw chose {Surname} after four turns without agreement.";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => new string?[] { OtherAgentId }.Where(id => !string.IsNullOrEmpty(id)).Select(id => id!).ToArray();
    [JsonIgnore] public override bool FieldsAreValid => OtherAgentId is not null && CoupleName is not null && Surname is not null;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record MedicalCareAllowedEventPayload(string? ActorId, [property: JsonRequired] string CaregiverId) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string Kind => "medical_care_allowed";
    [JsonIgnore] public override string DiagnosticDetail => $"{ActorId}:medical_caregiver:{CaregiverId}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => new string?[] { CaregiverId }.Where(id => !string.IsNullOrEmpty(id)).Select(id => id!).ToArray();
    [JsonIgnore] public override bool FieldsAreValid => CaregiverId is not null;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record MedicalCareRevokedEventPayload(string? ActorId, [property: JsonRequired] string CaregiverId) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string Kind => "medical_care_revoked";
    [JsonIgnore] public override string DiagnosticDetail => $"{ActorId}:medical_caregiver:{CaregiverId}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => new string?[] { CaregiverId }.Where(id => !string.IsNullOrEmpty(id)).Select(id => id!).ToArray();
    [JsonIgnore] public override bool FieldsAreValid => CaregiverId is not null;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record MedicalTreatmentCompletedEventPayload(string? ActorId) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string Kind => "medical_treatment_completed";
    [JsonIgnore] public override string DiagnosticDetail => $"{ActorId}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => [];
    [JsonIgnore] public override bool FieldsAreValid => true;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record MedicalTreatmentInterruptedEventPayload(string? ActorId) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string Kind => "medical_treatment_interrupted";
    [JsonIgnore] public override string DiagnosticDetail => $"{ActorId}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => [];
    [JsonIgnore] public override bool FieldsAreValid => true;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record MedicalTreatmentStartedEventPayload(string? ActorId, [property: JsonRequired] string Caregiver, [property: JsonRequired] string SupplyId) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string Kind => "medical_treatment_started";
    [JsonIgnore] public override string DiagnosticDetail => $"{ActorId}:medicine:{Caregiver}:{SupplyId}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => new string?[] { SupplyId }.Where(id => !string.IsNullOrEmpty(id)).Select(id => id!).ToArray();
    [JsonIgnore] public override bool FieldsAreValid => Caregiver is not null && SupplyId is not null;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ParenthoodCancelledEventPayload(string? ActorId) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string Kind => "parenthood_cancelled";
    [JsonIgnore] public override string DiagnosticDetail => $"{ActorId}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => [];
    [JsonIgnore] public override bool FieldsAreValid => true;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ParenthoodCompletedEventPayload(string? ActorId) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string Kind => "parenthood_completed";
    [JsonIgnore] public override string DiagnosticDetail => $"{ActorId}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => [];
    [JsonIgnore] public override bool FieldsAreValid => true;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ParenthoodPostponedEventPayload(string? ActorId) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string Kind => "parenthood_postponed";
    [JsonIgnore] public override string DiagnosticDetail => $"{ActorId}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => [];
    [JsonIgnore] public override bool FieldsAreValid => true;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ParenthoodPreparingEventPayload(string? ActorId) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string Kind => "parenthood_preparing";
    [JsonIgnore] public override string DiagnosticDetail => $"{ActorId}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => [];
    [JsonIgnore] public override bool FieldsAreValid => true;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ParenthoodRequestedEventPayload(string? ActorId) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string Kind => "parenthood_requested";
    [JsonIgnore] public override string DiagnosticDetail => $"{ActorId}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => [];
    [JsonIgnore] public override bool FieldsAreValid => true;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record PartnershipAcceptedEventPayload(string? ActorId) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string Kind => "partnership_accepted";
    [JsonIgnore] public override string DiagnosticDetail => $"{ActorId}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => [];
    [JsonIgnore] public override bool FieldsAreValid => true;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record PartnershipEndedEventPayload(string? ActorId) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string Kind => "partnership_ended";
    [JsonIgnore] public override string DiagnosticDetail => $"{ActorId}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => [];
    [JsonIgnore] public override bool FieldsAreValid => true;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record PartnershipExpiredEventPayload(string? ActorId) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string Kind => "partnership_expired";
    [JsonIgnore] public override string DiagnosticDetail => $"{ActorId}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => [];
    [JsonIgnore] public override bool FieldsAreValid => true;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record PartnershipProposedEventPayload(string? ActorId) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string Kind => "partnership_proposed";
    [JsonIgnore] public override string DiagnosticDetail => $"{ActorId}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => [];
    [JsonIgnore] public override bool FieldsAreValid => true;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record PartnershipRefusedEventPayload(string? ActorId) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string Kind => "partnership_refused";
    [JsonIgnore] public override string DiagnosticDetail => $"{ActorId}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => [];
    [JsonIgnore] public override bool FieldsAreValid => true;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record PersonalGoodsCollectedEventPayload(string? ActorId, [property: JsonRequired] string ItemKind, [property: JsonRequired] int Quantity, [property: JsonRequired] string OwnerId) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string Kind => "personal_goods_collected";
    [JsonIgnore] public override string DiagnosticDetail => $"{ActorId}|{ItemKind}|{Quantity}|{OwnerId}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => new string?[] { OwnerId }.Where(id => !string.IsNullOrEmpty(id)).Select(id => id!).ToArray();
    [JsonIgnore] public override bool FieldsAreValid => ItemKind is not null && OwnerId is not null;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record PersonalGoodsStoredEventPayload(string? ActorId, [property: JsonRequired] string ItemKind, [property: JsonRequired] int Quantity) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string Kind => "personal_goods_stored";
    [JsonIgnore] public override string DiagnosticDetail => $"{ActorId}|{ItemKind}|{Quantity}|{ActorId}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => [];
    [JsonIgnore] public override bool FieldsAreValid => ItemKind is not null;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record RelocationCancelledEventPayload(string? ActorId, [property: JsonRequired] string HouseholdId, [property: JsonRequired] string Reason) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string Kind => "relocation_cancelled";
    [JsonIgnore] public override string DiagnosticDetail => $"{ActorId}|{HouseholdId}|{Reason}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => new string?[] { HouseholdId }.Where(id => !string.IsNullOrEmpty(id)).Select(id => id!).ToArray();
    [JsonIgnore] public override bool FieldsAreValid => HouseholdId is not null && Reason is not null;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record RelocationNoticeEventPayload(string? ActorId, [property: JsonRequired] string HouseholdId, [property: JsonRequired] string Reason, [property: JsonRequired] long Deadline) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string Kind => "relocation_notice";
    [JsonIgnore] public override string DiagnosticDetail => $"{ActorId}|{HouseholdId}|{Reason}|{Deadline}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => new string?[] { HouseholdId }.Where(id => !string.IsNullOrEmpty(id)).Select(id => id!).ToArray();
    [JsonIgnore] public override bool FieldsAreValid => HouseholdId is not null && Reason is not null;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record SkillLearnedEventPayload(string? ActorId, [property: JsonRequired] string Skill, [property: JsonRequired] string TeacherId) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string Kind => "skill_learned";
    [JsonIgnore] public override string DiagnosticDetail => $"{ActorId}|{Skill}|{TeacherId}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => new string?[] { TeacherId }.Where(id => !string.IsNullOrEmpty(id)).Select(id => id!).ToArray();
    [JsonIgnore] public override bool FieldsAreValid => Skill is not null && TeacherId is not null;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record SocialStandingChangedEventPayload(string? ActorId, [property: JsonRequired] string SubjectId, [property: JsonRequired] string Reason) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string Kind => "social_standing_changed";
    [JsonIgnore] public override string DiagnosticDetail => $"{ActorId}:{SubjectId}:{Reason}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => new string?[] { SubjectId }.Where(id => !string.IsNullOrEmpty(id)).Select(id => id!).ToArray();
    [JsonIgnore] public override bool FieldsAreValid => SubjectId is not null && Reason is not null;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record AgentSleptEventPayload(string? ActorId) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string Kind => "agent_slept";
    [JsonIgnore] public override string DiagnosticDetail => ActorId ?? "";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => [];
    [JsonIgnore] public override bool FieldsAreValid => true;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record AgentBuildingProposedEventPayload(string? ActorId) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string Kind => "agent_building_proposed";
    [JsonIgnore] public override string DiagnosticDetail => ActorId ?? "";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => [];
    [JsonIgnore] public override bool FieldsAreValid => true;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record HousingRequestCancelledEventPayload(string? ActorId, [property: JsonRequired] string HouseholdId) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string Kind => "housing_request_cancelled";
    [JsonIgnore] public override string DiagnosticDetail => $"{ActorId}:{HouseholdId}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => new string?[] { HouseholdId }.Where(id => !string.IsNullOrEmpty(id)).Select(id => id!).ToArray();
    [JsonIgnore] public override bool FieldsAreValid => HouseholdId is not null;
}


[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record HousingRequestBlockedCapacityEventPayload(string? ActorId, [property: JsonRequired] string HouseholdId) : WorldEventPayload(ActorId)
{
    [JsonIgnore] public override string Kind => "housing_request_blocked_capacity";
    [JsonIgnore] public override string DiagnosticDetail => $"{ActorId}:{HouseholdId}";
    [JsonIgnore] public override IReadOnlyList<string> SubjectIds => new string?[] { HouseholdId }.Where(id => !string.IsNullOrEmpty(id)).Select(id => id!).ToArray();
    [JsonIgnore] public override bool FieldsAreValid => HouseholdId is not null;
}
