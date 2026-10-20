using ClankerWorld.GodotClient.UI;
using Godot;

namespace ClankerWorld.GodotClient;

public partial class Main
{
    // Content checks read the whole caption; font/layout checks read Label.Text.
    private static string CaptionForChecks(Label label) => label is FittedLabel fitted ? fitted.FullText : label.Text;

    private async Task VerifyPanelDraftPreservationAsync()
    {
        using var panel = new VBoxContainer();
        using var name = new LineEdit { Text = "Zoë 🌲 de la Montaña" };
        using var message = new TextEdit { Text = "A half-typed message" };
        using var choice = new OptionButton();
        choice.AddItem("First", 1);
        choice.AddItem("Second", 2);
        choice.SetItemMetadata(0, "first");
        choice.SetItemMetadata(1, "second");
        choice.Select(1);
        panel.AddChild(name);
        panel.AddChild(message);
        panel.AddChild(choice);
        uiLayer.AddChild(panel);
        name.CaretColumn = 4;
        name.Select(0, 3);
        message.SetCaretColumn(7);
        using (var draft = new PanelDraft(panel))
        {
            name.Text = "Server name";
            message.Text = "Refreshed text";
            choice.Clear();
            choice.AddItem("Second", 2);
            choice.AddItem("First", 1);
            choice.SetItemMetadata(0, "second");
            choice.SetItemMetadata(1, "first");
            choice.Select(1);
        }
        if (name.Text != "Zoë 🌲 de la Montaña" || name.CaretColumn != 4 ||
            name.GetSelectedText() != "Zoë" || message.Text != "A half-typed message" ||
            message.GetCaretColumn() != 7 || choice.GetSelectedId() != 2)
            throw new InvalidOperationException("A refresh must preserve text, caret, selection and selected identity.");
        panel.Free();

        using var caption = new FittedLabel
        {
            Text = new string('W', 80),
            Position = new(20, 20),
            Size = new(120, 30),
            ZIndex = 1000,
        };
        menuLayer.AddChild(caption);
        var viewport = GetViewport();
        var previousPointer = viewport.GetMousePosition();
        try
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            var pointer = caption.GetGlobalRect().GetCenter();
            viewport.NotifyMouseEntered();
            viewport.PushInput(new InputEventMouseMotion { Position = pointer, GlobalPosition = pointer }, true);
            if (caption.Text == caption.FullText || viewport.GuiGetHoveredControl() != caption ||
                caption.TooltipText != caption.FullText)
                throw new InvalidOperationException($"A shortened name must receive pointer hover: shortened={caption.Text != caption.FullText}, hover={viewport.GuiGetHoveredControl()?.GetType().Name}, filter={caption.MouseFilter}, rect={caption.GetGlobalRect()}, tooltipKept={caption.TooltipText == caption.FullText}.");
        }
        finally
        {
            viewport.PushInput(new InputEventMouseMotion { Position = previousPointer, GlobalPosition = previousPointer }, true);
            caption.Free();
        }
    }
}
