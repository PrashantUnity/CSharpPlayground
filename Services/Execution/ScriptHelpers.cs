using System;

namespace PdfEditorApp.Plugins.CSharpEditor.Services;

/// <summary>
/// Called directly from any script or notebook cell (they are imported with <c>using static</c>), so a test is one
/// readable line: <c>Check("Example 1", sol.TwoSum(new[] { 2, 7, 11, 15 }, 9), "[0,1]");</c>
/// </summary>
public static class ScriptHelpers
{
    /// <summary>Prints ✅ when <paramref name="actual"/> matches the LeetCode-style <paramref name="expected"/> text, ❌ with both otherwise.</summary>
    public static bool Check<T>(string name, T actual, string expected, bool anyOrder = false) =>
        Judge.Check(name, Judge.Format(actual, typeof(T)), expected, anyOrder);

    /// <summary>Prints a value the way LeetCode writes it: <c>[0,1]</c>, <c>true</c>, <c>"bab"</c>, <c>[3,9,20,null,null,15,7]</c>.</summary>
    public static void Show<T>(T value) => Console.WriteLine(Judge.Format(value, typeof(T)));

    /// <summary>The same text as <see cref="Show{T}"/>, as a string for messages.</summary>
    public static string Format<T>(T value) => Judge.Format(value, typeof(T));
}
