using ClankerWorld.GodotClient.Protocol;
using ClankerWorld.GodotClient.UI;
using Godot;
using System.Globalization;

namespace ClankerWorld.GodotClient;

/// <summary>
/// The selected agent's quick card beside them on the map: what they are
/// doing and how they are. Rendering it also fills the Profile, which shows
/// the same snapshot.
/// </summary>
public partial class Main
{
    private const int QuickCardWidth = 206;
    private readonly Label quickCardNameLabel = new();
    private readonly Label quickCardActivityLabel = new();
    private readonly Label quickCardOrderLabel = new();
    private readonly VBoxContainer quickCardMeters = new();
    private readonly PixelMeter quickFullnessMeter = new() { Kind = MeterKind.Fullness, Caption = "Fullness", CaptionWidth = 54 };
    private readonly PixelMeter quickWarmthMeter = new() { Kind = MeterKind.Warmth, Caption = "Warmth", CaptionWidth = 54 };
    private readonly PixelMeter quickIllnessMeter = new() { Kind = MeterKind.Illness, Caption = "Illness", CaptionWidth = 54 };
    private readonly Button quickCardProfileButton = new();
    private readonly Button quickCardSpeakButton = new();
    private OwnerWorldSnapshot? agentCardSnapshot;

    private void BuildAgentCards()
    {
        BuildQuickCard();
        BuildAgentProfile();
        BuildThoughtsReader();
        BuildOrdersReader();
        BuildConversationReader();
        uiLayer.AddChild(agentQuickCard);
        uiLayer.AddChild(agentProfilePanel);
        uiLayer.AddChild(thoughtsPanel);
        uiLayer.AddChild(ordersPanel);
        uiLayer.AddChild(conversationPanel);
    }

    private void BuildQuickCard()
    {
        var body = new VBoxContainer();
        body.AddThemeConstantOverride("separation", 5);
        var heading = new HBoxContainer();
        heading.AddThemeConstantOverride("separation", 4);
        quickCardNameLabel.ThemeTypeVariation = "HeadingLabel";
        quickCardNameLabel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        quickCardNameLabel.ClipText = true;
        quickCardNameLabel.MouseFilter = Control.MouseFilterEnum.Pass;
        heading.AddChild(quickCardNameLabel);
        StyleIconButton(findAgentButton, PixelGlyph.Find);
        findAgentButton.TooltipText = "Center the map on this agent (C)";
        findAgentButton.Pressed += CenterOnSelectedAgent;
        heading.AddChild(findAgentButton);
        StyleIconButton(clearSelectionButton, PixelGlyph.Close);
        clearSelectionButton.TooltipText = "Close (Esc)";
        clearSelectionButton.Pressed += ClearInhabitantSelection;
        heading.AddChild(clearSelectionButton);
        body.AddChild(heading);

        quickCardActivityLabel.ThemeTypeVariation = "SoftLabel";
        quickCardActivityLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        body.AddChild(quickCardActivityLabel);
        ConfigureOrderLabel(quickCardOrderLabel);
        body.AddChild(quickCardOrderLabel);
        quickCardMeters.AddThemeConstantOverride("separation", 3);
        foreach (var meter in new[] { quickFullnessMeter, quickWarmthMeter, quickIllnessMeter })
            quickCardMeters.AddChild(meter);
        body.AddChild(quickCardMeters);

        var actions = new HBoxContainer();
        actions.AddThemeConstantOverride("separation", 4);
        quickCardProfileButton.Text = "Profile";
        quickCardProfileButton.TooltipText = "Open everything about this agent.";
        StyleButton(quickCardProfileButton);
        quickCardProfileButton.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        quickCardProfileButton.Pressed += () => OpenAgentProfile(speak: false);
        actions.AddChild(quickCardProfileButton);
        quickCardSpeakButton.Text = "Speak";
        quickCardSpeakButton.TooltipText = "Say something to this agent.";
        StyleButton(quickCardSpeakButton);
        quickCardSpeakButton.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        quickCardSpeakButton.Pressed += () => OpenAgentProfile(speak: true);
        actions.AddChild(quickCardSpeakButton);
        body.AddChild(actions);

        AddPanelContents(agentQuickCard, body);
        agentQuickCard.ThemeTypeVariation = "HudPanel";
        agentQuickCard.CustomMinimumSize = new Vector2(QuickCardWidth, 0);
        agentQuickCard.ZIndex = 70;
        agentQuickCard.Hide();
    }

    /// <summary>Whether a decision came from an agent's own model rather than a routine helper or the built-in rules.</summary>
    private static bool IsModelProvider(string provider) => IsHostedProvider(provider);

