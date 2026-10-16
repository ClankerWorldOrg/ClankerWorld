using ClankerWorld.GodotClient.ClientState;
using ClankerWorld.GodotClient.Pairing;
using ClankerWorld.GodotClient.Protocol;
using ClankerWorld.GodotClient.UI;
using Godot;
using System.Globalization;

namespace ClankerWorld.GodotClient;

/// <summary>
/// The Agents list: portrait cards the player sees, over a hidden text list
/// that keeps the selection, living agents first and the deceased after.
/// </summary>
public partial class Main
{
    private readonly SlotList agentListCards = new();
    private readonly List<string> agentListCardIds = [];

    private AgentListPresentation[]? renderedAgentListPresentation;
    private string? renderedAgentListWorldId;
    private string? renderedAgentListTheme;

    private void BuildAgentListCards(Control body)
    {
        // The text list stays for selection and checks, but never shows.
        agentList.Hide();
        agentListCards.Compact = true;
        agentListCards.CustomMinimumSize = new Vector2(380, 60);
        agentListCards.ItemClicked += index => SelectAgentListCard(index, keepAgentListOpen: false);
        agentListCards.ItemNavigated += index => SelectAgentListCard(index, keepAgentListOpen: true);
        agentListCards.ItemActivated += index =>
        {
            SelectAgentListCard(index, keepAgentListOpen: true);
            OpenAgentProfile(speak: false);
        };
        body.AddChild(agentListCards);
        agentList.Hide();
    }

    private void SelectAgentListCard(long index, bool keepAgentListOpen)
    {
        if (index < 0 || index >= agentListCardIds.Count) return;
        for (var row = 0; row < agentList.ItemCount; row++)
            if (agentList.GetItemMetadata(row).AsString() == agentListCardIds[(int)index])
            {
                SelectAgentFromList(row, keepAgentListOpen);
                return;
            }
    }

    private void RenderAgentListCards(OwnerWorldAgent[] agents)
    {
        var previousSelection = agentListCards.GetSelectedItems();
        var restoringSelection = previousSelection.Length > 0 && previousSelection[0] < agentListCardIds.Count &&
            agentListCardIds[previousSelection[0]] == selectedAgentId;
        agentListCards.Clear();
        agentListCardIds.Clear();
        foreach (var person in agents)
        {
            var living = IsLiving(person);
            List<SlotTag> tags = [];
            string detail;
            if (!living) detail = "Died";
            else
            {
                detail = person.DecisionFactors.Any(factor => factor.Key == "decision-pending")
                    ? "Deciding what to do"
                    : Capitalize(person.PublicIntention?.Summary ?? "taking in the surroundings");
                if (GameUiText.NeedLabel(person, "fullness-state") is "hungry" or "very hungry") tags.Add(new SlotTag("Hungry", Note: true));
                if (person.Survival is { } survival)
                {
                    if (GameUiText.NeedLabel(person, "warmth-state") == "cold") tags.Add(new SlotTag("Cold", Note: true));
                    if (GameUiText.NeedLabel(person, "illness-state") == "ill") tags.Add(new SlotTag("Ill", Note: true));
                }
            }
            agentListCards.AddItem(person.DisplayName, detail, AgentPortrait(person, living), tags, muted: !living);
            agentListCardIds.Add(person.Id);
            if (person.Id == selectedAgentId) agentListCards.Select(agentListCardIds.Count - 1, scrollIntoView: !restoringSelection);
        }
        agentListCards.Visible = agents.Length > 0;
        var rows = Math.Max(1, agents.Length);
        agentListWantedHeight = rows * 50;
        FitHudLists();
        QueueHudListsFit();
    }

    private static string Capitalize(string text) => text.Length == 0 ? text : char.ToUpperInvariant(text[0]) + text[1..];

    private readonly record struct AgentListPresentation(string Id, string Name, bool Living,
        string Activity, string Fullness, bool Cold, bool Ill, int PortraitStage);

    private static AgentListPresentation AgentListPresentationFor(OwnerWorldAgent person)
    {
        var living = IsLiving(person);
        var activity = !living ? string.Empty : person.DecisionFactors.Any(factor => factor.Key == "decision-pending")
            ? "deciding what to do"
            : person.PublicIntention?.Summary ?? "taking in the surroundings";
        var fullness = living ? GameUiText.NeedLabel(person, "fullness-state") : string.Empty;
        return new(person.Id, person.DisplayName, living, activity,
            fullness is "hungry" or "very hungry" ? fullness : string.Empty,
            living && GameUiText.NeedLabel(person, "warmth-state") == "cold",
            living && GameUiText.NeedLabel(person, "illness-state") == "ill",
            AgentSprites.StageIndex(person.DecisionFactors.FirstOrDefault(factor => factor.Key == "age-band")?.Detail));
    }

