using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using PdfEditorApp.Plugins.CSharpEditor.Visualizers.Models;

namespace PdfEditorApp.Plugins.CSharpEditor.Visualizers.Services;

/// <summary>
/// Draws a trie as a tree of letters. The live trie is re-read at every step, so inserted letters appear (outlined as
/// new), word ends are green with the word underneath, and <c>path:</c> lights up the letters walked so far.
/// </summary>
public sealed class TrieTracker
{
    public const int MaxNodes = 300;
    private const string RootId = "trie_root";

    private readonly object _root;
    private readonly Func<object, IEnumerable<(string Label, object Child)>> _children;
    private readonly Func<object, bool> _isWord;
    private readonly WatchList _watches = new();

    public VisualizerOptions Options { get; }
    public VisualizerSequence Sequence { get; }

    private TrieTracker(object root, Func<object, IEnumerable<(string, object)>> children, Func<object, bool> isWord, string title, int sourceLine, string sourceFile)
    {
        _root = root;
        _children = children;
        _isWord = isWord;
        Sequence = new VisualizerSequence();
        Options = new VisualizerOptions { Title = title, Kind = VisualizerKind.Tree, Sequence = Sequence };
        Step("The trie starts as a single empty root", sourceLine: sourceLine, sourceFile: sourceFile);
    }

    /// <summary>
    /// Finds the node's children (a Dictionary&lt;char, Node&gt; or a Node[26] array) and its end-of-word flag
    /// (a bool such as IsEnd / IsWord / EndOfWord, or a string Word that is set at word ends) by reflection.
    /// </summary>
    public static TrieTracker Create(
        object root,
        string? title = null,
        [CallerLineNumber] int sourceLine = 0,
        [CallerFilePath] string sourceFile = "")
    {
        var type = root.GetType();
        var children = FindChildren(type) ?? throw new ArgumentException(
            $"{type.Name} needs a children member: a Dictionary<char, {type.Name}> or a {type.Name}[] array.", nameof(root));
        return new TrieTracker(root, children, FindWordFlag(type), title ?? "Trie", sourceLine, sourceFile);
    }

    /// <summary>For any other trie shape: say how to list a node's children (letter, child) and whether it ends a word.</summary>
    public static TrieTracker Create<TNode>(
        TNode root,
        Func<TNode, IEnumerable<KeyValuePair<char, TNode>>> children,
        Func<TNode, bool> isWord,
        string? title = null,
        [CallerLineNumber] int sourceLine = 0,
        [CallerFilePath] string sourceFile = "") where TNode : class =>
        new(root,
            node => children((TNode)node).Where(kv => kv.Value != null).Select(kv => (kv.Key.ToString(), (object)kv.Value)),
            node => isWord((TNode)node),
            title ?? "Trie", sourceLine, sourceFile);

    /// <summary>Shows a live list, set or plain value (<c>Watch(() => word)</c>) under the trie at every later step.</summary>
    public TrieTracker Watch(object collection, [CallerArgumentExpression(nameof(collection))] string name = "")
    {
        _watches.Add(collection, name);
        return this;
    }

    /// <summary>Records the trie as it is now, lighting up the letters of <paramref name="path"/> walked from the root.</summary>
    public TrieTracker Step(
        string description,
        string? path = null,
        [CallerLineNumber] int sourceLine = 0,
        [CallerFilePath] string sourceFile = "")
    {
        int budget = MaxNodes;
        var snapshot = Build(_root, "root", string.Empty, null, ref budget);

        var step = new VisualizerStep(Sequence.TotalSteps, description, VisualizerKind.Tree)
        {
            Snapshot = snapshot,
            SourceLine = sourceLine,
            SourceFile = sourceFile,
            Watches = _watches.Capture()
        };

        if (!string.IsNullOrEmpty(path))
        {
            var reached = HighlightPath(snapshot, path);
            step.ActiveNodeIds.Add(reached.Id);
            step.AuxiliaryInfo["path"] = reached == snapshot ? "(root)" : IdToWord(reached.Id);
            if (IdToWord(reached.Id).Length < path.Length) step.AuxiliaryInfo["missing"] = $"'{path[IdToWord(reached.Id).Length]}'";
        }

        Options.TreeData = snapshot;
        Sequence.AddStep(step);
        return this;
    }

