namespace D3sk.ChatUi.Tests;

public class CorePackageTests
{
    [Fact]
    public void Core_package_has_no_Avalonia_reference()
    {
        var assembly = typeof(ChatUiInfo).Assembly;
        var avalonia = assembly.GetReferencedAssemblies()
            .Where(a => a.Name!.StartsWith("Avalonia", StringComparison.Ordinal));
        Assert.Empty(avalonia);
    }
}
