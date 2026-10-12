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
                SelectInhabitantFromList(row, keepAgentListOpen);
                return;
            }
    }

    private void RenderAgentListCards(OwnerWorldInhabitant[] inhabitants)
    {
        var previousSelection = agentListCards.GetSelectedItems();
        var restoringSelection = previousSelection.Length > 0 && previousSelection[0] < agentListCardIds.Count &&
            agentListCardIds[previousSelection[0]] == selectedInhabitantId;
        agentListCards.Clear();
        agentListCardIds.Clear();
        foreach (var person in inhabitants)
        {
            var living = IsLiving(person);
            List<SlotTag> tags = [];
            string detail;
            if (!living) detail = "Died";
            else
            {
                detail = person.DecisionFactors.Any(factor => factor.Key == "decision-pending")
                    ? "Deciding what to do"
                    : Capitalize(GameUiText.ActivityPhrase(person.PublicIntention?.CandidateId, person.PublicIntention?.Summary));
                if (GameUiText.FullnessState(person.HungerBasisPoints) is "hungry" or "very hungry") tags.Add(new SlotTag("Hungry", Note: true));
                if (person.Survival is { } survival)
                {
                    if (survival.WarmthBasisPoints < 4_000) tags.Add(new SlotTag("Cold", Note: true));
                    if (survival.IllnessBasisPoints >= 1_500) tags.Add(new SlotTag("Ill", Note: true));
                }
            }
            agentListCards.AddItem(person.DisplayName, detail, AgentPortrait(person, living), tags, muted: !living);
            agentListCardIds.Add(person.Id);
            if (person.Id == selectedInhabitantId) agentListCards.Select(agentListCardIds.Count - 1, scrollIntoView: !restoringSelection);
        }
        agentListCards.Visible = inhabitants.Length > 0;
        var rows = Math.Max(1, inhabitants.Length);
        agentListWantedHeight = rows * 50;
        FitHudLists();
        QueueHudListsFit();
    }

    private static string Capitalize(string text) => text.Length == 0 ? text : char.ToUpperInvariant(text[0]) + text[1..];

    private readonly record struct AgentListPresentation(string Id, string Name, bool Living,
        string Activity, string Fullness, bool Cold, bool Ill, int PortraitStage);

    private static AgentListPresentation AgentListPresentationFor(OwnerWorldInhabitant person)
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

    private void RenderInhabitantList(OwnerWorldSnapshot snapshot)
    {
        var previousSelection = selectedInhabitantId;
        var selectionFound = false;
        // The living come first, each with what they are doing; the deceased
        // follow under their own heading so history stays inspectable.
        var inhabitants = snapshot.Inhabitants
            .Where(inhabitant => !inhabitant.IsDraft)
            .OrderBy(inhabitant => IsLiving(inhabitant) ? 0 : 1)
            .ThenBy(inhabitant => inhabitant.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var presentation = inhabitants.Select(AgentListPresentationFor).ToArray();
        if (renderedAgentListWorldId == snapshot.WorldId && renderedAgentListTheme == UiTheme.Current.Name &&
            renderedAgentListPresentation is { } previous && presentation.SequenceEqual(previous))
        {
            RefreshAgentListSelection();
            return;
        }
        agentList.Clear();
        var living = inhabitants.Count(IsLiving);
        var deceased = inhabitants.Length - living;
        var hungry = inhabitants.Count(person => IsLiving(person) &&
            GameUiText.FullnessState(person.HungerBasisPoints) is "hungry" or "very hungry");
        agentsSummaryLabel.Text = inhabitants.Length == 0
            ? "No one lives here yet."
            : (deceased == 0 ? $"{living} living" : $"{living} living · {deceased} deceased") +
              (hungry == 0 ? string.Empty : $" · {hungry} hungry");

        foreach (var inhabitant in inhabitants)
        {
            if (!IsLiving(inhabitant) && living > 0 && agentList.ItemCount == living)
            {
                var header = agentList.AddItem("Deceased", selectable: false);
                agentList.SetItemCustomFgColor(header, UiTheme.Current.InkMuted);
            }
            var rowText = AgentListRow(inhabitant);
            var row = agentList.AddItem(rowText);
            agentList.SetItemMetadata(row, inhabitant.Id);
            // The full row, in case a long activity is clipped at this width.
            agentList.SetItemTooltip(row, rowText + "\n" + (IsLiving(inhabitant)
                ? "Select to find this agent on the map and open their card."
                : "Select to open this historical profile."));
            if (!IsLiving(inhabitant)) agentList.SetItemCustomFgColor(row, UiTheme.Current.InkMuted);
            if (string.Equals(inhabitant.Id, previousSelection, StringComparison.Ordinal))
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
            selectedInhabitantId = null;
            agentList.DeselectAll();
        }
        RenderAgentListCards(inhabitants);
        agentsPanel.Size = agentsPanel.GetCombinedMinimumSize();
        renderedAgentListPresentation = presentation;
        renderedAgentListWorldId = snapshot.WorldId;
        renderedAgentListTheme = UiTheme.Current.Name;
    }

    private void RefreshAgentListSelection()
    {
        var card = selectedInhabitantId is { } id ? agentListCardIds.IndexOf(id) : -1;
        if (card < 0) selectedInhabitantId = null;
        var selectedCards = agentListCards.GetSelectedItems();
        var cardMatches = card < 0 ? selectedCards.Length == 0 : selectedCards.Length == 1 && selectedCards[0] == card;
        var selectedRows = agentList.GetSelectedItems();
        var selectedRowId = selectedRows.Length == 1 ? agentList.GetItemMetadata(selectedRows[0]).AsString() : null;
        if (cardMatches && selectedRowId == selectedInhabitantId) return;
        agentList.DeselectAll();
        if (card >= 0)
            for (var row = 0; row < agentList.ItemCount; row++)
                if (agentList.GetItemMetadata(row).AsString() == selectedInhabitantId)
                {
                    agentList.Select(row);
                    break;
                }
        if (!cardMatches) agentListCards.Select(card);
    }

    private static bool IsLiving(OwnerWorldInhabitant inhabitant) =>
        string.Equals(inhabitant.Lifecycle, "active", StringComparison.OrdinalIgnoreCase);

    private static string AgentListRow(OwnerWorldInhabitant inhabitant)
    {
        if (!IsLiving(inhabitant)) return $"{inhabitant.DisplayName}  ·  died";
        var activity = inhabitant.DecisionFactors.Any(factor => factor.Key == "decision-pending")
            ? "deciding what to do"
            : GameUiText.ActivityPhrase(inhabitant.PublicIntention?.CandidateId, inhabitant.PublicIntention?.Summary);
        var fullness = GameUiText.FullnessState(inhabitant.HungerBasisPoints);
        return $"{inhabitant.DisplayName}  ·  {activity}" +
            (fullness is "hungry" or "very hungry" ? $"  ·  {fullness}" : string.Empty);
    }

    /// <summary>Moves the camera to a living agent; historical profiles have no map position to show.</summary>
    private void CenterOnInhabitant(string inhabitantId)
    {
        if (renderedMapSnapshot?.Inhabitants.FirstOrDefault(person => person.Id == inhabitantId) is not { } person ||
            person.IsDraft || !IsLiving(person))
            return;
        CenterKeyboardCameraAt(new Vector2(person.Position.X + 0.5f, person.Position.Y + 0.5f));
    }
}