    private TreeNodeData Build(object node, string label, string prefix, TreeNodeData? parent, ref int budget)
    {
        budget--;
        var data = new TreeNodeData(label, prefix.Length == 0 ? RootId : "trie_" + prefix)
        {
            Parent = parent,
            Depth = prefix.Length
        };

        if (prefix.Length > 0 && _isWord(node))
        {
            data.State = TreeNodeState.Matched;
            data.SubLabel = prefix;
        }

        foreach (var (childLabel, child) in _children(node).OrderBy(c => c.Label, StringComparer.Ordinal))
        {
            if (budget <= 0) break;
            data.Children.Add(Build(child, childLabel, prefix + childLabel, data, ref budget));
        }

        return data;
    }

    // Letters on the way are Path; where the walk stops is Current. A word end keeps its green on the way past.
    private static TreeNodeData HighlightPath(TreeNodeData root, string path)
    {
        var node = root;
        foreach (char letter in path)
        {
            var next = node.Children.FirstOrDefault(c => c.DisplayValue == letter.ToString());
            if (next == null) break;
            if (node != root && node.State != TreeNodeState.Matched) node.State = TreeNodeState.Path;
            node = next;
        }

        node.State = TreeNodeState.Current;
        node.IsActive = true;
        return node;
    }

    private static string IdToWord(string id) => id == RootId ? string.Empty : id["trie_".Length..];

    private static Func<object, IEnumerable<(string, object)>>? FindChildren(Type type)
    {
        foreach (var member in Members(type))
        {
            var memberType = MemberType(member);

            if (memberType.IsArray && type.IsAssignableFrom(memberType.GetElementType()!))
            {
                return node => ((Array?)GetValue(member, node) ?? Array.Empty<object>())
                    .Cast<object?>()
                    .Select((child, index) => (Letter: ((char)('a' + index)).ToString(), Child: child))
                    .Where(c => c.Child != null)
                    .Select(c => (c.Letter, c.Child!));
            }

            var dictionary = memberType.GetInterfaces().Append(memberType)
                .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IDictionary<,>));
            if (dictionary != null && type.IsAssignableFrom(dictionary.GetGenericArguments()[1]))
            {
                return node => GetValue(member, node) is IDictionary map ? Entries(map) : Enumerable.Empty<(string, object)>();
            }
        }
        return null;
    }

    // IDictionary's own enumerator yields DictionaryEntry; plain IEnumerable on a Dictionary<,> yields KeyValuePair.
    private static IEnumerable<(string, object)> Entries(IDictionary map)
    {
        var entry = map.GetEnumerator();
        while (entry.MoveNext())
        {
            if (entry.Value != null) yield return (entry.Key.ToString() ?? "?", entry.Value);
        }
    }

    private static Func<object, bool> FindWordFlag(Type type)
    {
        var members = Members(type).ToList();
        var flag = members.FirstOrDefault(m => MemberType(m) == typeof(bool) && IsWordName(m.Name));
        if (flag != null) return node => GetValue(flag, node) is true;

        var word = members.FirstOrDefault(m => MemberType(m) == typeof(string) && m.Name.Equals("word", StringComparison.OrdinalIgnoreCase));
        if (word != null) return node => GetValue(word, node) is string s && s.Length > 0;

        return _ => false;
    }

    private static bool IsWordName(string name)
    {
        var lower = name.ToLowerInvariant();
        return lower.Contains("end") || lower.Contains("word") || lower.Contains("terminal") || lower.Contains("leaf") || lower.Contains("complete");
    }

    private static IEnumerable<MemberInfo> Members(Type type)
    {
        const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
        return type.GetFields(flags).Where(f => !f.Name.Contains('<')).Cast<MemberInfo>()
            .Concat(type.GetProperties(flags).Where(p => p.GetIndexParameters().Length == 0));
    }

    private static Type MemberType(MemberInfo member) => member is FieldInfo f ? f.FieldType : ((PropertyInfo)member).PropertyType;

    private static object? GetValue(MemberInfo member, object node) =>
        member is FieldInfo f ? f.GetValue(node) : ((PropertyInfo)member).GetValue(node);
}
