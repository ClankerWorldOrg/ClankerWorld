using Godot;

namespace ClankerWorld.GodotClient.UI;

/// <summary>Refreshes update facts while visible edits, selected values and scroll stay with the player.</summary>
public sealed class PanelDraft : IDisposable
{
    private readonly List<Action> restore = [];

    public PanelDraft(Node root)
    {
        Capture(root);
    }

    private void Capture(Node node)
    {
        if (node is Control control && !control.IsVisibleInTree()) return;
        switch (node)
        {
            case LineEdit line when line.Editable:
                var text = line.Text;
                var caret = line.CaretColumn;
                var from = line.HasSelection() ? line.GetSelectionFromColumn() : 0;
                var to = line.HasSelection() ? line.GetSelectionToColumn() : 0;
                restore.Add(() =>
                {
                    if (!GodotObject.IsInstanceValid(line)) return;
                    if (line.Text != text) line.Text = text;
                    line.CaretColumn = caret;
                    if (from != to) line.Select(from, to);
                });
                break;
            case TextEdit edit when edit.Editable:
                var draft = edit.Text;
                var row = edit.GetCaretLine();
                var column = edit.GetCaretColumn();
                var selected = edit.HasSelection();
                var startRow = selected ? edit.GetSelectionFromLine() : 0;
                var startColumn = selected ? edit.GetSelectionFromColumn() : 0;
                var endRow = selected ? edit.GetSelectionToLine() : 0;
                var endColumn = selected ? edit.GetSelectionToColumn() : 0;
                restore.Add(() =>
                {
                    if (!GodotObject.IsInstanceValid(edit)) return;
                    if (edit.Text != draft) edit.Text = draft;
                    edit.SetCaretLine(row);
                    edit.SetCaretColumn(column);
                    if (selected) edit.Select(startRow, startColumn, endRow, endColumn);
                });
                break;
            case OptionButton choice when choice.Selected >= 0:
                var id = choice.GetSelectedId();
                var metadata = choice.GetItemMetadata(choice.Selected);
                restore.Add(() =>
                {
                    if (!GodotObject.IsInstanceValid(choice)) return;
                    using var identity = new Godot.Collections.Array { metadata };
                    for (var index = 0; index < choice.ItemCount; index++)
                        if (identity.Contains(choice.GetItemMetadata(index)) &&
                            (metadata.VariantType != Variant.Type.Nil || choice.GetItemId(index) == id))
                        { choice.Select(index); break; }
                });
                break;
            case ScrollContainer scroll:
                var vertical = scroll.ScrollVertical;
                var horizontal = scroll.ScrollHorizontal;
                restore.Add(() =>
                {
                    if (!GodotObject.IsInstanceValid(scroll)) return;
                    scroll.ScrollVertical = vertical;
                    scroll.ScrollHorizontal = horizontal;
                });
                break;
        }
        foreach (var child in node.GetChildren()) Capture(child);
    }

    public void Dispose()
    {
        foreach (var apply in restore) apply();
        restore.Clear();
    }
}
