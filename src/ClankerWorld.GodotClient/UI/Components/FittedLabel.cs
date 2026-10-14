using Godot;

namespace ClankerWorld.GodotClient.UI;

/// <summary>
/// A one-line label that shortens text too long for its width and ends it
/// with three full stops. Godot's own shortening adds the ellipsis character,
/// which the body font draws at mid-height.
/// </summary>
public partial class FittedLabel : Label
{
    private string fullText = string.Empty;
    private string automaticTooltip = string.Empty;

    // Ordinary label assignments use the same fitting path as FullText.
    public new string Text { get => base.Text; set => FullText = value; }

    public FittedLabel()
    {
        // Trimming without a mark keeps the minimum width small; the text set
        // here always fits, so Godot never needs to trim it.
        TextOverrunBehavior = TextServer.OverrunBehavior.TrimChar;
        SizeFlagsHorizontal = SizeFlags.ExpandFill;
        MouseFilter = MouseFilterEnum.Pass;
    }

    /// <summary>The whole text; <see cref="Label.Text"/> holds what fits.</summary>
    public string FullText
    {
        get => fullText;
        set
        {
            fullText = value;
            if (TooltipText.Length == 0 || TooltipText == automaticTooltip)
            {
                automaticTooltip = value;
                TooltipText = value;
            }
            Fit();
        }
    }

    public override void _Notification(int what)
    {
        if (what is (int)NotificationResized or (int)NotificationThemeChanged or (int)NotificationVisibilityChanged) Fit();
    }

    private void Fit()
    {
        if (AutowrapMode != TextServer.AutowrapMode.Off)
            TextOverrunBehavior = TextServer.OverrunBehavior.NoTrimming;
        var width = Size.X;
        var font = GetThemeFont("font");
        var size = GetThemeFontSize("font_size");
        // A hidden label keeps only its minimum width, so keep the whole text until it is shown and laid out.
        base.Text = width <= 0 || font is null || !IsVisibleInTree() ||
            AutowrapMode == TextServer.AutowrapMode.Off && TextOverrunBehavior == TextServer.OverrunBehavior.NoTrimming
            ? fullText
            : AutowrapMode != TextServer.AutowrapMode.Off
                ? MaxLinesVisible <= 0 ? fullText : Shorten(fullText, font.GetHeight(size) * MaxLinesVisible,
                    text => font.GetMultilineStringSize(text, HorizontalAlignment.Left, width, size).Y)
            : Shorten(fullText, width, text => font.GetStringSize(text, HorizontalAlignment.Left, -1, size).X);
    }

    /// <summary>The longest start of <paramref name="text"/> that fits with "..." after it, or the whole text if it fits.</summary>
    public static string Shorten(string text, float width, Func<string, float> measure)
        => SaveTimelineLayout.Shorten(text, width, measure);
}
