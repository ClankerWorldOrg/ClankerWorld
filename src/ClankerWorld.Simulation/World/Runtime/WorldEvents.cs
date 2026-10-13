namespace ClankerWorld.Simulation.World;

internal static class WorldEvents
{
    public static WorldEventPayload Lesson(string kind, string? actor) => kind switch
    {
        "requested" => new LessonRequestedEventPayload(actor),
        "accepted" => new LessonAcceptedEventPayload(actor),
        "declined" => new LessonDeclinedEventPayload(actor),
        "paused" => new LessonPausedEventPayload(actor),
        "training" => new LessonTrainingEventPayload(actor),
        "completed" => new LessonCompletedEventPayload(actor),
        "cancelled" => new LessonCancelledEventPayload(actor),
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown event kind."),
    };

    public static WorldEventPayload Parenthood(string kind, string? actor) => kind switch
    {
        "requested" => new ParenthoodRequestedEventPayload(actor),
        "preparing" => new ParenthoodPreparingEventPayload(actor),
        "postponed" => new ParenthoodPostponedEventPayload(actor),
        "cancelled" => new ParenthoodCancelledEventPayload(actor),
        "completed" => new ParenthoodCompletedEventPayload(actor),
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown event kind."),
    };

    public static WorldEventPayload ToolRequest(string kind, string? actor, string RequestId) => kind switch
    {
        "tool_request_accepted" => new ToolRequestAcceptedEventPayload(actor, RequestId),
        "tool_request_refused" => new ToolRequestRefusedEventPayload(actor, RequestId),
        "tool_request_withdrawn" => new ToolRequestWithdrawnEventPayload(actor, RequestId),
        "tool_request_ready" => new ToolRequestReadyEventPayload(actor, RequestId),
        "tool_request_completed" => new ToolRequestCompletedEventPayload(actor, RequestId),
        "tool_request_interrupted" => new ToolRequestInterruptedEventPayload(actor, RequestId),
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown event kind."),
    };

    public static WorldEventPayload Housing(string kind, string? actor, string HouseholdId) => kind switch
    {
        "housing_request_refused" => new HousingRequestRefusedEventPayload(actor, HouseholdId),
        "housing_request_expired" => new HousingRequestExpiredEventPayload(actor, HouseholdId),
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown event kind."),
    };

    public static WorldEventPayload Child(string kind, string? actor, string TargetId) => kind switch
    {
        "converse" => new ChildConverseEventPayload(actor, TargetId),
        "play" => new ChildPlayEventPayload(actor, TargetId),
        "learn" => new ChildLearnEventPayload(actor, TargetId),
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown event kind."),
    };

    public static WorldEventPayload FarmStock(string kind, string? actor, string ResourceId, int Quantity, string DestinationId) => kind switch
    {
        "farm_flour_picked_up" => new FarmFlourPickedUpEventPayload(actor, ResourceId, Quantity, DestinationId),
        "farm_grain_picked_up" => new FarmGrainPickedUpEventPayload(actor, ResourceId, Quantity, DestinationId),
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown event kind."),
    };

    public static WorldEventPayload ProductionFailure(string kind, string? actor, string JobId) => kind switch
    {
        "production_worker_unavailable" => new ProductionWorkerUnavailableEventPayload(actor, JobId),
        "production_action_refused" => new ProductionActionRefusedEventPayload(actor, JobId),
        "production_output_unsupported" => new ProductionOutputUnsupportedEventPayload(actor, JobId),
        "production_input_unusable" => new ProductionInputUnusableEventPayload(actor, JobId),
        "production_tool_unusable" => new ProductionToolUnusableEventPayload(actor, JobId),
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown event kind."),
    };

    public static WorldEventPayload ContentStaged(string kind, string? actor, string PackageId) => kind switch
    {
        "restaurant_content_staged" => new RestaurantContentStagedEventPayload(actor, PackageId),
        "pottery_content_staged" => new PotteryContentStagedEventPayload(actor, PackageId),
        "silo_content_staged" => new SiloContentStagedEventPayload(actor, PackageId),
        "tailor_content_staged" => new TailorContentStagedEventPayload(actor, PackageId),
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown event kind."),
    };

    public static WorldEventPayload CivicNotice(string kind, string? actor, string TownId, string SubjectId, string NoticeText) => kind switch
    {
        "council" => new TownCivicCouncilEventPayload(actor, TownId, SubjectId, NoticeText),
        "election" => new TownCivicElectionEventPayload(actor, TownId, SubjectId, NoticeText),
        "runoff" => new TownCivicRunoffEventPayload(actor, TownId, SubjectId, NoticeText),
        "proposal" => new TownCivicProposalEventPayload(actor, TownId, SubjectId, NoticeText),
        "result" => new TownCivicResultEventPayload(actor, TownId, SubjectId, NoticeText),
        "cancelled" => new TownCivicCancelledEventPayload(actor, TownId, SubjectId, NoticeText),
        "law" => new TownCivicLawEventPayload(actor, TownId, SubjectId, NoticeText),
        "government" => new TownCivicGovernmentEventPayload(actor, TownId, SubjectId, NoticeText),
        "mayor" => new TownCivicMayorEventPayload(actor, TownId, SubjectId, NoticeText),
        "land_hearing" => new TownCivicLandHearingEventPayload(actor, TownId, SubjectId, NoticeText),
        "land_transfer" => new TownCivicLandTransferEventPayload(actor, TownId, SubjectId, NoticeText),
        "nonviolent_hearing" => new TownCivicNonviolentHearingEventPayload(actor, TownId, SubjectId, NoticeText),
        "law_case" => new TownCivicLawCaseEventPayload(actor, TownId, SubjectId, NoticeText),
        "remedy" => new TownCivicRemedyEventPayload(actor, TownId, SubjectId, NoticeText),
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown event kind."),
    };

