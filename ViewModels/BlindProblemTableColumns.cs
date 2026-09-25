using System;
using System.Linq;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;

namespace PdfEditorApp.Plugins.CSharpEditor.ViewModels;

/// <summary>
/// Column widths of the Blind 75 table, shared by its header and every row. Status and the action buttons have fixed
/// widths, Title takes whatever is left, and Category, Acceptance and Difficulty keep the widths they are dragged to.
/// Dragging a divider moves only that divider, as in VS Code's tables: the column on one side grows by what the other
/// side gives up, and a column that reaches its minimum passes the rest on to the next one. When the table is too
/// narrow for them and a readable title, the resizable columns shrink toward their minimums, and if even that isn't
/// enough Category steps aside (the category chips and the details panel still show it).
/// </summary>
public sealed class BlindProblemTableColumns : ObservableObject
{
    public const double StatusWidth = 56;
    public const double ActionsWidth = 156;
    public const double MinTitleWidth = 180;

    // Title, Category, Acceptance, Difficulty: the columns the dividers sit between, left to right. Divider 1 is
    // Title|Category, 2 is Category|Acceptance and 3 is Acceptance|Difficulty. Title's entry is never stored: it's
    // whatever the others leave.
    private static readonly double[] Minimums = { MinTitleWidth, 76, 72, 72 };
    private static readonly double[] Defaults = { 0, 168, 104, 96 };

    private readonly double[] _preferred = (double[])Defaults.Clone();
    private double[]? _dragStart;
    private double _availableWidth;

    public GridLength StatusColumn { get; } = new(StatusWidth);
    public GridLength ActionsColumn { get; } = new(ActionsWidth);
    public GridLength CategoryColumn => new(Widths()[1]);
    public GridLength AcceptanceColumn => new(Widths()[2]);
    public GridLength DifficultyColumn => new(Widths()[3]);

    /// <summary>Title's width as laid out (0 until the table has been measured).</summary>
    public double TitleWidth => Widths()[0];

    /// <summary>False when the table can't fit every column at its minimum next to a readable title.</summary>
    public bool IsCategoryVisible => _availableWidth <= 0 || _availableWidth >= StatusWidth + ActionsWidth + Minimums.Sum();

    /// <summary>The width every row's columns share: the table header's, inside its padding.</summary>
    public double AvailableWidth
    {
        get => _availableWidth;
        set
        {
            if (Math.Abs(_availableWidth - value) < 0.5) return;
            _availableWidth = value;
            RaiseWidthsChanged();
        }
    }

    /// <summary>Title, Category, Acceptance and Difficulty as laid out now.</summary>
    public double[] Widths()
    {
        var widths = (double[])_preferred.Clone();
        if (_availableWidth <= 0) return widths;
        if (!IsCategoryVisible) widths[1] = 0;

        // Give back only what a readable title needs, from each column in proportion to what it has above its minimum.
        double room = _availableWidth - StatusWidth - ActionsWidth - MinTitleWidth;
        double excess = widths.Skip(1).Sum() - room;
        double slack = Enumerable.Range(1, 3).Sum(i => Math.Max(0, widths[i] - Minimum(i)));
        if (excess > 0 && slack > 0)
        {
            double share = Math.Min(1, excess / slack);
            for (int i = 1; i < widths.Length; i++) widths[i] -= Math.Max(0, widths[i] - Minimum(i)) * share;
        }

        widths[0] = Math.Max(0, _availableWidth - StatusWidth - ActionsWidth - widths.Skip(1).Sum());
        return widths;
    }

    /// <summary>Remembers the widths at the start of a drag, so every move is measured from there.</summary>
    public void BeginResize() => _dragStart = Widths();

    /// <summary>
    /// Puts <paramref name="divider"/> (1 = Title|Category, 2 = Category|Acceptance, 3 = Acceptance|Difficulty)
    /// <paramref name="offset"/> pixels from where the drag began, or as far as the minimums allow.
    /// </summary>
    public void Resize(int divider, double offset)
    {
        if (divider < 1 || divider > 3 || _availableWidth <= 0) return;

        var widths = (double[])(_dragStart ?? Widths()).Clone();
        if (offset > 0) Shift(widths, grow: NearestShown(divider - 1, -1), firstDonor: divider, step: 1, offset);
        else if (offset < 0) Shift(widths, grow: NearestShown(divider, 1), firstDonor: divider - 1, step: -1, -offset);

        // A hidden Category keeps the width it had, for when there's room again.
        for (int i = 1; i < widths.Length; i++)
        {
            if (IsShown(i)) _preferred[i] = widths[i];
        }
        RaiseWidthsChanged();
    }

    public void EndResize() => _dragStart = null;

    /// <summary>Back to the default widths (double-clicking a divider).</summary>
    public void Reset()
    {
        Array.Copy(Defaults, _preferred, Defaults.Length);
        _dragStart = null;
        RaiseWidthsChanged();
    }

    // Column `grow` gains up to `amount`, taken from `firstDonor` and then the columns beyond it in the direction of
    // `step`, none of them going below its minimum (a hidden column has nothing to give).
    private void Shift(double[] widths, int grow, int firstDonor, int step, double amount)
    {
        double taken = 0;
        for (int i = firstDonor; i >= 0 && i < widths.Length && taken < amount; i += step)
        {
            double give = Math.Min(amount - taken, Math.Max(0, widths[i] - Minimum(i)));
            widths[i] -= give;
            taken += give;
        }
        widths[grow] += taken;
    }

    private bool IsShown(int column) => column != 1 || IsCategoryVisible;

    private double Minimum(int column) => IsShown(column) ? Minimums[column] : 0;

    // The first shown column from `column` on in the direction of `step` (Title is always shown).
    private int NearestShown(int column, int step)
    {
        while (!IsShown(column)) column += step;
        return column;
    }

    private void RaiseWidthsChanged()
    {
        OnPropertyChanged(nameof(IsCategoryVisible));
        OnPropertyChanged(nameof(CategoryColumn));
        OnPropertyChanged(nameof(AcceptanceColumn));
        OnPropertyChanged(nameof(DifficultyColumn));
        OnPropertyChanged(nameof(TitleWidth));
    }
}
