using ClankerWorld.GodotClient.Pairing;
using ClankerWorld.GodotClient.Protocol;
using ClankerWorld.GodotClient.UI;
using Godot;

namespace ClankerWorld.GodotClient;

public partial class Main
{
    /// <summary>Saves from two versions of events are grouped apart, and each branch's latest point is clear.</summary>
    private void VerifySaveBranchList()
    {
        var start = DateTimeOffset.UnixEpoch;
        var first = new SaveBranch("a", 1);
        var second = new SaveBranch("b", 2, "flood", "Before the flood", 10);
        ManualWorldSave flood = new("flood", "Before the flood", start, 10, false, first, BranchPosition: 1);
        ManualWorldSave harvest = new("harvest", "Big harvest", start.AddMinutes(1), 90, false, first, "flood", start, BranchPosition: 2);
        ManualWorldSave winter = new("winter", "Hungry winter", start.AddMinutes(2), 60, false, second, "flood", start, BranchPosition: 1);
        ManualWorldSave old = new("old", "Old save", start.AddMinutes(-1), 5);
        ManualWorldSave[] saves = [old, flood, harvest, winter];
        var ordered = OrderSavesByBranch(saves).Select(save => save.Id).ToArray();
        if (!ordered.SequenceEqual(["winter", "harvest", "flood", "old"]))
            throw new InvalidOperationException("Saves must be grouped by branch, newest branch and newest point first: " +
                string.Join(", ", ordered));
        FailUiCheckIf(!IsLatestInBranch(harvest, saves) || !IsLatestInBranch(winter, saves) ||
            IsLatestInBranch(flood, saves) || IsLatestInBranch(old, saves), "Only the newest point of a branch may continue it.");
        FailUiCheckIf(BranchLabel(second) != "From Before the flood" || BranchLabel(first) != "Branch 1" ||
            BranchLabel(null) != "Earlier saves", "Branch labels must name the branch, or saves from before branches.");

        // The middle save was deleted, and a recovery copy of the first point
        // was made most recently. Equal ticks and creation dates cannot order history.
        ManualWorldSave pausedFirst = new("paused-first", "Before changing Jev", start.AddMinutes(3), 10,
            false, first, BranchPosition: 1);
        ManualWorldSave pausedLater = new("paused-later", "Jev off", start.AddMinutes(2), 10,
            false, first, "deleted-middle", start.AddMinutes(1), BranchPosition: 3);
        ManualWorldSave[] retainedPausedSaves = [pausedFirst, pausedLater];
        FailUiCheckIf(IsLatestInBranch(pausedFirst, retainedPausedSaves) || !IsLatestInBranch(pausedLater, retainedPausedSaves) ||
            !OrderSavesByBranch(retainedPausedSaves).Select(save => save.Id).SequenceEqual(["paused-later", "paused-first"]), "Paused history must retain its latest position after an intermediate save is deleted.");

        allListedManualSaves = saves;
        listedManualSaves = OrderSavesByBranch(saves);
        RenderManualSaveList();
        var winterCard = manualSaveList.GetItemTitle(0);
        FailUiCheckIf(manualSaveList.ItemCount != 4 || winterCard != "Hungry winter", "The save list must show every branch's saves.");
        FailUiCheckIf(!manualSaveList.FindChildren("*", nameof(Label), true, false).OfType<Label>()
            .Any(label => label.TooltipText == "From Before the flood" && label.Text.StartsWith("FROM ", StringComparison.Ordinal)), "The actual save-list card must show its recorded branch origin.");
        allListedManualSaves = [];
        listedManualSaves = [];
        manualSaveList.Clear();
    }

