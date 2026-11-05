namespace D3sk.ChatUi.Avalonia.QuickSettings;

/// <summary>Which quick setting a picker controls.</summary>
public enum QuickSettingKind
{
    /// <summary>The model.</summary>
    Model,

    /// <summary>The permission mode (may change mid-run; the host clamps it).</summary>
    Permission,

    /// <summary>The reasoning effort.</summary>
    Effort,
}

/// <summary>One choice in a picker: the value the host maps, and the label shown.</summary>
public sealed record SettingOption(string Value, string Label);

/// <summary>A setting change the host maps to its own command; the controls decide nothing.</summary>
public sealed record QuickSettingChange(QuickSettingKind Kind, string Value);
