using ClankerWorld.GodotClient.ClientState;
using ClankerWorld.GodotClient.Pairing;
using ClankerWorld.GodotClient.UI;
using Godot;
using System.Globalization;

namespace ClankerWorld.GodotClient;

public partial class Main
{
    private readonly record struct RosterPresentation(string Id, string Name, bool Living,
        string Activity, string Fullness, bool Cold, bool Ill, int PortraitStage);
    private RosterPresentation[]? renderedRosterPresentation;
    private string? renderedRosterWorldId;
    private string? renderedRosterTheme;

    private static RosterPresentation RosterPresentationFor(OwnerWorldAgent person)
    {
        var living = IsLiving(person);
        var activity = !living ? string.Empty : person.DecisionFactors.Any(factor => factor.Key == "decision-pending")
            ? "deciding what to do"
            : GameUiText.ActivityPhrase(person.PublicIntention?.CandidateId, person.PublicIntention?.Summary);
        var fullness = living ? GameUiText.FullnessState(person.HungerBasisPoints) : string.Empty;
        return new(person.Id, person.DisplayName, living, activity,
            fullness is "hungry" or "very hungry" ? fullness : string.Empty,
            living && person.Survival is { WarmthBasisPoints: < 4_000 },
            living && person.Survival is { IllnessBasisPoints: >= 1_500 },
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
        var presentation = agents.Select(RosterPresentationFor).ToArray();
        if (renderedRosterWorldId == snapshot.WorldId && renderedRosterTheme == UiTheme.Current.Name &&
            renderedRosterPresentation is { } previous && presentation.SequenceEqual(previous))
        {
            RefreshRosterSelection();
            return;
        }
        agentList.Clear();
        var living = agents.Count(IsLiving);
        var deceased = agents.Length - living;
        var hungry = agents.Count(person => IsLiving(person) &&
            GameUiText.FullnessState(person.HungerBasisPoints) is "hungry" or "very hungry");
        rosterSummaryLabel.Text = agents.Length == 0
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
            var rowText = RosterRow(agent);
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
        rosterPanel.Size = rosterPanel.GetCombinedMinimumSize();

        if (!selectionFound)
        {
            selectedAgentId = null;
            agentList.DeselectAll();
        }
        RenderRosterCards(agents);
        rosterPanel.Size = rosterPanel.GetCombinedMinimumSize();
        renderedRosterPresentation = presentation;
        renderedRosterWorldId = snapshot.WorldId;
        renderedRosterTheme = UiTheme.Current.Name;
    }

    private void RefreshRosterSelection()
    {
        var card = selectedAgentId is { } id ? rosterCardIds.IndexOf(id) : -1;
        if (card < 0) selectedAgentId = null;
        var selectedCards = rosterCards.GetSelectedItems();
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
        if (!cardMatches) rosterCards.Select(card);
    }

    private static bool IsLiving(OwnerWorldAgent agent) =>
        string.Equals(agent.Lifecycle, "active", StringComparison.OrdinalIgnoreCase);

    private static string RosterRow(OwnerWorldAgent agent)
    {
        if (!IsLiving(agent)) return $"{agent.DisplayName}  ·  died";
        var activity = agent.DecisionFactors.Any(factor => factor.Key == "decision-pending")
            ? "deciding what to do"
            : GameUiText.ActivityPhrase(agent.PublicIntention?.CandidateId, agent.PublicIntention?.Summary);
        var fullness = GameUiText.FullnessState(agent.HungerBasisPoints);
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