    private async Task VerifyManualSaveListOwnershipAsync()
    {
        var previousConnection = CaptureOwnerCheckConnection();
        var previousObservation = observationSession.Current;
        var previousCi = System.Environment.GetEnvironmentVariable("CI");
        System.Environment.SetEnvironmentVariable("CI", "true");
        using var signer = OwnerDeviceKey.CreateEphemeralForContinuousIntegration();
        using var host = new WorldActionSmokeHost(signer.PublicKeySpkiBase64);
        try
        {
            ConnectOwnerCheck(host, signer);
            // A freshly paired launch has its catalog, but has not entered a
            // world or accepted an observation. Exercise both real buttons and
            // the signed catalog/save-list transport before any baseline.
            observationSession.ReplaceRegistration(registration);
            host.Catalog = WorldActionSmokeCatalog(["A", "B"]);
            host.ManualSaves =
            [new("title-named", "Title save", DateTimeOffset.UnixEpoch, 0),
             new("title-auto", "Autosave", DateTimeOffset.UnixEpoch.AddMinutes(1), 1, true)];
            OpenWorldMenu(create: false);
            await RefreshWorldListAsync();
            ChooseWorldActionSmokeRow(0);
            await worldSaveHistoryRead;
            FailUiCheckIf(observationSession.Current is not null || listedManualSaves.Length != 2 || !WorldHistoryEmbedded ||
                !manualSaveTimeline.IsVisibleInTree() || manualSaveOverlay.Visible || worldHistoryLoading, "Selecting a world must show its timeline directly without entering it.");
            for (var row = 0; row < listedManualSaves.Length; row++)
            {
                manualSaveList.Select(row);
                manualSaveList.EmitSignal(SlotList.SignalName.ItemSelected, (long)row);
                var save = listedManualSaves[row];
                FailUiCheckIf(manualSaveDeleteButton.Disabled, "A selected title-screen save must offer Delete Save.");
                manualSaveDeleteButton.EmitSignal(BaseButton.SignalName.Pressed);
                if (!deletionConfirmation.Visible || pendingDeletion is not { Kind: "save", WorldId: "world-A" } deletion ||
                    deletion.Id != save.Id || deletion.ExpectedCreatedUtc != save.CreatedUtc)
                    throw new InvalidOperationException("Delete Save on a fresh title must confirm the exact save and catalog world identity.");
                deletionConfirmation.Hide();
                pendingDeletion = null;
            }
            FailUiCheckIf(host.DeleteCount != 0, "Opening and dismissing confirmation must not delete any save.");
            foreach (var fail in new[] { false, true })
            {
                var stale = new TaskCompletionSource<WorldSaveHistory>();
                CancellationToken token = default;
                var previousWorld = SelectedListedWorld()!;
                var oldRead = ReadListedWorldSavesAsync(previousWorld, value => { token = value; return stale.Task; });
                ChooseWorldActionSmokeRow(1);
                await worldSaveHistoryRead;
                FailUiCheckIf(!token.IsCancellationRequested || listedSaveWorldId != "world-B" || !WorldHistoryEmbedded, "Choosing another world must cancel the old history and show the chosen world's saves.");
                ChooseTimelineSave(listedManualSaves[0].Id);
                FailUiCheckIf(!manualSaveDeleteButton.Disabled, "Deleting a save of an inactive world must wait until that world is open.");
                manualSaveDeleteButton.EmitSignal(BaseButton.SignalName.Pressed);
                FailUiCheckIf(deletionConfirmation.Visible || pendingDeletion is not null, "An inactive world's save must refuse deletion even if the old button signal arrives.");
                var chosenId = SelectedManualSave()?.Id;
                var status = worldMenuStatus.Text;
                if (fail) stale.SetException(new IOException("Controlled stale history failure."));
                else stale.SetResult(new(previousWorld.WorldId, [], new(null, null, false), null, 0));
                await oldRead;
                FailUiCheckIf(listedSaveWorldId != "world-B" || SelectedManualSave()?.Id != chosenId || worldMenuStatus.Text != status, "A late history reply or failure must not replace the new world's timeline or selection.");
            }
            await ReadListedWorldSavesAsync(SelectedListedWorld()!, _ => Task.FromException<WorldSaveHistory>(
                new IOException("Controlled unavailable history.")));
            FailUiCheckIf(!worldSelectButton.Disabled || !worldMenuStatus.Text.StartsWith("Could not list saves:", StringComparison.Ordinal), "A failed history read must explain the failure and keep Load unavailable until a retry.");
            var hiddenReply = new TaskCompletionSource<WorldSaveHistory>();
            CancellationToken hiddenToken = default;
            var hiddenRead = ReadListedWorldSavesAsync(SelectedListedWorld()!, token => { hiddenToken = token; return hiddenReply.Task; });
            var hiddenWorldId = SelectedListedWorld()!.WorldId;
            worldBackButton.EmitSignal(BaseButton.SignalName.Pressed);
            if (!hiddenToken.IsCancellationRequested) throw new InvalidOperationException("Back must cancel the history read.");
            hiddenReply.SetResult(new(hiddenWorldId, [], new(null, null, false), null, 0));
            await hiddenRead;
            FailUiCheckIf(worldMenuOverlay.Visible || manualSaveHistory.IsVisibleInTree(), "Back must close Load World and its embedded history together.");
            var handshake = new OwnerWorldHandshake(new(1, 1),
                ["owner-observation.read.v1", "owner-event-presentation.v2", "agent-inspection.read.v1", "spatial-knowledge.read.v1",
                 "owner-control.request.v1", "paused-authoring.request.v1"], []);
            void AcceptWorld(string id)
            {
                observationSession.ResetAfterLoad();
                var snapshot = new OwnerWorldSnapshot(id, 0, "save-list-map", [new(0, 0, "meadow")], [], [], null, 0)
                {
                    Authoring = new(true, 0, 0, 0, "save-list-map", "save-list-map", "clear", "spring", []),
                };
                if (!observationSession.TryAccept(new(handshake, new(snapshot, new(0, 0, []))), 0, out var failure))
                    throw new InvalidOperationException("Save-list observation fixture was refused: " + failure);
            }
            ManualWorldSave[] oldSaves = [new("old-save", "Old save", DateTimeOffset.UnixEpoch, 0)];
            ManualWorldSave[] currentSaves = [new("new-save", "New current save", DateTimeOffset.UnixEpoch, 0), oldSaves[0]];
            AcceptWorld("world-A");
            host.Catalog = WorldActionSmokeCatalog(["A", "B"]);
            OpenWorldMenu(create: false);
            await RefreshWorldListAsync();
            ChooseWorldActionSmokeRow(0);
            await worldSaveHistoryRead;
            FailUiCheckIf(!HandleEscape() || worldMenuOverlay.Visible, "Escape from Load World closes the timeline with its parent panel.");
            host.ManualSaves = currentSaves;
            menuLoadSaveButton.EmitSignal(BaseButton.SignalName.Pressed);
            var loadDeadline = System.Diagnostics.Stopwatch.StartNew();
            while (listedManualSaves.Length != currentSaves.Length && loadDeadline.Elapsed < TimeSpan.FromSeconds(5))
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            FailUiCheckIf(!manualSaveOverlay.Visible || WorldHistoryEmbedded || !manualSaveLoadMode ||
                manualSaveBackButton.TooltipText != "Back to the Pause Menu" || listedManualSaves.Length != 2, "The in-world Load Save button must open the same timeline directly.");
            manualSaveBackButton.EmitSignal(BaseButton.SignalName.Pressed);
            host.ManualSaves = null;
            foreach (var scenario in new[] { "create", "back", "late-failure" })
            {
                AcceptWorld("save-world-A");
                var delayed = new TaskCompletionSource<ManualWorldSave[]>();
                CancellationToken oldToken = default;
                // Ignore cancellation deliberately: transport completion can race
                // cancellation, and only the current opening may publish either outcome.
                var opening = OpenManualSavesAsync(false, token => { oldToken = token; return delayed.Task; });
                FailUiCheckIf(!manualSaveOverlay.Visible || manualSaveCreateButton.Disabled || !manualSaveOverwriteButton.Disabled, "Creating a save must stay usable while its list is slow.");
                if (!manualSaveNewBox.Visible || manualSaveName.Text != DisplayWorldClock(0) || manualSaveList.Placeholder != "Checking saves...")
                    throw new InvalidOperationException($"A new save must open named after the world's date while the list loads: {manualSaveName.Text}.");
                if (scenario == "create")
                {
                    manualSaveName.Text = "New current save";
                    await CreateManualSaveAsync();
                    FailUiCheckIf(host.SaveCreateCount != 1 || manualSaveOverlay.Visible, "The signed create must close its save dialog after success.");
                }
                else manualSaveBackButton.EmitSignal(BaseButton.SignalName.Pressed);
                FailUiCheckIf(!oldToken.IsCancellationRequested, "Closing Save World must cancel its pending list.");
                await OpenManualSavesAsync(false, _ => Task.FromResult(currentSaves));
                manualSaveList.Select(0);
                manualSaveList.EmitSignal(SlotList.SignalName.ItemSelected, 0L);
                manualSaveOverwriteButton.EmitSignal(BaseButton.SignalName.Pressed);
                FailUiCheckIf(!manualSaveOverwriteConfirmation.Visible || pendingOverwriteSaveId != "new-save", "The current selected save must open Overwrite confirmation.");
                manualSaveOverwriteConfirmation.Hide();
                var currentStatus = manualSaveStatus.Text;
                if (scenario == "late-failure") delayed.SetException(new IOException("Controlled stale save-list failure."));
                else delayed.SetResult(oldSaves);
                await opening;
                FailUiCheckIf(manualSaveList.ItemCount != 2 || listedManualSaves[0].Id != "new-save" ||
                    manualSaveList.GetItemTitle(0) != "New current save" ||
                    manualSaveList.GetSelectedItems() is not [0] || manualSaveOverwriteButton.Disabled ||
                    manualSaveDeleteButton.Disabled || manualSaveStatus.Text != currentStatus, "An earlier save-list reply must not replace the current rows, selection or status.");
                manualSaveOverwriteButton.EmitSignal(BaseButton.SignalName.Pressed);
                FailUiCheckIf(!manualSaveOverwriteConfirmation.Visible || pendingOverwriteSaveId != "new-save", "Overwrite must still confirm the current selected save after a stale reply.");
                manualSaveOverwriteConfirmation.Hide();
                manualSaveList.Clear();
                RefreshControlAvailability();
                FailUiCheckIf(!manualSaveLoadButton.Disabled || !manualSaveOverwriteButton.Disabled || !manualSaveDeleteButton.Disabled, "Clearing save rows must disable every selected-save action.");
                manualSaveOverlay.Hide();
            }
            AcceptWorld("save-world-A");
            var oldWorldReply = new TaskCompletionSource<ManualWorldSave[]>();
            var oldWorldOpening = OpenManualSavesAsync(false, _ => oldWorldReply.Task);
            AcceptWorld("save-world-B");
            oldWorldReply.SetResult(oldSaves);
            await oldWorldOpening;
            FailUiCheckIf(manualSaveList.ItemCount != 0 || !manualSaveOverwriteButton.Disabled, "A previous world's save list must not publish after changing worlds.");
            AcceptWorld("save-world-A");
            foreach (var loadMode in new[] { false, true })
            {
                await OpenManualSavesAsync(loadMode, _ => Task.FromException<ManualWorldSave[]>(
                    new IOException("Controlled current save-list failure.")));
                FailUiCheckIf(manualSaveList.Placeholder != "No saves to show." ||
                    !manualSaveStatus.Text.StartsWith("Could not list saves:", StringComparison.Ordinal) ||
                    !manualSaveLoadButton.Disabled || !manualSaveOverwriteButton.Disabled ||
                    !manualSaveDeleteButton.Disabled || manualSaveCreateButton.Disabled != loadMode, "A failed save-list read must end its checking placeholder and keep only creating a new save available.");
                manualSaveOverlay.Hide();
            }
        }
        finally
        {
            worldListRequest.Cancel();
            worldMenuOverlay.Hide();
            listedWorlds = [];
            listedActiveWorldId = null;
            worldSelectionList.Clear();
            manualSaveOverlay.Hide();
            manualSaveOverwriteConfirmation.Hide();
            pendingOverwriteSaveId = null;
            RestoreOwnerCheckConnection(previousConnection);
            observationSession.ResetAfterLoad();
            if (previousObservation is not null)
                observationSession.TryAccept(previousObservation, previousObservation.Baseline.Events.AfterEventId, out _);
            System.Environment.SetEnvironmentVariable("CI", previousCi);
        }
    }

