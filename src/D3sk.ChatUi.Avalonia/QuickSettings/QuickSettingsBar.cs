using Avalonia.Controls;
using Avalonia.Layout;

namespace D3sk.ChatUi.Avalonia.QuickSettings;

/// <summary>Model, permission and effort pickers. A pick raises <see cref="SettingChanged"/>; nothing else happens here.</summary>
public sealed class QuickSettingsBar : UserControl
{
    private readonly Dictionary<QuickSettingKind, ComboBox> _pickers = new();
    private bool _quiet;

    /// <summary>Creates the bar with one empty picker per setting.</summary>
    public QuickSettingsBar()
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        foreach (var kind in Enum.GetValues<QuickSettingKind>())
        {
            var box = new ComboBox { Name = kind.ToString(), IsVisible = false, MinWidth = 110 };
            box.SelectionChanged += (_, _) => OnPicked(kind, box);
            _pickers[kind] = box;
            row.Children.Add(box);
        }
        Content = row;
    }

    /// <summary>Raised when the user picks a value.</summary>
    public event EventHandler<QuickSettingChange>? SettingChanged;

    /// <summary>Sets a picker's choices and current value; raises nothing. No options hides the picker (the CLI lacks it).</summary>
    public void SetOptions(QuickSettingKind kind, IReadOnlyList<SettingOption> options, string? current)
    {
        var box = _pickers[kind];
        _quiet = true;
        try
        {
            box.ItemsSource = options.ToList();
            box.DisplayMemberBinding = new global::Avalonia.Data.Binding(nameof(SettingOption.Label));
            box.SelectedItem = options.FirstOrDefault(o => o.Value == current);
            box.IsVisible = options.Count > 0;
        }
        finally
        {
            _quiet = false;
        }
    }

    /// <summary>The picker for a setting, for the host's layout and tests.</summary>
    public ComboBox Picker(QuickSettingKind kind) => _pickers[kind];

    private void OnPicked(QuickSettingKind kind, ComboBox box)
    {
        if (_quiet || box.SelectedItem is not SettingOption option)
        {
            return;
        }
        SettingChanged?.Invoke(this, new QuickSettingChange(kind, option.Value));
    }
}
