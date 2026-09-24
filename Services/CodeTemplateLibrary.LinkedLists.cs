using System.Collections.Generic;
using Material.Icons;
using PdfEditorApp.Plugins.CSharpEditor.Models;

namespace PdfEditorApp.Plugins.CSharpEditor.Services;

public static partial class CodeTemplateLibrary
{
    private static IEnumerable<CodeTemplate> GetLinkedListTemplates() => new List<CodeTemplate>
    {
        new()
        {
            Id = "leetcode_206_reverse_linked_list",
            Title = "206. Reverse Linked List (Pointer Rewiring)",
            Category = "Algorithms",
            Kind = WorkspaceItemKind.Notebook,
            Description = "Watch prev, curr and next move while each node's arrow flips to point backwards.",
            IconKind = MaterialIconKind.VectorLink,
            AccentColor = "#f97316",
            AccentBackground = "#3b1d0a",
            AccentBorder = "#ea580c",
            CategoryBadge = "LeetCode 206 • Easy",
            Tags = new List<string> { "Linked List", "Pointers", "Reversal", "LinkedListTracker" },
            Notes = @"# 206. Reverse Linked List

Given the head of a singly linked list, reverse the list and return the new head.
- The nodes stay where they are: watch each arrow flip when `curr.next = prev` runs.
- `prev`, `curr` and `next` are drawn as labelled pointers; a pointer that is null points at the **null** box.
- The node whose arrow just changed is outlined in lime, and each step highlights the line that recorded it.",
            InitialCode = @"public class ListNode
{
    public int val;
    public ListNode? next;
    public ListNode(int val, ListNode? next = null) { this.val = val; this.next = next; }
}

var head = new ListNode(1, new ListNode(2, new ListNode(3, new ListNode(4))));
var tracker = LinkedListTracker.Create(head, ""206. Reverse Linked List"");

ListNode? prev = null;
ListNode? curr = head;
tracker.Step(""Start: prev is null, curr is the head"", new { prev, curr });

while (curr != null)
{
    ListNode? next = curr.next;   // remember the rest of the list
    tracker.Step($""Save next = {next?.val.ToString() ?? ""null""}"", new { prev, curr, next });

    curr.next = prev;             // flip this node's arrow
    tracker.Step($""Point {curr.val} back at {prev?.val.ToString() ?? ""null""}"", new { prev, curr, next });

    prev = curr;                  // move both pointers one step
    curr = next;
    tracker.Step(""Advance prev and curr"", new { prev, curr });
}

tracker.Step(""Done: prev is the new head"", new { head = prev });
Display.Visualizer(tracker);"
        }
    };
}
