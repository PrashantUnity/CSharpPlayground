using System;
using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;

namespace PdfEditorApp.Plugins.CSharpEditor.Models;

public partial class TestCaseItem : ObservableObject
{
    [ObservableProperty]
    private string _name = "Case 1";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowsCall))]
    private string _input = string.Empty;

    [ObservableProperty]
    private string _expectedOutput = string.Empty;

    /// <summary>What the run printed for this case: the answer, or "got … · expected …" when it's wrong.</summary>
    [ObservableProperty]
    private string _actualOutput = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasResult), nameof(IsPassed), nameof(IsFailed), nameof(IsNotRun))]
    private bool? _passed;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotRun))]
    private bool _isRunning;

    /// <summary>
    /// The C# expression the case checks, when the Test Cases panel wrote its <c>Check(Name, Call, ExpectedOutput)</c>
    /// line into the script. Empty for cases whose Check line came with the script.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsFromPanel), nameof(ShowsCall))]
    private string _call = string.Empty;

    /// <summary>The answer's elements may come in any order (Check's anyOrder).</summary>
    [ObservableProperty]
    private bool _anyOrder;

    [JsonIgnore]
    public bool IsFromPanel => !string.IsNullOrWhiteSpace(Call);

    /// <summary>The card shows the checked expression when there is no input as the problem states it.</summary>
    [JsonIgnore]
    public bool ShowsCall => string.IsNullOrWhiteSpace(Input) && IsFromPanel;

    [JsonIgnore]
    public bool HasResult => Passed.HasValue;

    [JsonIgnore]
    public bool IsPassed => Passed == true;

    [JsonIgnore]
    public bool IsFailed => Passed == false;

    [JsonIgnore]
    public bool IsNotRun => Passed == null && !IsRunning;
}
