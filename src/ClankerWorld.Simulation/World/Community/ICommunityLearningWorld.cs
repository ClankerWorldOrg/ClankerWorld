using System.Collections.ObjectModel;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

internal interface ICommunityLearningWorld
{
    AgentStore PhysicalAgents { get; }
    SocietyCheckpoint Society { get; }
    long WorldTick { get; }
    int ResourceInteractionRange { get; }
    GridPoint SharedStoragePosition { get; }
    bool HasSurvivalState { get; }
    bool IsAdult(string actor);
    bool HasSkill(string actor, SkillKind skill);
    ActorCapabilityState? CapabilitiesFor(string actor);
    bool NeedsUrgentFood(AgentState person);
    bool NeedsUrgentWarmth(AgentState person);
    OwnerQueuedInstruction? PendingInstructionFor(string actor);
    bool IsConversationBusy(string actor);
    bool HasCompetingLessonDecision(string actor);
    bool HasHousingDecision(string actor);
    bool ModelDecisionPending(string actor);
    bool IsWithinInteractionRange(GridPoint origin, GridPoint destination, int range);
    bool CanReach(string actor, GridPoint target, ReachPurpose purpose, int range, int load, SwimmingPolicy swimming);
    ReadOnlyCollection<GridPoint> Reach(string actor, GridPoint target, ReachPurpose purpose, int range, int load, SwimmingPolicy swimming);
    ActionCheck CheckActionPlan(ActionPlan plan);
    void MoveToward(string actor, AgentState person, GridPoint destination, string reason, int range);
    void RecordAttemptedAction(string actor, string candidate);
    void PauseProjectForLesson(string actor, AgentProject project);
    void GainSkill(string actor, SkillKind skill, string teacher);
    void IncreaseTrust(string actor, string other, int amount, string reason);
    void RecordLessonGratitude(string student, string teacher, SkillKind skill);
    void MarkCurrentSchema();
    void AppendEvent(string kind, string detail);
}
