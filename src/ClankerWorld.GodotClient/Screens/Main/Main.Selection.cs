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
    private void SelectInhabitantFromList(long index, bool keepAgentListOpen = false)
    {
        if (index < 0 || index >= agentList.ItemCount)
        {
            return;
        }

        var inhabitantId = agentList.GetItemMetadata((int)index).AsString();
        if (string.Equals(inhabitantId, selectedInhabitantId, StringComparison.Ordinal))
        {
            if (!keepAgentListOpen) ClearInhabitantSelection();
            return;
        }

        selectedInhabitantId = inhabitantId;
        if (!keepAgentListOpen) agentsPanel.Hide();
        // The roster promises to find the agent, so bring them into view.
        CenterOnInhabitant(inhabitantId);
        if (observationSession.Current is { } current)
        {
            RenderSelectedInhabitantCard(current.Baseline.Snapshot);
            RenderMap(current.Baseline.Snapshot);
            RefreshControlAvailability();
        }
    }

    private void SelectInhabitant(string inhabitantId)
    {
        if (string.Equals(inhabitantId, selectedInhabitantId, StringComparison.Ordinal))
        {
            ClearInhabitantSelection();
            return;
        }

        selectedInhabitantId = inhabitantId;
        for (var index = 0; index < agentList.ItemCount; index++)
        {
            if (string.Equals(agentList.GetItemMetadata(index).AsString(), inhabitantId, StringComparison.Ordinal))
            {
                agentList.Select(index);
                break;
            }
        }

        if (observationSession.Current is { } current)
        {
            RenderSelectedInhabitantCard(current.Baseline.Snapshot);
            RenderMap(current.Baseline.Snapshot);
            RefreshControlAvailability();
        }
    }

    private void ClearInhabitantSelection()
    {
        CloseAgentModelEditor();
        familyTreePanel.Hide();
        memoriesPanel.Hide();
        thoughtsPanel.Hide();
        ordersPanel.Hide();
        conversationPanel.Hide();
        selectedInhabitantId = null;
        agentList.DeselectAll();
        agentQuickCard.Hide();
        agentProfilePanel.Hide();
        if (observationSession.Current is { } current)
        {
            RenderSelectedInhabitantCard(current.Baseline.Snapshot);
            RenderMap(current.Baseline.Snapshot);
            RefreshControlAvailability();
        }
    }
}