    private void RenderAgentList(OwnerWorldSnapshot snapshot)
    {
        var previousSelection = selectedAgentId;
        var selectionFound = false;
        // The living come first, each with what they are doing; the deceased
        // follow under their own heading so history stays inspectable.
        var agents = snapshot.Agents
            .Where(agent => !agent.IsDraft)
            .OrderBy(agent => IsLiving(agent) ? 0 : 1)
            .ThenBy(agent => agent.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var presentation = agents.Select(AgentListPresentationFor).ToArray();
        if (renderedAgentListWorldId == snapshot.WorldId && renderedAgentListTheme == UiTheme.Current.Name &&
            renderedAgentListPresentation is { } previous && presentation.SequenceEqual(previous))
        {
            RefreshAgentListSelection();
            return;
        }
        agentList.Clear();
        var living = agents.Count(IsLiving);
        var deceased = agents.Length - living;
        var hungry = agents.Count(person => IsLiving(person) &&
            GameUiText.NeedLabel(person, "fullness-state") is "hungry" or "very hungry");
        agentsSummaryLabel.Text = agents.Length == 0
            ? "No one lives here yet."
            : (deceased == 0 ? $"{living} living" : $"{living} living · {deceased} deceased") +
              (hungry == 0 ? string.Empty : $" · {hungry} hungry");

        foreach (var agent in agents)
        {
            if (!IsLiving(agent) && living > 0 && agentList.ItemCount == living)
            {
                var header = agentList.AddItem("Deceased", selectable: false);
                agentList.SetItemCustomFgColor(header, UiTheme.Current.InkMuted);
            }
            var rowText = AgentListRow(agent);
            var row = agentList.AddItem(rowText);
            agentList.SetItemMetadata(row, agent.Id);
            // The full row, in case a long activity is clipped at this width.
            agentList.SetItemTooltip(row, rowText + "\n" + (IsLiving(agent)
                ? "Select to find this agent on the map and open their card."
                : "Select to open this historical profile."));
            if (!IsLiving(agent)) agentList.SetItemCustomFgColor(row, UiTheme.Current.InkMuted);
            if (string.Equals(agent.Id, previousSelection, StringComparison.Ordinal))
            {
                selectionFound = true;
                agentList.Select(row);
            }
        }
        // Fit the list to its rows instead of reserving a tall empty box.
        var rowHeight = agentList.GetThemeFont("font").GetHeight(agentList.GetThemeFontSize("font_size")) +
            agentList.GetThemeConstant("v_separation") + 4;
        agentList.CustomMinimumSize = new Vector2(agentList.CustomMinimumSize.X,
            Math.Clamp(agentList.ItemCount * rowHeight + 12, 40, 360));
        agentsPanel.Size = agentsPanel.GetCombinedMinimumSize();

        if (!selectionFound)
        {
            selectedAgentId = null;
            agentList.DeselectAll();
        }
        RenderAgentListCards(agents);
        agentsPanel.Size = agentsPanel.GetCombinedMinimumSize();
        renderedAgentListPresentation = presentation;
        renderedAgentListWorldId = snapshot.WorldId;
        renderedAgentListTheme = UiTheme.Current.Name;
    }

    private void RefreshAgentListSelection()
    {
        var card = selectedAgentId is { } id ? agentListCardIds.IndexOf(id) : -1;
        if (card < 0) selectedAgentId = null;
        var selectedCards = agentListCards.GetSelectedItems();
        var cardMatches = card < 0 ? selectedCards.Length == 0 : selectedCards.Length == 1 && selectedCards[0] == card;
        var selectedRows = agentList.GetSelectedItems();
        var selectedRowId = selectedRows.Length == 1 ? agentList.GetItemMetadata(selectedRows[0]).AsString() : null;
        if (cardMatches && selectedRowId == selectedAgentId) return;
        agentList.DeselectAll();
        if (card >= 0)
            for (var row = 0; row < agentList.ItemCount; row++)
                if (agentList.GetItemMetadata(row).AsString() == selectedAgentId)
                {
                    agentList.Select(row);
                    break;
                }
        if (!cardMatches) agentListCards.Select(card);
    }

    private static bool IsLiving(OwnerWorldAgent agent) =>
        string.Equals(agent.Lifecycle, "active", StringComparison.OrdinalIgnoreCase);

    private static string AgentListRow(OwnerWorldAgent agent)
    {
        if (!IsLiving(agent)) return $"{agent.DisplayName}  ·  died";
        var activity = agent.DecisionFactors.Any(factor => factor.Key == "decision-pending")
            ? "deciding what to do"
            : agent.PublicIntention?.Summary ?? "taking in the surroundings";
        var fullness = GameUiText.NeedLabel(agent, "fullness-state");
        return $"{agent.DisplayName}  ·  {activity}" +
            (fullness is "hungry" or "very hungry" ? $"  ·  {fullness}" : string.Empty);
    }

    /// <summary>Moves the camera to a living agent; historical profiles have no map position to show.</summary>
    private void CenterOnAgent(string agentId)
    {
        if (renderedMapSnapshot?.Agents.FirstOrDefault(person => person.Id == agentId) is not { } person ||
            person.IsDraft || !IsLiving(person))
            return;
        CenterKeyboardCameraAt(new Vector2(person.Position.X + 0.5f, person.Position.Y + 0.5f));
    }
}
