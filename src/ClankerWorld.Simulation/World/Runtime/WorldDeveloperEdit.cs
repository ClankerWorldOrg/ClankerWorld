namespace ClankerWorld.Simulation.World;

/// <summary>An explicit owner edit bound to the world and observation it was prepared from.</summary>
public sealed record WorldDeveloperEdit(string WorldId, long ExpectedEventId, string AgentId,
    string Operation, string Value, int Amount = 0, string? OtherAgentId = null);

public sealed record WorldDeveloperEditResult(bool Applied, bool AlreadyApplied, string? Failure);
