using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Styling;
using D3sk.ChatUi.Avalonia.Theme;

namespace D3sk.ChatUi.Avalonia.Tests.Theme;

public class ChatThemeTests
{
    public static TheoryData<string> Ids()
    {
        var data = new TheoryData<string>();
        foreach (var p in ChatPalettes.All) data.Add(p.Id);
        return data;
    }

    [Fact]
    [Trait("Preserve", "P46")]
    public void The_six_palettes_of_the_web_desk_are_kept()
    {
        Assert.Equal(["teal", "slate", "midnight", "ocean", "forest", "dusk"], ChatPalettes.All.Select(p => p.Id));
        var teal = ChatPalettes.Get("teal");
        Assert.Equal(Color.Parse("#13242d"), teal.Bg);
        Assert.Equal(Color.Parse("#869aa4"), teal.Muted);
        Assert.Equal(Color.Parse("#96887f"), ChatPalettes.Get("dusk").Muted);
    }

    [Fact]
    public void An_unknown_id_falls_back_to_teal()
    {
        Assert.Equal("teal", ChatPalettes.Get("nope").Id);
        Assert.Equal("teal", ChatPalettes.Get(null).Id);
    }

    [Theory]
    [MemberData(nameof(Ids))]
    [Trait("Accessibility", "B26")]
    public void Every_text_colour_reaches_4_5_to_1_on_every_background(string id)
    {
        var p = ChatPalettes.Get(id);
        foreach (var fg in p.Foregrounds)
            foreach (var bg in p.Backgrounds)
                Assert.True(Contrast.Ratio(fg, bg) >= Contrast.MinimumRatio,
                    $"{id}: {fg} on {bg} = {Contrast.Ratio(fg, bg):F2}");
    }

    [Fact]
    public void Contrast_matches_the_wcag_extremes()
    {
        Assert.Equal(21, Contrast.Ratio(Colors.Black, Colors.White), 3);
        Assert.Equal(1, Contrast.Ratio(Colors.Gray, Colors.Gray), 3);
    }

    [Fact]
    [Trait("Accessibility", "B26")]
    public void No_text_size_is_under_13()
    {
        Assert.All(ChatTypography.Sizes.Values, s => Assert.True(s >= 13));
        Assert.True(ChatTypography.MinimumSize >= 13);
    }

    [Fact]
    [Trait("Accessibility", "B26")]
    public void The_focus_ring_is_visible_on_every_background()
    {
        foreach (var p in ChatPalettes.All)
            foreach (var bg in p.Backgrounds)
                Assert.True(Contrast.Ratio(ChatFocus.Ring, bg) >= 3.0, $"{p.Id} {bg}");
        Assert.True(ChatFocus.Thickness >= 2);
    }

    [AvaloniaFact]
    [Trait("Accessibility", "B26")]
    public void Focus_styles_set_a_ring_for_keyboard_focus()
    {
        var styles = ChatFocus.CreateStyles();
        Assert.NotEmpty(styles);
        Assert.All(styles.OfType<Style>(), s =>
            Assert.Contains(s.Setters.OfType<Setter>(), x => x.Property == global::Avalonia.Controls.Primitives.TemplatedControl.BorderThicknessProperty));
    }

    [AvaloniaFact]
    public void Apply_puts_brushes_and_sizes_in_the_resources_and_switches()
    {
        var res = new ResourceDictionary();
        ChatTheme.Apply(res, "slate");
        Assert.Equal(Color.Parse("#1e1e1e"), ((SolidColorBrush)res["Chat.Bg"]!).Color);
        Assert.Equal(14.0, res["Chat.FontSize.Body"]);
        ChatTheme.Apply(res, "dusk");
        Assert.Equal(Color.Parse("#1c1614"), ((SolidColorBrush)res["Chat.Bg"]!).Color);
    }
}
