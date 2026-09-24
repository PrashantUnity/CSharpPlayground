using System.Collections.Generic;
using PdfEditorApp.Plugins.CSharpEditor.Models;

namespace PdfEditorApp.Plugins.CSharpEditor.Services;

public static partial class Blind75CurriculumEnhancer
{
    private static void EnhanceLinkedListsAndTrees(BlindProblemItem p)
    {
        switch (p.Number)
        {
            case 206: // Reverse Linked List
                p.GetType().GetProperty(nameof(p.ThinkingProcessMarkdown))?.SetValue(p, """
                    ### 🧠 How to Think: Reverse Linked List
                    1. **Pointer Inversion**: In `A -> B`, you want `B -> A`.
                    2. **Loss of Forward Pointer**: The moment you point `curr.next = prev`, you lose access to the remaining list!
                    3. **The 3-Pointer Dance**: Save `next = curr.next` BEFORE rewiring `curr.next = prev`. Then slide `prev = curr` and `curr = next`.
                    """);
                p.Approaches.Clear();
                p.Approaches.Add(new ProblemApproachItem
                {
                    Name = "Recursive Reversal",
                    Kind = ApproachKind.Naive,
                    TimeComplexity = "O(N)",
                    SpaceComplexity = "O(N) (Call Stack)",
                    Intuition = "Recursively reverse the sublist starting at head.next, then point head.next.next = head.",
                    BottleneckExplanation = "Uses implicit stack frames proportional to list length; risk of stack overflow on large inputs.",
                    Code = """
                    public ListNode ReverseListRecursive(ListNode head)
                    {
                        if (head == null || head.next == null) return head;
                        var newHead = ReverseListRecursive(head.next);
                        head.next.next = head;
                        head.next = null;
                        return newHead;
                    }
                    """
                });
                p.Approaches.Add(new ProblemApproachItem
                {
                    Name = "Iterative 3-Pointer (Optimal)",
                    Kind = ApproachKind.Optimal,
                    TimeComplexity = "O(N)",
                    SpaceComplexity = "O(1)",
                    Intuition = "Iterate forward maintaining prev, curr, and next pointers.",
                    Code = """
                    public ListNode ReverseList(ListNode head)
                    {
                        ListNode prev = null;
                        var curr = head;
                        while (curr != null)
                        {
                            var next = curr.next;
                            curr.next = prev;
                            prev = curr;
                            curr = next;
                        }
                        return prev;
                    }
                    """
                });
                p.GetType().GetProperty(nameof(p.VisualizationCode))?.SetValue(p, """
                    public class ListNode
                    {
                        public int val;
                        public ListNode? next;
                        public ListNode(int val, ListNode? next = null) { this.val = val; this.next = next; }
                    }

                    var head = new ListNode(1, new ListNode(2, new ListNode(3, new ListNode(4, new ListNode(5)))));

                    // 1. Wrap in LinkedListTracker
                    var tracker = LinkedListTracker.Create(head, title: "206. Reverse Linked List");

                    // 2. User's own algorithm loop:
                    ListNode? prev = null;
                    ListNode? curr = head;
                    tracker.Step("Start: prev is null, curr is the head", new { prev, curr });

                    while (curr != null)
                    {
                        ListNode? next = curr.next;
                        tracker.Step($"Save next = {next?.val.ToString() ?? "null"}", new { prev, curr, next });

                        curr.next = prev;
                        tracker.Step($"Point {curr.val} back at {prev?.val.ToString() ?? "null"}", new { prev, curr, next });

                        prev = curr;
                        curr = next;
                        tracker.Step("Advance prev and curr", new { prev, curr });
                    }

                    tracker.Step("Done: prev is the new head", new { head = prev });
                    Display.Visualizer(tracker);
                    Console.WriteLine("Use playback controls to scrub through the linked list reversal!");
                    """);
                break;

            case 141: // Linked List Cycle
                p.GetType().GetProperty(nameof(p.ThinkingProcessMarkdown))?.SetValue(p, """
                    ### 🧠 How to Think: Linked List Cycle
                    1. **Visited Tracking**: If you visit a node you have seen before, a cycle exists.
                    2. **Space vs Speed Trade-off**: Storing nodes in a `HashSet<ListNode>` costs $O(N)$ extra memory.
                    3. **Floyd's Cycle-Finding Algorithm (Tortoise & Hare)**: Run two pointers at different speeds (`slow` moves 1 step, `fast` moves 2 steps). If there is a cycle, the fast pointer will eventually lap and collide with the slow pointer!
                    """);
                p.Approaches.Clear();
                p.Approaches.Add(new ProblemApproachItem
                {
                    Name = "HashSet Visited Tracking",
                    Kind = ApproachKind.Naive,
                    TimeComplexity = "O(N)",
                    SpaceComplexity = "O(N)",
                    Intuition = "Add every node to a hash set as you traverse. If already present, return true.",
                    BottleneckExplanation = "Requires allocating memory for every node reference in the list.",
                    Code = """
                    public bool HasCycleHashSet(ListNode head)
                    {
                        var visited = new HashSet<ListNode>();
                        var curr = head;
                        while (curr != null)
                        {
                            if (!visited.Add(curr)) return true;
                            curr = curr.next;
                        }
                        return false;
                    }
                    """
                });
                p.Approaches.Add(new ProblemApproachItem
                {
                    Name = "Floyd's Tortoise & Hare (Optimal)",
                    Kind = ApproachKind.Optimal,
                    TimeComplexity = "O(N)",
                    SpaceComplexity = "O(1)",
                    Intuition = "Fast and slow pointers. Fast moves 2x speed; collision confirms a cycle.",
                    Code = """
                    public bool HasCycle(ListNode head)
                    {
                        if (head == null || head.next == null) return false;
                        var slow = head;
                        var fast = head.next;
                        while (slow != fast)
                        {
                            if (fast == null || fast.next == null) return false;
                            slow = slow.next;
                            fast = fast.next.next;
                        }
                        return true;
                    }
                    """
                });
                break;

            case 226: // Invert Binary Tree
                p.GetType().GetProperty(nameof(p.ThinkingProcessMarkdown))?.SetValue(p, """
                    ### 🧠 How to Think: Invert Binary Tree
                    1. **Local Operation**: Inverting a tree means swapping the left and right subtrees at every single node.
                    2. **Recursive Decomposition**: First invert the left subtree, then invert the right subtree, then swap `root.left` and `root.right`.
                    3. **Base Case**: If `root == null`, return `null`.
                    """);
                p.Approaches.Clear();
                p.Approaches.Add(new ProblemApproachItem
                {
                    Name = "Recursive DFS (Optimal Post-Order)",
                    Kind = ApproachKind.Optimal,
                    TimeComplexity = "O(N)",
                    SpaceComplexity = "O(H) (Tree Height)",
                    Intuition = "Depth-first traversal recursively swapping child pointers.",
                    Code = """
                    public TreeNode InvertTree(TreeNode root)
                    {
                        if (root == null) return null;
                        var temp = root.left;
                        root.left = InvertTree(root.right);
                        root.right = InvertTree(temp);
                        return root;
                    }
                    """
                });
                p.Approaches.Add(new ProblemApproachItem
                {
                    Name = "Iterative BFS (Queue Level-Order)",
                    Kind = ApproachKind.Naive,
                    TimeComplexity = "O(N)",
                    SpaceComplexity = "O(W) (Max Width)",
                    Intuition = "Queue-based breadth-first traversal swapping left and right at each popped node.",
                    Code = """
                    public TreeNode InvertTreeBFS(TreeNode root)
                    {
                        if (root == null) return null;
                        var queue = new Queue<TreeNode>();
                        queue.Enqueue(root);
                        while (queue.Count > 0)
                        {
                            var curr = queue.Dequeue();
                            var temp = curr.left;
                            curr.left = curr.right;
                            curr.right = temp;
                            if (curr.left != null) queue.Enqueue(curr.left);
                            if (curr.right != null) queue.Enqueue(curr.right);
                        }
                        return root;
                    }
                    """
                });
                p.GetType().GetProperty(nameof(p.VisualizationCode))?.SetValue(p, """
                    // 1. Wrap in TreeTracker (automatically parses serialized tree)
                    var tracker = TreeTracker.Create("[4, 2, 7, 1, 3, 6, 9]", title: "226. Invert Binary Tree");

                    // 2. User's own algorithm loop:
                    void InvertTreeVis(TreeNodeData? node)
                    {
                        if (node == null) return;
                        tracker.Visit(node, $"Inspecting [{node.DisplayValue}] to swap its subtrees", pointer: "curr");
                        tracker.SwapChildren(node, $"Swapped children of [{node.DisplayValue}]");
                        InvertTreeVis(node.Left);
                        InvertTreeVis(node.Right);
                        tracker.Highlight(node, TreeNodeState.Matched, $"Subtree [{node.DisplayValue}] inverted");
                    }
                    InvertTreeVis(tracker.Root);
                    tracker.ClearPointers();
                    tracker.Snapshot("Tree Inversion Complete!");

                    Display.Visualizer(tracker);
                    Console.WriteLine("Use playback controls to scrub through tree inversion!");
                    """);
                break;

            case 98: // Validate Binary Search Tree
                p.GetType().GetProperty(nameof(p.ThinkingProcessMarkdown))?.SetValue(p, """
                    ### 🧠 How to Think: Validate BST
                    1. **The Common Pitfall**: It is NOT enough that `node.left.val < node.val` and `node.right.val > node.val`!
                       - Every node in the left subtree must be less than ALL its ancestors!
                    2. **Range Propagation**: Pass down valid bounds `[min, max]`. When going left, update `max = node.val`. When going right, update `min = node.val`.
                    """);
                p.Approaches.Clear();
                p.Approaches.Add(new ProblemApproachItem
                {
                    Name = "In-Order List Validation",
                    Kind = ApproachKind.Naive,
                    TimeComplexity = "O(N)",
                    SpaceComplexity = "O(N)",
                    Intuition = "Collect in-order traversal into a list; verify it is strictly monotonically increasing.",
                    BottleneckExplanation = "Requires allocating a list storing all N elements.",
                    Code = """
                    public bool IsValidBSTInorder(TreeNode root)
                    {
                        var list = new List<int>();
                        void Inorder(TreeNode node)
                        {
                            if (node == null) return;
                            Inorder(node.left);
                            list.Add(node.val);
                            Inorder(node.right);
                        }
                        Inorder(root);
                        for (int i = 1; i < list.Count; i++)
                            if (list[i] <= list[i - 1]) return false;
                        return true;
                    }
                    """
                });
                p.Approaches.Add(new ProblemApproachItem
                {
                    Name = "Recursive DFS with Range Bounds (Optimal)",
                    Kind = ApproachKind.Optimal,
                    TimeComplexity = "O(N)",
                    SpaceComplexity = "O(H)",
                    Intuition = "Pass long? min and long? max down the recursion tree.",
                    Code = """
                    public bool IsValidBST(TreeNode root)
                    {
                        return Validate(root, null, null);
                    }

                    private bool Validate(TreeNode node, long? min, long? max)
                    {
                        if (node == null) return true;
                        if ((min.HasValue && node.val <= min.Value) || (max.HasValue && node.val >= max.Value))
                            return false;
                        return Validate(node.left, min, node.val) && Validate(node.right, node.val, max);
                    }
                    """
                });
                p.GetType().GetProperty(nameof(p.VisualizationCode))?.SetValue(p, """
                    // 1. Wrap in TreeTracker (automatically parses serialized tree)
                    var tracker = TreeTracker.Create("[5, 1, 4, null, null, 3, 6]", title: "98. Validate BST");

                    // 2. User's own algorithm loop:
                    bool Validate(TreeNodeData? node, long min, long max)
                    {
                        if (node == null) return true;
                        long val = long.Parse(node.DisplayValue);
                        string range = $"({min}, {max})";
                        tracker.Visit(node, $"Inspecting [{node.DisplayValue}], valid range: {range}", subLabel: range, pointer: "curr");
                        if (val <= min || val >= max)
                        {
                            tracker.Highlight(node, TreeNodeState.Target, $"Violation! Node [{node.DisplayValue}] outside range {range}");
                            return false;
                        }
                        tracker.Highlight(node, TreeNodeState.Matched, $"Node [{node.DisplayValue}] within valid range");
                        return Validate(node.Left, min, val) && Validate(node.Right, val, max);
                    }
                    bool isValid = Validate(tracker.Root, long.MinValue, long.MaxValue);
                    tracker.ClearPointers();
                    tracker.Snapshot(isValid ? "Valid BST!" : "Invalid BST detected!");

                    Display.Visualizer(tracker);
                    Console.WriteLine($"BST Validation Complete! Is Valid = {isValid}");
                    """);
                break;
        }
    }
}
