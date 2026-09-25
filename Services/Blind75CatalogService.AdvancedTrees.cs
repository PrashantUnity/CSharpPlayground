using System.Collections.Generic;
using PdfEditorApp.Plugins.CSharpEditor.Models;

namespace PdfEditorApp.Plugins.CSharpEditor.Services;

public static partial class Blind75CatalogService
{
    private static IEnumerable<BlindProblemItem> GetAdvancedTreeProblems() => new List<BlindProblemItem>
    {
        new()
        {
            Id = "blind75_102_binary_tree_level_order_traversal",
            Number = 102,
            Title = "Binary Tree Level Order Traversal",
            Category = "Trees",
            Difficulty = ProblemDifficulty.Medium,
            AcceptanceRate = 68.0,
            IsPremium = false,
            Tags = new List<string> { "Tree", "BFS", "Binary Tree" },
            TimeComplexity = "O(n)",
            SpaceComplexity = "O(w) for the queue (w = the widest level)",
            DescriptionMarkdown = """
            Given the `root` of a binary tree, return the values of its nodes **level by level**: the root's level first, then the next level, and so on, each level from left to right.

            ### Example 1
            - **Input:** `root = [3,9,20,null,null,15,7]`
            - **Output:** `[[3],[9,20],[15,7]]`

            ### Example 2
            - **Input:** `root = [1]`
            - **Output:** `[[1]]`

            ### Example 3
            - **Input:** `root = []`
            - **Output:** `[]`

            ### Constraints
            - The number of nodes is in the range `[0, 2000]`.
            - `-1000 <= Node.val <= 1000`
            """,
            ThinkingProcessMarkdown = """
            **Restate it:** read the tree like a page, one row at a time, top to bottom, left to right.

            1. **Breadth-first search uses a queue:** take a node from the front, then put its children at the back. Nodes come out in exactly the order "all of level 0, then all of level 1, …".
            2. **Where does a level end?** At the start of each round, the queue holds exactly the nodes of one level (their children haven't been added yet). So read `count = queue.Count`, take exactly `count` nodes into this level's list, and queue their children for the next round.
            3. **Every node enters and leaves the queue once:** `O(n)` time; the queue never holds more than about two levels.
            4. **Walk Example 1:** queue `[3]` → level `[3]`, queue `[9,20]` → level `[9,20]`, queue `[15,7]` → level `[15,7]`, queue empty.

            **Another way:** depth-first search that carries the depth: the first time a depth is reached, start a new list for it; append each node's value to `levels[depth]`.

            **Pattern to remember:** "level by level" or "shortest number of steps" → BFS with a queue, and `queue.Count` at the start of a round marks the level boundary.

            **Common mistakes:** looping `for (i = 0; i < queue.Count; i++)` while the queue grows (read the count first); using a stack (that is DFS order); adding `null` children.
            """,
            Approaches = new List<ProblemApproachItem>
            {
                new()
                {
                    Name = "Depth-first, carrying the depth",
                    Kind = ApproachKind.Alternative,
                    TimeComplexity = "O(n)",
                    SpaceComplexity = "O(h) for the recursion",
                    Intuition = "Visit nodes left before right, passing the depth along. The first node at a new depth opens that level's list; each node appends its value to `levels[depth]`.",
                    Code = """
                    List<List<int>> LevelOrderByDepth(TreeNode root)
                    {
                        var levels = new List<List<int>>();
                        void Walk(TreeNode node, int depth)
                        {
                            if (node == null) return;
                            if (depth == levels.Count) levels.Add(new List<int>());   // first node seen on this level
                            levels[depth].Add(node.val);
                            Walk(node.left, depth + 1);
                            Walk(node.right, depth + 1);
                        }
                        Walk(root, 0);
                        return levels;
                    }

                    Console.WriteLine(Judge.Format(LevelOrderByDepth(BuildTree(3, 9, 20, null, null, 15, 7))));   // [[3],[9,20],[15,7]]
                    """
                },
                new()
                {
                    Name = "Breadth-first with a queue, one level per round",
                    Kind = ApproachKind.Optimal,
                    TimeComplexity = "O(n)",
                    SpaceComplexity = "O(w)",
                    Intuition = "Each round, the queue holds exactly one level: take that many nodes, record their values, and queue their children for the next round."
                }
            },
            SupportCode = TreeNodeSupportCode,
            SolutionCode = """
            public class Solution
            {
                public IList<IList<int>> LevelOrder(TreeNode root)
                {
                    var levels = new List<IList<int>>();
                    var queue = new Queue<TreeNode>();
                    if (root != null) queue.Enqueue(root);
                    while (queue.Count > 0)
                    {
                        var level = new List<int>();
                        for (int count = queue.Count; count > 0; count--)   // exactly the nodes of this level
                        {
                            var node = queue.Dequeue();
                            level.Add(node.val);
                            if (node.left != null) queue.Enqueue(node.left);
                            if (node.right != null) queue.Enqueue(node.right);
                        }
                        levels.Add(level);
                    }
                    return levels;
                }
            }
            """,
            Tests = new List<BlindTest>
            {
                new() { Name = "Example 1", Input = "root = [3,9,20,null,null,15,7]", Expected = "[[3],[9,20],[15,7]]", Call = "sol.LevelOrder(BuildTree(3, 9, 20, null, null, 15, 7))" },
                new() { Name = "Example 2", Input = "root = [1]", Expected = "[[1]]", Call = "sol.LevelOrder(BuildTree(1))" },
                new() { Name = "Example 3", Input = "root = []", Expected = "[]", Call = "sol.LevelOrder(BuildTree())" }
            },
            ExtraTests = new List<BlindTest>
            {
                new() { Name = "Only left children", Input = "root = [1,2,null,3]", Expected = "[[1],[2],[3]]", Call = "sol.LevelOrder(BuildTree(1, 2, null, 3))" },
                new() { Name = "A full tree", Input = "root = [1,2,3,4,5,6,7]", Expected = "[[1],[2,3],[4,5,6,7]]", Call = "sol.LevelOrder(BuildTree(1, 2, 3, 4, 5, 6, 7))" },
                new() { Name = "Gaps inside a level", Input = "root = [1,2,3,null,4,null,5]", Expected = "[[1],[2,3],[4,5]]", Call = "sol.LevelOrder(BuildTree(1, 2, 3, null, 4, null, 5))" },
                new() { Name = "Negative values", Input = "root = [-1,-2,-3]", Expected = "[[-1],[-2,-3]]", Call = "sol.LevelOrder(BuildTree(-1, -2, -3))" }
            },
            StressTestCode = """
            TreeNode RandomTree(Random random, int size)
            {
                if (size == 0) return null;
                int leftSize = random.Next(size);
                return new TreeNode(random.Next(-9, 10), RandomTree(random, leftSize), RandomTree(random, size - 1 - leftSize));
            }
            judge.Agree("Random trees vs depth-first",
                random => RandomTree(random, random.Next(0, 12)),
                root => (object)LevelOrderByDepth(root),
                root => sol.LevelOrder(root));
            """,
            VisualizerKind = "Tree",
            VisualizationDescription = """
            Example 1 with the queue and the answer shown underneath. Each round starts by reading how many nodes are in
            the queue (one level), takes exactly that many, labels them with their level and queues their children.
            """,
            VisualizationCode = """
            var root = BuildTree(3, 9, 20, null, null, 15, 7);
            var tracker = TreeTracker.Create(root, "102. Level Order Traversal: one queue, one level per round");
            var queue = new Queue<TreeNode>();
            var levels = new List<List<int>>();
            tracker.Watch(queue);
            tracker.Watch(levels);

            queue.Enqueue(root);
            tracker.Snapshot("Start with only the root in the queue");
            for (int depth = 0; queue.Count > 0; depth++)
            {
                var level = new List<int>();
                levels.Add(level);
                int count = queue.Count;
                tracker.Snapshot($"Level {depth}: the queue holds exactly this level's {count} node{(count == 1 ? "" : "s")}, so take {count}");
                for (; count > 0; count--)
                {
                    var node = queue.Dequeue();
                    level.Add(node.val);
                    var children = new[] { node.left, node.right }.Where(child => child != null).ToList();
                    foreach (var child in children) queue.Enqueue(child);
                    tracker.Visit(node, children.Count == 0
                            ? $"Take {node.val} into level {depth}; it has no children"
                            : $"Take {node.val} into level {depth} and queue its children {string.Join(" and ", children.Select(c => c.val))} for the next level",
                        subLabel: $"level {depth}");
                }
            }

            tracker.ClearCurrent();
            tracker.Snapshot($"The queue is empty: {Judge.Format(levels)}");
            Display.Visualizer(tracker);
            """
        },
        new()
        {
            Id = "blind75_98_validate_binary_search_tree",
            Number = 98,
            Title = "Validate Binary Search Tree",
            Category = "Trees",
            Difficulty = ProblemDifficulty.Medium,
            AcceptanceRate = 33.5,
            IsPremium = false,
            Tags = new List<string> { "Tree", "DFS", "BST", "Binary Tree" },
            TimeComplexity = "O(n)",
            SpaceComplexity = "O(h) for the recursion",
            DescriptionMarkdown = """
            Given the `root` of a binary tree, decide whether it is a valid **binary search tree** (BST):

            - every value in a node's **left** subtree is **strictly less** than the node's value,
            - every value in its **right** subtree is **strictly greater**, and
            - both subtrees are binary search trees too.

            ### Example 1
            - **Input:** `root = [2,1,3]`
            - **Output:** `true`

            ### Example 2
            - **Input:** `root = [5,1,4,null,null,3,6]`
            - **Output:** `false`
            - **Why:** 4 is in 5's right subtree, but 4 < 5.

            ### Constraints
            - The number of nodes is in the range `[1, 10^4]`.
            - `-2^31 <= Node.val <= 2^31 - 1`
            """,
            ThinkingProcessMarkdown = """
            **Restate it:** every node must be bigger than everything that ended up on its left and smaller than everything on its right, **all the way up**, not just compared with its parent.

            1. **The trap:** checking only `left.val < node.val < right.val` is not enough. In `[5,4,6,null,null,3,7]`, 3 is a fine left child of 6, but it sits in 5's **right** subtree, so it must be greater than 5.
            2. **Pass the allowed range down.** The root may hold anything: `(−∞, ∞)`. Going **left** from a node caps the range at that node's value; going **right** raises the floor to it. Every node must fall strictly inside its range.
            3. **Why `long`:** values can be `int.MinValue` or `int.MaxValue`, so the "no limit" bounds need a wider type (or nullable bounds).
            4. **Walk `[5,4,6,null,null,3,7]`:** 5 in `(−∞,∞)` ✓. 4 in `(−∞,5)` ✓. 6 in `(5,∞)` ✓. 3 in `(5,6)` ✗ → `false`.

            **Another way:** an in-order traversal of a BST visits values in increasing order, so the tree is valid exactly when every value is bigger than the previous one.

            **Pattern to remember:** when a rule involves ancestors, pass what the ancestors imply down the recursion (here a range; elsewhere a running sum or a path).

            **Common mistakes:** comparing only with the direct children; allowing equal values (`<=` instead of `<`); using `int.MinValue`/`int.MaxValue` as the open bounds.
            """,
            Approaches = new List<ProblemApproachItem>
            {
                new()
                {
                    Name = "Check each node against its whole subtrees",
                    Kind = ApproachKind.Naive,
                    TimeComplexity = "O(n²) worst case",
                    SpaceComplexity = "O(h)",
                    Intuition = "At every node, scan its entire left subtree for values that are too big and its right subtree for values that are too small, then recurse.",
                    BottleneckExplanation = "A node deep down is re-checked once for every ancestor, which is `O(n²)` for a long chain; passing a range down checks each node once.",
                    Code = """
                    bool IsValidByCheckingSubtrees(TreeNode root)
                    {
                        bool All(TreeNode node, Func<int, bool> ok) => node == null || (ok(node.val) && All(node.left, ok) && All(node.right, ok));
                        if (root == null) return true;
                        return All(root.left, v => v < root.val) && All(root.right, v => v > root.val)
                            && IsValidByCheckingSubtrees(root.left) && IsValidByCheckingSubtrees(root.right);
                    }

                    Console.WriteLine(Judge.Format(IsValidByCheckingSubtrees(BuildTree(5, 1, 4, null, null, 3, 6))));   // false
                    """
                },
                new()
                {
                    Name = "In-order traversal must strictly increase",
                    Kind = ApproachKind.Alternative,
                    TimeComplexity = "O(n)",
                    SpaceComplexity = "O(h)",
                    Intuition = "Visit left subtree, node, right subtree; in a BST that lists the values in sorted order, so any value not bigger than the previous one breaks it.",
                    Code = """
                    bool IsValidByInorder(TreeNode root)
                    {
                        long previous = long.MinValue;
                        bool Walk(TreeNode node)
                        {
                            if (node == null) return true;
                            if (!Walk(node.left) || node.val <= previous) return false;
                            previous = node.val;
                            return Walk(node.right);
                        }
                        return Walk(root);
                    }

                    Console.WriteLine(Judge.Format(IsValidByInorder(BuildTree(2, 1, 3))));   // true
                    """
                },
                new()
                {
                    Name = "Pass the allowed range down",
                    Kind = ApproachKind.Optimal,
                    TimeComplexity = "O(n)",
                    SpaceComplexity = "O(h)",
                    Intuition = "Each node must lie strictly between the bounds its ancestors set. Going left, the node's value becomes the upper bound; going right, the lower bound."
                }
            },
            SupportCode = TreeNodeSupportCode,
            SolutionCode = """
            public class Solution
            {
                public bool IsValidBST(TreeNode root) => IsValid(root, long.MinValue, long.MaxValue);

                // Every value in this subtree must lie strictly between low and high.
                private bool IsValid(TreeNode node, long low, long high)
                {
                    if (node == null) return true;
                    if (node.val <= low || node.val >= high) return false;
                    return IsValid(node.left, low, node.val) && IsValid(node.right, node.val, high);
                }
            }
            """,
            Tests = new List<BlindTest>
            {
                new() { Name = "Example 1", Input = "root = [2,1,3]", Expected = "true", Call = "sol.IsValidBST(BuildTree(2, 1, 3))" },
                new() { Name = "Example 2", Input = "root = [5,1,4,null,null,3,6]", Expected = "false", Call = "sol.IsValidBST(BuildTree(5, 1, 4, null, null, 3, 6))" }
            },
            ExtraTests = new List<BlindTest>
            {
                new() { Name = "A grandchild breaks it", Input = "root = [5,4,6,null,null,3,7]", Expected = "false", Call = "sol.IsValidBST(BuildTree(5, 4, 6, null, null, 3, 7))" },
                new() { Name = "Equal values are not allowed", Input = "root = [2,2,2]", Expected = "false", Call = "sol.IsValidBST(BuildTree(2, 2, 2))" },
                new() { Name = "The int extremes", Input = "root = [-2147483648,null,2147483647]", Expected = "true", Call = "sol.IsValidBST(BuildTree(int.MinValue, null, int.MaxValue))" },
                new() { Name = "A single node", Input = "root = [1]", Expected = "true", Call = "sol.IsValidBST(BuildTree(1))" },
                new() { Name = "A bigger valid tree", Input = "root = [8,4,12,2,6,10,14]", Expected = "true", Call = "sol.IsValidBST(BuildTree(8, 4, 12, 2, 6, 10, 14))" }
            },
            StressTestCode = """
            TreeNode RandomTree(Random random, int size)
            {
                if (size == 0) return null;
                int leftSize = random.Next(size);
                return new TreeNode(random.Next(0, 6), RandomTree(random, leftSize), RandomTree(random, size - 1 - leftSize));
            }
            TreeNode Insert(TreeNode node, int value)
            {
                if (node == null) return new TreeNode(value);
                if (value < node.val) node.left = Insert(node.left, value);
                else node.right = Insert(node.right, value);
                return node;
            }
            TreeNode RandomBst(Random random, int size)
            {
                TreeNode root = null;
                foreach (int value in Enumerable.Range(0, 20).OrderBy(_ => random.Next()).Take(size)) root = Insert(root, value);
                return root;
            }
            judge.Agree("Random trees and BSTs vs checking whole subtrees",
                random => random.Next(2) == 0 ? RandomBst(random, random.Next(1, 10)) : RandomTree(random, random.Next(1, 6)),
                root => IsValidByCheckingSubtrees(root),
                root => sol.IsValidBST(root));
            """,
            VisualizerKind = "Tree",
            VisualizationDescription = """
            `[5,4,6,null,null,3,7]`: every node's label is the range its ancestors allow. Going left lowers the ceiling,
            going right raises the floor. 3 is a fine child of 6, but it sits to the right of 5, so it must be bigger
            than 5; it turns red and the ancestor that set the broken bound is highlighted.
            """,
            VisualizationCode = """
            var root = BuildTree(5, 4, 6, null, null, 3, 7);
            var tracker = TreeTracker.Create(root, "98. Validate BST: each node must fit the range its ancestors allow");
            string Bound(long value) => value == long.MinValue ? "-∞" : value == long.MaxValue ? "∞" : value.ToString();

            // lowFrom / highFrom: the ancestors that set each bound, so a violation can name who it breaks with.
            bool Check(TreeNode node, long low, long high, TreeNode lowFrom, TreeNode highFrom)
            {
                if (node == null) return true;
                string range = $"({Bound(low)}, {Bound(high)})";
                if (node.val <= low || node.val >= high)
                {
                    bool tooSmall = node.val <= low;
                    var ancestor = tooSmall ? lowFrom : highFrom;
                    tracker.Visit(node, $"{node.val} must lie in {range}", subLabel: range);
                    tracker.Mark(ancestor, TreeNodeState.Candidate);
                    tracker.Highlight(node, TreeNodeState.Target, tooSmall
                        ? $"{node.val} is in the right subtree of {ancestor.val}, so it must be greater than {ancestor.val}. Not a valid BST"
                        : $"{node.val} is in the left subtree of {ancestor.val}, so it must be less than {ancestor.val}. Not a valid BST");
                    return false;
                }

                tracker.Visit(node, $"{node.val} lies in {range}: fine. Its left subtree must stay below {node.val}, its right subtree above it", subLabel: range);
                return Check(node.left, low, node.val, lowFrom, node) && Check(node.right, node.val, high, node, highFrom);
            }

            bool valid = Check(root, long.MinValue, long.MaxValue, null, null);
            if (valid)
            {
                tracker.ClearCurrent();
                tracker.Snapshot("Every node fits its range: a valid BST");
            }
            Display.Visualizer(tracker);
            """
        },
        new()
        {
            Id = "blind75_230_kth_smallest_element_in_a_bst",
            Number = 230,
            Title = "Kth Smallest Element in a BST",
            Category = "Trees",
            Difficulty = ProblemDifficulty.Medium,
            AcceptanceRate = 73.0,
            IsPremium = false,
            Tags = new List<string> { "Tree", "DFS", "BST", "Binary Tree" },
            TimeComplexity = "O(h + k)",
            SpaceComplexity = "O(h) for the stack",
            DescriptionMarkdown = """
            Given the `root` of a binary search tree and an integer `k`, return the `k`-th smallest value in the tree (counting from 1).

            ### Example 1
            - **Input:** `root = [3,1,4,null,2], k = 1`
            - **Output:** `1`

            ### Example 2
            - **Input:** `root = [5,3,6,2,4,null,null,1], k = 3`
            - **Output:** `3`
            - **Why:** in sorted order the values are 1, 2, 3, 4, 5, 6.

            ### Constraints
            - The tree has `n` nodes, with `1 <= k <= n <= 10^4`.
            - `0 <= Node.val <= 10^4`
            - **Follow-up:** if the BST changes often and you need the k-th smallest often, how would you speed it up?
            """,
            ThinkingProcessMarkdown = """
            **Restate it:** list the BST's values in sorted order and pick the `k`-th, ideally without listing all of them.

            1. **In-order traversal of a BST is sorted:** left subtree (all smaller), then the node, then the right subtree (all larger). So the `k`-th node an in-order walk visits is the answer.
            2. **Stop early with an explicit stack.** Push nodes while going as far **left** as possible (smaller values first). Pop one: it's the next value in sorted order. Then continue with its right subtree. After `k` pops you're done.
            3. **Cost:** you walk down one path (`h` nodes) and then pop `k` times: `O(h + k)`, instead of visiting all `n` nodes.
            4. **Walk Example 2 (k = 3):** push 5, 3, 2, 1. Pop 1 (#1), its right is empty. Pop 2 (#2). Pop 3 (#3) → **3**.
            5. **Follow-up:** store in every node the size of its left subtree. Then compare `k` with it at each node and go left or right, `O(h)` per query, updating the sizes on insert and delete.

            **Pattern to remember:** "sorted order of a BST" = in-order traversal; an explicit stack lets you pause and resume it.

            **Common mistakes:** counting in preorder or level order; forgetting that `k` is 1-based; collecting all `n` values when you could stop after `k`.
            """,
            Approaches = new List<ProblemApproachItem>
            {
                new()
                {
                    Name = "Collect every value and sort",
                    Kind = ApproachKind.Naive,
                    TimeComplexity = "O(n log n)",
                    SpaceComplexity = "O(n)",
                    Intuition = "Gather all the values with any traversal, sort them, and take position `k - 1`.",
                    BottleneckExplanation = "It ignores the BST order (sorting costs `O(n log n)`) and always reads all `n` nodes, even when `k` is 1.",
                    Code = """
                    int KthSmallestBySorting(TreeNode root, int k)
                    {
                        var values = new List<int>();
                        void Collect(TreeNode node)
                        {
                            if (node == null) return;
                            values.Add(node.val);
                            Collect(node.left);
                            Collect(node.right);
                        }
                        Collect(root);
                        values.Sort();
                        return values[k - 1];
                    }

                    Console.WriteLine(Judge.Format(KthSmallestBySorting(BuildTree(5, 3, 6, 2, 4, null, null, 1), 3)));   // 3
                    """
                },
                new()
                {
                    Name = "In-order with a stack, stop at the k-th",
                    Kind = ApproachKind.Optimal,
                    TimeComplexity = "O(h + k)",
                    SpaceComplexity = "O(h)",
                    Intuition = "Go left pushing nodes; each pop is the next smallest value; after popping, continue into the right subtree. The k-th pop is the answer."
                }
            },
            SupportCode = TreeNodeSupportCode,
            SolutionCode = """
            public class Solution
            {
                public int KthSmallest(TreeNode root, int k)
                {
                    var stack = new Stack<TreeNode>();
                    var node = root;
                    while (true)
                    {
                        while (node != null)          // go as far left as possible: smaller values come first
                        {
                            stack.Push(node);
                            node = node.left;
                        }
                        node = stack.Pop();           // the next value in sorted order
                        if (--k == 0) return node.val;
                        node = node.right;            // then the values between it and its parent
                    }
                }
            }
            """,
            Tests = new List<BlindTest>
            {
                new() { Name = "Example 1", Input = "root = [3,1,4,null,2], k = 1", Expected = "1", Call = "sol.KthSmallest(BuildTree(3, 1, 4, null, 2), 1)" },
                new() { Name = "Example 2", Input = "root = [5,3,6,2,4,null,null,1], k = 3", Expected = "3", Call = "sol.KthSmallest(BuildTree(5, 3, 6, 2, 4, null, null, 1), 3)" }
            },
            ExtraTests = new List<BlindTest>
            {
                new() { Name = "The largest", Input = "root = [5,3,6,2,4,null,null,1], k = 6", Expected = "6", Call = "sol.KthSmallest(BuildTree(5, 3, 6, 2, 4, null, null, 1), 6)" },
                new() { Name = "A single node", Input = "root = [7], k = 1", Expected = "7", Call = "sol.KthSmallest(BuildTree(7), 1)" },
                new() { Name = "A right chain", Input = "root = [1,null,2,null,3], k = 2", Expected = "2", Call = "sol.KthSmallest(BuildTree(1, null, 2, null, 3), 2)" },
                new() { Name = "Inside a right subtree", Input = "root = [3,1,4,null,2], k = 2", Expected = "2", Call = "sol.KthSmallest(BuildTree(3, 1, 4, null, 2), 2)" }
            },
            StressTestCode = """
            TreeNode Insert(TreeNode node, int value)
            {
                if (node == null) return new TreeNode(value);
                if (value < node.val) node.left = Insert(node.left, value);
                else node.right = Insert(node.right, value);
                return node;
            }
            judge.Agree("Random BSTs vs sorting",
                random =>
                {
                    int size = random.Next(1, 12);
                    TreeNode root = null;
                    foreach (int value in Enumerable.Range(0, 30).OrderBy(_ => random.Next()).Take(size)) root = Insert(root, value);
                    return (root, k: random.Next(1, size + 1));
                },
                input => KthSmallestBySorting(input.root, input.k),
                input => sol.KthSmallest(input.root, input.k));
            """,
            VisualizerKind = "Tree",
            VisualizationDescription = """
            Example 2 with `k = 3` and the stack underneath. Nodes are pushed while going left (smaller values first);
            every pop is the next value in sorted order and gets its rank as a label, until the 3rd pop, which is the answer.
            """,
            VisualizationCode = """
            var root = BuildTree(5, 3, 6, 2, 4, null, null, 1);
            int k = 3;
            var tracker = TreeTracker.Create(root, "230. Kth Smallest in a BST: in-order visits values in sorted order");
            var stack = new Stack<TreeNode>();
            tracker.Watch(stack);

            int rank = 0;
            var node = root;
            while (true)
            {
                while (node != null)
                {
                    stack.Push(node);
                    tracker.Visit(node, $"Push {node.val} and go left: everything smaller than {node.val} comes before it");
                    node = node.left;
                }

                node = stack.Pop();
                rank++;
                if (rank == k)
                {
                    tracker.Visit(node, $"Pop {node.val}: it is number {rank} in sorted order", subLabel: $"#{rank}");
                    tracker.Highlight(node, TreeNodeState.Matched, $"Pop number {rank} is the one we wanted (k = {k}), so the answer is {node.val}");
                    break;
                }
                tracker.Visit(node, $"Nothing smaller is left: pop {node.val}, number {rank} in sorted order. Next, the values in its right subtree", subLabel: $"#{rank}");
                node = node.right;
            }

            Display.Visualizer(tracker);
            """
        },
        new()
        {
            Id = "blind75_105_construct_binary_tree_from_preorder_and_inorder_traversal",
            Number = 105,
            Title = "Construct Binary Tree from Preorder and Inorder Traversal",
            Category = "Trees",
            Difficulty = ProblemDifficulty.Medium,
            AcceptanceRate = 64.5,
            IsPremium = false,
            Tags = new List<string> { "Array", "Hash Table", "Divide and Conquer", "Tree", "Binary Tree" },
            TimeComplexity = "O(n)",
            SpaceComplexity = "O(n)",
            DescriptionMarkdown = """
            Given two arrays, `preorder` and `inorder`, which are the preorder and inorder traversals of the same binary tree (with unique values), build the tree and return its root.

            - **Preorder** lists a node, then its left subtree, then its right subtree.
            - **Inorder** lists the left subtree, then the node, then the right subtree.

            ### Example 1
            - **Input:** `preorder = [3,9,20,15,7], inorder = [9,3,15,20,7]`
            - **Output:** `[3,9,20,null,null,15,7]`

            ### Example 2
            - **Input:** `preorder = [-1], inorder = [-1]`
            - **Output:** `[-1]`

            ### Constraints
            - `1 <= preorder.length <= 3000` and `inorder.length == preorder.length`
            - `-3000 <= preorder[i], inorder[i] <= 3000`, all values unique.
            - Both arrays are traversals of the same tree.
            """,
            ThinkingProcessMarkdown = """
            **Restate it:** each traversal alone is ambiguous, but together they pin the tree down. Which parts of the arrays belong to which subtree?

            1. **Preorder starts with the root:** `preorder[0] = 3` is the root.
            2. **Inorder splits around the root:** in `[9, 3, 15, 20, 7]`, everything left of 3 (`[9]`) is the left subtree and everything right of it (`[15, 20, 7]`) is the right subtree.
            3. **Recurse on each side.** The next unused preorder value is always the root of the next subtree we build, as long as we build left before right (that's the order preorder lists them). So keep one index into `preorder` that just moves forward.
            4. **Find positions fast:** a dictionary from value to inorder index makes each split `O(1)`, so the whole build is `O(n)`. Searching the array each time, or copying sub-arrays, makes it `O(n²)`.
            5. **Walk Example 1:** root 3 splits inorder into `[9]` and `[15,20,7]`. Next preorder value 9 is the left subtree (alone → leaf). Next is 20, root of `[15,20,7]`: 15 goes left, 7 right.

            **Pattern to remember:** preorder (or postorder) tells you **who** the root is; inorder tells you **how big** each side is. Divide and conquer on index ranges, not copies.

            **Common mistakes:** building the right subtree before the left (the preorder index gets out of step); off-by-one ranges; copying arrays at every level.
            """,
            Approaches = new List<ProblemApproachItem>
            {
                new()
                {
                    Name = "Search for the root and slice the arrays",
                    Kind = ApproachKind.Naive,
                    TimeComplexity = "O(n²)",
                    SpaceComplexity = "O(n²) in copies",
                    Intuition = "The first preorder value is the root; find it in inorder to learn the left subtree's size, then recurse on the matching slices of both arrays.",
                    BottleneckExplanation = "Every call scans `inorder` for the root and copies both slices, which adds up to `O(n²)` for a skewed tree.",
                    Code = """
                    TreeNode BuildBySlicing(int[] preorder, int[] inorder)
                    {
                        if (preorder.Length == 0) return null;
                        int mid = Array.IndexOf(inorder, preorder[0]);   // the left subtree has mid values
                        return new TreeNode(preorder[0],
                            BuildBySlicing(preorder[1..(mid + 1)], inorder[..mid]),
                            BuildBySlicing(preorder[(mid + 1)..], inorder[(mid + 1)..]));
                    }

                    Console.WriteLine(Judge.Format(BuildBySlicing(new[] { 3, 9, 20, 15, 7 }, new[] { 9, 3, 15, 20, 7 })));   // [3,9,20,null,null,15,7]
                    """
                },
                new()
                {
                    Name = "One preorder index and an inorder lookup",
                    Kind = ApproachKind.Optimal,
                    TimeComplexity = "O(n)",
                    SpaceComplexity = "O(n)",
                    Intuition = "Build the subtree for an inorder range: its root is the next preorder value, its position (from a dictionary) splits the range, and the left side is built before the right."
                }
            },
            SupportCode = TreeNodeClassCode,
            SolutionCode = """
            public class Solution
            {
                private int _next;                        // the next preorder value is the next subtree's root
                private Dictionary<int, int> _inorderIndex;

                public TreeNode BuildTree(int[] preorder, int[] inorder)
                {
                    _next = 0;
                    _inorderIndex = new Dictionary<int, int>();
                    for (int i = 0; i < inorder.Length; i++) _inorderIndex[inorder[i]] = i;
                    return Build(preorder, 0, inorder.Length - 1);
                }

                // Builds the subtree whose values are inorder[lo..hi].
                private TreeNode Build(int[] preorder, int lo, int hi)
                {
                    if (lo > hi) return null;
                    var root = new TreeNode(preorder[_next++]);
                    int mid = _inorderIndex[root.val];    // left of mid: left subtree; right of mid: right subtree
                    root.left = Build(preorder, lo, mid - 1);
                    root.right = Build(preorder, mid + 1, hi);
                    return root;
                }
            }
            """,
            Tests = new List<BlindTest>
            {
                new() { Name = "Example 1", Input = "preorder = [3,9,20,15,7], inorder = [9,3,15,20,7]", Expected = "[3,9,20,null,null,15,7]", Call = "sol.BuildTree(new[] { 3, 9, 20, 15, 7 }, new[] { 9, 3, 15, 20, 7 })" },
                new() { Name = "Example 2", Input = "preorder = [-1], inorder = [-1]", Expected = "[-1]", Call = "sol.BuildTree(new[] { -1 }, new[] { -1 })" }
            },
            ExtraTests = new List<BlindTest>
            {
                new() { Name = "A left chain", Input = "preorder = [1,2,3], inorder = [3,2,1]", Expected = "[1,2,null,3]", Call = "sol.BuildTree(new[] { 1, 2, 3 }, new[] { 3, 2, 1 })" },
                new() { Name = "A right chain", Input = "preorder = [1,2,3], inorder = [1,2,3]", Expected = "[1,null,2,null,3]", Call = "sol.BuildTree(new[] { 1, 2, 3 }, new[] { 1, 2, 3 })" },
                new() { Name = "A full tree", Input = "preorder = [4,2,1,3,6,5,7], inorder = [1,2,3,4,5,6,7]", Expected = "[4,2,6,1,3,5,7]", Call = "sol.BuildTree(new[] { 4, 2, 1, 3, 6, 5, 7 }, new[] { 1, 2, 3, 4, 5, 6, 7 })" },
                new() { Name = "Two nodes", Input = "preorder = [1,2], inorder = [2,1]", Expected = "[1,2]", Call = "sol.BuildTree(new[] { 1, 2 }, new[] { 2, 1 })" }
            },
            StressTestCode = """
            TreeNode RandomUniqueTree(Random random, int size)
            {
                var values = new Queue<int>(Enumerable.Range(-size, 3 * size).OrderBy(_ => random.Next()));
                TreeNode Grow(int count)
                {
                    if (count == 0) return null;
                    int leftCount = random.Next(count);
                    var node = new TreeNode(values.Dequeue());
                    node.left = Grow(leftCount);
                    node.right = Grow(count - 1 - leftCount);
                    return node;
                }
                return Grow(size);
            }
            List<int> Preorder(TreeNode node) => node == null ? new List<int>() : new[] { node.val }.Concat(Preorder(node.left)).Concat(Preorder(node.right)).ToList();
            List<int> Inorder(TreeNode node) => node == null ? new List<int>() : Inorder(node.left).Append(node.val).Concat(Inorder(node.right)).ToList();
            judge.Agree("Random trees vs slicing",
                random =>
                {
                    var tree = RandomUniqueTree(random, random.Next(1, 12));
                    return (preorder: Preorder(tree).ToArray(), inorder: Inorder(tree).ToArray());
                },
                input => BuildBySlicing(input.preorder, input.inorder),
                input => sol.BuildTree(input.preorder, input.inorder));
            """,
            VisualizerKind = "Tree",
            VisualizationDescription = """
            Example 1, built live. Each step takes the next preorder value as the root of the current inorder range
            (shown under the node), then uses its position in inorder to split the range into the left and right
            subtrees. Each new node is linked in right away so you can watch the tree grow.
            """,
            VisualizationCode = """
            int[] preorder = { 3, 9, 20, 15, 7 }, inorder = { 9, 3, 15, 20, 7 };
            var inorderIndex = new Dictionary<int, int>();
            for (int i = 0; i < inorder.Length; i++) inorderIndex[inorder[i]] = i;
            string Slice(int lo, int hi) => lo > hi ? "[]" : $"[{string.Join(",", inorder[lo..(hi + 1)])}]";

            int next = 0;
            TreeTracker tracker = null;
            TreeNode root = null;

            // The solution's recursion; each new node is linked to its parent at once, so it can be drawn right away.
            TreeNode Build(int lo, int hi, TreeNode parent, bool isLeft)
            {
                if (lo > hi) return null;
                var node = new TreeNode(preorder[next++]);
                int mid = inorderIndex[node.val];
                if (parent == null)
                {
                    root = node;
                    tracker = TreeTracker.Create(root, "105. Build a Tree from Preorder + Inorder");
                }
                else if (isLeft) parent.left = node;
                else parent.right = node;

                tracker.Sync();
                tracker.Visit(node, lo == hi
                        ? $"preorder[{next - 1}] = {node.val} is the only value of its inorder range {Slice(lo, hi)}: a leaf"
                        : $"preorder[{next - 1}] = {node.val} is the root of inorder {Slice(lo, hi)}. In inorder, {Slice(lo, mid - 1)} is left of it and {Slice(mid + 1, hi)} right of it",
                    subLabel: Slice(lo, hi));
                node.left = Build(lo, mid - 1, node, true);
                node.right = Build(mid + 1, hi, node, false);
                return node;
            }

            Build(0, inorder.Length - 1, null, false);
            tracker.ClearCurrent();
            tracker.Snapshot($"Every preorder value has been placed: {Judge.Format(root)}");
            Display.Visualizer(tracker);
            """
        },
        new()
        {
            Id = "blind75_124_binary_tree_maximum_path_sum",
            Number = 124,
            Title = "Binary Tree Maximum Path Sum",
            Category = "Trees",
            Difficulty = ProblemDifficulty.Hard,
            AcceptanceRate = 41.0,
            IsPremium = false,
            Tags = new List<string> { "Dynamic Programming", "Tree", "DFS", "Binary Tree" },
            TimeComplexity = "O(n)",
            SpaceComplexity = "O(h) for the recursion",
            DescriptionMarkdown = """
            A **path** in a binary tree is a sequence of nodes where each neighbouring pair is joined by an edge, and no node appears twice. A path doesn't have to pass through the root, and it has at least one node. Its **path sum** is the sum of its values.

            Given the `root`, return the **largest path sum** of any path.

            ### Example 1
            - **Input:** `root = [1,2,3]`
            - **Output:** `6`
            - **Why:** the path `2 → 1 → 3` sums to 6.

            ### Example 2
            - **Input:** `root = [-10,9,20,null,null,15,7]`
            - **Output:** `42`
            - **Why:** `15 → 20 → 7` sums to 42; including -10 would only lower it.

            ### Constraints
            - The number of nodes is in the range `[1, 3 * 10^4]`.
            - `-1000 <= Node.val <= 1000`
            """,
            ThinkingProcessMarkdown = """
            **Restate it:** among all paths (going up and then down, like an upside-down V), find the biggest total.

            1. **Every path has a highest node,** its "top". From the top the path goes down the left side, the right side, both, or neither.
            2. **Gain of a node:** the best sum of a path that **starts** at the node and goes **down one side**: `gain(node) = node.val + max(0, gain(left), gain(right))`. A negative branch is simply left out (that's the `0`).
            3. **Best path with this node as its top:** `node.val + max(0, gain(left)) + max(0, gain(right))`. Compute it at every node and keep the maximum.
            4. **But return only one side:** a parent can extend a path through this node only down **one** of its branches (a path can't fork), so the recursion returns `gain`, not the two-sided sum.
            5. **Walk Example 2:** gain(15) = 15, gain(7) = 7. At 20 the two-sided path is `20 + 15 + 7 = 42` (best so far) and it reports gain `20 + 15 = 35`. gain(9) = 9. At -10: `-10 + 9 + 35 = 34 < 42`. Answer **42**.

            **Pattern to remember:** tree DP where each node reports one thing upward (the one-sided gain) and updates a global answer with something else (the two-sided path). Diameter of a tree works the same way.

            **Common mistakes:** returning the two-sided sum to the parent; starting the best at 0 (an all-negative tree's answer is its largest single value); forgetting to drop negative gains.
            """,
            Approaches = new List<ProblemApproachItem>
            {
                new()
                {
                    Name = "Every node as the top, recomputing downward paths",
                    Kind = ApproachKind.Naive,
                    TimeComplexity = "O(n²) worst case",
                    SpaceComplexity = "O(h)",
                    Intuition = "For each node, compute from scratch the best downward path into its left and into its right subtree, and add them to its value.",
                    BottleneckExplanation = "The best downward path of a subtree is recomputed for every ancestor, so a long chain costs `O(n²)`; post-order computes each gain once.",
                    Code = """
                    int MaxPathSumBruteForce(TreeNode root)
                    {
                        int Down(TreeNode node) => node == null ? 0 : node.val + Math.Max(0, Math.Max(Down(node.left), Down(node.right)));
                        int best = int.MinValue;
                        void Each(TreeNode node)
                        {
                            if (node == null) return;
                            best = Math.Max(best, node.val + Math.Max(0, Down(node.left)) + Math.Max(0, Down(node.right)));   // node as the top
                            Each(node.left);
                            Each(node.right);
                        }
                        Each(root);
                        return best;
                    }

                    Console.WriteLine(Judge.Format(MaxPathSumBruteForce(BuildTree(-10, 9, 20, null, null, 15, 7))));   // 42
                    """
                },
                new()
                {
                    Name = "Post-order: report one-sided gains, record two-sided paths",
                    Kind = ApproachKind.Optimal,
                    TimeComplexity = "O(n)",
                    SpaceComplexity = "O(h)",
                    RecurrenceRelation = "gain(null) = 0\ngain(node) = node.val + max(0, gain(left), gain(right))\nbest = max over nodes of node.val + max(0, gain(left)) + max(0, gain(right))",
                    Intuition = "Each call returns the best downward gain and, on the way, updates the answer with the best path that turns at this node."
                }
            },
            SupportCode = TreeNodeSupportCode,
            SolutionCode = """
            public class Solution
            {
                private int _best;

                public int MaxPathSum(TreeNode root)
                {
                    _best = int.MinValue;
                    Gain(root);
                    return _best;
                }

                // The best sum of a path that starts at node and goes down one side.
                private int Gain(TreeNode node)
                {
                    if (node == null) return 0;
                    int left = Math.Max(0, Gain(node.left));        // a negative branch is better left out
                    int right = Math.Max(0, Gain(node.right));
                    _best = Math.Max(_best, node.val + left + right);   // the best path whose top is this node
                    return node.val + Math.Max(left, right);            // the parent can use only one side
                }
            }
            """,
            Tests = new List<BlindTest>
            {
                new() { Name = "Example 1", Input = "root = [1,2,3]", Expected = "6", Call = "sol.MaxPathSum(BuildTree(1, 2, 3))" },
                new() { Name = "Example 2", Input = "root = [-10,9,20,null,null,15,7]", Expected = "42", Call = "sol.MaxPathSum(BuildTree(-10, 9, 20, null, null, 15, 7))" }
            },
            ExtraTests = new List<BlindTest>
            {
                new() { Name = "One negative node", Input = "root = [-3]", Expected = "-3", Call = "sol.MaxPathSum(BuildTree(-3))" },
                new() { Name = "All negative", Input = "root = [-2,-1]", Expected = "-1", Call = "sol.MaxPathSum(BuildTree(-2, -1))" },
                new() { Name = "Skip a negative branch", Input = "root = [1,-2,3]", Expected = "4", Call = "sol.MaxPathSum(BuildTree(1, -2, 3))" },
                new() { Name = "A long winding path", Input = "root = [5,4,8,11,null,13,4,7,2,null,null,null,1]", Expected = "48", Call = "sol.MaxPathSum(BuildTree(5, 4, 8, 11, null, 13, 4, 7, 2, null, null, null, 1))" }
            },
            StressTestCode = """
            TreeNode RandomTree(Random random, int size)
            {
                if (size == 0) return null;
                int leftSize = random.Next(size);
                return new TreeNode(random.Next(-9, 10), RandomTree(random, leftSize), RandomTree(random, size - 1 - leftSize));
            }
            judge.Agree("Random trees vs recomputing every path",
                random => RandomTree(random, random.Next(1, 12)),
                root => MaxPathSumBruteForce(root),
                root => sol.MaxPathSum(root));
            """,
            VisualizerKind = "Tree",
            VisualizationDescription = """
            Example 2. On the way back up, each node's label shows the one-sided gain it reports to its parent, and the
            step text shows the two-sided path that turns at it; `best` keeps the record. At the end the winning path,
            15 → 20 → 7, is marked.
            """,
            VisualizationCode = """
            var root = BuildTree(-10, 9, 20, null, null, 15, 7);
            var tracker = TreeTracker.Create(root, "124. Maximum Path Sum: gains go up, the best turn is recorded");
            int best = int.MinValue;
            TreeNode bestTop = null;
            var gains = new Dictionary<TreeNode, int>();
            tracker.Watch(() => best);

            int Gain(TreeNode node)
            {
                if (node == null) return 0;
                tracker.Visit(node, $"Visit {node.val}: first find the best gains of its two subtrees");
                int left = Math.Max(0, Gain(node.left));
                int right = Math.Max(0, Gain(node.right));
                int through = node.val + left + right;
                bool record = through > best;
                if (record) (best, bestTop) = (through, node);
                int gain = node.val + Math.Max(left, right);
                gains[node] = gain;
                tracker.Visit(node, $"At {node.val}: the path turning here sums {node.val} + {left} + {right} = {through}{(record ? ", a new best" : "")}. It reports gain {node.val} + {Math.Max(left, right)} = {gain} to its parent",
                    subLabel: $"gain {gain}");
                return gain;
            }

            Gain(root);

            // The winning path: its top, then down each side for as long as the gain helps.
            int G(TreeNode node) => node == null ? 0 : gains[node];
            var path = new List<TreeNode> { bestTop };
            foreach (var side in new[] { bestTop.left, bestTop.right })
            {
                for (var node = side; node != null && G(node) > 0; node = G(node.left) >= G(node.right) ? node.left : node.right) path.Add(node);
            }
            tracker.ClearCurrent();
            tracker.MarkPath(path, $"The best path turns at {bestTop.val}: {string.Join(" + ", path.Select(n => n.val))} = {best}");
            Display.Visualizer(tracker);
            """
        },
        new()
        {
            Id = "blind75_297_serialize_and_deserialize_binary_tree",
            Number = 297,
            Title = "Serialize and Deserialize Binary Tree",
            Category = "Trees",
            Difficulty = ProblemDifficulty.Hard,
            AcceptanceRate = 57.5,
            IsPremium = false,
            Tags = new List<string> { "String", "Tree", "DFS", "BFS", "Design", "Binary Tree" },
            TimeComplexity = "O(n)",
            SpaceComplexity = "O(n)",
            DescriptionMarkdown = """
            Design a `Codec` with two methods:

            - `serialize(root)` turns a binary tree into a string, and
            - `deserialize(data)` turns that string back into **the same tree**.

            You may choose any string format, as long as the round trip `deserialize(serialize(root))` gives back an identical tree.

            ### Example 1
            - **Input:** `root = [1,2,3,null,null,4,5]`
            - **Output:** `[1,2,3,null,null,4,5]`
            - **Why:** after serializing and deserializing, the tree is unchanged.

            ### Example 2
            - **Input:** `root = []`
            - **Output:** `[]`

            ### Constraints
            - The number of nodes is in the range `[0, 10^4]`.
            - `-1000 <= Node.val <= 1000`
            """,
            ThinkingProcessMarkdown = """
            **Restate it:** write a tree down in a way that can be read back unambiguously.

            1. **Values alone aren't enough.** The preorder `1, 2` could be 2 as a left child or a right child of 1. What's missing is **where the empty spots are**.
            2. **Write the empty children too.** Preorder with a marker `#` for every `null`: `[1,2,3,null,null,4,5]` becomes `1,2,#,#,3,4,#,#,5,#,#`. Now every shape has its own string.
            3. **Reading it back is the same recursion:** take the next token; `#` means "no node here"; a number means "make a node, then read its **left** subtree, then its **right** subtree". The tokens are consumed in exactly the order they were written.
            4. **Both directions are `O(n)`:** each node and each null marker is written and read once.
            5. **Another format:** LeetCode's own level order (`1,2,3,null,null,4,5`), written with a queue and read back by filling children level by level.

            **Pattern to remember:** to make a traversal reversible, record the structure (null markers) along with the values. A recursive reader that mirrors the writer consumes the stream in the same order.

            **Common mistakes:** leaving out null markers; joining values without a separator (`12` vs `1,2`); reading the right subtree before the left.
            """,
            Approaches = new List<ProblemApproachItem>
            {
                new()
                {
                    Name = "Level order, the way LeetCode prints trees",
                    Kind = ApproachKind.Alternative,
                    TimeComplexity = "O(n)",
                    SpaceComplexity = "O(n)",
                    Intuition = "Write nodes breadth-first with `null` for missing children; to read it back, hand out the values two at a time as the children of the nodes in a queue.",
                    Code = """
                    string SerializeLevels(TreeNode root)
                    {
                        var parts = new List<string>();
                        var queue = new Queue<TreeNode>();
                        queue.Enqueue(root);
                        while (queue.Count > 0)
                        {
                            var node = queue.Dequeue();
                            parts.Add(node == null ? "null" : node.val.ToString());
                            if (node != null) { queue.Enqueue(node.left); queue.Enqueue(node.right); }
                        }
                        return string.Join(",", parts);
                    }

                    TreeNode DeserializeLevels(string data)
                    {
                        var tokens = data.Split(',');
                        if (tokens[0] == "null") return null;
                        var root = new TreeNode(int.Parse(tokens[0]));
                        var parents = new Queue<TreeNode>();
                        parents.Enqueue(root);
                        for (int i = 1; i < tokens.Length; i += 2)
                        {
                            var parent = parents.Dequeue();
                            if (tokens[i] != "null") parents.Enqueue(parent.left = new TreeNode(int.Parse(tokens[i])));
                            if (i + 1 < tokens.Length && tokens[i + 1] != "null") parents.Enqueue(parent.right = new TreeNode(int.Parse(tokens[i + 1])));
                        }
                        return root;
                    }

                    Console.WriteLine(SerializeLevels(BuildTree(1, 2, 3, null, null, 4, 5)));   // 1,2,3,null,null,4,5,null,null,null,null
                    """
                },
                new()
                {
                    Name = "Preorder with null markers",
                    Kind = ApproachKind.Optimal,
                    TimeComplexity = "O(n)",
                    SpaceComplexity = "O(n)",
                    Intuition = "Write a node's value, then its left subtree, then its right subtree, with `#` for every missing child. Read it back with the same recursion, consuming tokens in order."
                }
            },
            SupportCode = TreeNodeSupportCode,
            SolutionCode = """
            public class Codec
            {
                // Preorder with "#" for every missing child: [1,2,3,null,null,4,5] -> 1,2,#,#,3,4,#,#,5,#,#
                public string serialize(TreeNode root)
                {
                    var parts = new List<string>();
                    void Write(TreeNode node)
                    {
                        if (node == null) { parts.Add("#"); return; }
                        parts.Add(node.val.ToString());
                        Write(node.left);
                        Write(node.right);
                    }
                    Write(root);
                    return string.Join(",", parts);
                }

                // Reads the tokens back in the same order: a value makes a node, then its left and right subtrees follow.
                public TreeNode deserialize(string data)
                {
                    var tokens = new Queue<string>(data.Split(','));
                    TreeNode Read()
                    {
                        string token = tokens.Dequeue();
                        if (token == "#") return null;
                        var node = new TreeNode(int.Parse(token));
                        node.left = Read();
                        node.right = Read();
                        return node;
                    }
                    return Read();
                }
            }
            """,
            TestSetupCode = """
            var codec = new Codec();
            TreeNode RoundTrip(TreeNode root) => codec.deserialize(codec.serialize(root));
            """,
            Tests = new List<BlindTest>
            {
                new() { Name = "Example 1", Input = "root = [1,2,3,null,null,4,5]", Expected = "[1,2,3,null,null,4,5]", Call = "RoundTrip(BuildTree(1, 2, 3, null, null, 4, 5))" },
                new() { Name = "Example 2", Input = "root = []", Expected = "[]", Call = "RoundTrip(BuildTree())" }
            },
            ExtraTests = new List<BlindTest>
            {
                new() { Name = "What the string looks like", Input = "serialize([1,2,3,null,null,4,5])", Expected = "\"1,2,#,#,3,4,#,#,5,#,#\"", Call = "codec.serialize(BuildTree(1, 2, 3, null, null, 4, 5))" },
                new() { Name = "A single node", Input = "root = [0]", Expected = "[0]", Call = "RoundTrip(BuildTree(0))" },
                new() { Name = "Negative values", Input = "root = [-1,null,-2]", Expected = "[-1,null,-2]", Call = "RoundTrip(BuildTree(-1, null, -2))" },
                new() { Name = "A left chain", Input = "root = [1,2,null,3]", Expected = "[1,2,null,3]", Call = "RoundTrip(BuildTree(1, 2, null, 3))" }
            },
            StressTestCode = """
            TreeNode RandomTree(Random random, int size)
            {
                if (size == 0) return null;
                int leftSize = random.Next(size);
                return new TreeNode(random.Next(-99, 100), RandomTree(random, leftSize), RandomTree(random, size - 1 - leftSize));
            }
            judge.Agree("Random trees: preorder round trip vs level-order round trip",
                random => RandomTree(random, random.Next(0, 14)),
                root => DeserializeLevels(SerializeLevels(root)),
                root => RoundTrip(root));
            """,
            VisualizerKind = "Tree",
            VisualizationDescription = """
            Two players. First `serialize` walks `[1,2,3,null,null,4,5]` in preorder and `data` grows one token at a time,
            with `#` for every missing child. Then `deserialize` reads those tokens back in the same order and the tree
            grows again node by node, each `#` closing off an empty spot.
            """,
            VisualizationCode = """
            // 1. Serialize: preorder, writing "#" for every missing child
            var original = BuildTree(1, 2, 3, null, null, 4, 5);
            var writer = TreeTracker.Create(original, "297. serialize: preorder with # for empty children");
            var parts = new List<string>();
            string data = "";
            writer.Watch(() => data);

            void Write(TreeNode node, TreeNode parent, string side)
            {
                if (node == null)
                {
                    parts.Add("#");
                    data = string.Join(",", parts);
                    writer.Visit(parent, $"{parent.val} has no {side} child: write #");
                    return;
                }
                parts.Add(node.val.ToString());
                data = string.Join(",", parts);
                writer.Visit(node, $"Write {node.val}, then its left subtree, then its right subtree");
                Write(node.left, node, "left");
                Write(node.right, node, "right");
            }
            Write(original, null, "");
            writer.ClearCurrent();
            writer.Snapshot($"serialize returns \"{data}\"");
            Display.Visualizer(writer);

            // 2. Deserialize: read the tokens back in the same order (each new node is linked in at once to draw it)
            var tokens = new Queue<string>(data.Split(','));
            string rest = data;
            TreeTracker tracker = null;
            TreeNode rebuilt = null;

            TreeNode Read(TreeNode parent, bool isLeft)
            {
                string token = tokens.Dequeue();
                rest = string.Join(",", tokens);
                if (token == "#")
                {
                    tracker.Visit(parent, $"Read #: {parent.val}'s {(isLeft ? "left" : "right")} child is empty");
                    return null;
                }

                var node = new TreeNode(int.Parse(token));
                if (parent == null)
                {
                    rebuilt = node;
                    tracker = TreeTracker.Create(rebuilt, "297. deserialize: read the tokens back in the same order");
                    tracker.Watch(() => rest);
                }
                else if (isLeft) parent.left = node;
                else parent.right = node;
                tracker.Sync();
                tracker.Visit(node, $"Read {token}: a new node; the next tokens describe its left subtree, then its right");

                node.left = Read(node, true);
                node.right = Read(node, false);
                return node;
            }
            Read(null, true);
            tracker.ClearCurrent();
            tracker.Snapshot($"All tokens are used: the rebuilt tree is {Judge.Format(rebuilt)}, the same as the original");
            Display.Visualizer(tracker);
            """
        }
    };
}
