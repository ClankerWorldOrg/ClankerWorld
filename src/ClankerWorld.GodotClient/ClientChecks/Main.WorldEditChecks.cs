using System.Net;
using System.Text.Json;
using ClankerWorld.GodotClient.Pairing;
using ClankerWorld.GodotClient.Protocol;
using ClankerWorld.GodotClient.UI;
using Godot;

namespace ClankerWorld.GodotClient;

public partial class Main
{
    private async Task VerifyWorldEditSmokeAsync()
    {
        try
        {
            await VerifyWorldEditPickerAsync();
            GD.Print("World edit picker checks passed.");
            GetTree().Quit();
        }
        catch (Exception exception)
        {
            GD.PushError(exception.ToString());
            GetTree().Quit(1);
        }
    }

    private async Task VerifyWorldEditPickerAsync()
    {
        var previousRegistration = registration;
        var previousKey = deviceKey;
        var previousUrl = serverUrlInput.Text;
        var previousObservation = observationSession.Current;
        var previousMap = renderedMapSnapshot;
        var previousRenderedGeneration = renderedObservationGeneration;
        var previousPending = pendingSubmission;
        var previousDeveloperMode = developerMode;
        var previousInWorld = isInWorld;
        var previousMainMenu = mainMenuOverlay.Visible;
        var previousCi = System.Environment.GetEnvironmentVariable("CI");
        System.Environment.SetEnvironmentVariable("CI", "true");
        using var signer = OwnerDeviceKey.CreateEphemeralForContinuousIntegration();
        using var host = new WorldActionSmokeHost(signer.PublicKeySpkiBase64);
        var bytes = new byte[256 * 128];
        var snapshot = new OwnerWorldSnapshot("world-edit-picker", 0, "picker-map", [], [], [], null, 0)
        {
            PackedTerrain = new(256, 128, "terrain-kind-v1", Convert.ToBase64String(bytes)),
            Authoring = new(true, 0, 0, 0, "picker-map", "picker-map", "clear", "spring", [])
            {
                EditTools =
                [
                    new("set_terrain", "Terrain", [new("meadow", "Meadow"), new("mountain", "Mountains")]),
                    new("place_resource", "Resource", [new("food", "Berry bush", true)]),
                    new("place_object", "Object", [new("bedroll", "Bedroll")]),
                ],
            },
        };
        host.Reconnect = new(new(new(1, 1), ["owner-observation.read.v1", "owner-event-presentation.v2",
            "agent-inspection.read.v1", "spatial-knowledge.read.v1", "owner-control.request.v1", "paused-authoring.request.v1"], []),
            new(snapshot, new(0, 0, [])));
        OwnerAuthoringBatchAction? sent = null;
        var refuse = false;
        var refuseWithReceipt = false;
        var jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        host.ActionHandler = (path, envelope, response) =>
        {
            if (path != OwnerPairingEndpoints.OwnerAuthoring) return Task.FromResult<object?>(null);
            sent = envelope.GetProperty("action").Deserialize<OwnerAuthoringBatchAction>(jsonOptions)!;
            if (refuse)
            {
                response.StatusCode = (int)HttpStatusCode.Conflict;
                return Task.FromResult<object?>(new { code = "edit_refused", detail = "Peaks can't hold a building." });
            }
            if (refuseWithReceipt)
                return Task.FromResult<object?>(new OwnerAuthoringBatchReceipt(sent.BatchId, false, "That tile is occupied.", 0, 0, "picker-map"));
            bytes[127 * 256 + 255] = TerrainCodes.Encode("mountain");
            snapshot = snapshot with { PackedTerrain = snapshot.PackedTerrain! with { Data = Convert.ToBase64String(bytes) } };
            host.Reconnect = host.Reconnect! with { Baseline = host.Reconnect.Baseline with { Snapshot = snapshot } };
            return Task.FromResult<object?>(new OwnerAuthoringBatchReceipt(sent.BatchId, true, null, 0, 0, "picker-map"));
        };
        try
        {
            await WaitForWorldPreviewSmokeAsync(() => !isRefreshing && !isOwnerAction, "world-edit picker setup");
            registration = new(host.Authority, "smoke-device", signer.PublicKeyFingerprint, host.Address);
            deviceKey = signer;
            serverUrlInput.Text = host.Address;
            pendingSubmission = null;
            developerMode = true;
            isInWorld = true;
            mainMenuOverlay.Hide();
            pauseMenuPanel.Hide();
            developerPanel.Show();
            worldEditAdvanced.ButtonPressed = false;
            if (!observationSession.TryAccept(host.Reconnect, 0, out var failure))
                throw new InvalidOperationException($"The picker needs a confirmed baseline: {failure}");
            renderedObservationGeneration = observationSession.RequestGeneration;
            Render(snapshot, []);
            if (worldEditTool.ItemCount != 3 || worldEditPick.ItemCount != 2 || worldEditAdvancedBody.Visible ||
                worldEditTool.GetItemText(0) != "Terrain" || worldEditPick.GetItemText(1) != "Mountains")
                throw new InvalidOperationException("Edit world must offer only the server's named tools and kinds, with typed fields hidden.");
            worldEditPick.Select(1);
            worldEditPick.EmitSignal(OptionButton.SignalName.ItemSelected, 1);
            if (!worldEditPaint.ButtonPressed)
                throw new InvalidOperationException("Choosing a kind must activate map picking without another step.");
            ActivateMapTile(snapshot, new(255, 127), Vector2.Zero);
            HandleMapInput(new InputEventMouseMotion
            {
                Position = KeyboardMapCanvasPoint(new(254, 127)),
                ButtonMask = MouseButtonMask.Left,
            });
            ActivateMapTile(snapshot, new(255, 127), Vector2.Zero);
            ActivateMapTile(snapshot, new(256, 127), Vector2.Zero);
            if (worldEditPreview.Tiles.Count != 2 || worldEditPreview.KindName != "Mountains" || submitWorldEditButton.Disabled ||
                TerrainCodes.NameFor(terrainMap!.At(255, 127)) != "Meadow")
                throw new InvalidOperationException("Picking must preview each valid tile once without changing the observed map.");
            // A fresh deserialization of the same catalog must preserve the preview.
            Render(snapshot with
            {
                Authoring = snapshot.Authoring! with
                {
                    EditTools = snapshot.Authoring.EditTools.Select(tool =>
                tool with { Kinds = tool.Kinds.ToArray() }).ToArray()
                }
            }, []);
            if (worldEditTiles.Count != 2) throw new InvalidOperationException("Normal refreshes must preserve the preview.");
            await SubmitWorldEditAsync();
            if (sent?.Operations.Count != 2 || sent.Operations.Any(operation => operation.Kind != "set_terrain" || operation.Value != "mountain") ||
                worldEditTiles.Count != 0 || pendingSubmission is not null || TerrainCodes.NameFor(terrainMap!.At(255, 127)) != "Mountain")
                throw new InvalidOperationException($"Apply must send the preview as one batch and clear it after confirmation: operations={sent?.Operations.Count}, tiles={worldEditTiles.Count}, pending={pendingSubmission is not null}, terrain={terrainMap!.At(255, 127)}, status={statusLabel.Text}.");
            worldEditTool.Select(1);
            worldEditTool.EmitSignal(OptionButton.SignalName.ItemSelected, 1);
            if (worldEditPick.ItemCount != 1 || worldEditPick.GetItemText(0) != "Berry bush")
                throw new InvalidOperationException("Choosing Resource must show its supported kinds.");
            ActivateMapTile(snapshot, new(255, 127), Vector2.Zero);
            var before = terrainMap;
            refuse = true;
            await SubmitWorldEditAsync();
            if (sent?.Operations is not [{ Kind: "place_resource", Value: "food", IsRenewable: true }] ||
                !statusLabel.Text.Contains("Peaks can't hold a building.", StringComparison.Ordinal) ||
                !statusLabel.Text.StartsWith("World edit refused; nothing changed.", StringComparison.Ordinal) ||
                !ReferenceEquals(before, terrainMap) || worldEditTiles.Count != 1 || pendingSubmission is not null || isOwnerAction)
                throw new InvalidOperationException("A refusal must show the server's reason, preserve the world and preview, and release request recovery.");
            refuse = false;
            refuseWithReceipt = true;
            await SubmitWorldEditAsync();
            if (!statusLabel.Text.Contains("That tile is occupied.", StringComparison.Ordinal) || pendingSubmission is not null || worldEditTiles.Count != 1)
                throw new InvalidOperationException($"A refused success-status receipt must also preserve the preview and show a refusal: tiles={worldEditTiles.Count}, pending={pendingSubmission is not null}, status={statusLabel.Text}.");
            using (var draft = new PanelDraft(this))
                Render(snapshot with { Authoring = snapshot.Authoring! with { EditTools = snapshot.Authoring.EditTools.Reverse().ToArray() } }, []);
            if (PickedWorldEditTool?.Operation != "place_resource" || PickedWorldEditKind?.Value != "food" ||
                worldEditPick.GetItemText(worldEditPick.Selected) != "Berry bush" || worldEditTiles.Count != 0)
                throw new InvalidOperationException("Reordered server choices must retain the selected tool and kind together and clear the old preview.");
            var running = host.Reconnect! with
            {
                Baseline = host.Reconnect.Baseline with
                {
                    Snapshot = snapshot with { Authoring = snapshot.Authoring! with { IsPaused = false } },
                },
            };
            if (!observationSession.TryAccept(running, 0, out failure)) throw new InvalidOperationException(failure);
            Render(running.Baseline.Snapshot, []);
            if (!submitWorldEditButton.Disabled || worldEditPreview.Tiles.Count != 0 || worldEditPaint.ButtonPressed)
                throw new InvalidOperationException("Resuming must stop picking and clear uncommitted tiles.");
            Render(snapshot with { WorldId = "another-picker-world" }, []);
            if (worldEditTiles.Count != 0) throw new InvalidOperationException("A world switch must discard the old preview.");
            Render(snapshot with { Authoring = snapshot.Authoring! with { EditTools = [] } }, []);
            if (worldEditTool.ItemCount != 0 || !worldEditPaint.Disabled || !submitWorldEditButton.Disabled)
                throw new InvalidOperationException("A server with no editing tools must not retain another world's choices.");
        }
        finally
        {
            CloseDeveloperTools();
            pendingSubmissionStore.TryForget();
            registration = previousRegistration;
            deviceKey = previousKey;
            serverUrlInput.Text = previousUrl;
            if (previousObservation is not null)
                observationSession.TryAccept(previousObservation, previousObservation.Baseline.Events.AfterEventId, out _);
            renderedObservationGeneration = previousRenderedGeneration;
            pendingSubmission = previousPending;
            developerMode = previousDeveloperMode;
            isInWorld = previousInWorld;
            mainMenuOverlay.Visible = previousMainMenu;
            if (previousMap is not null) Render(previousMap, []);
            RefreshControlAvailability();
            System.Environment.SetEnvironmentVariable("CI", previousCi);
        }
    }
}
