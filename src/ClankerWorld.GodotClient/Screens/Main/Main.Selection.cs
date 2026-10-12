using ClankerWorld.GodotClient.ClientState;
using ClankerWorld.GodotClient.Pairing;
using ClankerWorld.GodotClient.UI;
using Godot;
using System.Globalization;

namespace ClankerWorld.GodotClient;

/// <summary>
/// Selecting an agent from the Agents list or the map, and clearing the
/// selection.
/// </summary>
public partial class Main
{
    private void SelectAgentFromList(long index, bool keepAgentListOpen = false)
    {
        if (index < 0 || index >= agentList.ItemCount)
        {
            return;
        }

        var agentId = agentList.GetItemMetadata((int)index).AsString();
        if (string.Equals(agentId, selectedAgentId, StringComparison.Ordinal))
        {
            if (!keepAgentListOpen) ClearAgentSelection();
            return;
        }

        selectedAgentId = agentId;
        if (!keepAgentListOpen) agentsPanel.Hide();
        // The roster promises to find the agent, so bring them into view.
        CenterOnAgent(agentId);
        if (observationSession.Current is { } current)
        {
            RenderSelectedAgentCard(current.Baseline.Snapshot);
            RenderMap(current.Baseline.Snapshot);
            RefreshControlAvailability();
        }
    }

    private void SelectAgent(string agentId)
    {
        if (string.Equals(agentId, selectedAgentId, StringComparison.Ordinal))
        {
            ClearAgentSelection();
            return;
        }

        selectedAgentId = agentId;
        for (var index = 0; index < agentList.ItemCount; index++)
        {
            if (string.Equals(agentList.GetItemMetadata(index).AsString(), agentId, StringComparison.Ordinal))
            {
                agentList.Select(index);
                break;
            }
        }

        if (observationSession.Current is { } current)
        {
            RenderSelectedAgentCard(current.Baseline.Snapshot);
            RenderMap(current.Baseline.Snapshot);
            RefreshControlAvailability();
        }
    }

    private void ClearAgentSelection()
    {
        CloseAgentModelEditor();
        familyTreePanel.Hide();
        memoriesPanel.Hide();
        thoughtsPanel.Hide();
        ordersPanel.Hide();
        conversationPanel.Hide();
        selectedAgentId = null;
        agentList.DeselectAll();
        agentQuickCard.Hide();
        agentProfilePanel.Hide();
        if (observationSession.Current is { } current)
        {
            RenderSelectedAgentCard(current.Baseline.Snapshot);
            RenderMap(current.Baseline.Snapshot);
            RefreshControlAvailability();
        }
    }
}
