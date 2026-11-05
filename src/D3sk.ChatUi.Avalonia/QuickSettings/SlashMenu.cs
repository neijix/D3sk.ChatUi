using Avalonia.Controls;

namespace D3sk.ChatUi.Avalonia.QuickSettings;

/// <summary>One slash command as the host supplies it.</summary>
public sealed record SlashCommand(string Name, string Description);

/// <summary>Lists the commands it is given, filtered by what follows the slash. It discovers nothing.</summary>
public sealed class SlashMenu : UserControl
{
    private readonly ListBox _list = new();
    private IReadOnlyList<SlashCommand> _all = [];

    /// <summary>Creates an empty menu.</summary>
    public SlashMenu()
    {
        _list.ItemTemplate = new global::Avalonia.Controls.Templates.FuncDataTemplate<SlashCommand>(
            (c, _) => new TextBlock { Text = c is null ? "" : $"/{c.Name}  {c.Description}" });
        _list.SelectionChanged += (_, _) =>
        {
            if (_list.SelectedItem is SlashCommand picked)
            {
                Chosen?.Invoke(this, picked);
            }
        };
        Content = _list;
    }

    /// <summary>Raised when the user picks a command.</summary>
    public event EventHandler<SlashCommand>? Chosen;

    /// <summary>The commands now shown.</summary>
    public IReadOnlyList<SlashCommand> Shown => _list.ItemsSource as IReadOnlyList<SlashCommand> ?? [];

    /// <summary>Replaces the commands the host gives.</summary>
    public void SetCommands(IReadOnlyList<SlashCommand> commands)
    {
        _all = commands;
        Filter("");
    }

    /// <summary>Shows the commands whose name starts with the typed text (leading slash ignored, case-insensitive); the menu hides when none match.</summary>
    public void Filter(string typed)
    {
        var prefix = typed.TrimStart('/');
        var shown = _all.Where(c => c.Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)).ToList();
        _list.SelectedItem = null;
        _list.ItemsSource = shown;
        IsVisible = shown.Count > 0;
    }
}
