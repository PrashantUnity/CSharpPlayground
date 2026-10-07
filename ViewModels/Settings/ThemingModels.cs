using System;
using CommunityToolkit.Mvvm.ComponentModel;
using PdfEditorApp.Plugins.CSharpEditor.Services.Extensibility.Theming;
using PdfEditorApp.Plugins.CSharpEditor.Services.Extensibility.Theming.ColorMath;

namespace PdfEditorApp.Plugins.CSharpEditor.ViewModels.Settings;

/// <summary>
/// Friendly display item for the color harmony mode picker.
/// </summary>
public sealed class HarmonyModeOption
{
    public ColorHarmonyMode Mode { get; init; }
    public string Title { get; init; } = string.Empty;
    public string OffsetLabel { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;

    public override string ToString() => Title;
}

/// <summary>
/// Friendly display item for the color-blindness simulation selector.
/// </summary>
public sealed class VisionDeficiencyOption
{
    public VisionDeficiency Deficiency { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Prevalence { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;

    public override string ToString() => Name;
}

/// <summary>
/// Observable category filter pill for the runtime token inspector.
/// </summary>
public sealed partial class CategoryFilterItemViewModel : ObservableObject
{
    public string Name { get; init; } = string.Empty;

    [ObservableProperty]
    private bool _isSelected;
}

/// <summary>
/// Observable item representing a single theme token for live inspection and editing.
/// </summary>
public partial class ColorTokenItemViewModel : ObservableObject
{
    public string Key { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string Category { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;

    [ObservableProperty]
    private string _currentHex = "#000000";

    [ObservableProperty]
    private bool _isCopied;
}

/// <summary>
/// Observable item representing an individual stop in an 11-stop tonal scale.
/// </summary>
public partial class TonalSwatchItemViewModel : ObservableObject
{
    [ObservableProperty]
    private int _stop;

    [ObservableProperty]
    private string _hex = "#000000";

    [ObservableProperty]
    private bool _isKeyRole;

    [ObservableProperty]
    private bool _isCopied;

    public string ForegroundHex => Stop <= 400 ? "#0F172A" : "#F8FAFC";
}

/// <summary>
/// Observable item representing an individual contrast pairing evaluated under both WCAG 2.1 and APCA (WCAG 3.0).
/// </summary>
public partial class ContrastPairingItemViewModel : ObservableObject
{
    public string RoleName { get; init; } = string.Empty;
    public string ForeRole { get; init; } = string.Empty;
    public string BackRole { get; init; } = string.Empty;

    [ObservableProperty]
    private string _foreHex = "#FFFFFF";

    [ObservableProperty]
    private string _backHex = "#000000";

    [ObservableProperty]
    private float _wcagRatio;

    [ObservableProperty]
    private string _wcagBadge = "AA";

    [ObservableProperty]
    private bool _isWcagPassed = true;

    [ObservableProperty]
    private float _apcaLc;

    [ObservableProperty]
    private string _apcaRating = string.Empty;

    [ObservableProperty]
    private bool _isApcaPassed = true;
}

/// <summary>
/// Observable item representing status distinction safety under a specific visual deficiency.
/// </summary>
public partial class VisionDeficiencyStatusItemViewModel : ObservableObject
{
    public string DeficiencyName { get; init; } = string.Empty;
    public VisionDeficiency Deficiency { get; init; }

    [ObservableProperty]
    private string _simulatedSuccessHex = "#10B981";

    [ObservableProperty]
    private string _simulatedErrorHex = "#EF4444";

    [ObservableProperty]
    private float _deltaE;

    [ObservableProperty]
    private bool _isSafe = true;

    [ObservableProperty]
    private string _statusLabel = "Safe (Distinct)";
}

/// <summary>
/// Curated harmonious seed preset providing an aesthetic baseline.
/// </summary>
public sealed record HarmonicSeedPreset(
    string Name,
    float Hue,
    ColorHarmonyMode Mode,
    string SeedHex,
    string Description);
