namespace ClankerWorld.Simulation.World;

internal sealed partial class ProductionSystem
{
    internal const int ProjectWorkTicks = 10;

    internal static void ValidateLocalAgentProject(AgentProject project)
    {
        if (string.IsNullOrWhiteSpace(project.CandidateId) || !TownConstructionCandidateIds.TryParse(project.CandidateId, out _) ||
            string.IsNullOrWhiteSpace(project.Label) || !BoundedText.IsWithinLimit(project.Label, 256) || project.StartedTick < 0 ||
            project.LastTransitionTick < project.StartedTick || project.WorkDone is < 0 or > ProjectWorkTicks ||
            project.Stage is not ("acquiring" or "gathering" or "delivering" or "travelling" or "working" or "waiting" or "blocked" or "paused" or "completed" or "cancelled") ||
            project.RequiresFreshChoice && (project.Stage != "paused" || project.JobId is not null) ||
            project.OrderInstructionId is { } orderId && (string.IsNullOrWhiteSpace(orderId) || orderId != orderId.Trim() || orderId.Any(char.IsControl)))
            throw new InvalidDataException("The saved agent project is invalid.");
    }
}
