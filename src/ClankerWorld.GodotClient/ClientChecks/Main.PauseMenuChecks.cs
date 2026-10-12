using ClankerWorld.GodotClient.Pairing;
using ClankerWorld.GodotClient.Protocol;

namespace ClankerWorld.GodotClient;

public partial class Main
{
    private async Task VerifyPauseMenuResumeAsync()
    {
        var previousRegistration = registration;
        var previousKey = deviceKey;
        var previousUrl = serverUrlInput.Text;
        var previousObservation = observationSession.Current;
        var previousInWorld = isInWorld;
        var previousMainMenu = mainMenuOverlay.Visible;
        var previousReturnToMainMenu = returnToMainMenu;
        var previousCi = System.Environment.GetEnvironmentVariable("CI");
        System.Environment.SetEnvironmentVariable("CI", "true");
        using var signer = OwnerDeviceKey.CreateEphemeralForContinuousIntegration();
        try
        {
            // Check the ordinary running and deliberate-pause controls before the delayed case.
            foreach (var (initiallyPaused, delayResume, reopenAgain, switchWorld) in new[]
                { (false, false, false, false), (true, false, false, false), (false, true, false, false),
                  (false, true, true, false), (false, true, false, true) })
            {
                using var host = new WorldActionSmokeHost(signer.PublicKeySpkiBase64);
                using var nextHost = switchWorld ? new WorldActionSmokeHost(signer.PublicKeySpkiBase64) : null;
                var handshake = new OwnerWorldHandshake(new(1, 1),
                    ["owner-observation.read.v1", "agent-inspection.read.v1", "spatial-knowledge.read.v1",
                     "owner-control.request.v1", "paused-authoring.request.v1"], []);
                var snapshot = new OwnerWorldSnapshot("pause-menu-world", 0, "pause-menu-map", [new(0, 0, "meadow")],
                    [], [], null, 0)
                {
                    FounderSetup = new(4, 4, true),
                    Authoring = new(initiallyPaused, 0, 0, 0, "pause-menu-map", "pause-menu-map", "clear", "spring", []),
                };
                OwnerWorldReconnect Reply(bool paused, string? worldId = null) => new(handshake, new(snapshot with
                {
                    WorldId = worldId ?? snapshot.WorldId,
                    Authoring = snapshot.Authoring! with { IsPaused = paused },
                }, new(0, 0, [])));
                var resumeReceived = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                var releaseResume = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                var secondResumeReceived = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                var releaseSecondResume = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                var resumes = 0;
                var pausedAtHost = initiallyPaused;
                var nextResumes = 0;
                var nextPaused = true;
                if (nextHost is not null)
                {
                    nextHost.Reconnect = Reply(true, "next-pause-menu-world");
                    nextHost.ControlHandler = paused =>
                    {
                        if (!paused) nextResumes++;
                        nextPaused = paused;
                        nextHost.Reconnect = Reply(paused, "next-pause-menu-world");
                        return Task.FromResult(new OwnerControlReceipt(paused ? "pause" : "resume", true, paused, 0, 0, 0));
                    };
                }
                host.Reconnect = Reply(initiallyPaused);
                host.ControlHandler = async paused =>
                {
                    var resumeNumber = paused ? 0 : Interlocked.Increment(ref resumes);
                    if (resumeNumber == 1 && delayResume)
                    {
                        resumeReceived.TrySetResult();
                        await releaseResume.Task.ConfigureAwait(false);
                    }
                    if (resumeNumber == 2 && reopenAgain)
                    {
                        secondResumeReceived.TrySetResult();
                        await releaseSecondResume.Task.ConfigureAwait(false);
                    }
                    pausedAtHost = paused;
                    host.Reconnect = Reply(paused);
                    return new OwnerControlReceipt(paused ? "pause" : "resume", true, paused, 0, 0, 0);
                };
                registration = new(host.Authority, "smoke-device", signer.PublicKeyFingerprint, host.Address);
                deviceKey = signer;
                serverUrlInput.Text = host.Address;
                observationSession.ResetAfterLoad();
                isInWorld = true;
                mainMenuOverlay.Hide();
                returnToMainMenu = false;
                ClosePauseMenu();
                await RefreshAsync().WaitAsync(TimeSpan.FromSeconds(5));
                if (observationSession.Current?.Baseline.Snapshot.Authoring?.IsPaused != initiallyPaused)
                    throw new InvalidOperationException("The pause-menu fixture must first display its signed initial observation.");
                Task? firstClosing = null;
                Task? secondOpening = null;
                Task? secondClosing = null;
                Task? thirdOpening = null;
                try
                {
                    await TogglePauseMenuAsync().WaitAsync(TimeSpan.FromSeconds(5));
                    if (!pauseMenuPanel.Visible || !pausedAtHost || menuPausedWorld == initiallyPaused)
                        throw new InvalidOperationException("Opening the first menu must pause and remember whether the world was running.");
                    firstClosing = ClosePauseMenuAsync();
                    if (delayResume)
                    {
                        await resumeReceived.Task.WaitAsync(TimeSpan.FromSeconds(5));
                        if (nextHost is not null)
                        {
                            registration = new(nextHost.Authority, "smoke-device", signer.PublicKeyFingerprint, nextHost.Address);
                            serverUrlInput.Text = nextHost.Address;
                            await RefreshAsync().WaitAsync(TimeSpan.FromSeconds(5));
                            if (observationSession.Current?.Baseline.Snapshot.WorldId != "next-pause-menu-world")
                                throw new InvalidOperationException("The next deliberately paused world must be displayed before opening its menu.");
                        }
                        secondOpening = TogglePauseMenuAsync();
                        if (!pauseMenuPanel.Visible || host.PauseCount != 1)
                            throw new InvalidOperationException("The reopened menu must queue its pause behind the held Resume.");
                        if (reopenAgain)
                        {
                            secondClosing = ClosePauseMenuAsync();
                        }
                        releaseResume.TrySetResult();
                        await Task.WhenAll(firstClosing, secondOpening).WaitAsync(TimeSpan.FromSeconds(5));
                        if (reopenAgain)
                        {
                            await secondResumeReceived.Task.WaitAsync(TimeSpan.FromSeconds(5));
                            thirdOpening = TogglePauseMenuAsync();
                            if (!pauseMenuPanel.Visible || host.PauseCount != 2)
                                throw new InvalidOperationException("A third menu must wait behind the second Resume after the first one has completed.");
                            releaseSecondResume.TrySetResult();
                        }
                        if (secondClosing is not null && thirdOpening is not null)
                            await Task.WhenAll(secondClosing, thirdOpening).WaitAsync(TimeSpan.FromSeconds(5));
                    }
                    else
                    {
                        await firstClosing.WaitAsync(TimeSpan.FromSeconds(5));
                        secondOpening = TogglePauseMenuAsync();
                        await secondOpening.WaitAsync(TimeSpan.FromSeconds(5));
                    }
                    await ClosePauseMenuAsync().WaitAsync(TimeSpan.FromSeconds(5));
                    if (nextHost is not null)
                    {
                        if (pauseMenuPanel.Visible || !nextPaused || nextHost.PauseCount != 1 || nextResumes != 0 ||
                            host.PauseCount != 1 || resumes != 1 ||
                            observationSession.Current?.Baseline.Snapshot.Authoring?.IsPaused != true)
                            throw new InvalidOperationException("A pending Resume from the old world must not resume the next deliberately paused world.");
                        continue;
                    }
                    var menuCount = reopenAgain ? 3 : 2;
                    if (pauseMenuPanel.Visible || pausedAtHost != initiallyPaused || host.PauseCount != menuCount ||
                        resumes != (initiallyPaused ? 0 : menuCount) ||
                        observationSession.Current?.Baseline.Snapshot.Authoring?.IsPaused != initiallyPaused)
                        throw new InvalidOperationException($"Closing all menus must restore the initial pause state: delayed={delayResume}, initiallyPaused={initiallyPaused}, finalPaused={pausedAtHost}, pauses={host.PauseCount}, resumes={resumes}.");
                }
                finally
                {
                    releaseResume.TrySetResult();
                    releaseSecondResume.TrySetResult();
                    if (firstClosing is not null) await firstClosing.WaitAsync(TimeSpan.FromSeconds(5));
                    if (secondOpening is not null) await secondOpening.WaitAsync(TimeSpan.FromSeconds(5));
                    if (secondClosing is not null) await secondClosing.WaitAsync(TimeSpan.FromSeconds(5));
                    if (thirdOpening is not null) await thirdOpening.WaitAsync(TimeSpan.FromSeconds(5));
                    ClosePauseMenu();
                }
            }
        }
        finally
        {
            registration = previousRegistration;
            deviceKey = previousKey;
            serverUrlInput.Text = previousUrl;
            observationSession.ResetAfterLoad();
            if (previousObservation is not null)
                observationSession.TryAccept(previousObservation, previousObservation.Baseline.Events.AfterEventId, out _);
            isInWorld = previousInWorld;
            mainMenuOverlay.Visible = previousMainMenu;
            returnToMainMenu = previousReturnToMainMenu;
            System.Environment.SetEnvironmentVariable("CI", previousCi);
            RefreshControlAvailability();
            RefreshMainMenuAvailability();
            statusToast.Hide();
        }
    }
}
