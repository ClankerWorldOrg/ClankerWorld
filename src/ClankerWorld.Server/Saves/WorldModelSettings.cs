using System.Security.Cryptography;
using System.Text.Json;
using ClankerWorld.Contracts;
using ClankerWorld.Server.Providers;
using ClankerWorld.Simulation.World;

namespace ClankerWorld.Server.Saves;

/// <summary>Preview and bind consent to the model routes in the exact selected save or world.</summary>
public static class WorldModelSettings
{
    public static OwnerModelSettingsPreview Preview(string targetId, WorldRuntimeState target,
        IReadOnlyList<AgentProviderAssignment> assignments, WorldRuntime current, ProviderConfigurationStore providers)
    {
        static AgentProviderAssignment[] Ordered(IReadOnlyList<AgentProviderAssignment> items) =>
            items.OrderBy(item => item.AgentId, StringComparer.Ordinal).ThenBy(item => item.Role, StringComparer.Ordinal).ToArray();
        var selected = Ordered(assignments);
        var previous = Ordered(providers.CaptureRuntimeConfiguration().Assignments ?? []);
        var changed = !selected.SequenceEqual(previous) || target.RoutineHelper != current.RoutineHelper;
        var approval = Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new
        {
            targetId, worldId = target.Society.Society.WorldId, assignments = selected, helper = target.RoutineHelper,
        })));
        var keys = providers.CaptureStatus().CredentialSlots ?? [];
        string Key(string? id) => id is null ? "default key" :
            keys.FirstOrDefault(item => item.Id == id)?.Label ?? "unavailable saved key";
        var details = selected.Select(item =>
        {
            var name = target.Society.Society.Agents.FirstOrDefault(person => person.Id == item.AgentId)?.Name ?? "Saved agent";
            return $"{name}: {item.Provider}, {item.Model ?? "local rules"}, {Key(item.CredentialSlotId)}" +
                (item.Thinking is null ? string.Empty : $", thinking {item.Thinking}");
        }).ToList();
        var helper = target.RoutineHelper;
        details.Add(helper is null ? "Routine helper: installation default" : helper.Provider == "off"
            ? "Routine helper: Off" : $"Routine helper: {helper.Provider}, {helper.Model}, {Key(helper.CredentialSlotId)}");
        return new(approval, changed, details);
    }

    public static void RequireApproval(OwnerModelSettingsPreview preview, string? approval)
    {
        if (preview.RequiresConfirmation && preview.Approval != approval)
            throw new ModelSettingsConfirmationRequiredException();
    }
}

public sealed class ModelSettingsConfirmationRequiredException() : InvalidOperationException(
    "This save changes model or key assignments. Review its model settings and confirm before loading.");
