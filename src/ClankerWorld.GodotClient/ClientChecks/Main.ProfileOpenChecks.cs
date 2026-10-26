using ClankerWorld.GodotClient.Protocol;
using ClankerWorld.Simulation;
using Godot;
using System.Diagnostics;
using System.Globalization;
using System.Text.Json;

namespace ClankerWorld.GodotClient;

public partial class Main
{
    // Other UI checks have already opened profiles. This needs a fresh process.
    private async Task VerifyProfileOpenAsync()
    {
        using var errors = new ProfileCheckErrors();
        OS.AddLogger(errors);
        try
        {
            var args = OS.GetCmdlineUserArgs();
            var input = args.Single(arg => arg.StartsWith("--profile-snapshot=", StringComparison.Ordinal))[19..];
            var reconnect = JsonSerializer.Deserialize<OwnerWorldReconnect>(File.ReadAllText(input))
                ?? throw new InvalidOperationException("The profile check needs a generated owner baseline.");
            var snapshot = reconnect.Baseline.Snapshot;
            FailUiCheckIf(snapshot.Agents.Count < 5 || snapshot.Agents.All(agent => BoundedText.IsValid(agent.Id, 127)) ||
                snapshot.CalendarPace is not { } pace || snapshot.WorldTick < pace.TicksPerDay * 3L, "The profile check needs a hard generated world after three game days, including a born child.");
            if (!observationSession.TryAccept(reconnect, 0, out var failure))
                throw new InvalidOperationException(failure);

            DisplayServer.WindowSetMode(DisplayServer.WindowMode.Windowed);
            var dimensions = args.Single(arg => arg.StartsWith("--profile-window=", StringComparison.Ordinal))[17..].Split('x');
            GetWindow().Size = new(int.Parse(dimensions[0], CultureInfo.InvariantCulture), int.Parse(dimensions[1], CultureInfo.InvariantCulture));
            GetWindow().ContentScaleSize = GetWindow().Size;
            mainMenuOverlay.Hide();
            isInWorld = true;
            // Finish map loading before selection, as in the owner's report.
            // No profile is opened as a warm-up.
            Render(snapshot, reconnect.Baseline.Events.Events);
            for (var frame = 0; frame < 5; frame++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            ApplyUiScale();
            for (var frame = 0; frame < 5; frame++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

            var ordinal = 0;
            void Record(string stage, TimeSpan elapsed)
            {
                GD.Print(FormattableString.Invariant($"PROFILE_OPEN ordinal={ordinal} cold={ordinal == 0} stage={stage} elapsed_ms={elapsed.TotalMilliseconds:F2}"));
                if (elapsed > TimeSpan.FromSeconds(2))
                    throw new InvalidOperationException($"Profile {ordinal} stalled during {stage}.");
                FailUiCheckIf(errors.Count != 0, "Godot reported an error while opening a profile; see the preceding engine log.");
            }
            void Timed(string stage, Action action)
            {
                var start = Stopwatch.GetTimestamp();
                action();
                Record(stage, Stopwatch.GetElapsedTime(start));
            }
            async Task Frame(string stage)
            {
                var start = Stopwatch.GetTimestamp();
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                Record(stage, Stopwatch.GetElapsedTime(start));
            }
            void Click(Button button)
            {
                var point = button.GetGlobalRect().GetCenter();
                GetViewport().PushInput(new InputEventMouseMotion { Position = point, GlobalPosition = point }, true);
                GetViewport().PushInput(new InputEventMouseButton
                {
                    Position = point,
                    GlobalPosition = point,
                    ButtonIndex = MouseButton.Left,
                    ButtonMask = MouseButtonMask.Left,
                    Pressed = true,
                }, true);
                GetViewport().PushInput(new InputEventMouseButton
                {
                    Position = point,
                    GlobalPosition = point,
                    ButtonIndex = MouseButton.Left,
                    Pressed = false,
                }, true);
            }
            async Task OpenAndClose(OwnerWorldAgent agent)
            {
                if (!IsDeceased(agent))
                {
                    FailUiCheckIf(!agentQuickCard.IsVisibleInTree() || quickCardProfileButton.Disabled, "The profile check must start from the selected agent's usable quick card.");
                    Timed("profile-button", () => Click(quickCardProfileButton));
                }
                // Observe the following frames, including native text layout
                // and drawing, rather than timing only the click handler.
                for (var frame = 0; frame < 8; frame++) await Frame($"layout-{frame}");
                var bounds = agentProfilePanel.GetGlobalRect();
                FailUiCheckIf(!agentProfilePanel.IsVisibleInTree() || selectedActorNameLabel.FullText != agent.DisplayName ||
                    !GetViewportRect().Grow(1).Encloses(bounds) || !profileCloseButton.IsVisibleInTree(), "Every generated agent must open a readable profile with Back or Close inside the window.");
                var settled = agentProfilePanel.GetRect();
                for (var frame = 0; frame < 3; frame++)
                {
                    await Frame($"settled-{frame}");
                    FailUiCheckIf(agentProfilePanel.GetRect() != settled, "The profile kept changing size after its initial layout.");
                }
                Timed("back-button", () => Click(profileCloseButton));
                await Frame("back-frame");
                FailUiCheckIf(agentProfilePanel.Visible || !IsDeceased(agent) && !agentQuickCard.IsVisibleInTree(), "Back or Close must respond and return living agents to the quick card.");
            }

            foreach (var agent in snapshot.Agents)
            {
                Timed("select", () => SelectAgent(agent.Id));
                for (var frame = 0; frame < 3; frame++) await Frame($"quick-card-{frame}");
                await OpenAndClose(agent);
                ClearAgentSelection();
                ordinal++;
            }
            // Missing optional data must leave the rest of the profile usable.
            var first = snapshot.Agents[0];
            var incomplete = first with { Survival = null, Equipment = null, Skills = [], Proficiency = null, RecentPrivateThoughts = [] };
            selectedAgentId = first.Id;
            Timed("missing-data", () => RenderSelectedAgentCard(snapshot with
            {
                Agents = snapshot.Agents.Select(agent => agent.Id == first.Id ? incomplete : agent).ToArray(),
            }));
            await OpenAndClose(incomplete);
            FailUiCheckIf(profileWarmthMeter.Visible || privateThoughtHistory.Text != "None recorded yet.", "Unavailable inspection data must not prevent the profile from working.");
            GD.Print($"PROFILE_OPEN_CHECK passed agents={snapshot.Agents.Count} tick={snapshot.WorldTick} window={GetWindow().Size}");
            GetTree().Quit();
        }
        catch (Exception exception)
        {
            GD.PushError(exception.ToString());
            GetTree().Quit(1);
        }
        finally
        {
            OS.RemoveLogger(errors);
        }
    }

    private sealed partial class ProfileCheckErrors : Logger
    {
        private int count;
        public int Count => Volatile.Read(ref count);

        public override void _LogError(string function, string file, int line, string code, string rationale,
            bool editorNotify, int errorType, Godot.Collections.Array<ScriptBacktrace> scriptBacktraces)
        {
            if (errorType != (int)ErrorType.Warning) Interlocked.Increment(ref count);
        }
    }
}
