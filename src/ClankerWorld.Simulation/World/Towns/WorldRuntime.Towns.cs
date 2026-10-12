using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;
using ClankerWorld.AgentPlacement;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    public string PlaceFounder(string founderId, GridPoint position)
    {
        gate.Wait();
        try { return townSystem.PlaceFounder(civicWorld, founderId, position); }
        finally
        {
            gate.Release();
        }
    }

    public bool MoveFounder(string founderId, GridPoint position)
    {
        gate.Wait();
        try { return townSystem.MoveFounder(civicWorld, founderId, position); }
        finally { gate.Release(); }
    }

    public int UndoLastFounder(string founderId)
    {
        gate.Wait();
        try { return townSystem.UndoLastFounder(civicWorld, founderId); }
        finally { gate.Release(); }
    }

    public void ValidateFounderPlacement(string founderId, GridPoint position)
    {
        gate.Wait();
        try { ValidateFounderPlacementUnsafe(founderId, position); }
        finally { gate.Release(); }
    }

    private void ValidateFounderPlacementUnsafe(string founderId, GridPoint position) => townSystem.ValidateFounderPlacementUnsafe(civicWorld, founderId, position);
    public string? AddAgent(string agentId, GridPoint position)
    {
        gate.Wait();
        try
        {
            var membership = ValidateAgentPlacementUnsafe(agentId, position);
            EnsurePlacementIsUnambiguous(membership);
            return AddAgentUnsafe(agentId, position, membership);
        }
        finally { gate.Release(); }
    }

    public string? AddAgent(string agentId, GridPoint position, string? expectedHouseholdId, string? expectedTownId)
    {
        gate.Wait();
        try
        {
            var membership = ValidateAgentPlacementUnsafe(agentId, position);
            EnsureExpectedPlacement(agentId, membership, expectedHouseholdId, expectedTownId);
            return AddAgentUnsafe(agentId, position, membership);
        }
        finally { gate.Release(); }
    }

    private string? AddAgentUnsafe(string agentId, GridPoint position, AgentPlacementResolution membership) => townSystem.AddAgentUnsafe(civicWorld, agentId, position, membership);
    public void ValidateAgentPlacement(string agentId, GridPoint position)
    {
        gate.Wait();
        try { EnsurePlacementIsUnambiguous(ValidateAgentPlacementUnsafe(agentId, position)); }
        finally { gate.Release(); }
    }

    public void ValidateAgentPlacement(string agentId, GridPoint position,
        string? expectedHouseholdId, string? expectedTownId)
    {
        gate.Wait();
        try
        {
            var membership = ValidateAgentPlacementUnsafe(agentId, position);
            EnsureExpectedPlacement(agentId, membership, expectedHouseholdId, expectedTownId);
        }
        finally { gate.Release(); }
    }

    private AgentPlacementResolution ValidateAgentPlacementUnsafe(string agentId, GridPoint position) => townSystem.ValidateAgentPlacementUnsafe(civicWorld, agentId, position);
    private IEnumerable<string?> HouseholdLandClaimantsAt(GridPoint position, bool householdBuildingStands) => townSystem.HouseholdLandClaimantsAt(civicWorld, position, householdBuildingStands);
    private static void EnsurePlacementIsUnambiguous(AgentPlacementResolution membership) => TownSystem.EnsurePlacementIsUnambiguous(membership);
    private static void EnsureExpectedPlacement(string agentId, AgentPlacementResolution membership,
        string? expectedHouseholdId, string? expectedTownId) => TownSystem.EnsureExpectedPlacement(agentId, membership, expectedHouseholdId, expectedTownId);
    private bool IsHouseAt(GridPoint position) => TownSystem.IsHouseAt(civicWorld, position);
    public bool RenameAgent(string agentId, string name)
    {
        gate.Wait();
        try
        {
            if (founderSetup is null || !society.Checkpoint.Agents.Any(person => person.Id == agentId))
                throw new ArgumentException("Choose an agent in this world.", nameof(agentId));
            ArgumentNullException.ThrowIfNull(name);
            var existing = society.Checkpoint.GetAgent(agentId);
            if (existing.Name == name.Trim() && existing.HasChosenName && !existing.NeedsName) return false;
            if (AgentNameRules.CanonicalKey(name) is null)
                throw new ArgumentException("Choose a valid name.", nameof(name));
            if (AgentNameRules.IsTaken(society.Checkpoint, agentId, name))
                throw new AgentNameTakenException();
            if (marriages.SingleOrDefault(item => item.EndReceipt is null && item.CompletedTick is null && item.SurnameReceipt is null &&
                    AgentMarriageRules.HasParticipant(item, agentId)) is { } pendingMarriage &&
                !AgentMarriageRules.CanKeepSurnameChoices(pendingMarriage, name))
                throw new ArgumentException("Choose a shorter first or middle name so the marriage's surname choices still fit.", nameof(name));
            var marriageIndex = FindMarriageIndex(item => item.EndReceipt is null && item.CompletedTick is not null && AgentMarriageRules.HasParticipant(item, agentId));
            var result = marriageIndex < 0
                ? society.Apply(checkpoint => RenameFromPlayer(checkpoint, agentId, name))
                : RenameSpouses(marriages[marriageIndex], agentId, name, fromPlayer: true);
            var changed = result.NewEvents is { Count: > 0 };
            if (changed)
            {
                if (marriageIndex >= 0)
                {
                    var marriage = marriages[marriageIndex];
                    var surname = AgentNameRules.SurnameKey(society.Checkpoint.GetAgent(agentId).Name)!;
                    if (surname != marriage.CurrentSurname)
                        SetMarriageAt(marriageIndex, marriage with { LatestPlayerRename = new AgentMarriageRename(agentId, surname, WorldTick) });
                }
                AppendEvent("agent_renamed", agentId);
            }
            return changed;
        }
        finally { gate.Release(); }
    }

    public void StartWorld() => StartWorld(resume: true);

    public void StartWorld(bool resume)
    {
        gate.Wait();
        try { townSystem.StartWorld(civicWorld, resume); }
        finally
        {
            gate.Release();
        }
    }

    private void AddTownResident(string townId, string residentId, string reason) => townSystem.AddTownResident(civicWorld, townId, residentId, reason);
    private void RemoveTownResident(string residentId) => townSystem.RemoveTownResident(civicWorld, residentId);
    private string? TownForResident(string residentId) => townSystem.TownForResident(civicWorld, residentId);
    private TownRuntimeState? TownForOwnerPlacement(GridPoint position, BuildingDefinition definition) => townSystem.TownForOwnerPlacement(civicWorld, position, definition);
    private void AssignBuildingToTown(PlacedBuilding building, BuildingDefinition definition) => townSystem.AssignBuildingToTown(civicWorld, building, definition);
    private void SetTown(TownRuntimeState updated) => townSystem.SetTown(civicWorld, updated);
}
