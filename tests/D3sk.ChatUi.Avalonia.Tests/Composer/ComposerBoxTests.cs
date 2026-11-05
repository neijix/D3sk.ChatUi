using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using D3sk.ChatUi.Avalonia.Composer;
using D3sk.ChatUi.Composer;

namespace D3sk.ChatUi.Avalonia.Tests.Composer;

public class ComposerBoxTests
{
    [AvaloniaFact]
    public void Architect_button_prefixes_the_text_once()
    {
        var state = new ComposerState { Text = "refactor" };
        var box = new ComposerBox(state);
        var window = new Window { Content = box };
        window.Show();
        var button = box.GetVisualDescendantsOf<Button>().First(b => (string?)b.Content == "Architect");
        button.RaiseEvent(new global::Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        button.RaiseEvent(new global::Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        Assert.Equal("/architect refactor", state.Text);
    }
}

internal static class VisualExtensions
{
    public static IEnumerable<T> GetVisualDescendantsOf<T>(this global::Avalonia.Visual root)
        where T : class
    {
        foreach (var child in global::Avalonia.VisualTree.VisualExtensions.GetVisualDescendants(root))
        {
            if (child is T t)
            {
                yield return t;
            }
        }
    }
}
