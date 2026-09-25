using System.Collections.Generic;
using PdfEditorApp.Plugins.CSharpEditor.Models;

namespace PdfEditorApp.Plugins.CSharpEditor.Services;

public static partial class Blind75CatalogService
{
    // Every linked-list problem runs on LeetCode's ListNode; BuildList turns [1,2,3] into 1 -> 2 -> 3.
    private const string ListNodeSupportCode = """
        public class ListNode
        {
            public int val;
            public ListNode next;
            public ListNode(int val = 0, ListNode next = null) { this.val = val; this.next = next; }
        }

        // BuildList(1, 2, 3) is 1 -> 2 -> 3; BuildList() is the empty list, null.
        ListNode BuildList(params int[] values)
        {
            ListNode head = null;
            for (int i = values.Length - 1; i >= 0; i--) head = new ListNode(values[i], head);
            return head;
        }
        """;

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
            Tags = new List<string> { "Linked List", "Recursion" },
            TimeComplexity = "O(n)",
            SpaceComplexity = "O(1)",
            DescriptionMarkdown = """
            Given the `head` of a singly linked list, reverse the list and return the **new head**.

            ### Example 1
            - **Input:** `head = [1,2,3,4,5]`
            - **Output:** `[5,4,3,2,1]`
            - **Why:** every arrow now points the other way, so the old last node, 5, is the new head.

            ### Example 2
            - **Input:** `head = [1,2]`
            - **Output:** `[2,1]`

            ### Example 3
            - **Input:** `head = []`
            - **Output:** `[]`

            ### Constraints
            - The number of nodes is in the range `[0, 5000]`.
            - `-5000 <= Node.val <= 5000`
            - **Follow-up:** can you do it both iteratively and recursively?
            """,
            ThinkingProcessMarkdown = """
            **Restate it:** turn `1 → 2 → 3 → null` into `3 → 2 → 1 → null` by changing the `next` pointers, and return the old tail.

            1. **The easy way:** copy the values into a stack or array and build a new list backwards. It works, but uses `O(n)` extra memory and doesn't reverse the nodes you were given.
            2. **Reverse in place, one arrow at a time.** Walk the list with `curr`. Everything behind `curr` is already reversed and starts at `prev` (which starts as `null`, the end of the reversed list).
            3. **The one trap:** setting `curr.next = prev` overwrites the only link to the rest of the list. So first save `next = curr.next`, then flip, then move on: `prev = curr; curr = next`.
            4. **When `curr` becomes `null`,** every arrow has been flipped and `prev` is the new head.
            5. **Walk `[1,2,3]`:** flip 1 (1 → null), flip 2 (2 → 1), flip 3 (3 → 2), return 3.

            **Recursive version:** reverse everything after `head`, then make the node after `head` point back at it: `head.next.next = head; head.next = null`. Same idea, but `O(n)` call stack.

            **Pattern to remember:** when rewiring pointers, save what you're about to overwrite. `prev / curr / next` is the core move of many list problems (reorder, reverse in groups, palindrome list).

            **Common mistakes:** flipping before saving `next` (the rest of the list is lost); returning `head`, which is now the tail; forgetting the empty list.
            """,
            Approaches = new List<ProblemApproachItem>
            {
                new()
                {
                    Name = "Copy the values, build a new list",
                    Kind = ApproachKind.Naive,
                    TimeComplexity = "O(n)",
                    SpaceComplexity = "O(n)",
                    Intuition = "Push every value onto a stack; popping them gives the values in reverse order, so append each popped value to a new list.",
                    BottleneckExplanation = "It allocates a second list and never reverses the nodes you were given, which is what the problem (and an interviewer) asks for.",
                    Code = """
                    ListNode ReverseByCopying(ListNode head)
                    {
                        var values = new Stack<int>();
                        for (var node = head; node != null; node = node.next) values.Push(node.val);

                        var dummy = new ListNode();
                        var tail = dummy;
                        while (values.Count > 0) tail = tail.next = new ListNode(values.Pop());
                        return dummy.next;
                    }

                    Console.WriteLine(Judge.Format(ReverseByCopying(BuildList(1, 2, 3, 4, 5))));   // [5,4,3,2,1]
                    """
                },
                new()
                {
                    Name = "Recursion: reverse the rest, then hook the head on the end",
                    Kind = ApproachKind.Alternative,
                    TimeComplexity = "O(n)",
                    SpaceComplexity = "O(n) call stack",
                    Intuition = "Reverse the list after `head` recursively. The node right after `head` is now the tail of that reversed part, so point it back at `head` and end the list at `head`.",
                    Code = """
                    ListNode ReverseRecursively(ListNode head)
                    {
                        if (head == null || head.next == null) return head;   // empty or one node: already reversed
                        var newHead = ReverseRecursively(head.next);          // reverse everything after head
                        head.next.next = head;                                // the node after head points back at it
                        head.next = null;
                        return newHead;
                    }

                    Console.WriteLine(Judge.Format(ReverseRecursively(BuildList(1, 2))));   // [2,1]
                    """
                },
                new()
                {
                    Name = "Iterative: prev, curr, next",
                    Kind = ApproachKind.Optimal,
                    TimeComplexity = "O(n)",
                    SpaceComplexity = "O(1)",
                    Intuition = "Save `curr.next`, point `curr` back at `prev`, then step both forward. When `curr` falls off the end, `prev` is the new head."
                }
            },
            SupportCode = ListNodeSupportCode,
            SolutionCode = """
            public class Solution
            {
                public ListNode ReverseList(ListNode head)
                {
                    ListNode prev = null, curr = head;
                    while (curr != null)
                    {
                        var next = curr.next;   // save the rest before overwriting the link
                        curr.next = prev;       // flip this node's arrow
                        prev = curr;
                        curr = next;
                    }
                    return prev;                // the old tail is the new head
                }
            }
            """,
            Tests = new List<BlindTest>
            {
                new() { Name = "Example 1", Input = "head = [1,2,3,4,5]", Expected = "[5,4,3,2,1]", Call = "sol.ReverseList(BuildList(1, 2, 3, 4, 5))" },
                new() { Name = "Example 2", Input = "head = [1,2]", Expected = "[2,1]", Call = "sol.ReverseList(BuildList(1, 2))" },
                new() { Name = "Example 3", Input = "head = []", Expected = "[]", Call = "sol.ReverseList(BuildList())" }
            },
            ExtraTests = new List<BlindTest>
            {
                new() { Name = "A single node", Input = "head = [7]", Expected = "[7]", Call = "sol.ReverseList(BuildList(7))" },
                new() { Name = "Negative values", Input = "head = [-1,0,1]", Expected = "[1,0,-1]", Call = "sol.ReverseList(BuildList(-1, 0, 1))" },
                new() { Name = "Repeated values", Input = "head = [2,2,1]", Expected = "[1,2,2]", Call = "sol.ReverseList(BuildList(2, 2, 1))" }
            },
            StressTestCode = """
            judge.Agree("Random lists vs copying",
                random => Enumerable.Range(0, random.Next(0, 9)).Select(_ => random.Next(-5, 6)).ToArray(),
                values => ReverseByCopying(BuildList(values)),
                values => sol.ReverseList(BuildList(values)));
            """,
            VisualizerKind = "LinkedList",
            VisualizationDescription = """
            `[1,2,3,4,5]`. The nodes stay where they are and the arrows flip one by one: each round saves `next`, points
            `curr` back at `prev`, then moves both pointers forward. When `curr` reaches null, `prev` is the new head.
            """,
            VisualizationCode = """
            var head = BuildList(1, 2, 3, 4, 5);
            var tracker = LinkedListTracker.Create(head, "206. Reverse Linked List: flip one arrow at a time");
            string Name(ListNode node) => node == null ? "null" : node.val.ToString();

            ListNode prev = null, curr = head;
            tracker.Step("prev starts at null (nothing is reversed yet) and curr at the head", new { prev, curr });
            while (curr != null)
            {
                var next = curr.next;
                tracker.Step($"Save next = {Name(next)} first, or the rest of the list would be lost", new { prev, curr, next });
                curr.next = prev;
                tracker.Step($"Flip: {curr.val}.next now points to {Name(prev)}", new { prev, curr, next });
                prev = curr;
                curr = next;
                tracker.Step($"Step forward: prev = {Name(prev)}, curr = {Name(curr)}", new { prev, curr });
            }

            tracker.Step($"curr is null, so every arrow is flipped: prev = {prev.val} is the new head", new { head = prev });
            Display.Visualizer(tracker);
            """
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
            TimeComplexity = "O(n + m)",
            SpaceComplexity = "O(1)",
            DescriptionMarkdown = """
            You are given the heads of two **sorted** linked lists, `list1` and `list2`. Merge them into one sorted list by **splicing together their nodes** (no new nodes needed) and return the head of the merged list.

            ### Example 1
            - **Input:** `list1 = [1,2,4], list2 = [1,3,4]`
            - **Output:** `[1,1,2,3,4,4]`

            ### Example 2
            - **Input:** `list1 = [], list2 = []`
            - **Output:** `[]`

            ### Example 3
            - **Input:** `list1 = [], list2 = [0]`
            - **Output:** `[0]`

            ### Constraints
            - The number of nodes in each list is in the range `[0, 50]`.
            - `-100 <= Node.val <= 100`
            - Both lists are sorted in non-decreasing order.
            """,
            ThinkingProcessMarkdown = """
            **Restate it:** zip two sorted lists into one sorted list, reusing their nodes.

            1. **Think of two sorted piles of cards.** The smallest card left is always on top of one of the two piles, so compare the two fronts, take the smaller one, and repeat.
            2. **A dummy node** in front of the result means the first node isn't a special case: `tail` starts at the dummy, and each step does `tail.next = smaller; tail = tail.next`. The answer is `dummy.next`.
            3. **When one list runs out,** the other one's remaining nodes are already sorted and all bigger: attach them in one step, `tail.next = list1 ?? list2`.
            4. **Ties:** taking from `list1` when the fronts are equal keeps equal values in their original order (the merge is stable).
            5. **Walk Example 1:** 1 vs 1 → list1's 1. 2 vs 1 → list2's 1. 2 vs 3 → 2. 4 vs 3 → 3. 4 vs 4 → list1's 4. list1 is empty → attach `[4]`.

            **Pattern to remember:** "merge" is the heart of merge sort and of k-way merging (problem 23). The dummy-head trick shows up in almost every list-building problem.

            **Common mistakes:** forgetting to attach the leftover list; returning `dummy` instead of `dummy.next`; moving `tail` before linking.
            """,
            Approaches = new List<ProblemApproachItem>
            {
                new()
                {
                    Name = "Collect, sort, rebuild",
                    Kind = ApproachKind.Naive,
                    TimeComplexity = "O((n + m) log(n + m))",
                    SpaceComplexity = "O(n + m)",
                    Intuition = "Put every value from both lists into one array, sort it, and build a fresh list.",
                    BottleneckExplanation = "Sorting ignores that both lists are already sorted, and it creates new nodes instead of splicing the given ones.",
                    Code = """
                    ListNode MergeBySorting(ListNode list1, ListNode list2)
                    {
                        var values = new List<int>();
                        for (var node = list1; node != null; node = node.next) values.Add(node.val);
                        for (var node = list2; node != null; node = node.next) values.Add(node.val);
                        values.Sort();
                        return BuildList(values.ToArray());
                    }

                    Console.WriteLine(Judge.Format(MergeBySorting(BuildList(1, 2, 4), BuildList(1, 3, 4))));   // [1,1,2,3,4,4]
                    """
                },
                new()
                {
                    Name = "Recursion: the smaller head, then merge the rest",
                    Kind = ApproachKind.Alternative,
                    TimeComplexity = "O(n + m)",
                    SpaceComplexity = "O(n + m) call stack",
                    Intuition = "The merged list starts with the smaller head; its `next` is the merge of everything that's left.",
                    Code = """
                    ListNode MergeRecursively(ListNode list1, ListNode list2)
                    {
                        if (list1 == null) return list2;
                        if (list2 == null) return list1;
                        if (list1.val <= list2.val)
                        {
                            list1.next = MergeRecursively(list1.next, list2);
                            return list1;
                        }
                        list2.next = MergeRecursively(list1, list2.next);
                        return list2;
                    }

                    Console.WriteLine(Judge.Format(MergeRecursively(BuildList(), BuildList(0))));   // [0]
                    """
                },
                new()
                {
                    Name = "Iterative merge with a dummy head",
                    Kind = ApproachKind.Optimal,
                    TimeComplexity = "O(n + m)",
                    SpaceComplexity = "O(1)",
                    Intuition = "Keep a `tail` pointer starting at a dummy node. Link the smaller front, advance that list and `tail`; at the end attach whatever is left."
                }
            },
            SupportCode = ListNodeSupportCode,
            SolutionCode = """
            public class Solution
            {
                public ListNode MergeTwoLists(ListNode list1, ListNode list2)
                {
                    var dummy = new ListNode();   // a placeholder in front of the result
                    var tail = dummy;
                    while (list1 != null && list2 != null)
                    {
                        if (list1.val <= list2.val) { tail.next = list1; list1 = list1.next; }
                        else { tail.next = list2; list2 = list2.next; }
                        tail = tail.next;
                    }
                    tail.next = list1 ?? list2;   // whatever is left is already sorted
                    return dummy.next;
                }
            }
            """,
            Tests = new List<BlindTest>
            {
                new() { Name = "Example 1", Input = "list1 = [1,2,4], list2 = [1,3,4]", Expected = "[1,1,2,3,4,4]", Call = "sol.MergeTwoLists(BuildList(1, 2, 4), BuildList(1, 3, 4))" },
                new() { Name = "Example 2", Input = "list1 = [], list2 = []", Expected = "[]", Call = "sol.MergeTwoLists(BuildList(), BuildList())" },
                new() { Name = "Example 3", Input = "list1 = [], list2 = [0]", Expected = "[0]", Call = "sol.MergeTwoLists(BuildList(), BuildList(0))" }
            },
            ExtraTests = new List<BlindTest>
            {
                new() { Name = "One list is longer", Input = "list1 = [1], list2 = [2,3,4,5]", Expected = "[1,2,3,4,5]", Call = "sol.MergeTwoLists(BuildList(1), BuildList(2, 3, 4, 5))" },
                new() { Name = "All of list2 comes first", Input = "list1 = [5,6], list2 = [1,2]", Expected = "[1,2,5,6]", Call = "sol.MergeTwoLists(BuildList(5, 6), BuildList(1, 2))" },
                new() { Name = "Negative values", Input = "list1 = [-3,0], list2 = [-2]", Expected = "[-3,-2,0]", Call = "sol.MergeTwoLists(BuildList(-3, 0), BuildList(-2))" },
                new() { Name = "Equal values", Input = "list1 = [1,1], list2 = [1]", Expected = "[1,1,1]", Call = "sol.MergeTwoLists(BuildList(1, 1), BuildList(1))" }
            },
            StressTestCode = """
            judge.Agree("Random sorted lists vs sorting",
                random => (a: Enumerable.Range(0, random.Next(0, 6)).Select(_ => random.Next(-5, 6)).OrderBy(x => x).ToArray(),
                           b: Enumerable.Range(0, random.Next(0, 6)).Select(_ => random.Next(-5, 6)).OrderBy(x => x).ToArray()),
                input => MergeBySorting(BuildList(input.a), BuildList(input.b)),
                input => sol.MergeTwoLists(BuildList(input.a), BuildList(input.b)));
            """,
            VisualizerKind = "LinkedList",
            VisualizationDescription = """
            Example 1, one row per chain. The grey dummy starts the result row and the finished part turns green. Each
            step links the smaller front after `tail`; the rest of that list still hangs off the node just taken until
            the next link replaces it, which is how splicing works. At the end the leftover list is attached in one step.
            """,
            VisualizationCode = """
            var list1 = BuildList(1, 2, 4);
            var list2 = BuildList(1, 3, 4);
            var tracker = LinkedListTracker.Create(list1, "21. Merge Two Sorted Lists: always take the smaller front", rows: true);

            var dummy = new ListNode();
            var tail = dummy;
            tracker.Mark(dummy, "#334155");
            tracker.Step("A dummy node starts the result; tail is its last node", new { list1, list2, tail });

            while (list1 != null && list2 != null)
            {
                string why = list1.val <= list2.val
                    ? $"{list1.val} ≤ {list2.val}: link list1's {list1.val}"
                    : $"{list1.val} > {list2.val}: link list2's {list2.val}";
                if (list1.val <= list2.val) { tail.next = list1; list1 = list1.next; }
                else { tail.next = list2; list2 = list2.next; }
                tail = tail.next;
                tracker.Mark(tail);
                tracker.Step($"{why} after tail, and tail moves onto it", new { list1, list2, tail });
            }

            tail.next = list1 ?? list2;
            for (var node = tail.next; node != null; node = node.next) tracker.Mark(node);
            tracker.Step($"{(list1 == null ? "list1" : "list2")} is empty: attach the other list's remaining nodes, already sorted", new { list1, list2, tail });
            tracker.Step($"Done: dummy.next is the merged list {Judge.Format(dummy.next)}", new { head = dummy.next });
            Display.Visualizer(tracker);
            """
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
            TimeComplexity = "O(n)",
            SpaceComplexity = "O(1)",
            DescriptionMarkdown = """
            Given `head`, the head of a linked list, return `true` if the list has a **cycle**: some node can be reached again by following `next` pointers. Otherwise return `false`.

            The examples describe the cycle with `pos`, the index of the node the tail's `next` points back to (`-1` means no cycle). `pos` is **not** passed to your function.

            ### Example 1
            - **Input:** `head = [3,2,0,-4], pos = 1`
            - **Output:** `true`
            - **Why:** the tail, -4, links back to the node at index 1 (the 2).

            ### Example 2
            - **Input:** `head = [1,2], pos = 0`
            - **Output:** `true`

            ### Example 3
            - **Input:** `head = [1], pos = -1`
            - **Output:** `false`

            ### Constraints
            - The number of nodes is in the range `[0, 10^4]`.
            - `-10^5 <= Node.val <= 10^5`
            - `pos` is `-1` or a valid index in the list.
            - **Follow-up:** can you solve it using `O(1)` memory?
            """,
            ThinkingProcessMarkdown = """
            **Restate it:** following `next` either reaches `null` (no cycle) or loops forever. Detect the loop without looping forever.

            1. **Remember where you've been:** put each visited node (the node itself, not its value, since values can repeat) in a set. Seeing a node twice means a cycle. That's `O(n)` time and `O(n)` memory.
            2. **`O(1)` memory: a slow and a fast runner.** `slow` moves one node per step, `fast` moves two. Without a cycle, `fast` hits `null` first, and we're done.
            3. **With a cycle, they must meet.** Once both are inside the loop, `fast` gains exactly one node on `slow` every step. The gap shrinks 3, 2, 1, 0, so `fast` can't jump over `slow`; it lands on it, within one lap.
            4. **Loop condition:** keep going while `fast` and `fast.next` exist, since `fast` needs both steps.
            5. **Walk Example 1:** start 3/3; then slow 2, fast 0; slow 0, fast 2; slow -4, fast -4 → they meet → `true`.

            **Pattern to remember:** fast and slow pointers, also known as Floyd's tortoise and hare. The same trick finds the middle of a list and the start of the cycle.

            **Common mistakes:** comparing values instead of node references; reading `fast.next.next` without checking `fast.next`; checking `slow == fast` before moving (they start equal).
            """,
            Approaches = new List<ProblemApproachItem>
            {
                new()
                {
                    Name = "Remember visited nodes in a set",
                    Kind = ApproachKind.Naive,
                    TimeComplexity = "O(n)",
                    SpaceComplexity = "O(n)",
                    Intuition = "Walk the list and add every node to a set. Adding a node that's already there means we came back to it.",
                    BottleneckExplanation = "The set grows with the list: `O(n)` extra memory, and the follow-up asks for `O(1)`.",
                    Code = """
                    bool HasCycleWithSet(ListNode head)
                    {
                        var seen = new HashSet<ListNode>();   // by reference: equal values are still different nodes
                        for (var node = head; node != null; node = node.next)
                            if (!seen.Add(node)) return true;
                        return false;
                    }

                    Console.WriteLine(Judge.Format(HasCycleWithSet(BuildCycle(new[] { 3, 2, 0, -4 }, 1))));   // true
                    """
                },
                new()
                {
                    Name = "Slow and fast pointers (Floyd's cycle detection)",
                    Kind = ApproachKind.Optimal,
                    TimeComplexity = "O(n)",
                    SpaceComplexity = "O(1)",
                    Intuition = "Move `slow` one node and `fast` two nodes per step. `fast` reaching `null` means no cycle; the two meeting means there is one."
                }
            },
            SupportCode = ListNodeSupportCode + """


                // BuildCycle([3,2,0,-4], 1): the tail's next points back to the node at index 1 (-1 for no cycle).
                ListNode BuildCycle(int[] values, int pos)
                {
                    var head = BuildList(values);
                    if (pos < 0 || head == null) return head;
                    ListNode target = head, tail = head;
                    for (int i = 0; i < pos; i++) target = target.next;   // the node the tail links back to
                    while (tail.next != null) tail = tail.next;
                    tail.next = target;
                    return head;
                }
                """,
            SolutionCode = """
            public class Solution
            {
                public bool HasCycle(ListNode head)
                {
                    ListNode slow = head, fast = head;
                    while (fast != null && fast.next != null)
                    {
                        slow = slow.next;                  // one step
                        fast = fast.next.next;             // two steps
                        if (slow == fast) return true;     // fast came round behind slow: a loop
                    }
                    return false;                          // fast fell off the end
                }
            }
            """,
            Tests = new List<BlindTest>
            {
                new() { Name = "Example 1", Input = "head = [3,2,0,-4], pos = 1", Expected = "true", Call = "sol.HasCycle(BuildCycle(new[] { 3, 2, 0, -4 }, 1))" },
                new() { Name = "Example 2", Input = "head = [1,2], pos = 0", Expected = "true", Call = "sol.HasCycle(BuildCycle(new[] { 1, 2 }, 0))" },
                new() { Name = "Example 3", Input = "head = [1], pos = -1", Expected = "false", Call = "sol.HasCycle(BuildCycle(new[] { 1 }, -1))" }
            },
            ExtraTests = new List<BlindTest>
            {
                new() { Name = "Empty list", Input = "head = [], pos = -1", Expected = "false", Call = "sol.HasCycle(BuildList())" },
                new() { Name = "A node pointing at itself", Input = "head = [1], pos = 0", Expected = "true", Call = "sol.HasCycle(BuildCycle(new[] { 1 }, 0))" },
                new() { Name = "Long list without a cycle", Input = "head = [1,2,3,4,5,6], pos = -1", Expected = "false", Call = "sol.HasCycle(BuildCycle(new[] { 1, 2, 3, 4, 5, 6 }, -1))" },
                new() { Name = "Tail points at itself", Input = "head = [1,2,3,4], pos = 3", Expected = "true", Call = "sol.HasCycle(BuildCycle(new[] { 1, 2, 3, 4 }, 3))" },
                new() { Name = "Equal values, no cycle", Input = "head = [1,1,1], pos = -1", Expected = "false", Call = "sol.HasCycle(BuildCycle(new[] { 1, 1, 1 }, -1))" }
            },
            StressTestCode = """
            judge.Agree("Random lists and loops vs a visited set",
                random =>
                {
                    int size = random.Next(0, 9);
                    return (values: Enumerable.Range(0, size).Select(_ => random.Next(0, 4)).ToArray(), pos: size == 0 ? -1 : random.Next(-1, size));
                },
                input => HasCycleWithSet(BuildCycle(input.values, input.pos)),
                input => sol.HasCycle(BuildCycle(input.values, input.pos)));
            """,
            VisualizerKind = "LinkedList",
            VisualizationDescription = """
            `[1,2,3,4,5,6]` with the tail looping back to 3 (the red arrow). `slow` moves one node per step and `fast`
            two. Once both are inside the loop, `fast` closes the gap by one node every step, so they land on the same
            node and the answer is true.
            """,
            VisualizationCode = """
            var head = BuildCycle(new[] { 1, 2, 3, 4, 5, 6 }, 2);
            var tracker = LinkedListTracker.Create(head, "141. Linked List Cycle: slow and fast pointers");
            string Name(ListNode node) => node == null ? "null" : node.val.ToString();

            ListNode slow = head, fast = head;
            int moves = 0;
            bool cycle = false;
            tracker.Step("Both start at the head. Each move: slow goes 1 node, fast goes 2", new { slow, fast });
            while (fast != null && fast.next != null)
            {
                slow = slow.next;
                fast = fast.next.next;
                moves++;
                if (slow == fast)
                {
                    cycle = true;
                    tracker.Step($"Move {moves}: both are on {slow.val}. Only a loop can bring fast round behind slow, so the answer is true", new { slow, fast });
                    break;
                }
                tracker.Step($"Move {moves}: slow → {Name(slow)}, fast → {Name(fast)}", new { slow, fast });
            }

            if (!cycle) tracker.Step("fast reached the end of the list: no cycle, false", new { slow, fast });
            Display.Visualizer(tracker);
            """
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
            Tags = new List<string> { "Linked List", "Two Pointers", "Stack", "Recursion" },
            TimeComplexity = "O(n)",
            SpaceComplexity = "O(1)",
            DescriptionMarkdown = """
            You are given the head of a singly linked list `L0 → L1 → … → Ln-1 → Ln`. Reorder it **in place** to:

            `L0 → Ln → L1 → Ln-1 → L2 → Ln-2 → …`

            You may not change the values in the nodes; only the links may change. The function returns nothing.

            ### Example 1
            - **Input:** `head = [1,2,3,4]`
            - **Output:** `[1,4,2,3]`

            ### Example 2
            - **Input:** `head = [1,2,3,4,5]`
            - **Output:** `[1,5,2,4,3]`
            - **Why:** first, last, second, second-to-last, and the middle node 3 ends the list.

            ### Constraints
            - The number of nodes is in the range `[1, 5 * 10^4]`.
            - `1 <= Node.val <= 1000`
            """,
            ThinkingProcessMarkdown = """
            **Restate it:** alternate between the front and the back of the list: first, last, second, second-to-last, …

            1. **With extra memory it's easy:** put the nodes in an array and relink them with one index moving forward and one moving backward. That's `O(n)` extra space.
            2. **Look at the answer's shape:** `1, 5, 2, 4, 3` is the first half `1, 2, 3` woven together with the second half **reversed**, `5, 4`. So it splits into three problems you already know:
               - **Find the middle** with slow/fast pointers (fast moves two nodes, slow one).
               - **Cut** after the middle (`slow.next = null`) and **reverse** the second half (problem 206).
               - **Weave** the halves: take one node from each alternately, like merging (problem 21) without comparing.
            3. **Why the first half gets the middle:** stopping when `fast` can't take two more steps leaves the first half equal or one node longer, so the weave always ends on the first half's last node.
            4. **Walk `[1,2,3,4,5]`:** the middle is 3; halves `1,2,3` and `4,5`; reversed `5,4`; weave → `1,5,2,4,3`.

            **Pattern to remember:** hard-looking list problems are often three easy ones glued together: middle, reverse, merge.

            **Common mistakes:** forgetting `slow.next = null` (the halves stay connected and the weave makes a cycle); losing a `next` pointer during the weave (save both before relinking); an off-by-one middle for even lengths.
            """,
            Approaches = new List<ProblemApproachItem>
            {
                new()
                {
                    Name = "Nodes in an array, two indices",
                    Kind = ApproachKind.Naive,
                    TimeComplexity = "O(n)",
                    SpaceComplexity = "O(n)",
                    Intuition = "Store the nodes in a list so the back is reachable, then link front → back → next front → next back, moving `i` forward and `j` backward until they meet.",
                    BottleneckExplanation = "The array of nodes costs `O(n)` extra memory, which the reverse-the-second-half trick avoids.",
                    Code = """
                    void ReorderWithArray(ListNode head)
                    {
                        var nodes = new List<ListNode>();
                        for (var node = head; node != null; node = node.next) nodes.Add(node);
                        if (nodes.Count == 0) return;

                        int i = 0, j = nodes.Count - 1;
                        while (i < j)
                        {
                            nodes[i].next = nodes[j];   // front -> back
                            i++;
                            if (i == j) break;
                            nodes[j].next = nodes[i];   // back -> next front
                            j--;
                        }
                        nodes[i].next = null;           // the last node placed ends the list
                    }

                    var example = BuildList(1, 2, 3, 4, 5);
                    ReorderWithArray(example);
                    Console.WriteLine(Judge.Format(example));   // [1,5,2,4,3]
                    """
                },
                new()
                {
                    Name = "Middle, reverse the second half, weave",
                    Kind = ApproachKind.Optimal,
                    TimeComplexity = "O(n)",
                    SpaceComplexity = "O(1)",
                    Intuition = "Find the middle with slow/fast pointers, cut there, reverse the second half in place, then alternate nodes from the two halves."
                }
            },
            SupportCode = ListNodeSupportCode,
            SolutionCode = """
            public class Solution
            {
                public void ReorderList(ListNode head)
                {
                    if (head == null || head.next == null) return;

                    // 1. Find the middle: when fast can't take two more steps, slow ends the first half.
                    ListNode slow = head, fast = head;
                    while (fast.next != null && fast.next.next != null)
                    {
                        slow = slow.next;
                        fast = fast.next.next;
                    }

                    // 2. Cut after the middle and reverse the second half.
                    ListNode second = slow.next, prev = null;
                    slow.next = null;
                    while (second != null)
                    {
                        var next = second.next;
                        second.next = prev;
                        prev = second;
                        second = next;
                    }

                    // 3. Weave: one node from each half, alternately.
                    ListNode first = head;
                    second = prev;
                    while (second != null)
                    {
                        var firstNext = first.next;
                        var secondNext = second.next;
                        first.next = second;
                        second.next = firstNext;
                        first = firstNext;
                        second = secondNext;
                    }
                }
            }
            """,
            TestSetupCode = """
            // ReorderList returns nothing, so the tests look at the list it rearranged.
            ListNode Reordered(params int[] values)
            {
                var head = BuildList(values);
                sol.ReorderList(head);
                return head;
            }
            """,
            Tests = new List<BlindTest>
            {
                new() { Name = "Example 1", Input = "head = [1,2,3,4]", Expected = "[1,4,2,3]", Call = "Reordered(1, 2, 3, 4)" },
                new() { Name = "Example 2", Input = "head = [1,2,3,4,5]", Expected = "[1,5,2,4,3]", Call = "Reordered(1, 2, 3, 4, 5)" }
            },
            ExtraTests = new List<BlindTest>
            {
                new() { Name = "A single node", Input = "head = [1]", Expected = "[1]", Call = "Reordered(1)" },
                new() { Name = "Two nodes stay put", Input = "head = [1,2]", Expected = "[1,2]", Call = "Reordered(1, 2)" },
                new() { Name = "Three nodes", Input = "head = [1,2,3]", Expected = "[1,3,2]", Call = "Reordered(1, 2, 3)" },
                new() { Name = "Six nodes", Input = "head = [1,2,3,4,5,6]", Expected = "[1,6,2,5,3,4]", Call = "Reordered(1, 2, 3, 4, 5, 6)" }
            },
            StressTestCode = """
            judge.Agree("Random lists vs the array version",
                random => Enumerable.Range(0, random.Next(1, 10)).Select(_ => random.Next(1, 20)).ToArray(),
                values => { var head = BuildList(values); ReorderWithArray(head); return head; },
                values => { var head = BuildList(values); sol.ReorderList(head); return head; });
            """,
            VisualizerKind = "LinkedList",
            VisualizationDescription = """
            `[1,2,3,4,5]`, one row per chain. slow/fast find the middle, the cut splits off the second half (orange),
            which is reversed arrow by arrow, and then the weave takes one node from each half until the orange nodes
            sit between the others: 1, 5, 2, 4, 3.
            """,
            VisualizationCode = """
            var head = BuildList(1, 2, 3, 4, 5);
            var tracker = LinkedListTracker.Create(head, "143. Reorder List: middle, reverse, weave", rows: true);
            string Name(ListNode node) => node == null ? "null" : node.val.ToString();

            // 1. Find the middle
            ListNode slow = head, fast = head;
            tracker.Step("Part 1, find the middle: slow moves 1 node per step, fast moves 2", new { slow, fast });
            while (fast.next != null && fast.next.next != null)
            {
                slow = slow.next;
                fast = fast.next.next;
                tracker.Step($"slow → {slow.val}, fast → {fast.val}", new { slow, fast });
            }

            // 2. Cut after the middle and reverse the second half
            ListNode second = slow.next, prev = null;
            slow.next = null;
            for (var node = second; node != null; node = node.next) tracker.Mark(node, "#7c2d12");
            tracker.Step($"fast can't take two more steps, so {slow.val} ends the first half. Part 2: cut after it", new { slow, second });
            while (second != null)
            {
                var next = second.next;
                second.next = prev;
                prev = second;
                second = next;
                tracker.Step($"Reverse the second half: {prev.val} now points to {Name(prev.next)}", new { prev, second });
            }

            // 3. Weave the halves together
            ListNode first = head;
            second = prev;
            tracker.Step("Part 3, weave: take one node from each half in turn", new { first, second });
            while (second != null)
            {
                var firstNext = first.next;
                var secondNext = second.next;
                first.next = second;
                second.next = firstNext;
                tracker.Step($"Link {first.val} → {second.val} → {Name(firstNext)}", new { first, second, firstNext, secondNext });
                first = firstNext;
                second = secondNext;
            }

            tracker.Step($"Done: {Judge.Format(head)}, alternating front and back", new { head });
            Display.Visualizer(tracker);
            """
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
            TimeComplexity = "O(n)",
            SpaceComplexity = "O(1)",
            DescriptionMarkdown = """
            Given the `head` of a linked list, remove the `n`-th node **from the end** of the list and return the head.

            ### Example 1
            - **Input:** `head = [1,2,3,4,5], n = 2`
            - **Output:** `[1,2,3,5]`
            - **Why:** counting from the end, 5 is the 1st node and 4 is the 2nd, so 4 goes.

            ### Example 2
            - **Input:** `head = [1], n = 1`
            - **Output:** `[]`

            ### Example 3
            - **Input:** `head = [1,2], n = 1`
            - **Output:** `[1]`

            ### Constraints
            - The number of nodes is `sz`, with `1 <= sz <= 30`.
            - `0 <= Node.val <= 100`
            - `1 <= n <= sz`
            - **Follow-up:** can you do it in one pass?
            """,
            ThinkingProcessMarkdown = """
            **Restate it:** unlink the `n`-th node counting from the back. To unlink a node you need the node **before** it: `before.next = before.next.next`.

            1. **Two passes:** count the length `L`, then walk `L - n` nodes from a dummy in front of the head to reach the node before the target. Simple and `O(n)`, but it reads the list twice.
            2. **One pass with a gap:** start `fast` and `slow` at a dummy node and move `fast` `n + 1` nodes ahead. Now walk both until `fast` is `null`. The gap never changes, so `slow` stops exactly `n + 1` nodes before the end: right in front of the target.
            3. **Why a dummy node?** If the target is the head itself (`n == length`), the "node before it" doesn't exist. The dummy gives every node, including the head, a node in front, so there's no special case. Return `dummy.next`.
            4. **Walk Example 1** (`n = 2`): `fast` moves 3 nodes to 3. Then both move until `fast` is null: `slow` stops at 3. Link 3 → 5, skipping 4.

            **Pattern to remember:** "k-th from the end" = two pointers k apart moving together. The dummy head removes special cases for the first node.

            **Common mistakes:** moving `fast` only `n` steps (then `slow` lands *on* the target, not before it); no dummy, so removing the head crashes; returning `head` after the head was removed.
            """,
            Approaches = new List<ProblemApproachItem>
            {
                new()
                {
                    Name = "Two passes: count, then walk to the node before",
                    Kind = ApproachKind.Naive,
                    TimeComplexity = "O(n)",
                    SpaceComplexity = "O(1)",
                    Intuition = "The first pass counts `L` nodes. The target is at position `L - n` from the start, so walk `L - n` nodes from a dummy and skip the next one.",
                    BottleneckExplanation = "It is linear, but it reads the list twice; the follow-up asks for a single pass.",
                    Code = """
                    ListNode RemoveWithTwoPasses(ListNode head, int n)
                    {
                        int length = 0;
                        for (var node = head; node != null; node = node.next) length++;

                        var dummy = new ListNode(0, head);
                        var before = dummy;
                        for (int i = 0; i < length - n; i++) before = before.next;   // the node in front of the target
                        before.next = before.next.next;
                        return dummy.next;
                    }

                    Console.WriteLine(Judge.Format(RemoveWithTwoPasses(BuildList(1, 2, 3, 4, 5), 2)));   // [1,2,3,5]
                    """
                },
                new()
                {
                    Name = "One pass: two pointers n + 1 apart",
                    Kind = ApproachKind.Optimal,
                    TimeComplexity = "O(n)",
                    SpaceComplexity = "O(1)",
                    Intuition = "Give `fast` a head start of `n + 1` nodes from a dummy, then move both until `fast` is null; `slow` is then just before the node to remove."
                }
            },
            SupportCode = ListNodeSupportCode,
            SolutionCode = """
            public class Solution
            {
                public ListNode RemoveNthFromEnd(ListNode head, int n)
                {
                    var dummy = new ListNode(0, head);           // gives the head a node in front of it too
                    ListNode fast = dummy, slow = dummy;
                    for (int i = 0; i <= n; i++) fast = fast.next;   // open a gap of n + 1 nodes
                    while (fast != null)
                    {
                        fast = fast.next;
                        slow = slow.next;
                    }
                    slow.next = slow.next.next;                  // slow is just before the node to remove
                    return dummy.next;
                }
            }
            """,
            Tests = new List<BlindTest>
            {
                new() { Name = "Example 1", Input = "head = [1,2,3,4,5], n = 2", Expected = "[1,2,3,5]", Call = "sol.RemoveNthFromEnd(BuildList(1, 2, 3, 4, 5), 2)" },
                new() { Name = "Example 2", Input = "head = [1], n = 1", Expected = "[]", Call = "sol.RemoveNthFromEnd(BuildList(1), 1)" },
                new() { Name = "Example 3", Input = "head = [1,2], n = 1", Expected = "[1]", Call = "sol.RemoveNthFromEnd(BuildList(1, 2), 1)" }
            },
            ExtraTests = new List<BlindTest>
            {
                new() { Name = "Remove the head", Input = "head = [1,2,3], n = 3", Expected = "[2,3]", Call = "sol.RemoveNthFromEnd(BuildList(1, 2, 3), 3)" },
                new() { Name = "Remove the tail", Input = "head = [1,2,3], n = 1", Expected = "[1,2]", Call = "sol.RemoveNthFromEnd(BuildList(1, 2, 3), 1)" },
                new() { Name = "Middle of a longer list", Input = "head = [1,2,3,4,5,6], n = 3", Expected = "[1,2,3,5,6]", Call = "sol.RemoveNthFromEnd(BuildList(1, 2, 3, 4, 5, 6), 3)" },
                new() { Name = "Two nodes, remove the first", Input = "head = [1,2], n = 2", Expected = "[2]", Call = "sol.RemoveNthFromEnd(BuildList(1, 2), 2)" }
            },
            StressTestCode = """
            judge.Agree("Random lists vs two passes",
                random =>
                {
                    int size = random.Next(1, 10);
                    return (values: Enumerable.Range(0, size).Select(_ => random.Next(0, 10)).ToArray(), n: random.Next(1, size + 1));
                },
                input => RemoveWithTwoPasses(BuildList(input.values), input.n),
                input => sol.RemoveNthFromEnd(BuildList(input.values), input.n));
            """,
            VisualizerKind = "LinkedList",
            VisualizationDescription = """
            Example 1 behind a grey dummy node. `fast` first gets a head start of `n + 1 = 3` nodes, then both move
            together until `fast` runs off the end. `slow` is left just before the 2nd node from the end, whose link is
            skipped (the removed node turns red).
            """,
            VisualizationCode = """
            int n = 2;
            var head = BuildList(1, 2, 3, 4, 5);
            var dummy = new ListNode(0, head);
            var tracker = LinkedListTracker.Create(dummy, "19. Remove Nth Node From End: two pointers n + 1 apart");
            string Name(ListNode node) => node == null ? "null" : node.val.ToString();
            tracker.Mark(dummy, "#334155");

            ListNode fast = dummy, slow = dummy;
            tracker.Step($"A dummy node sits in front of the head. Goal: remove node {n} from the end", new { slow, fast });
            for (int i = 0; i <= n; i++)
            {
                fast = fast.next;
                tracker.Step($"Head start for fast: {i + 1} of {n + 1} nodes", new { slow, fast });
            }

            while (fast != null)
            {
                fast = fast.next;
                slow = slow.next;
                tracker.Step(fast == null
                    ? $"fast ran off the end, so slow ({slow.val}) is just before the node to remove"
                    : "Move both together: the gap between them stays the same", new { slow, fast });
            }

            var removed = slow.next;
            slow.next = slow.next.next;
            tracker.Mark(removed, "#7f1d1d");
            tracker.Step($"Skip it: {slow.val}.next jumps over {removed.val} straight to {Name(slow.next)}", new { slow, removed });
            tracker.Step($"Return dummy.next: {Judge.Format(dummy.next)}", new { head = dummy.next });
            Display.Visualizer(tracker);
            """
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
            Tags = new List<string> { "Linked List", "Divide and Conquer", "Heap (Priority Queue)", "Merge Sort" },
            TimeComplexity = "O(N log k)",
            SpaceComplexity = "O(k)",
            DescriptionMarkdown = """
            You are given an array `lists` of `k` linked lists, each sorted in ascending order. Merge them all into **one sorted linked list** and return its head.

            ### Example 1
            - **Input:** `lists = [[1,4,5],[1,3,4],[2,6]]`
            - **Output:** `[1,1,2,3,4,4,5,6]`

            ### Example 2
            - **Input:** `lists = []`
            - **Output:** `[]`

            ### Example 3
            - **Input:** `lists = [[]]`
            - **Output:** `[]`
            - **Why:** there is one list, and it is empty.

            ### Constraints
            - `k == lists.length`, `0 <= k <= 10^4`
            - `0 <= lists[i].length <= 500`, and the total number of nodes `N` is at most `10^4`.
            - `-10^4 <= lists[i][j] <= 10^4`, and each list is sorted in ascending order.
            """,
            ThinkingProcessMarkdown = """
            **Restate it:** problem 21 (merge two sorted lists), but with `k` lists at once.

            1. **The next node of the answer** is always the smallest of the `k` current fronts. Scanning all `k` fronts for every node costs `O(k)` per node, `O(N·k)` in total.
            2. **A min-heap finds that smallest front in `O(log k)`.** Put each list's first node in a priority queue keyed by its value. Repeatedly pop the smallest, link it after `tail`, and push that node's `next` (the new front of its list). Every node is pushed and popped once: `O(N log k)`.
            3. **The heap never holds more than `k` nodes,** one per list, so the extra memory is `O(k)`.
            4. **Another `O(N log k)` way:** merge the lists in pairs (1+2, 3+4, …), then pair up the results, like merge sort. There are `log k` rounds and each round touches all `N` nodes.
            5. **Walk Example 1:** the heap holds `{1, 1, 2}`. Pop a 1 and push 4 → `{1, 2, 4}`. Pop the other 1 and push 3 → `{2, 3, 4}`. Pop 2 and push 6 → … until the heap is empty.

            **Pattern to remember:** "the best of k candidates, over and over" is a heap. Merging k sorted streams is the classic example (it's also how databases merge sorted runs).

            **Common mistakes:** pushing `null` for empty lists; forgetting to push the popped node's `next`; merging lists one by one into a growing result (`O(N·k)`).
            """,
            Approaches = new List<ProblemApproachItem>
            {
                new()
                {
                    Name = "Collect every value, sort, rebuild",
                    Kind = ApproachKind.Naive,
                    TimeComplexity = "O(N log N)",
                    SpaceComplexity = "O(N)",
                    Intuition = "Pour all the values into one array, sort it, and build a new list.",
                    BottleneckExplanation = "It throws away the fact that each list is already sorted, and it builds new nodes instead of relinking the existing ones.",
                    Code = """
                    ListNode MergeKBySorting(ListNode[] lists)
                    {
                        var values = new List<int>();
                        foreach (var list in lists)
                            for (var node = list; node != null; node = node.next) values.Add(node.val);
                        values.Sort();
                        return BuildList(values.ToArray());
                    }

                    Console.WriteLine(Judge.Format(MergeKBySorting(new[] { BuildList(1, 4, 5), BuildList(1, 3, 4), BuildList(2, 6) })));   // [1,1,2,3,4,4,5,6]
                    """
                },
                new()
                {
                    Name = "Divide and conquer: merge in pairs",
                    Kind = ApproachKind.Alternative,
                    TimeComplexity = "O(N log k)",
                    SpaceComplexity = "O(1) extra (besides the list of lists)",
                    Intuition = "Merge lists two at a time with problem 21's merge. Each round halves the number of lists, so there are `log k` rounds of `O(N)` work.",
                    Code = """
                    ListNode MergeTwo(ListNode a, ListNode b)
                    {
                        var dummy = new ListNode();
                        var tail = dummy;
                        while (a != null && b != null)
                        {
                            if (a.val <= b.val) { tail.next = a; a = a.next; }
                            else { tail.next = b; b = b.next; }
                            tail = tail.next;
                        }
                        tail.next = a ?? b;
                        return dummy.next;
                    }

                    ListNode MergeKByPairs(ListNode[] lists)
                    {
                        if (lists.Length == 0) return null;
                        var round = lists.ToList();
                        while (round.Count > 1)
                        {
                            var merged = new List<ListNode>();
                            for (int i = 0; i < round.Count; i += 2)
                                merged.Add(i + 1 < round.Count ? MergeTwo(round[i], round[i + 1]) : round[i]);
                            round = merged;   // half as many lists, each about twice as long
                        }
                        return round[0];
                    }

                    Console.WriteLine(Judge.Format(MergeKByPairs(new[] { BuildList(1, 4, 5), BuildList(1, 3, 4), BuildList(2, 6) })));   // [1,1,2,3,4,4,5,6]
                    """
                },
                new()
                {
                    Name = "Min-heap of the k fronts",
                    Kind = ApproachKind.Optimal,
                    TimeComplexity = "O(N log k)",
                    SpaceComplexity = "O(k)",
                    Intuition = "A priority queue holds each list's current front. Pop the smallest, link it after `tail`, and push its `next`; repeat until the queue is empty."
                }
            },
            SupportCode = ListNodeSupportCode,
            SolutionCode = """
            public class Solution
            {
                public ListNode MergeKLists(ListNode[] lists)
                {
                    var heap = new PriorityQueue<ListNode, int>();   // each list's front node, smallest first
                    foreach (var list in lists)
                        if (list != null) heap.Enqueue(list, list.val);

                    var dummy = new ListNode();
                    var tail = dummy;
                    while (heap.Count > 0)
                    {
                        var node = heap.Dequeue();                   // the smallest front of all
                        tail.next = node;
                        tail = node;
                        if (node.next != null) heap.Enqueue(node.next, node.next.val);   // its list's new front
                    }
                    return dummy.next;
                }
            }
            """,
            Tests = new List<BlindTest>
            {
                new() { Name = "Example 1", Input = "lists = [[1,4,5],[1,3,4],[2,6]]", Expected = "[1,1,2,3,4,4,5,6]", Call = "sol.MergeKLists(new[] { BuildList(1, 4, 5), BuildList(1, 3, 4), BuildList(2, 6) })" },
                new() { Name = "Example 2", Input = "lists = []", Expected = "[]", Call = "sol.MergeKLists(new ListNode[0])" },
                new() { Name = "Example 3", Input = "lists = [[]]", Expected = "[]", Call = "sol.MergeKLists(new[] { BuildList() })" }
            },
            ExtraTests = new List<BlindTest>
            {
                new() { Name = "One list", Input = "lists = [[1,2,3]]", Expected = "[1,2,3]", Call = "sol.MergeKLists(new[] { BuildList(1, 2, 3) })" },
                new() { Name = "Some lists are empty", Input = "lists = [[],[1],[],[0,2]]", Expected = "[0,1,2]", Call = "sol.MergeKLists(new[] { BuildList(), BuildList(1), BuildList(), BuildList(0, 2) })" },
                new() { Name = "Negative values", Input = "lists = [[-3,5],[-1]]", Expected = "[-3,-1,5]", Call = "sol.MergeKLists(new[] { BuildList(-3, 5), BuildList(-1) })" },
                new() { Name = "Many single nodes", Input = "lists = [[5],[4],[3],[2],[1]]", Expected = "[1,2,3,4,5]", Call = "sol.MergeKLists(new[] { BuildList(5), BuildList(4), BuildList(3), BuildList(2), BuildList(1) })" }
            },
            StressTestCode = """
            judge.Agree("Random lists vs sorting everything",
                random => Enumerable.Range(0, random.Next(0, 5))
                    .Select(_ => Enumerable.Range(0, random.Next(0, 5)).Select(_ => random.Next(-5, 6)).OrderBy(x => x).ToArray())
                    .ToArray(),
                lists => MergeKBySorting(lists.Select(values => BuildList(values)).ToArray()),
                lists => sol.MergeKLists(lists.Select(values => BuildList(values)).ToArray()));
            """,
            VisualizerKind = "LinkedList",
            VisualizationDescription = """
            Example 1, one row per chain, with the heap of current fronts underneath (smallest first). Each step pops
            the smallest front, links it after `tail` (the finished part is green) and pushes the next node of the same
            list, until the heap is empty and the result row holds every node in order.
            """,
            VisualizationCode = """
            var lists = new[] { BuildList(1, 4, 5), BuildList(1, 3, 4), BuildList(2, 6) };
            var tracker = LinkedListTracker.Create(lists[0], "23. Merge k Sorted Lists: a min-heap of the fronts", rows: true);
            var heap = new PriorityQueue<ListNode, int>();
            tracker.Watch(heap);

            foreach (var list in lists)
                if (list != null) heap.Enqueue(list, list.val);
            var dummy = new ListNode();
            var tail = dummy;
            tracker.Mark(dummy, "#334155");
            tracker.Step("Push the front of every list into the heap; the result starts at a dummy node", new { list0 = lists[0], list1 = lists[1], list2 = lists[2], tail });

            while (heap.Count > 0)
            {
                var node = heap.Dequeue();
                tail.next = node;
                tail = node;
                tracker.Mark(node);
                string pushed = node.next == null ? "its list is now empty" : $"push its next, {node.next.val}";
                if (node.next != null) heap.Enqueue(node.next, node.next.val);
                tracker.Step($"Pop the smallest front, {node.val}, and link it after tail; {pushed}", new { tail });
            }

            tracker.Step($"The heap is empty: dummy.next is {Judge.Format(dummy.next)}", new { head = dummy.next });
            Display.Visualizer(tracker);
            """
        }
    };
}
