using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Avalonia.Styling;

namespace D3sk.ChatUi.Avalonia.Theme;

/// <summary>A focus ring that shows when a control is reached by keyboard.</summary>
public static class ChatFocus
{
    /// <summary>Ring thickness in pixels.</summary>
    public const double Thickness = 2;

    /// <summary>The ring colour, the web desk's accent (<c>--accent</c>), readable on every palette.</summary>
    public static Color Ring { get; } = ChatTokens.Action("accent");

    /// <summary>The <c>:focus-visible</c> styles for buttons, text boxes, tree and list items.</summary>
    public static Styles CreateStyles()
    {
        var styles = new Styles();
        foreach (var selector in new Func<Selector?, Selector>[]
                 {
                     s => s.OfType<Button>(),
                     s => s.OfType<TextBox>(),
                     s => s.OfType<TreeViewItem>(),
                     s => s.OfType<ListBoxItem>(),
                     s => s.OfType<ToggleButton>(),
                 })
        {
            var style = new Style(s => selector(s).Class(":focus-visible"));
            style.Setters.Add(new Setter(TemplatedControl.BorderBrushProperty, new SolidColorBrush(Ring)));
            style.Setters.Add(new Setter(TemplatedControl.BorderThicknessProperty, new global::Avalonia.Thickness(Thickness)));
            styles.Add(style);
        }
        return styles;
    }
}