    private Theme? renderedAgentCardTheme;

    /// <summary>Profile, Speak and card buttons carry pixel icons in the current theme.</summary>
    private void RefreshAgentCardIcons()
    {
        // Camera and observation refreshes keep the same theme. The compact
        // styles use logical margins and inherit the interface's scaling.
        if (ReferenceEquals(renderedAgentCardTheme, UiTheme.Theme)) return;
        var palette = UiTheme.Current;
        var dark = palette.Name == "dark";
        var wood = dark ? new Color("C99A62") : new Color("9C6C42");
        var green = dark ? new Color("8DBA6A") : palette.Primary;
        var gold = dark ? new Color("E8B04A") : new Color("B77C10");
        quickCardProfileButton.Icon = PixelIcons.Themed(PixelGlyph.Person, wood, 1);
        quickCardSpeakButton.Icon = PixelIcons.Themed(PixelGlyph.Speech, palette.Paper, 1);
        memoriesButton.Icon = PixelIcons.Themed(PixelGlyph.Book, wood, 1);
        familyTreeButton.Icon = PixelIcons.Themed(PixelGlyph.Tree, green, 1);
        modelSettingsButton.Icon = PixelIcons.Themed(PixelGlyph.Key, gold, 1);
        // Compact toggles copy the theme's tab styles, so copy them again for the new palette.
        foreach (var toggle in new Button[] { readThoughtsButton, instructionSuggestButton, instructionOrderButton,
                     instructionQueueToggle, instructionCancelButton, allOrdersButton, conversationHistoryButton })
            StyleCompactToggle(toggle);
        agentPortraitFrame.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = dark ? new Color("3E5A2E") : new Color("8FB06A"),
            BorderColor = palette.WoodEdge,
            BorderWidthLeft = 2,
            BorderWidthTop = 2,
            BorderWidthRight = 2,
            BorderWidthBottom = 2,
            ContentMarginLeft = 2,
            ContentMarginTop = 2,
            ContentMarginRight = 2,
            ContentMarginBottom = 2,
        });
        renderedAgentCardTheme = UiTheme.Theme;
    }

    private void CenterOnSelectedAgent()
    {
        if (selectedInhabitantId is { } id) CenterOnInhabitant(id);
    }

    private OwnerWorldInhabitant? SelectedInhabitant() =>
        agentCardSnapshot?.Inhabitants.FirstOrDefault(item =>
            string.Equals(item.Id, selectedInhabitantId, StringComparison.Ordinal));

    private static bool IsDeceased(OwnerWorldInhabitant inhabitant) =>
        string.Equals(inhabitant.Lifecycle, "dead", StringComparison.OrdinalIgnoreCase);

    private static string Sentence(string text) =>
        text.Length == 0 ? text : char.ToUpper(text[0], CultureInfo.CurrentCulture) + text[1..];

    /// <summary>A thought from today shows its time; older ones show the full date too.</summary>
    private string ThoughtTime(long tick, long now)
    {
        var (date, time) = SplitClock(DisplayWorldClock(tick));
        return date == SplitClock(DisplayWorldClock(now)).Date ? time : $"{date} · {time}";
    }

    private void RenderSelectedInhabitantCard(OwnerWorldSnapshot snapshot)
    {
        agentCardSnapshot = snapshot;
        var inhabitant = snapshot.Inhabitants.FirstOrDefault(item =>
            string.Equals(item.Id, selectedInhabitantId, StringComparison.Ordinal));
        if (inhabitant is null)
        {
            CloseAgentModelEditor();
            agentProfileRequested = false;
            renamingAgentId = null;
            refusedAgentRename.Forget();
            renameRow.Hide();
            quickCardNameLabel.Text = quickCardNameLabel.TooltipText = string.Empty;
            selectedActorNameLabel.Text = string.Empty;
            selectedActorSummaryLabel.Text = string.Empty;
            selectedActorConditionLabel.Text = string.Empty;
            SetPanelText(inhabitantDetails, string.Empty);
            privateThoughtHistory.Text = string.Empty;
            SetPanelText(instructionHistory, string.Empty);
            memoriesPanel.Hide();
            thoughtsPanel.Hide();
            ordersPanel.Hide();
            agentQuickCard.Hide();
            agentProfilePanel.Hide();
            return;
        }

        // One quick card at a time: choosing an agent closes a building's card.
        if (selectedBuildingId is not null) ClearBuildingSelection();
        string? Factor(string key) => inhabitant.DecisionFactors.FirstOrDefault(factor => factor.Key == key)?.Detail;
        var ageBand = Factor("age-band");
        var ageYears = Factor("age-years");
        var ageDays = Factor("age-days");
        var deathTick = Factor("death-tick");
        var deathCause = Factor("death-cause");
        var willStatus = Factor("will-status");
        var role = Factor("role");
        var isDeceased = IsDeceased(inhabitant);
        var waitingForDecision = inhabitant.DecisionFactors.Any(factor => factor.Key == "decision-pending");
        var decision = snapshot.Cognition?.Decisions?.FirstOrDefault(item => item.InhabitantId == inhabitant.Id);
        if (selectedAgentModelScroll.Visible && SelectedAgentModelTarget() != inhabitant.Id)
            CloseAgentModelEditor();

        // Name, age and one plain sentence for what they are doing, shared by both cards.
        quickCardNameLabel.Text = quickCardNameLabel.TooltipText = inhabitant.DisplayName;
        selectedActorNameLabel.Text = inhabitant.DisplayName;
        // Draft and refused names stay in the open field for the player to change,
        // while the labels above keep showing the name the host holds.
        if (!renameRow.Visible || renamingAgentId != inhabitant.Id) refusedAgentRename.Forget();
        if (!refusedAgentRename.Keeps(snapshot.WorldId, inhabitant.Id, renameAgentInput.Text) &&
            (renamingAgentId != inhabitant.Id || !renameRow.Visible))
        {
            renameAgentInput.Text = inhabitant.DisplayName;
            renamingAgentId = inhabitant.Id;
        }
        agentPortraitTexture.Atlas = AgentSprites.Atlas(32);
        agentPortraitTexture.Region = AgentSprites.Region(AgentSprites.VariantFor(inhabitant.Id), AgentSprites.StageIndex(ageBand), 32);
        selectedActorNameLabel.TooltipText = inhabitant.DisplayName;
        selectedActorSummaryLabel.Text = string.Join(" · ", new[]
        {
            isDeceased ? "Dead" : null,
            ageBand is null ? null : Pretty(ageBand),
            ageYears is null ? null : ageYears + " years",
            ageDays is null ? null : ageDays + " days",
            deathTick is not null && long.TryParse(deathTick, CultureInfo.InvariantCulture, out var finalTick) ? DisplayWorldClock(finalTick) : null,
        }.Where(part => part is not null));
        var activity = isDeceased
            ? $"Life ended{(deathCause is null ? "" : " · " + Pretty(deathCause))}."
            : waitingForDecision
            ? "Deciding what to do next"
            : Sentence(GameUiText.ActivityPhrase(inhabitant.PublicIntention?.CandidateId, inhabitant.PublicIntention?.Summary));
        // The Model line names whose model made their latest choice, such as OpenAI.
        var modelStatus = GameUiText.ModelStatus(Factor("model-status"));
        var modelProvider = decision is { } latest && IsModelProvider(latest.Provider) ? ProviderDisplayName(latest.Provider) + " · " : string.Empty;
        quickCardActivityLabel.Text = isDeceased ? activity : $"{activity}\nModel: {modelProvider}{modelStatus}";
        profileActivityLabel.Text = quickCardActivityLabel.Text;

        // How they are: bars where the host reports a value, and plain facts beside them.
        var fullness = NeedPercent(inhabitant.HungerBasisPoints);
        var survival = inhabitant.Survival;
        quickFullnessMeter.Percent = fullness;
        profileFullnessMeter.Percent = fullness;
        foreach (var meter in new[] { quickWarmthMeter, quickIllnessMeter, profileWarmthMeter, profileDietMeter, profileIllnessMeter })
            meter.Visible = survival is not null;
        if (survival is not null)
        {
            quickWarmthMeter.Percent = profileWarmthMeter.Percent = survival.WarmthBasisPoints / 100;
            quickIllnessMeter.Percent = profileIllnessMeter.Percent = survival.IllnessBasisPoints / 100;
            profileDietMeter.Percent = survival.NutritionBasisPoints / 100;
        }
        quickCardMeters.Visible = !isDeceased;
        profileMeters.Visible = !isDeceased;
        var carrying = inhabitant.Inventory.Count == 0
            ? "Carrying nothing"
            : "Carrying " + string.Join(", ", inhabitant.Inventory.Select(item => $"{GameUiText.ItemName(item.Kind).ToLowerInvariant()} ({item.Quantity})"));
        selectedActorConditionLabel.Text = isDeceased
            ? survival is null ? "Historical record" :
                $"At death · Warmth {survival.WarmthBasisPoints / 100}% · Illness {survival.IllnessBasisPoints / 100}% · Diet {survival.NutritionBasisPoints / 100}%"
            : survival is null ? carrying :
                $"{carrying} · {(survival.HasClothing ? "Clothed" : "No warm clothing")} · {(survival.HasTool ? "Has a tool" : "Working by hand")}";

        // What they are working on, learning and who chose their action.
        var details = new List<string>();
        if (Factor("personality") is { } personality) details.Add("Personality: " + personality);
        if (Factor("aspiration") is { } aspiration) details.Add("Aspiration: " + aspiration);
        details.AddRange(inhabitant.DecisionFactors.Where(factor => factor.Key == "identity-change")
            .Select(factor => factor.Detail));
        if (!isDeceased && !string.IsNullOrWhiteSpace(inhabitant.MedicalCareNote))
            details.Add(inhabitant.MedicalCareNote);
        if (!isDeceased && Factor("knowledge-writing") is { } writingProgress)
            details.Add(writingProgress);
        if (!isDeceased && !string.IsNullOrWhiteSpace(inhabitant.ToolMakingRequestNote))
            details.Add(inhabitant.ToolMakingRequestNote);
        if (!isDeceased && inhabitant.Equipment is { } equipment)
        {
            details.Add($"Cargo: {equipment.CarriedQuantity}/{equipment.Capacity}" +
                (equipment.CarriedQuantity > equipment.Capacity ? " · Full; store or set down a load before picking up more." : ""));
            if (equipment.ClothingKind is { } garment)
                details.Add($"Wearing {Pretty(garment).ToLowerInvariant()} · Condition {equipment.ClothingConditionPercent}%");
            if (equipment.CarryAidKind is { } aid)
                details.Add($"Equipped {Pretty(aid).ToLowerInvariant()} · Condition {equipment.CarryAidConditionPercent}%");
            if (equipment.OrnamentKind is { } ornament)
                details.Add($"Wearing {GameUiText.ItemName(ornament).ToLowerInvariant()}");
            if (equipment.RepairItemKind is { } repairItem)
                details.Add($"Repairing {Pretty(repairItem).ToLowerInvariant()} · {equipment.RepairWorkDone}/{equipment.RepairWorkRequired}");
        }
        foreach (var cart in snapshot.Handcarts.Where(cart => cart.OwnerId == inhabitant.Id || cart.PullerId == inhabitant.Id))
            details.Add(GameUiText.HandcartDescription(cart));
        foreach (var animal in snapshot.Animals.Where(animal => animal.RiderId == inhabitant.Id || animal.LeaderId == inhabitant.Id))
            details.Add(GameUiText.AnimalDescription(animal));
        if (!isDeceased && Factor("last-model-choice") is { } lastModelChoice)
            details.Add("Last model choice: " + Sentence(GameUiText.ActivityPhrase(lastModelChoice, null)));
        if (!isDeceased && Factor("model-setup-blocker") == "unsupported_request")
            details.Add("This model rejected the request format. Choose a compatible model in Model settings.");
        if (inhabitant.Project is { } project)
        {
            details.Add($"{project.Label} · {Pretty(project.Stage)} · {project.WorkDone}/{project.WorkRequired}");
            if (project.Blocker is not null) details.Add(project.Blocker);
        }
        if (role is not null and not "unassigned") details.Add($"Role: {Pretty(role)}");
        if (!isDeceased)
            details.AddRange(inhabitant.DecisionFactors.Where(factor => factor.Key == "guardian-care")
                .Select(factor => factor.Detail));
        if (Factor("housing") is { } housing && !isDeceased) details.Add(housing);
        if (!isDeceased && inhabitant.DecisionFactors.FirstOrDefault(factor => factor.Key == "town-membership") is { } townMembership)
        {
            details.Add(townMembership.Detail);
            if (townMembership.AcceptanceDeadlineTick is { } deadline)
                details.Add($"Acceptance deadline: {DisplayWorldClock(deadline)} · Paused time does not count.");
        }
        if (inhabitant.Lesson is { } lesson)
            details.Add($"Learning {Pretty(lesson.Skill)} with {lesson.TeacherName} · {Pretty(lesson.Stage)} · {lesson.Progress}/{lesson.Required}");
        foreach (var skill in inhabitant.Skills ?? [])
            details.Add($"{Pretty(skill.Kind)} skill · {(skill.TeacherName is { } teacher ? "taught by " + teacher : "learned by doing")} · {DisplayWorldClock(skill.LearnedTick)}");
        if (inhabitant.Proficiency is { } practice)
            details.Add($"Practice · Building {practice.Building}/30 · Farming {practice.Farming}/30 · Crafting {practice.Crafting}/30");
        if (isDeceased)
        {
            details.AddRange(GameUiText.FinalWillLines(willStatus, inhabitant.FinalWill));
        }
        else if (!waitingForDecision)
        {
            // A choice their own model made is named on the Model line; say so when something else made it.
            if (decision is null) details.Add("No decision yet");
            else if (decision.FellBack) details.Add("The model gave no usable choice, so built-in rules chose this.");
            else if (!IsModelProvider(decision.Provider)) details.Add($"{ProviderDisplayName(decision.Provider)} made their latest choice.");
        }
        SetPanelText(inhabitantDetails, string.Join("\n", details));
        inhabitantDetails.Visible = details.Count > 0;
        inhabitantDetails.TooltipText = decision is null ? "" :
            $"Last accepted decision\nRole: {decision.Role ?? "not reported"}\nModel: {decision.Model ?? "not reported"}\nConfidence: {decision.Confidence:P0}\n" +
            $"Latency: {decision.LatencyMilliseconds?.ToString(CultureInfo.CurrentCulture) ?? "—"} ms\n" +
            $"Tokens in/out: {decision.InputTokens?.ToString(CultureInfo.CurrentCulture) ?? "—"}/{decision.OutputTokens?.ToString(CultureInfo.CurrentCulture) ?? "—"}";
        if (inhabitant.Proficiency is not null)
            inhabitantDetails.TooltipText += (inhabitantDetails.TooltipText.Length == 0 ? "" : "\n") +
                "Practice: each completed project earns one point in its domain, up to 30. Every 10 points adds one work per preparation step. Materials, permissions and crop growth time are unchanged.";

        thoughtsHeading.Text = isDeceased ? "THOUGHTS · HISTORICAL" : "THOUGHTS";
        privateThoughtHistory.Text = inhabitant.RecentPrivateThoughts.Count == 0
            ? "None recorded yet."
            : $"{ThoughtTime(inhabitant.RecentPrivateThoughts[^1].WorldTick, snapshot.WorldTick)}  {GameUiText.PlainEllipses(inhabitant.RecentPrivateThoughts[^1].Text)}";
        // Keep the task that Cancel task targets, then the newest open messages
        // before closed ones, within the same four-message history.
        var pendingOrder = PendingOrderToCancel(snapshot, inhabitant.Id);
        var recentInstructions = snapshot.Instructions
            .Where(item => item.TargetInhabitantId == inhabitant.Id)
            .OrderBy(item => item.InstructionId == pendingOrder?.InstructionId ? 0 : item.State == "completed" ? 2 : 1)
            .ThenByDescending(item => item.SubmissionSequence)
            .Take(4)
            .OrderBy(item => item.SubmissionSequence)
            .Select(item =>
            {
                var status = item.Kind == "must_do"
                    ? InstructionOrderSummary(item)
                    : item.ObservedTick is null ? "Suggestion waiting for their personal model" : "Suggestion heard by their personal model";
                var reply = item.ObserverReply is null ? string.Empty : $"\nAgent reply: “{item.ObserverReply}”";
                return $"{status}\n“You said: {item.Text}”{reply}";
            })
            .ToArray();
        instructionCancelButton.Visible = !isDeceased && pendingOrder is not null;
        instructionCancelButton.Disabled = isOwnerAction || pendingSubmission is not null || registration is null || deviceKey is null;
        SetPanelText(instructionHistory, recentInstructions.Length == 0
            ? "No messages yet."
            : string.Join("\n\n", recentInstructions));
        RenderOrderSummary(snapshot, inhabitant, isDeceased);

        RenderProfilePeople(snapshot, inhabitant);
        RenderMemoryCards(snapshot, inhabitant);
        RenderThoughtsReader(snapshot, inhabitant);
        RenderOrdersReader(snapshot, inhabitant);

        modelSettingsButton.Disabled = isDeceased || registration is null;
        findAgentButton.Visible = !isDeceased;
        profileFindButton.Visible = !isDeceased;
        speakSection.Visible = !isDeceased;
        UpdateAgentProfileCloseButton(living: !isDeceased);

        // The dead have no place on the map, so their profile opens directly.
        var showProfile = agentProfileRequested || isDeceased;
        agentProfilePanel.Visible = showProfile;
        agentQuickCard.Visible = !showProfile;
        PositionSelectedInhabitantCard(snapshot);
        PositionAgentProfile();
    }
}
