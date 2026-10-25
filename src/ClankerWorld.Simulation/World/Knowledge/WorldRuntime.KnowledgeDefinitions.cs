namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private static ActorCapabilityPolicy KnowledgeWritingCapabilities =>
        new(ActionLifeStages.Adults, AllowUrgentFood: false, AllowUrgentWarmth: false, AllowActiveOrder: true);
    private static ActorCapabilityPolicy KnowledgeReadingCapabilities(bool share) =>
        new(ActionLifeStages.Adults, AllowUrgentFood: share, AllowUrgentWarmth: false, AllowActiveOrder: true);
    private static ActorCapabilityPolicy KnowledgeStorageCapabilities =>
        new(ActionLifeStages.Adults, AllowUrgentFood: true, AllowUrgentWarmth: false, AllowActiveOrder: true);

    private static class KnowledgeActions
    {
        internal static readonly ActionDefinition<KnowledgeWritingChoice, KnowledgeWritingActionPlan> Writing = new(
            DecodeReferenceChoice(KnowledgeWritingChoice.Decode), static choice => choice.Encode(), KnowledgeWritingCapabilities,
            static (world, actor, choice, _) => world.PlanKnowledgeWriting(actor, choice.Encode()),
            static (world, plan, policy) => world.CheckKnowledgeWriting(plan, policy),
            static (world, plan, action, _) => { world.ApplyCheckedKnowledgeWriting(plan, action.State); return true; });

        internal static readonly ActionDefinition<KnowledgeReadingChoice, KnowledgeReadingActionPlan> Reading = ReadingDefinition(false);
        internal static readonly ActionDefinition<KnowledgeReadingChoice, KnowledgeReadingActionPlan> Sharing = ReadingDefinition(true);

        private static ActionDefinition<KnowledgeReadingChoice, KnowledgeReadingActionPlan> ReadingDefinition(bool share) => new(
            DecodeReferenceChoice(candidate => KnowledgeReadingChoice.Decode(candidate) is { } choice && choice.Share == share ? choice : null),
            static choice => choice.Encode(), KnowledgeReadingCapabilities(share),
            static (world, actor, choice, _) => world.HeldKnowledgeArtifacts(actor).FirstOrDefault(item => item.Id == choice.ArtifactId) is { } artifact
                ? new(actor, artifact, choice.Share ? choice.RecipientId : actor, choice.Share) : null,
            static (world, plan, policy) => world.CheckKnowledgeReading(plan, policy),
            static (world, plan, _, _) => { world.ApplyCheckedKnowledgeReading(plan); return true; });

        internal static readonly ActionDefinition<KnowledgeStorageChoice, KnowledgeStorageActionPlan> Storage = new(
            DecodeReferenceChoice(KnowledgeStorageChoice.Decode), static choice => choice.Encode(), KnowledgeStorageCapabilities,
            static (world, actor, choice, _) => world.knowledge.Artifacts.FirstOrDefault(item => item.Id == choice.ArtifactId) is { } artifact
                ? world.PlanKnowledgeStorage(actor, artifact, choice.Collect) : null,
            static (world, plan, policy) => world.CheckKnowledgeStorage(plan, policy),
            static (world, plan, action, _) => world.ApplyCheckedKnowledgeStorage(plan, action.State));
    }
}
