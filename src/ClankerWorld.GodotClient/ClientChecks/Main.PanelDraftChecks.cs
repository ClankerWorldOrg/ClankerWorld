using ClankerWorld.GodotClient.UI;
using Godot;

namespace ClankerWorld.GodotClient;

public partial class Main
{
    // Content checks read the whole caption; font/layout checks read Label.Text.
    private static string CaptionForChecks(Label label) => label is FittedLabel fitted ? fitted.FullText : label.Text;

    private void VerifyPanelDraftPreservation()
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
        uiLayer.RemoveChild(panel);
    }
}
