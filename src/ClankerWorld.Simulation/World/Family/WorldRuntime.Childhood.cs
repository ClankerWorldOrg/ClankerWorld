using System.Security.Cryptography;
using System.Text;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private const int ChildSocialCooldownTicks = 120;

    private bool IsChild(string actor) => AgentQueries.IsChild(agentWorld, actor);

    private bool ChildSocialAvailable(string actor, string target, string kind) =>
        IsChild(actor) && actor != target && agents.ContainsKey(target) &&
        !LifeStageRules.IsInfant(society.Checkpoint.GetAgent(target).AgeBand) &&
        !society.Checkpoint.Memories.Any(memory => memory.OwnerId == actor && memory.SubjectId == target &&
            memory.Id.StartsWith($"child-social:{kind}:", StringComparison.Ordinal) &&
            memory.SourceTick > WorldTick - ChildSocialCooldownTicks);

    private void AddChildCandidates(List<CognitionCandidate> candidates, string actor, AgentState state)
    {
        var availableTargets = 0;
        foreach (var target in agents.Keys.Where(id => id != actor)
                 .OrderBy(id => map.FootDistance(state.Position, agents[id].Position))
                 .ThenBy(id => id, StringComparer.Ordinal))
        {
            var candidateCount = candidates.Count;
            OfferChildChoice(candidates, actor, new(ChildActionKind.Converse, target),
                "Talk with a nearby person and remember the encounter.", 38, target);
            OfferChildChoice(candidates, actor, new(ChildActionKind.Play, target),
                "Play together and build a friendship.", 34, target);
            OfferChildChoice(candidates, actor, new(ChildActionKind.Learn, target),
                "Learn a simple observation from an adult without taking an adult work role.", 42, target);
            if (candidates.Count > candidateCount && ++availableTargets == 3) break;
        }

        AddChildHelpingCandidates(candidates, actor, state);
        OfferChildChoice(candidates, actor, new(ChildActionKind.HelpFood),
            "Carry one spare food serving back to the household store.", 26);
    }

    private void ApplyChildCandidate(string actor, AgentState state, string candidate) =>
        ApplyChildPlan(PlanChildCandidate(actor, candidate));

    private void ApplyChildSocial(ChildActionPlan plan)
    {
        var actor = plan.Actor;
        var target = plan.Choice.Target!;
        var kind = plan.Choice.Kind switch
        {
            ChildActionKind.Converse => "converse",
            ChildActionKind.Play => "play",
            _ => "learn",
        };
        var state = agents[actor];
        if (!IsWithinInteractionRange(state.Position, plan.Destination, plan.InteractionRange))
        {
            MoveToward(actor, state, plan.Destination, "child_social", plan.InteractionRange);
            return;
        }

        // Keep short keys and cooldown prefixes; hash the complete identity when
        // ancestry makes it exceed the cognition memory boundary.
        var memoryId = ChildSocialMemoryId(kind, actor, target, WorldTick);
        society.Apply(checkpoint => SocietyRules.RecordSocialMemory(checkpoint,
            new(memoryId, actor, target,
                kind switch
                {
                    "converse" => "Talked with another person.",
                    "play" => "Played with another person.",
                    _ => "Learned by observing an adult.",
                }, "public", WorldTick)));
        IncreaseTrust(actor, target, 1, "child_" + kind);
        AppendEvent("child_" + kind, $"{actor}:{target}");
    }
}
