using PdfEditorApp.Plugins.CSharpEditor.Models;
using Xunit;

namespace CSharpEditorPlugin.Tests;

/// <summary>
/// What the Object Explorer shows for an object that points back to itself, the same in every language: the desktop's C#
/// inspector lists the members of the top object and of three levels below it, and the fifth level is a title with nothing
/// under it. A reference back to an object above is flagged but listed like any other (the desktop shows it unrolled).
/// </summary>
internal static class InspectorChain
{
    /// <summary>The nodes met by following one property down from <paramref name="top"/>: the top node, what it points to, and so on.</summary>
    public static List<ObjectInspectorNode> Follow(ObjectInspectorNode top, string property)
    {
        var nodes = new List<ObjectInspectorNode> { top };
        var current = top;
        while (current.Properties.FirstOrDefault(p => p.Name == property)?.ChildNode is { } next)
        {
            nodes.Add(next);
            current = next;
        }

        return nodes;
    }

    /// <summary>
    /// Five levels, each one but the last listing its members. With <paramref name="cycle"/> the property points back at the top
    /// object, so every level below it is flagged as a reference back (and still lists its members).
    /// </summary>
    public static void AssertFiveLevels(ObjectInspectorNode top, string property, IReadOnlyList<string> members, bool cycle = true)
    {
        var chain = Follow(top, property);
        Assert.Equal(5, chain.Count);

        for (var level = 0; level < 4; level++)
        {
            Assert.Equal(members, chain[level].Properties.Select(p => p.Name));
            Assert.Equal(cycle && level > 0, chain[level].IsCircularReference);
        }

        Assert.Empty(chain[4].Properties);
        if (cycle) Assert.Equal(top.HeaderTitle, chain[4].HeaderTitle);
    }
}
