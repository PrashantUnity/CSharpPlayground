using System.Collections.Generic;
using PdfEditorApp.Plugins.CSharpEditor.Models;

namespace PdfEditorApp.Plugins.CSharpEditor.Services;

public static partial class Blind75CatalogService
{
    private static IEnumerable<BlindProblemItem> GetLinkedListProblems() => new List<BlindProblemItem>
    {
        new()
        {
            Id = "blind75_206_reverse_linked_list",
            Number = 206,
            Title = "Reverse Linked List",
            Category = "Linked List",
            Difficulty = ProblemDifficulty.Easy,
            AcceptanceRate = 77.1,
            IsPremium = false,
            HasVisualizer = true,
            VisualizerKind = "LinkedList",
            Tags = new List<string> { "Linked List", "Recursion" },
            TimeComplexity = "O(N)",
            SpaceComplexity = "O(1)",
            DescriptionMarkdown = """
            Given the `head` of a singly linked list, reverse the list, and return *the reversed list*.
            """,
            StarterCode = """
            public class ListNode 
            {
                public int val;
                public ListNode next;
                public ListNode(int val = 0, ListNode next = null) { this.val = val; this.next = next; }
            }

            public class Solution 
            {
                public ListNode ReverseList(ListNode head) 
                {
                    ListNode prev = null;
                    var curr = head;
                    while (curr != null) 
                    {
                        var nextTemp = curr.next;
                        curr.next = prev;
                        prev = curr;
                        curr = nextTemp;
                    }
                    return prev;
                }
            }

            var head = new ListNode(1, new ListNode(2, new ListNode(3, new ListNode(4, new ListNode(5)))));
            var sol = new Solution();
            var reversed = sol.ReverseList(head);
            reversed.Dump("Reversed Linked List");
            """,
            TestCases = new List<TestCaseItem>
            {
                new() { Name = "[1,2,3,4,5]", Input = "[1,2,3,4,5]", ExpectedOutput = "[5,4,3,2,1]" }
            }
        },
        new()
        {
            Id = "blind75_21_merge_two_sorted_lists",
            Number = 21,
            Title = "Merge Two Sorted Lists",
            Category = "Linked List",
            Difficulty = ProblemDifficulty.Easy,
            AcceptanceRate = 64.5,
            IsPremium = false,
            Tags = new List<string> { "Linked List", "Recursion" },
            TimeComplexity = "O(N + M)",
            SpaceComplexity = "O(1)",
            DescriptionMarkdown = """
            Merge the two lists into one **sorted** list. The list should be made by splicing together the nodes of the first two lists.
            """,
            StarterCode = """
            public class ListNode 
            {
                public int val;
                public ListNode next;
                public ListNode(int val = 0, ListNode next = null) { this.val = val; this.next = next; }
            }

            public class Solution 
            {
                public ListNode MergeTwoLists(ListNode list1, ListNode list2) 
                {
                    var dummy = new ListNode(-1);
                    var curr = dummy;
                    while (list1 != null && list2 != null)
                    {
                        if (list1.val <= list2.val) { curr.next = list1; list1 = list1.next; }
                        else { curr.next = list2; list2 = list2.next; }
                        curr = curr.next;
                    }
                    curr.next = list1 ?? list2;
                    return dummy.next;
                }
            }

            var sol = new Solution();
            var l1 = new ListNode(1, new ListNode(2, new ListNode(4)));
            var l2 = new ListNode(1, new ListNode(3, new ListNode(4)));
            sol.MergeTwoLists(l1, l2).Dump("Merged List");
            """,
            TestCases = new List<TestCaseItem>
            {
                new() { Name = "[1,2,4], [1,3,4]", Input = "[1,2,4], [1,3,4]", ExpectedOutput = "[1,1,2,3,4,4]" }
            }
        },
        new()
        {
            Id = "blind75_141_linked_list_cycle",
            Number = 141,
            Title = "Linked List Cycle",
            Category = "Linked List",
            Difficulty = ProblemDifficulty.Easy,
            AcceptanceRate = 51.1,
            IsPremium = false,
            Tags = new List<string> { "Hash Table", "Linked List", "Two Pointers" },
            TimeComplexity = "O(N)",
            SpaceComplexity = "O(1)",
            DescriptionMarkdown = """
            Given `head`, the head of a linked list, determine if the linked list has a cycle in it.
            """,
            StarterCode = """
            public class ListNode 
            {
                public int val;
                public ListNode next;
                public ListNode(int x) { val = x; next = null; }
            }

            public class Solution 
            {
                public bool HasCycle(ListNode head) 
                {
                    if (head == null) return false;
                    var slow = head;
                    var fast = head;
                    while (fast != null && fast.next != null)
                    {
                        slow = slow.next;
                        fast = fast.next.next;
                        if (slow == fast) return true;
                    }
                    return false;
                }
            }

            var sol = new Solution();
            var n1 = new ListNode(3);
            var n2 = new ListNode(2);
            var n3 = new ListNode(0);
            var n4 = new ListNode(-4);
            n1.next = n2; n2.next = n3; n3.next = n4; n4.next = n2;
            sol.HasCycle(n1).Dump("HasCycle (expected: True)");
            """,
            TestCases = new List<TestCaseItem>
            {
                new() { Name = "Cycle present", Input = "head = [3,2,0,-4], pos = 1", ExpectedOutput = "True" }
            }
        },
        new()
        {
            Id = "blind75_143_reorder_list",
            Number = 143,
            Title = "Reorder List",
            Category = "Linked List",
            Difficulty = ProblemDifficulty.Medium,
            AcceptanceRate = 58.0,
            IsPremium = false,
            Tags = new List<string> { "Linked List", "Two Pointers", "Stack" },
            TimeComplexity = "O(N)",
            SpaceComplexity = "O(1)",
            DescriptionMarkdown = """
            You are given the head of a singly linked-list. Reorder the list to be on the following form: `L0 → Ln → L1 → Ln - 1 → L2 → Ln - 2 → …`
            """,
            StarterCode = """
            public class ListNode 
            {
                public int val;
                public ListNode next;
                public ListNode(int val = 0, ListNode next = null) { this.val = val; this.next = next; }
            }

            public class Solution 
            {
                public void ReorderList(ListNode head) 
                {
                    if (head?.next == null) return;
                    ListNode slow = head, fast = head;
                    while (fast.next != null && fast.next.next != null) { slow = slow.next; fast = fast.next.next; }
                    ListNode prev = null, curr = slow.next;
                    slow.next = null;
                    while (curr != null) { var nxt = curr.next; curr.next = prev; prev = curr; curr = nxt; }
                    ListNode first = head, second = prev;
                    while (second != null) { var t1 = first.next; var t2 = second.next; first.next = second; second.next = t1; first = t1; second = t2; }
                }
            }

            var sol = new Solution();
            var head = new ListNode(1, new ListNode(2, new ListNode(3, new ListNode(4))));
            sol.ReorderList(head);
            head.Dump("Reordered List");
            """,
            TestCases = new List<TestCaseItem>
            {
                new() { Name = "[1,2,3,4]", Input = "[1,2,3,4]", ExpectedOutput = "[1,4,2,3]" }
            }
        },
        new()
        {
            Id = "blind75_19_remove_nth_node_from_end_of_list",
            Number = 19,
            Title = "Remove Nth Node From End of List",
            Category = "Linked List",
            Difficulty = ProblemDifficulty.Medium,
            AcceptanceRate = 46.5,
            IsPremium = false,
            Tags = new List<string> { "Linked List", "Two Pointers" },
            TimeComplexity = "O(N)",
            SpaceComplexity = "O(1)",
            DescriptionMarkdown = """
            Given the `head` of a linked list, remove the `nth` node from the end of the list and return its head.
            """,
            StarterCode = """
            public class ListNode 
            {
                public int val;
                public ListNode next;
                public ListNode(int val = 0, ListNode next = null) { this.val = val; this.next = next; }
            }

            public class Solution 
            {
                public ListNode RemoveNthFromEnd(ListNode head, int n) 
                {
                    var dummy = new ListNode(0, head);
                    var fast = dummy;
                    var slow = dummy;
                    for (int i = 0; i <= n; i++) fast = fast.next;
                    while (fast != null) { fast = fast.next; slow = slow.next; }
                    slow.next = slow.next.next;
                    return dummy.next;
                }
            }

            var sol = new Solution();
            var head = new ListNode(1, new ListNode(2, new ListNode(3, new ListNode(4, new ListNode(5)))));
            sol.RemoveNthFromEnd(head, 2).Dump("After Removal");
            """,
            TestCases = new List<TestCaseItem>
            {
                new() { Name = "[1,2,3,4,5], n=2", Input = "[1,2,3,4,5], n=2", ExpectedOutput = "[1,2,3,5]" }
            }
        },
        new()
        {
            Id = "blind75_23_merge_k_sorted_lists",
            Number = 23,
            Title = "Merge k Sorted Lists",
            Category = "Linked List",
            Difficulty = ProblemDifficulty.Hard,
            AcceptanceRate = 53.0,
            IsPremium = false,
            Tags = new List<string> { "Linked List", "Divide and Conquer", "Heap" },
            TimeComplexity = "O(N log k)",
            SpaceComplexity = "O(k)",
            DescriptionMarkdown = """
            You are given an array of `k` linked-lists `lists`, each linked-list is sorted in ascending order. Merge all the linked-lists into one sorted linked-list and return it.
            """,
            StarterCode = """
            using System.Collections.Generic;

            public class ListNode 
            {
                public int val;
                public ListNode next;
                public ListNode(int val = 0, ListNode next = null) { this.val = val; this.next = next; }
            }

            public class Solution 
            {
                public ListNode MergeKLists(ListNode[] lists) 
                {
                    var pq = new PriorityQueue<ListNode, int>();
                    foreach (var l in lists) if (l != null) pq.Enqueue(l, l.val);
                    var dummy = new ListNode(0);
                    var curr = dummy;
                    while (pq.Count > 0)
                    {
                        var node = pq.Dequeue();
                        curr.next = node;
                        curr = curr.next;
                        if (node.next != null) pq.Enqueue(node.next, node.next.val);
                    }
                    return dummy.next;
                }
            }

            var sol = new Solution();
            var lists = new[] {
                new ListNode(1, new ListNode(4, new ListNode(5))),
                new ListNode(1, new ListNode(3, new ListNode(4))),
                new ListNode(2, new ListNode(6))
            };
            sol.MergeKLists(lists).Dump("Merged K Lists");
            """,
            TestCases = new List<TestCaseItem>
            {
                new() { Name = "3 lists", Input = "[[1,4,5],[1,3,4],[2,6]]", ExpectedOutput = "[1,1,2,3,4,4,5,6]" }
            }
        }
    };
}
