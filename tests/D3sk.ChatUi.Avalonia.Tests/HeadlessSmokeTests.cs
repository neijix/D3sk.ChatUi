using Avalonia.Controls;
using Avalonia.Headless.XUnit;

namespace D3sk.ChatUi.Avalonia.Tests;

public class HeadlessSmokeTests
{
    [AvaloniaFact]
    public void A_window_lays_out_headless()
    {
        var box = new TextBlock { Text = ChatUiAvaloniaInfo.PackageId };
        var window = new Window { Content = box, Width = 200, Height = 100 };
        window.Show();
        Assert.True(box.Bounds.Width > 0);
    }
}
