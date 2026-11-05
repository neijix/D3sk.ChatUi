using Avalonia.Media;

namespace D3sk.ChatUi.Avalonia.Theme;

/// <summary>WCAG 2.x contrast ratio between two opaque colours.</summary>
public static class Contrast
{
    /// <summary>The least ratio the desk accepts for text.</summary>
    public const double MinimumRatio = 4.5;

    /// <summary>Ratio from 1 to 21, order of the colours does not matter.</summary>
    public static double Ratio(Color a, Color b)
    {
        var la = Luminance(a);
        var lb = Luminance(b);
        var (hi, lo) = la >= lb ? (la, lb) : (lb, la);
        return (hi + 0.05) / (lo + 0.05);
    }

    private static double Luminance(Color c) =>
        0.2126 * Channel(c.R) + 0.7152 * Channel(c.G) + 0.0722 * Channel(c.B);

    private static double Channel(byte v)
    {
        var s = v / 255.0;
        return s <= 0.03928 ? s / 12.92 : Math.Pow((s + 0.055) / 1.055, 2.4);
    }
}