    private async Task VerifyLoadWorldTimelineAsync()
    {
        var previousConnection = CaptureOwnerCheckConnection();
        var previousObservation = observationSession.Current;
        var previousInWorld = isInWorld;
        var previousCi = System.Environment.GetEnvironmentVariable("CI");
        System.Environment.SetEnvironmentVariable("CI", "true");
        using var signer = OwnerDeviceKey.CreateEphemeralForContinuousIntegration();
        try
        {
            foreach (var chooseOlder in new[] { true, false })
            {
                using var host = new WorldActionSmokeHost(signer.PublicKeySpkiBase64);
                ConnectOwnerCheck(host, signer);
                observationSession.ReplaceRegistration(registration);
                host.Catalog = WorldActionSmokeCatalog(["A", "B"]);
                var branch = new SaveBranch("first", 1);
                var fork = new SaveBranch("fork", 2, "old", "Before the flood", 10);
                host.ManualSaves =
                [new("old", "Before the flood", DateTimeOffset.UnixEpoch, 10, false, branch, BranchPosition: 1),
                 new("later", "First future", DateTimeOffset.UnixEpoch.AddMinutes(1), 90, false, branch, BranchPosition: 2),
                 new("forked", "Another future", DateTimeOffset.UnixEpoch.AddMinutes(2), 50, false, fork, BranchPosition: 1)];
                host.ReleasePause.SetResult();
                host.ReleaseSelect.SetResult();
                host.SelectedWorld = host.Catalog.Worlds.Single(world => world.Id == "B");
                host.LoadReceipt = new(chooseOlder ? "old" : "forked", "backup", chooseOlder ? 10 : 50);
                var handshake = new OwnerWorldHandshake(new(1, 1),
                    ["owner-observation.read.v1", "owner-event-presentation.v2", "agent-inspection.read.v1", "spatial-knowledge.read.v1",
                     "owner-control.request.v1", "paused-authoring.request.v1"], []);
                var snapshot = new OwnerWorldSnapshot("world-B", host.LoadReceipt.WorldTick, "load-world-map",
                    [new(0, 0, "meadow")], [], [], null, 0)
                {
                    FounderSetup = new(4, 4, true),
                    Authoring = new(true, 0, 0, 0, "load-world-map", "load-world-map", "clear", "spring", []),
                };
                host.Reconnect = new(handshake, new(snapshot, new(snapshot.WorldTick, 0, [])));
                ShowMainMenu();
                OpenWorldMenu(create: false);
                await RefreshWorldListAsync();
                ChooseWorldActionSmokeRow(1);
                await worldSaveHistoryRead;
                for (var frame = 0; frame < 4; frame++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                FailUiCheckIf(!manualSaveTimeline.IsVisibleInTree() || manualSaveTimeline.Lanes.Count != 2 ||
                    !manualSaveViewChoice.IsVisibleInTree() || !manualSaveDetails.IsVisibleInTree() ||
                    worldSelectButton.Disabled || observationSession.Current is not null || host.SelectReceived.Task.IsCompleted, "Load World must draw every branch and the shared controls without activating the selected world.");
                if (chooseOlder)
                {
                    // Click the older branch's point through the real timeline input handler.
                    var rows = manualSaveTimeline.Rows;
                    rows._GuiInput(new InputEventMouseButton
                    {
                        ButtonIndex = MouseButton.Left,
                        Pressed = true,
                        Position = rows.PointPosition("old") ?? throw new InvalidOperationException("The older save must be drawn."),
                    });
                    FailUiCheckIf(SelectedManualSave()?.Id != "old" || !manualSaveDetails.FindChildren("*", nameof(Label), true, false)
                        .OfType<Label>().Any(label => label is FittedLabel fitted && fitted.FullText == "Before the flood"), "Choosing another branch's older save must update the shared description card.");
                    for (var frame = 0; frame < 3; frame++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                    var visible = worldMenuScroll.GetGlobalRect().Grow(2);
                    FailUiCheckIf(menuLayer.Size.X >= 920 && (!visible.Encloses(manualSaveTimeline.GetGlobalRect()) ||
                        !visible.Encloses(worldSelectButton.GetGlobalRect())), "Load World must keep its timeline and Load button in view without outer scrolling at the default size.");
                    var evidence = OS.GetEnvironment("CLANKERWORLD_LOAD_TIMELINE_EVIDENCE");
                    if (!string.IsNullOrEmpty(evidence))
                    {
                        using var capture = GetViewport().GetTexture().GetImage();
                        FailUiCheckIf(capture.SavePng(evidence) != Error.Ok, "Load World timeline evidence could not be saved.");
                    }
                    manualSaveViewChoice.Select(1);
                    manualSaveViewChoice.EmitSignal(SegmentedChoice.SignalName.ItemSelected, 1L);
                    FailUiCheckIf(!manualSaveList.IsVisibleInTree() || SelectedManualSave()?.Id != "old", "The shared List switch must keep the timeline's selected save.");
                }
                await SelectListedWorldAsync();
                if (await host.SelectReceived.Task.WaitAsync(TimeSpan.FromSeconds(5)) != "B" ||
                    await host.LoadReceived.Task.WaitAsync(TimeSpan.FromSeconds(5)) != (chooseOlder ? "old" : "forked") ||
                    !isInWorld || worldMenuOverlay.Visible || mainMenuOverlay.Visible ||
                    observationSession.Current?.Baseline.Snapshot is not { WorldId: "world-B", Authoring.IsPaused: true })
                    throw new InvalidOperationException("Load must select the chosen world, load the chosen save (or newest), and enter it paused.");
                manualSaveShowsList = false;
            }
            GD.Print("NATIVE_LOAD_WORLD_TIMELINE selectionBranchesListChosenAndNewestPausedLoad=passed");
        }
        finally
        {
            worldMenuOverlay.Hide();
            manualSaveOverlay.Hide();
            RestoreOwnerCheckConnection(previousConnection);
            isInWorld = previousInWorld;
            observationSession.ReplaceRegistration(previousConnection.Registration);
            if (previousObservation is not null)
                observationSession.TryAccept(previousObservation, previousObservation.Baseline.Events.AfterEventId, out _);
            System.Environment.SetEnvironmentVariable("CI", previousCi);
        }
    }

}
