namespace ClankerWorld.Simulation.World;

internal sealed partial class AgentStore
{
    internal void ChooseIdentity(string actor, AgentState basis, string personality, string aspiration, bool identityChoicePending) => Commit(actor, basis with { Personality = personality, Aspiration = aspiration, IdentityChoicePending = identityChoicePending });

    internal void FinishHouseholdDeparture(string actor, AgentState basis, HousingState? housing, string? lastDecisionContext) => Commit(actor, basis with { Housing = housing, LastDecisionContext = lastDecisionContext });

    internal void FinishIdentityMoment(string actor, AgentState basis, string personality, string aspiration, string? lastDecisionContext) => Commit(actor, basis with { Personality = personality, Aspiration = aspiration, LastDecisionContext = lastDecisionContext });

    internal static AgentState PrepareForDeath(AgentState basis, MedicalTreatmentState? medicalTreatment, MedicalSupplyTrip? medicalSupplyTrip, GuardianPlacementState? guardianPlacement, PersonalEquipment? equipment) => basis with { MedicalTreatment = medicalTreatment, MedicalSupplyTrip = medicalSupplyTrip, GuardianPlacement = guardianPlacement, Equipment = equipment };

    internal void RecordDecisionContext(string actor, AgentState basis, string? lastDecisionContext) => Commit(actor, basis with { LastDecisionContext = lastDecisionContext });

    internal void RecordDepartureAllowance(string actor, AgentState basis, IReadOnlyList<HouseholdDeparture>? departures) => Commit(actor, basis with { Departures = departures });

    internal void RecordEquipment(string actor, AgentState basis, PersonalEquipment? equipment) => Commit(actor, basis with { Equipment = equipment });

    internal void RecordExploration(string actor, AgentState basis, ExplorationState? exploration) => Commit(actor, basis with { Exploration = exploration });

    internal static AgentState PrepareExploration(AgentState basis, ExplorationState? exploration) => basis with { Exploration = exploration };

    internal void RecordFullness(string actor, AgentState basis, int fullnessBasisPoints) => Commit(actor, basis with { FullnessBasisPoints = fullnessBasisPoints });

    internal static AgentState PrepareFullness(AgentState basis, int fullnessBasisPoints) => basis with { FullnessBasisPoints = fullnessBasisPoints };

    internal void RecordGuardianPlacement(string actor, AgentState basis, GuardianPlacementState? guardianPlacement) => Commit(actor, basis with { GuardianPlacement = guardianPlacement });

    internal void RecordGuardianSearch(string actor, AgentState basis, GuardianSearchState? guardianSearch) => Commit(actor, basis with { GuardianSearch = guardianSearch });

    internal void RecordHouseholdDeparture(string actor, AgentState basis, AgentProject? project, HousingState? housing, string? lastDecisionContext) => Commit(actor, basis with { Project = project, Housing = housing, LastDecisionContext = lastDecisionContext });

    internal void RecordHousing(string actor, AgentState basis, HousingState? housing) => Commit(actor, basis with { Housing = housing });

    internal void RecordIdentityMoments(string actor, AgentState basis, IReadOnlyList<AgentIdentityMoment>? identityMoments) => Commit(actor, basis with { IdentityMoments = identityMoments });

    internal void RecordLesson(string actor, AgentState basis, SkillLesson? lesson) => Commit(actor, basis with { Lesson = lesson });

    internal void RecordMeal(string actor, AgentState basis, int fullnessBasisPoints, SurvivalCondition? survival) => Commit(actor, basis with { FullnessBasisPoints = fullnessBasisPoints, Survival = survival });

    internal static AgentState PrepareMeal(AgentState basis, int fullnessBasisPoints, SurvivalCondition? survival) => basis with { FullnessBasisPoints = fullnessBasisPoints, Survival = survival };

    internal void RecordMedicalConsent(string actor, AgentState basis, MedicalConsentState? medicalConsent, MedicalTreatmentState? medicalTreatment) => Commit(actor, basis with { MedicalConsent = medicalConsent, MedicalTreatment = medicalTreatment });

    internal void RecordMedicalRecovery(string actor, AgentState basis, SurvivalCondition? survival, MedicalTreatmentState? medicalTreatment) => Commit(actor, basis with { Survival = survival, MedicalTreatment = medicalTreatment });

    internal void RecordMedicalSupplyTrip(string actor, AgentState basis, MedicalSupplyTrip? medicalSupplyTrip) => Commit(actor, basis with { MedicalSupplyTrip = medicalSupplyTrip });

    internal void RecordMedicalTreatment(string actor, AgentState basis, MedicalTreatmentState? medicalTreatment) => Commit(actor, basis with { MedicalTreatment = medicalTreatment });

    internal void RecordModelAttempt(string actor, AgentState basis, ModelAttempt? lastModelAttempt) => Commit(actor, basis with { LastModelAttempt = lastModelAttempt });

    internal void RecordMovement(string actor, AgentState basis, GridPoint position, int moveWaitTicks, int travelCooldownTicks) => Commit(actor, basis with { Position = position, MoveWaitTicks = moveWaitTicks, TravelCooldownTicks = travelCooldownTicks });

    internal void RecordMovementWait(string actor, AgentState basis, int moveWaitTicks) => Commit(actor, basis with { MoveWaitTicks = moveWaitTicks });

    internal void RecordParenthood(string actor, AgentState basis, ParenthoodPlan? parenthood) => Commit(actor, basis with { Parenthood = parenthood });

    internal void RecordPosition(string actor, AgentState basis, GridPoint position) => Commit(actor, basis with { Position = position });

    internal void RecordProficiency(string actor, AgentState basis, WorkProficiency? proficiency) => Commit(actor, basis with { Proficiency = proficiency });

    internal void RecordProject(string actor, AgentState basis, AgentProject? project) => Commit(actor, basis with { Project = project });

    internal static AgentState PrepareProject(AgentState basis, AgentProject? project) => basis with { Project = project };

    internal void RecordRecentThoughts(string actor, AgentState basis, IReadOnlyList<RecentThought>? recentThoughts) => Commit(actor, basis with { RecentThoughts = recentThoughts });

    internal void RecordSkills(string actor, AgentState basis, IReadOnlyList<LearnedSkill>? skills) => Commit(actor, basis with { Skills = skills });

    internal void RecordSocialStanding(string actor, AgentState basis, IReadOnlyList<SocialStandingEntry>? socialStanding) => Commit(actor, basis with { SocialStanding = socialStanding });

    internal void RecordSurvival(string actor, AgentState basis, SurvivalCondition? survival) => Commit(actor, basis with { Survival = survival });

    internal static AgentState PrepareSurvival(AgentState basis, SurvivalCondition? survival) => basis with { Survival = survival };

    internal void RecordTravelCooldown(string actor, AgentState basis, int travelCooldownTicks) => Commit(actor, basis with { TravelCooldownTicks = travelCooldownTicks });

    internal void SelectChildModel(string actor, AgentState basis, ChildPersonalModelSelection? childModelSelection) => Commit(actor, basis with { ChildModelSelection = childModelSelection });

}
