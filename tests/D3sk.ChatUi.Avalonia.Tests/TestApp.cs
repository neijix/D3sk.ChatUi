using Avalonia;
using Avalonia.Headless;
using Avalonia.Themes.Fluent;
using D3sk.ChatUi.Avalonia.Tests;

[assembly: AvaloniaTestApplication(typeof(TestApp))]

namespace D3sk.ChatUi.Avalonia.Tests;

public class TestApp : Application
{
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<TestApp>().UseHeadless(new AvaloniaHeadlessPlatformOptions());

    public override void Initialize() => Styles.Add(new FluentTheme());
}