    public static WorldEventPayload LandCase(string kind, string? actor, string TownId, string CaseId, int Revision, string Status) => kind switch
    {
        "opened" => new LandCaseOpenedEventPayload(actor, TownId, CaseId, Revision, Status),
        "notice" => new LandCaseNoticeEventPayload(actor, TownId, CaseId, Revision, Status),
        "evidence" => new LandCaseEvidenceEventPayload(actor, TownId, CaseId, Revision, Status),
        "response" => new LandCaseResponseEventPayload(actor, TownId, CaseId, Revision, Status),
        "judge_consent" => new LandCaseJudgeConsentEventPayload(actor, TownId, CaseId, Revision, Status),
        "judge_election" => new LandCaseJudgeElectionEventPayload(actor, TownId, CaseId, Revision, Status),
        "judge_assigned" => new LandCaseJudgeAssignedEventPayload(actor, TownId, CaseId, Revision, Status),
        "ruling" => new LandCaseRulingEventPayload(actor, TownId, CaseId, Revision, Status),
        "reopen_requested" => new LandCaseReopenRequestedEventPayload(actor, TownId, CaseId, Revision, Status),
        "reopened" => new LandCaseReopenedEventPayload(actor, TownId, CaseId, Revision, Status),
        "rejected" => new LandCaseRejectedEventPayload(actor, TownId, CaseId, Revision, Status),
        "inspected" => new LandCaseInspectedEventPayload(actor, TownId, CaseId, Revision, Status),
        "relayed" => new LandCaseRelayedEventPayload(actor, TownId, CaseId, Revision, Status),
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown event kind."),
    };

    public static WorldEventPayload LandTransfer(string kind, string? actor, string TownId, string RequestId, string Status) => kind switch
    {
        "proposed" => new LandTransferProposedEventPayload(actor, TownId, RequestId, Status),
        "read" => new LandTransferReadEventPayload(actor, TownId, RequestId, Status),
        "consent" => new LandTransferConsentEventPayload(actor, TownId, RequestId, Status),
        "withdrawn" => new LandTransferWithdrawnEventPayload(actor, TownId, RequestId, Status),
        "settled" => new LandTransferSettledEventPayload(actor, TownId, RequestId, Status),
        "blocked" => new LandTransferBlockedEventPayload(actor, TownId, RequestId, Status),
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown event kind."),
    };

    public static WorldEventPayload LawCase(string kind, string? actor, string TownId, string SubjectId) => kind switch
    {
        "opened" => new LawCaseOpenedEventPayload(actor, TownId, SubjectId),
        "evidence" => new LawCaseEvidenceEventPayload(actor, TownId, SubjectId),
        "response" => new LawCaseResponseEventPayload(actor, TownId, SubjectId),
        "inspected" => new LawCaseInspectedEventPayload(actor, TownId, SubjectId),
        "relayed" => new LawCaseRelayedEventPayload(actor, TownId, SubjectId),
        "judge_consent" => new LawCaseJudgeConsentEventPayload(actor, TownId, SubjectId),
        "judge_election" => new LawCaseJudgeElectionEventPayload(actor, TownId, SubjectId),
        "judge_assigned" => new LawCaseJudgeAssignedEventPayload(actor, TownId, SubjectId),
        "finding" => new LawCaseFindingEventPayload(actor, TownId, SubjectId),
        "reopen_requested" => new LawCaseReopenRequestedEventPayload(actor, TownId, SubjectId),
        "reopened" => new LawCaseReopenedEventPayload(actor, TownId, SubjectId),
        "rejected" => new LawCaseRejectedEventPayload(actor, TownId, SubjectId),
        "offer" => new LawCaseOfferEventPayload(actor, TownId, SubjectId),
        "offer_response" => new LawCaseOfferResponseEventPayload(actor, TownId, SubjectId),
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown event kind."),
    };

    public static WorldEventPayload FieldWork(FarmWorkKind kind, string actor, string fieldId, string? crop) => kind switch
    {
        FarmWorkKind.Till => new FieldPreparedEventPayload(actor, fieldId, crop),
        FarmWorkKind.Plant => new FieldPlantedEventPayload(actor, fieldId, crop),
        FarmWorkKind.Tend => new FieldTendedEventPayload(actor, fieldId, crop),
        FarmWorkKind.Harvest => new FieldHarvestedEventPayload(actor, fieldId, crop),
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    public static WorldEventPayload BuildingPlacement(string kind, string? actor, string subjectId, string definitionId, int x, int y, string? householdId) => kind switch
    {
        "building_placed" => new BuildingPlacedEventPayload(actor, subjectId, definitionId, null, x, y, householdId),
        "build_completed" => new BuildCompletedEventPayload(actor, subjectId, definitionId, null, x, y, householdId),
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    public static WorldEventPayload ProductionStarted(string kind, string? actor, string jobId, string recipeId, string buildingId) => kind switch
    {
        "recipe_started" => new RecipeStartedEventPayload(actor, jobId, recipeId, buildingId),
        "build_started" => new BuildStartedEventPayload(actor, jobId, recipeId, buildingId, null, null, null),
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };
}
