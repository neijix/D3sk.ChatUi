using Avalonia.Headless.XUnit;
using D3sk.ChatUi.Avalonia.QuickSettings;

namespace D3sk.ChatUi.Avalonia.Tests.QuickSettings;

[Trait("Category", "Preserve")]
public class QuickSettingsTests
{
    private static readonly SettingOption[] Modes =
    [
        new("plan", "Plan"), new("acceptEdits", "Accept edits"),
    ];

    [AvaloniaFact]
    public void A_pick_emits_a_typed_change_for_the_host()
    {
        var bar = new QuickSettingsBar();
        bar.SetOptions(QuickSettingKind.Permission, Modes, "plan");
        QuickSettingChange? got = null;
        bar.SettingChanged += (_, c) => got = c;

        bar.Picker(QuickSettingKind.Permission).SelectedIndex = 1;

        Assert.Equal(new QuickSettingChange(QuickSettingKind.Permission, "acceptEdits"), got);
    }

    [AvaloniaFact]
    public void Setting_options_from_the_host_raises_nothing_and_empty_hides_the_picker()
    {
        var bar = new QuickSettingsBar();
        var raised = 0;
        bar.SettingChanged += (_, _) => raised++;

        bar.SetOptions(QuickSettingKind.Model, Modes, "plan");
        bar.SetOptions(QuickSettingKind.Effort, [], null);

        Assert.Equal(0, raised);
        Assert.True(bar.Picker(QuickSettingKind.Model).IsVisible);
        Assert.False(bar.Picker(QuickSettingKind.Effort).IsVisible);
    }

    [AvaloniaFact]
    public void The_slash_menu_lists_given_commands_filters_and_reports_a_pick()
    {
        var menu = new SlashMenu();
        menu.SetCommands([new("model", "Pick"), new("memory", "Edit"), new("clear", "Reset")]);
        Assert.Equal(3, menu.Shown.Count);

        menu.Filter("/me");
        Assert.Equal(["memory"], menu.Shown.Select(c => c.Name).ToArray());

        SlashCommand? chosen = null;
        menu.Chosen += (_, c) => chosen = c;
        var list = (global::Avalonia.Controls.ListBox)menu.Content!;
        list.SelectedIndex = 0;
        Assert.Equal("memory", chosen?.Name);

        menu.Filter("zzz");
        Assert.False(menu.IsVisible);
    }
}
