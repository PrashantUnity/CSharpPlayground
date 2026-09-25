using System.Collections.Generic;
using PdfEditorApp.Plugins.CSharpEditor.Models;

namespace PdfEditorApp.Plugins.CSharpEditor.Services;

public static partial class Blind75CatalogService
{
    // Every tree problem runs on LeetCode's TreeNode; BuildTree reads the level-order arrays LeetCode prints.
    private const string TreeNodeClassCode = """
        public class TreeNode
        {
            public int val;
            public TreeNode left;
            public TreeNode right;
            public TreeNode(int val = 0, TreeNode left = null, TreeNode right = null) { this.val = val; this.left = left; this.right = right; }
        }
        """;

    private const string TreeNodeSupportCode = TreeNodeClassCode + """


        // BuildTree(3, 9, 20, null, null, 15, 7) reads LeetCode's level order: null marks a missing child.
        TreeNode BuildTree(params int?[] values)
        {
            if (values.Length == 0 || values[0] == null) return null;
            var root = new TreeNode(values[0].Value);
            var parents = new Queue<TreeNode>();
            parents.Enqueue(root);
            for (int i = 1; i < values.Length; i += 2)
            {
                var parent = parents.Dequeue();
                if (values[i] != null) parents.Enqueue(parent.left = new TreeNode(values[i].Value));
                if (i + 1 < values.Length && values[i + 1] != null) parents.Enqueue(parent.right = new TreeNode(values[i + 1].Value));
            }
            return root;
        }
        """;

    // LeetCode hands LCA problems the nodes themselves; Find looks a node up by its value.
    private const string FindNodeSupportCode = """


        // Find(root, 5) is the node holding 5 (values are unique in these problems).
        TreeNode Find(TreeNode node, int value) =>
            node == null || node.val == value ? node : Find(node.left, value) ?? Find(node.right, value);
        """;

    private static IEnumerable<BlindProblemItem> GetTreeProblems()
    {
        var list = new List<BlindProblemItem>(GetBasicTreeProblems());
        list.AddRange(GetAdvancedTreeProblems());
        return list;
    }

    private static IEnumerable<BlindProblemItem> GetBasicTreeProblems() => new List<BlindProblemItem>
    {
        new()
        {
            Id = "blind75_226_invert_binary_tree",
            Number = 226,
            Title = "Invert Binary Tree",
            Category = "Trees",
            Difficulty = ProblemDifficulty.Easy,
            AcceptanceRate = 78.5,
            IsPremium = false,
            Tags = new List<string> { "Tree", "Depth-First Search", "Breadth-First Search", "Binary Tree" },
            TimeComplexity = "O(n)",
            SpaceComplexity = "O(h) for the recursion (h = height)",
            DescriptionMarkdown = """
            Given the `root` of a binary tree, **invert** it, i.e. turn it into its mirror image, and return its root.

            Mirroring swaps every node's left and right child, all the way down.

            ### Example 1
            - **Input:** `root = [4,2,7,1,3,6,9]`
            - **Output:** `[4,7,2,9,6,3,1]`
            - **Why:** 4's children 2 and 7 swap sides, and inside them 1/3 and 6/9 swap too.

            ### Example 2
            - **Input:** `root = [2,1,3]`
            - **Output:** `[2,3,1]`

            ### Example 3
            - **Input:** `root = []`
            - **Output:** `[]`

            ### Constraints
            - The number of nodes is in the range `[0, 100]`.
            - `-100 <= Node.val <= 100`
            """,
            ThinkingProcessMarkdown = """
            **Restate it:** hold the tree up to a mirror. What you see is every node with its two children swapped.

            1. **Look at one node.** Mirroring the whole tree means its left subtree ends up on the right, mirrored, and its right subtree ends up on the left, mirrored. So: swap the two children, then mirror each of them.
            2. **That is a recursion** with a tiny base case: an empty tree (`null`) is its own mirror.
            3. **Order doesn't matter.** Each node's swap is independent of the others, so you can swap before or after visiting the children, or go level by level with a queue (breadth-first). Every node is handled once: `O(n)`.
            4. **Walk Example 1:** at 4, swap 2 and 7 → `4(7, 2)`. At 7, swap 6 and 9 → `7(9, 6)`. At 2, swap 1 and 3 → `2(3, 1)`. Level order: `[4,7,2,9,6,3,1]`.

            **Pattern to remember:** many tree problems are "do something at this node, then trust the recursion for the two subtrees". Write the base case (`null`) first.

            **Common mistakes:** overwriting `root.left` before saving it (swap with a temporary or a tuple); swapping only the top level; forgetting to return the root.
            """,
            Approaches = new List<ProblemApproachItem>
            {
                new()
                {
                    Name = "Build a mirrored copy",
                    Kind = ApproachKind.Naive,
                    TimeComplexity = "O(n)",
                    SpaceComplexity = "O(n)",
                    Intuition = "A mirrored tree has the same root value, the mirror of the right subtree on the left and the mirror of the left subtree on the right. Build it as new nodes.",
                    BottleneckExplanation = "It allocates a second tree instead of inverting the one you were given, which is what the problem asks for.",
                    Code = """
                    TreeNode MirrorCopy(TreeNode root) =>
                        root == null ? null : new TreeNode(root.val, MirrorCopy(root.right), MirrorCopy(root.left));

                    Show(MirrorCopy(BuildTree(4, 2, 7, 1, 3, 6, 9)));   // [4,7,2,9,6,3,1]
                    """
                },
                new()
                {
                    Name = "Level by level with a queue",
                    Kind = ApproachKind.Alternative,
                    TimeComplexity = "O(n)",
                    SpaceComplexity = "O(w) (w = the widest level)",
                    Intuition = "Take nodes from a queue, swap each one's children and queue the children. No recursion, so a very deep tree can't overflow the call stack.",
                    Code = """
                    TreeNode InvertIteratively(TreeNode root)
                    {
                        var queue = new Queue<TreeNode>();
                        if (root != null) queue.Enqueue(root);
                        while (queue.Count > 0)
                        {
                            var node = queue.Dequeue();
                            (node.left, node.right) = (node.right, node.left);
                            if (node.left != null) queue.Enqueue(node.left);
                            if (node.right != null) queue.Enqueue(node.right);
                        }
                        return root;
                    }

                    Show(InvertIteratively(BuildTree(2, 1, 3)));   // [2,3,1]
                    """
                },
                new()
                {
                    Name = "Recursion: swap, then invert both subtrees",
                    Kind = ApproachKind.Optimal,
                    TimeComplexity = "O(n)",
                    SpaceComplexity = "O(h)",
                    Intuition = "Swap the node's children, then invert each child the same way; `null` is already inverted."
                }
            },
            SupportCode = TreeNodeSupportCode,
            SolutionCode = """
            public class Solution
            {
                public TreeNode InvertTree(TreeNode root)
                {
                    if (root == null) return null;
                    (root.left, root.right) = (root.right, root.left);   // mirror this node
                    InvertTree(root.left);                               // then everything below it
                    InvertTree(root.right);
                    return root;
                }
            }
            """,
            Tests = new List<BlindTest>
            {
                new() { Name = "Example 1", Input = "root = [4,2,7,1,3,6,9]", Expected = "[4,7,2,9,6,3,1]", Call = "sol.InvertTree(BuildTree(4, 2, 7, 1, 3, 6, 9))" },
                new() { Name = "Example 2", Input = "root = [2,1,3]", Expected = "[2,3,1]", Call = "sol.InvertTree(BuildTree(2, 1, 3))" },
                new() { Name = "Example 3", Input = "root = []", Expected = "[]", Call = "sol.InvertTree(BuildTree())" }
            },
            ExtraTests = new List<BlindTest>
            {
                new() { Name = "A single node", Input = "root = [1]", Expected = "[1]", Call = "sol.InvertTree(BuildTree(1))" },
                new() { Name = "A left chain becomes a right chain", Input = "root = [1,2,null,3]", Expected = "[1,null,2,null,3]", Call = "sol.InvertTree(BuildTree(1, 2, null, 3))" },
                new() { Name = "Negative values", Input = "root = [0,-1,1]", Expected = "[0,1,-1]", Call = "sol.InvertTree(BuildTree(0, -1, 1))" }
            },
            VisualizerKind = "Tree",
            VisualizationDescription = """
            Example 1. The recursion visits each node (amber), swaps its two children, and the tree is redrawn at once so
            you can watch it mirror itself; swapped nodes turn pink. Leaves have two empty children, so nothing changes there.
            """,
            VisualizationCode = """
            var root = BuildTree(4, 2, 7, 1, 3, 6, 9);
            var tracker = TreeTracker.Create(root, "226. Invert Binary Tree: swap the children of every node");
            string Name(TreeNode node) => node == null ? "null" : node.val.ToString();

            void Invert(TreeNode node)
            {
                if (node == null) return;
                if (node.left == null && node.right == null)
                {
                    tracker.Visit(node, $"{node.val} is a leaf: swapping its two empty children changes nothing");
                    return;
                }

                tracker.Visit(node, $"Visit {node.val}: swap its children {Name(node.left)} and {Name(node.right)}");
                (node.left, node.right) = (node.right, node.left);
                tracker.Sync();
                tracker.Highlight(node, TreeNodeState.Swapped, $"Swapped: {node.val} now has {Name(node.left)} on the left and {Name(node.right)} on the right. Next, invert inside each of them");
                Invert(node.left);
                Invert(node.right);
            }

            Invert(root);
            tracker.ClearCurrent();
            tracker.Snapshot($"Every node has been swapped, so the whole tree is mirrored: {Format(root)}");
            Display.Visualizer(tracker);
            """
        },
        new()
        {
            Id = "blind75_104_maximum_depth_of_binary_tree",
            Number = 104,
            Title = "Maximum Depth of Binary Tree",
            Category = "Trees",
            Difficulty = ProblemDifficulty.Easy,
            AcceptanceRate = 76.5,
            IsPremium = false,
            Tags = new List<string> { "Tree", "DFS", "BFS", "Binary Tree" },
            TimeComplexity = "O(n)",
            SpaceComplexity = "O(h) for the recursion",
            DescriptionMarkdown = """
            Given the `root` of a binary tree, return its **maximum depth**: the number of nodes on the longest path from the root down to a leaf.

            ### Example 1
            - **Input:** `root = [3,9,20,null,null,15,7]`
            - **Output:** `3`
            - **Why:** the path `3 → 20 → 15` (or `3 → 20 → 7`) has three nodes; `3 → 9` has only two.

            ### Example 2
            - **Input:** `root = [1,null,2]`
            - **Output:** `2`

            ### Constraints
            - The number of nodes is in the range `[0, 10^4]`.
            - `-100 <= Node.val <= 100`
            """,
            ThinkingProcessMarkdown = """
            **Restate it:** how many levels does the tree have?

            1. **Think about one node.** The deepest path through it goes down into whichever subtree is deeper. So `depth(node) = 1 + max(depth(left), depth(right))`, and an empty tree has depth `0`.
            2. **That definition is already the code:** a post-order recursion (children first, then the node). Each node is visited once: `O(n)` time, and the call stack holds one frame per level: `O(h)`.
            3. **Another view: count levels.** Breadth-first search processes the tree one level at a time with a queue; the number of rounds is the depth. It never recurses, which helps for very deep, skinny trees.
            4. **Walk Example 1:** 9 is a leaf → 1. 15 and 7 are leaves → 1 each, so 20 → `1 + max(1, 1) = 2`. The root → `1 + max(1, 2) = 3`.

            **Pattern to remember:** "the answer for a node is built from the answers for its children" is post-order DFS. Height, diameter, balanced-tree checks and path sums all follow it.

            **Common mistakes:** returning `1` for `null` (the empty tree has depth 0); counting edges instead of nodes; only following the left side.
            """,
            Approaches = new List<ProblemApproachItem>
            {
                new()
                {
                    Name = "Breadth-first: count the levels",
                    Kind = ApproachKind.Alternative,
                    TimeComplexity = "O(n)",
                    SpaceComplexity = "O(w) (w = the widest level)",
                    Intuition = "Process the tree one level at a time with a queue; every round adds one to the depth.",
                    Code = """
                    int MaxDepthByLevels(TreeNode root)
                    {
                        int depth = 0;
                        var level = new Queue<TreeNode>();
                        if (root != null) level.Enqueue(root);
                        while (level.Count > 0)
                        {
                            depth++;                                    // one more level
                            for (int k = level.Count; k > 0; k--)       // everything on this level
                            {
                                var node = level.Dequeue();
                                if (node.left != null) level.Enqueue(node.left);
                                if (node.right != null) level.Enqueue(node.right);
                            }
                        }
                        return depth;
                    }

                    Show(MaxDepthByLevels(BuildTree(3, 9, 20, null, null, 15, 7)));   // 3
                    """
                },
                new()
                {
                    Name = "Recursion: 1 + the deeper subtree",
                    Kind = ApproachKind.Optimal,
                    TimeComplexity = "O(n)",
                    SpaceComplexity = "O(h)",
                    Intuition = "An empty tree has depth 0; any other node adds one level to the deeper of its two subtrees."
                }
            },
            SupportCode = TreeNodeSupportCode,
            SolutionCode = """
            public class Solution
            {
                public int MaxDepth(TreeNode root)
                {
                    if (root == null) return 0;                                   // an empty tree has no levels
                    return 1 + Math.Max(MaxDepth(root.left), MaxDepth(root.right));   // this node + the deeper side
                }
            }
            """,
            Tests = new List<BlindTest>
            {
                new() { Name = "Example 1", Input = "root = [3,9,20,null,null,15,7]", Expected = "3", Call = "sol.MaxDepth(BuildTree(3, 9, 20, null, null, 15, 7))" },
                new() { Name = "Example 2", Input = "root = [1,null,2]", Expected = "2", Call = "sol.MaxDepth(BuildTree(1, null, 2))" }
            },
            ExtraTests = new List<BlindTest>
            {
                new() { Name = "Empty tree", Input = "root = []", Expected = "0", Call = "sol.MaxDepth(BuildTree())" },
                new() { Name = "A single node", Input = "root = [0]", Expected = "1", Call = "sol.MaxDepth(BuildTree(0))" },
                new() { Name = "A left chain", Input = "root = [1,2,null,3,null,4]", Expected = "4", Call = "sol.MaxDepth(BuildTree(1, 2, null, 3, null, 4))" },
                new() { Name = "Deepest on the right", Input = "root = [1,2,3,null,null,4,5,null,null,6]", Expected = "4", Call = "sol.MaxDepth(BuildTree(1, 2, 3, null, null, 4, 5, null, null, 6))" }
            },
            VisualizerKind = "Tree",
            VisualizationDescription = """
            Example 1. Each node is visited on the way down (amber) and answered on the way back up: its label shows
            `1 + max(left, right)` once both subtrees are measured. At the end the longest root-to-leaf path is marked.
            """,
            VisualizationCode = """
            var root = BuildTree(3, 9, 20, null, null, 15, 7);
            var tracker = TreeTracker.Create(root, "104. Maximum Depth: 1 + the deeper subtree");

            int Depth(TreeNode node)
            {
                if (node == null) return 0;
                bool leaf = node.left == null && node.right == null;
                tracker.Visit(node, leaf
                    ? $"Visit {node.val}: a leaf, both of its subtrees are empty (depth 0)"
                    : $"Visit {node.val}: its depth is 1 + the deeper of its two subtrees, so measure them first");
                int left = Depth(node.left);
                int right = Depth(node.right);
                int depth = 1 + Math.Max(left, right);
                tracker.Visit(node, $"Back at {node.val}: left gives {left}, right gives {right}, so 1 + {Math.Max(left, right)} = {depth}", subLabel: $"depth {depth}");
                return depth;
            }

            int answer = Depth(root);

            int Height(TreeNode node) => node == null ? 0 : 1 + Math.Max(Height(node.left), Height(node.right));
            var path = new List<TreeNode>();
            for (var node = root; node != null; node = Height(node.left) >= Height(node.right) ? node.left : node.right) path.Add(node);
            tracker.MarkPath(path, $"The answer is {answer}: the longest path from the root, {string.Join(" → ", path.Select(n => n.val))}");
            Display.Visualizer(tracker);
            """
        },
        new()
        {
            Id = "blind75_100_same_tree",
            Number = 100,
            Title = "Same Tree",
            Category = "Trees",
            Difficulty = ProblemDifficulty.Easy,
            AcceptanceRate = 63.5,
            IsPremium = false,
            Tags = new List<string> { "Tree", "DFS", "BFS", "Binary Tree" },
            TimeComplexity = "O(n)",
            SpaceComplexity = "O(h) for the recursion",
            DescriptionMarkdown = """
            Given the roots of two binary trees `p` and `q`, return `true` if they are the **same**: the same shape, with the same value in every matching position.

            ### Example 1
            - **Input:** `p = [1,2,3], q = [1,2,3]`
            - **Output:** `true`

            ### Example 2
            - **Input:** `p = [1,2], q = [1,null,2]`
            - **Output:** `false`
            - **Why:** the same values, but 2 is a left child in `p` and a right child in `q`.

            ### Example 3
            - **Input:** `p = [1,2,1], q = [1,1,2]`
            - **Output:** `false`

            ### Constraints
            - The number of nodes in each tree is in the range `[0, 100]`.
            - `-10^4 <= Node.val <= 10^4`
            """,
            ThinkingProcessMarkdown = """
            **Restate it:** walk both trees in lockstep and look for any difference.

            1. **Compare the roots first.** Two trees are the same exactly when the roots hold the same value **and** the left subtrees are the same **and** the right subtrees are the same. That sentence is the recursion.
            2. **Base cases decide the shape:** both `null` → the same (two empty trees); exactly one `null` → different (one side has a node the other doesn't).
            3. **Stop at the first difference.** `&&` short-circuits, so once one pair differs, nothing else is compared.
            4. **Walk Example 2:** roots 1 and 1 match. Left children: `2` vs `null` → one is missing → `false`.

            **Another way:** write both trees out as strings with a marker for every missing child (e.g. `1,2,#,#,3,#,#`) and compare the strings. The markers matter: without them `[1,2]` and `[1,null,2]` both print `1,2`.

            **Pattern to remember:** comparing two trees is a recursion over **pairs** of nodes. It is the building block of Subtree of Another Tree (problem 572) and Symmetric Tree.

            **Common mistakes:** comparing only the values in traversal order (different shapes can produce the same order); reading `p.val` before checking for `null`.
            """,
            Approaches = new List<ProblemApproachItem>
            {
                new()
                {
                    Name = "Write both trees out and compare the text",
                    Kind = ApproachKind.Naive,
                    TimeComplexity = "O(n)",
                    SpaceComplexity = "O(n)",
                    Intuition = "Serialize each tree in preorder with `#` for every missing child; the trees are the same exactly when the strings are.",
                    BottleneckExplanation = "It always builds both complete strings, using `O(n)` memory, even when the roots already differ.",
                    Code = """
                    string Serialize(TreeNode node) => node == null ? "#" : $"{node.val},{Serialize(node.left)},{Serialize(node.right)}";
                    bool IsSameBySerializing(TreeNode p, TreeNode q) => Serialize(p) == Serialize(q);

                    Show(IsSameBySerializing(BuildTree(1, 2), BuildTree(1, null, 2)));   // false
                    """
                },
                new()
                {
                    Name = "Recursion over pairs of nodes",
                    Kind = ApproachKind.Optimal,
                    TimeComplexity = "O(n)",
                    SpaceComplexity = "O(h)",
                    Intuition = "Two `null`s are equal, one `null` is not; otherwise the values must match and both pairs of subtrees must be the same. The first difference ends the search."
                }
            },
            SupportCode = TreeNodeSupportCode,
            SolutionCode = """
            public class Solution
            {
                public bool IsSameTree(TreeNode p, TreeNode q)
                {
                    if (p == null || q == null) return p == q;   // both empty: same; only one empty: different
                    return p.val == q.val && IsSameTree(p.left, q.left) && IsSameTree(p.right, q.right);
                }
            }
            """,
            Tests = new List<BlindTest>
            {
                new() { Name = "Example 1", Input = "p = [1,2,3], q = [1,2,3]", Expected = "true", Call = "sol.IsSameTree(BuildTree(1, 2, 3), BuildTree(1, 2, 3))" },
                new() { Name = "Example 2", Input = "p = [1,2], q = [1,null,2]", Expected = "false", Call = "sol.IsSameTree(BuildTree(1, 2), BuildTree(1, null, 2))" },
                new() { Name = "Example 3", Input = "p = [1,2,1], q = [1,1,2]", Expected = "false", Call = "sol.IsSameTree(BuildTree(1, 2, 1), BuildTree(1, 1, 2))" }
            },
            ExtraTests = new List<BlindTest>
            {
                new() { Name = "Both empty", Input = "p = [], q = []", Expected = "true", Call = "sol.IsSameTree(BuildTree(), BuildTree())" },
                new() { Name = "One empty", Input = "p = [], q = [1]", Expected = "false", Call = "sol.IsSameTree(BuildTree(), BuildTree(1))" },
                new() { Name = "Differs deep down", Input = "p = [1,2,3,4], q = [1,2,3,5]", Expected = "false", Call = "sol.IsSameTree(BuildTree(1, 2, 3, 4), BuildTree(1, 2, 3, 5))" },
                new() { Name = "Bigger equal trees", Input = "p = [5,3,8,1,4,7,9], q = [5,3,8,1,4,7,9]", Expected = "true", Call = "sol.IsSameTree(BuildTree(5, 3, 8, 1, 4, 7, 9), BuildTree(5, 3, 8, 1, 4, 7, 9))" }
            },
            VisualizerKind = "Tree",
            VisualizationDescription = """
            `p = [1,2,3,4]` (left) and `q = [1,2,3,5]` (right) drawn side by side. Each step compares the two nodes in the
            same position (amber): equal values turn green and the comparison moves on to the left children, then the
            right. 4 and 5 differ, so the answer is false and the right subtrees are never compared.
            """,
            VisualizationCode = """
            // Only for the drawing: one picture with p on the left and q on the right.
            class SideBySide
            {
                public string val = "vs";
                public TreeNode left, right;
            }

            var p = BuildTree(1, 2, 3, 4);
            var q = BuildTree(1, 2, 3, 5);
            var tracker = TreeTracker.Create(new SideBySide { left = p, right = q }, "100. Same Tree: compare both trees in lockstep");
            tracker.SetPointer(p, "p");
            tracker.SetPointer(q, "q");
            tracker.Snapshot("p on the left, q on the right: compare them pair by pair, starting with the roots");

            bool Same(TreeNode a, TreeNode b)
            {
                if (a == null && b == null) return true;
                if (a == null || b == null)
                {
                    var lonely = a ?? b;
                    tracker.Mark(lonely, TreeNodeState.Target);
                    tracker.Snapshot($"{lonely.val} has no partner in the other tree: the shapes differ, so the trees are different");
                    return false;
                }

                tracker.Mark(a, TreeNodeState.Current);
                tracker.Mark(b, TreeNodeState.Current);
                tracker.Snapshot($"Compare {a.val} with {b.val}, which sit in the same position in both trees");
                bool equal = a.val == b.val;
                tracker.Mark(a, equal ? TreeNodeState.Matched : TreeNodeState.Target);
                tracker.Mark(b, equal ? TreeNodeState.Matched : TreeNodeState.Target);
                tracker.Snapshot(equal
                    ? $"{a.val} = {b.val}: so far the same. Next compare their left children, then their right children"
                    : $"{a.val} ≠ {b.val}: the trees are different, stop here");
                return equal && Same(a.left, b.left) && Same(a.right, b.right);
            }

            bool same = Same(p, q);
            tracker.Snapshot(same
                ? "Every pair matched: the trees are the same, true"
                : "Answer: false. The && stopped at the first difference, so the remaining pairs were never compared");
            Display.Visualizer(tracker);
            """
        },
        new()
        {
            Id = "blind75_572_subtree_of_another_tree",
            Number = 572,
            Title = "Subtree of Another Tree",
            Category = "Trees",
            Difficulty = ProblemDifficulty.Easy,
            AcceptanceRate = 49.0,
            IsPremium = false,
            Tags = new List<string> { "Tree", "DFS", "String Matching", "Binary Tree" },
            TimeComplexity = "O(m · n)",
            SpaceComplexity = "O(h) for the recursion",
            DescriptionMarkdown = """
            Given the roots of two binary trees `root` and `subRoot`, return `true` if `root` has a **subtree** that is the same as `subRoot` (same shape and values).

            A subtree is a node together with **all** of its descendants; `root` itself also counts as one of its subtrees.

            ### Example 1
            - **Input:** `root = [3,4,5,1,2], subRoot = [4,1,2]`
            - **Output:** `true`
            - **Why:** the node 4 in `root`, with its children 1 and 2, is exactly `subRoot`.

            ### Example 2
            - **Input:** `root = [3,4,5,1,2,null,null,null,null,0], subRoot = [4,1,2]`
            - **Output:** `false`
            - **Why:** node 4's subtree also contains the 0 under 2, so it isn't identical to `subRoot`, and no other node starts a match.

            ### Constraints
            - `root` has between 1 and 2000 nodes, `subRoot` between 1 and 1000.
            - `-10^4 <= Node.val <= 10^4`
            """,
            ThinkingProcessMarkdown = """
            **Restate it:** is there a node in `root` where, starting from it, the tree is **the same tree** as `subRoot`?

            1. **Reuse Same Tree (problem 100).** Try every node of `root` as a starting point and ask `IsSame(node, subRoot)`. The first `true` answers the question.
            2. **Visiting every node is itself a recursion:** `IsSubtree(node) = IsSame(node, subRoot) || IsSubtree(node.left) || IsSubtree(node.right)`, with `IsSubtree(null) = false`.
            3. **Cost:** up to `m` starting nodes, each compared against up to `n` nodes of `subRoot`: `O(m · n)` in the worst case, but a mismatch usually shows up at the first comparison.
            4. **"All of its descendants" matters:** in Example 2, 4-1-2 matches the top of `subRoot`, but 2 has an extra child 0 there, so Same Tree says no.
            5. **Faster, for the curious:** write both trees as strings with markers for missing children and search for one string inside the other; with a linear-time string search (KMP) that is `O(m + n)`.

            **Pattern to remember:** break a problem into smaller ones you've solved: here "visit every node" + "are these two trees the same?".

            **Common mistakes:** only comparing values of the starting node; accepting a match that stops early (the `null` checks in Same Tree prevent that); forgetting that `root` itself is a candidate.
            """,
            Approaches = new List<ProblemApproachItem>
            {
                new()
                {
                    Name = "Serialize both trees and search the text",
                    Kind = ApproachKind.Alternative,
                    TimeComplexity = "O(m + n) with a linear string search",
                    SpaceComplexity = "O(m + n)",
                    Intuition = "Write each tree in preorder with `#` for a missing child and a comma before every value (so 2 can't match inside 12). `subRoot` is a subtree exactly when its string appears inside `root`'s.",
                    Code = """
                    string Serialize(TreeNode node) => node == null ? ",#" : $",{node.val}{Serialize(node.left)}{Serialize(node.right)}";
                    bool IsSubtreeBySerializing(TreeNode root, TreeNode subRoot) => Serialize(root).Contains(Serialize(subRoot));

                    Show(IsSubtreeBySerializing(BuildTree(3, 4, 5, 1, 2), BuildTree(4, 1, 2)));   // true
                    """
                },
                new()
                {
                    Name = "Try Same Tree from every node",
                    Kind = ApproachKind.Optimal,
                    TimeComplexity = "O(m · n)",
                    SpaceComplexity = "O(h)",
                    Intuition = "Walk `root`; at each node check whether the tree starting there is the same as `subRoot`. The simplest correct answer, and what interviewers usually expect."
                }
            },
            SupportCode = TreeNodeSupportCode,
            SolutionCode = """
            public class Solution
            {
                public bool IsSubtree(TreeNode root, TreeNode subRoot)
                {
                    if (root == null) return false;   // ran out of starting points
                    return IsSame(root, subRoot) || IsSubtree(root.left, subRoot) || IsSubtree(root.right, subRoot);
                }

                private bool IsSame(TreeNode a, TreeNode b)
                {
                    if (a == null || b == null) return a == b;
                    return a.val == b.val && IsSame(a.left, b.left) && IsSame(a.right, b.right);
                }
            }
            """,
            Tests = new List<BlindTest>
            {
                new() { Name = "Example 1", Input = "root = [3,4,5,1,2], subRoot = [4,1,2]", Expected = "true", Call = "sol.IsSubtree(BuildTree(3, 4, 5, 1, 2), BuildTree(4, 1, 2))" },
                new() { Name = "Example 2", Input = "root = [3,4,5,1,2,null,null,null,null,0], subRoot = [4,1,2]", Expected = "false", Call = "sol.IsSubtree(BuildTree(3, 4, 5, 1, 2, null, null, null, null, 0), BuildTree(4, 1, 2))" }
            },
            ExtraTests = new List<BlindTest>
            {
                new() { Name = "The whole tree", Input = "root = [1,2,3], subRoot = [1,2,3]", Expected = "true", Call = "sol.IsSubtree(BuildTree(1, 2, 3), BuildTree(1, 2, 3))" },
                new() { Name = "A leaf", Input = "root = [1,2,3], subRoot = [3]", Expected = "true", Call = "sol.IsSubtree(BuildTree(1, 2, 3), BuildTree(3))" },
                new() { Name = "The top matches, the rest doesn't", Input = "root = [1,2,3], subRoot = [1,2]", Expected = "false", Call = "sol.IsSubtree(BuildTree(1, 2, 3), BuildTree(1, 2))" },
                new() { Name = "Deep in a chain", Input = "root = [1,null,2,null,3,null,4], subRoot = [3,null,4]", Expected = "true", Call = "sol.IsSubtree(BuildTree(1, null, 2, null, 3, null, 4), BuildTree(3, null, 4))" },
                new() { Name = "Digits don't blend", Input = "root = [12], subRoot = [2]", Expected = "false", Call = "sol.IsSubtree(BuildTree(12), BuildTree(2))" }
            },
            VisualizerKind = "Tree",
            VisualizationDescription = """
            Example 2: `root` on the left, `subRoot` on the right. Each starting node of `root` (teal) is compared with
            `subRoot` in lockstep, green for equal pairs and red at the first difference. Node 4 comes closest, but its 2
            has an extra child. Starting points that failed are greyed out.
            """,
            VisualizationCode = """
            // Only for the drawing: one picture with root on the left and subRoot on the right.
            class SideBySide
            {
                public string val = "vs";
                public TreeNode left, right;
            }

            var root = BuildTree(3, 4, 5, 1, 2, null, null, null, null, 0);
            var subRoot = BuildTree(4, 1, 2);
            var tracker = TreeTracker.Create(new SideBySide { left = root, right = subRoot }, "572. Subtree of Another Tree: try every starting node");
            tracker.SetPointer(root, "root");
            tracker.SetPointer(subRoot, "subRoot");
            var tried = new List<TreeNode>();

            bool Same(TreeNode a, TreeNode b)
            {
                if (a == null && b == null) return true;
                if (a == null || b == null)
                {
                    var lonely = a ?? b;
                    tracker.Mark(lonely, TreeNodeState.Target);
                    tracker.Snapshot($"{lonely.val} has no partner on the other side: not the same tree");
                    return false;
                }

                bool equal = a.val == b.val;
                tracker.Mark(a, equal ? TreeNodeState.Matched : TreeNodeState.Target);
                tracker.Mark(b, equal ? TreeNodeState.Matched : TreeNodeState.Target);
                tracker.Snapshot(equal ? $"{a.val} = {b.val}: matches so far" : $"{a.val} ≠ {b.val}: not the same tree");
                return equal && Same(a.left, b.left) && Same(a.right, b.right);
            }

            bool IsSubtree(TreeNode node)
            {
                if (node == null) return false;
                tracker.ClearMarks();
                foreach (var failed in tried) tracker.Mark(failed, TreeNodeState.Pruned);
                tracker.Mark(node, TreeNodeState.Candidate);
                tracker.Snapshot($"Could the subtree that starts at {node.val} be the same tree as subRoot?");
                if (Same(node, subRoot)) return true;
                tried.Add(node);
                return IsSubtree(node.left) || IsSubtree(node.right);
            }

            bool found = IsSubtree(root);
            tracker.Snapshot(found ? "Found a match: true" : "No starting node gives exactly subRoot: false");
            Display.Visualizer(tracker);
            """
        },
        new()
        {
            Id = "blind75_235_lowest_common_ancestor_of_a_binary_search_tree",
            Number = 235,
            Title = "Lowest Common Ancestor of a Binary Search Tree",
            Category = "Trees",
            Difficulty = ProblemDifficulty.Medium,
            AcceptanceRate = 65.0,
            IsPremium = false,
            Tags = new List<string> { "Tree", "DFS", "BST", "Binary Tree" },
            TimeComplexity = "O(h)",
            SpaceComplexity = "O(1)",
            DescriptionMarkdown = """
            Given a **binary search tree** (BST) and two of its nodes `p` and `q`, return their **lowest common ancestor** (LCA): the deepest node that has both `p` and `q` in its subtree. A node counts as being in its own subtree.

            In a BST, every value in a node's left subtree is smaller than the node's value and every value in its right subtree is larger.

            ### Example 1
            - **Input:** `root = [6,2,8,0,4,7,9,null,null,3,5], p = 2, q = 8`
            - **Output:** `6`
            - **Why:** 2 is in 6's left subtree and 8 in its right, so 6 is the deepest node above both.

            ### Example 2
            - **Input:** `root = [6,2,8,0,4,7,9,null,null,3,5], p = 2, q = 4`
            - **Output:** `2`
            - **Why:** 4 is below 2, and a node is its own ancestor.

            ### Example 3
            - **Input:** `root = [2,1], p = 2, q = 1`
            - **Output:** `2`

            ### Constraints
            - The number of nodes is in the range `[2, 10^5]`.
            - `-10^9 <= Node.val <= 10^9`, and all values are unique.
            - `p != q`, and both exist in the BST.
            """,
            ThinkingProcessMarkdown = """
            **Restate it:** walking down from the root, find the last node that is still on the way to **both** `p` and `q`.

            1. **In a general tree you'd have to search** (that's problem 236). In a BST the values tell you where `p` and `q` are without searching.
            2. **At any node there are three cases:**
               - both values are **smaller** → `p` and `q` are both in the left subtree, so the LCA is too: go left;
               - both are **larger** → go right;
               - otherwise they are on different sides (or one of them is this node) → paths to `p` and `q` split here, so this node is the LCA.
            3. **One walk down, no recursion needed:** `O(h)` time, where `h` is the height, and `O(1)` memory.
            4. **Walk Example 1 with p = 3, q = 5:** at 6 both are smaller → left to 2. At 2 both are larger → right to 4. At 4, 3 is smaller and 5 is larger → **4**.

            **Pattern to remember:** in a BST, comparing with the current node tells you which subtree holds a value, so many searches become a single path from the root.

            **Common mistakes:** searching the whole tree (ignores the BST property); stopping only when `node == p` or `node == q` (misses the split case); using `<=` so a node equal to `p` is sent further down.
            """,
            Approaches = new List<ProblemApproachItem>
            {
                new()
                {
                    Name = "Record both root-to-node paths, keep the last shared node",
                    Kind = ApproachKind.Naive,
                    TimeComplexity = "O(h)",
                    SpaceComplexity = "O(h)",
                    Intuition = "Walk from the root to `p` and to `q` (the BST says which way to go), storing both paths. They agree up to the LCA and then part ways.",
                    BottleneckExplanation = "It stores two whole paths, although the split point can be spotted during one walk without remembering anything.",
                    Code = """
                    TreeNode LcaByPaths(TreeNode root, TreeNode p, TreeNode q)
                    {
                        List<TreeNode> PathTo(TreeNode target)
                        {
                            var path = new List<TreeNode>();
                            for (var node = root; node != null; node = target.val < node.val ? node.left : node.right)
                            {
                                path.Add(node);
                                if (node == target) break;
                            }
                            return path;
                        }

                        var toP = PathTo(p);
                        var toQ = PathTo(q);
                        TreeNode shared = null;
                        for (int i = 0; i < Math.Min(toP.Count, toQ.Count) && toP[i] == toQ[i]; i++) shared = toP[i];
                        return shared;
                    }

                    var bst = BuildTree(6, 2, 8, 0, 4, 7, 9, null, null, 3, 5);
                    Show(LcaByPaths(bst, Find(bst, 2), Find(bst, 4)).val);   // 2
                    """
                },
                new()
                {
                    Name = "Walk down until p and q split",
                    Kind = ApproachKind.Optimal,
                    TimeComplexity = "O(h)",
                    SpaceComplexity = "O(1)",
                    Intuition = "Go left while both values are smaller, right while both are larger; the first node where that stops is the LCA."
                }
            },
            SupportCode = TreeNodeSupportCode + FindNodeSupportCode,
            SolutionCode = """
            public class Solution
            {
                public TreeNode LowestCommonAncestor(TreeNode root, TreeNode p, TreeNode q)
                {
                    var node = root;
                    while (node != null)
                    {
                        if (p.val < node.val && q.val < node.val) node = node.left;         // both on the left
                        else if (p.val > node.val && q.val > node.val) node = node.right;   // both on the right
                        else return node;                                                    // they split here
                    }
                    return null;
                }
            }
            """,
            TestSetupCode = """
            // The answer is a node; the tests compare its value.
            int LcaValue(TreeNode root, int p, int q) => sol.LowestCommonAncestor(root, Find(root, p), Find(root, q)).val;
            """,
            Tests = new List<BlindTest>
            {
                new() { Name = "Example 1", Input = "root = [6,2,8,0,4,7,9,null,null,3,5], p = 2, q = 8", Expected = "6", Call = "LcaValue(BuildTree(6, 2, 8, 0, 4, 7, 9, null, null, 3, 5), 2, 8)" },
                new() { Name = "Example 2", Input = "root = [6,2,8,0,4,7,9,null,null,3,5], p = 2, q = 4", Expected = "2", Call = "LcaValue(BuildTree(6, 2, 8, 0, 4, 7, 9, null, null, 3, 5), 2, 4)" },
                new() { Name = "Example 3", Input = "root = [2,1], p = 2, q = 1", Expected = "2", Call = "LcaValue(BuildTree(2, 1), 2, 1)" }
            },
            ExtraTests = new List<BlindTest>
            {
                new() { Name = "Both on the right", Input = "root = [6,2,8,0,4,7,9,null,null,3,5], p = 7, q = 9", Expected = "8", Call = "LcaValue(BuildTree(6, 2, 8, 0, 4, 7, 9, null, null, 3, 5), 7, 9)" },
                new() { Name = "A deep pair", Input = "root = [6,2,8,0,4,7,9,null,null,3,5], p = 3, q = 5", Expected = "4", Call = "LcaValue(BuildTree(6, 2, 8, 0, 4, 7, 9, null, null, 3, 5), 3, 5)" },
                new() { Name = "The root and a leaf", Input = "root = [6,2,8,0,4,7,9,null,null,3,5], p = 5, q = 6", Expected = "6", Call = "LcaValue(BuildTree(6, 2, 8, 0, 4, 7, 9, null, null, 3, 5), 5, 6)" },
                new() { Name = "Negative values", Input = "root = [0,-5,5,-8,-3], p = -8, q = -3", Expected = "-5", Call = "LcaValue(BuildTree(0, -5, 5, -8, -3), -8, -3)" }
            },
            VisualizerKind = "Tree",
            VisualizationDescription = """
            The BST from Example 1 with `p = 3` and `q = 5` (teal, labelled). Each step compares both values with the
            current node (amber) and moves left or right while they stay on the same side. At 4 they split, so 4 turns
            green: it is the lowest common ancestor.
            """,
            VisualizationCode = """
            var root = BuildTree(6, 2, 8, 0, 4, 7, 9, null, null, 3, 5);
            TreeNode p = Find(root, 3), q = Find(root, 5);
            var tracker = TreeTracker.Create(root, "235. LCA of a BST: walk down until p and q split");
            tracker.SetPointer(p, "p");
            tracker.SetPointer(q, "q");
            tracker.Mark(p, TreeNodeState.Candidate);
            tracker.Mark(q, TreeNodeState.Candidate);
            tracker.Snapshot($"Looking for the lowest node above both p = {p.val} and q = {q.val}. Start at the root");

            var node = root;
            while (node != null)
            {
                if (p.val < node.val && q.val < node.val)
                {
                    tracker.Visit(node, $"{p.val} and {q.val} are both smaller than {node.val}: both are in its left subtree, so go left");
                    node = node.left;
                }
                else if (p.val > node.val && q.val > node.val)
                {
                    tracker.Visit(node, $"{p.val} and {q.val} are both larger than {node.val}: both are in its right subtree, so go right");
                    node = node.right;
                }
                else
                {
                    tracker.Visit(node, $"{Math.Min(p.val, q.val)} ≤ {node.val} ≤ {Math.Max(p.val, q.val)}: p and q are no longer on the same side");
                    tracker.Highlight(node, TreeNodeState.Matched, $"The paths to {p.val} and {q.val} split at {node.val}, so {node.val} is the lowest common ancestor");
                    break;
                }
            }

            Display.Visualizer(tracker);
            """
        },
        new()
        {
            Id = "blind75_236_lowest_common_ancestor_of_a_binary_tree",
            Number = 236,
            Title = "Lowest Common Ancestor of a Binary Tree",
            Category = "Trees",
            Difficulty = ProblemDifficulty.Medium,
            AcceptanceRate = 62.0,
            IsPremium = false,
            Tags = new List<string> { "Tree", "DFS", "Binary Tree" },
            TimeComplexity = "O(n)",
            SpaceComplexity = "O(h) for the recursion",
            DescriptionMarkdown = """
            Given a binary tree (not necessarily a search tree) and two of its nodes `p` and `q`, return their **lowest common ancestor** (LCA): the deepest node that has both `p` and `q` in its subtree. A node counts as being in its own subtree.

            ### Example 1
            - **Input:** `root = [3,5,1,6,2,0,8,null,null,7,4], p = 5, q = 1`
            - **Output:** `3`

            ### Example 2
            - **Input:** `root = [3,5,1,6,2,0,8,null,null,7,4], p = 5, q = 4`
            - **Output:** `5`
            - **Why:** 4 is below 5, and a node is its own ancestor.

            ### Example 3
            - **Input:** `root = [1,2], p = 1, q = 2`
            - **Output:** `1`

            ### Constraints
            - The number of nodes is in the range `[2, 10^5]`.
            - `-10^9 <= Node.val <= 10^9`, and all values are unique.
            - `p != q`, and both exist in the tree.
            """,
            ThinkingProcessMarkdown = """
            **Restate it:** without values to guide us (this is not a BST), find the deepest node whose subtree holds both `p` and `q`.

            1. **Ask each subtree a question:** "does it contain `p` or `q`, and if so, which node should I know about?" Let the recursion return:
               - `null` if neither is inside,
               - the node itself if it **is** `p` or `q` (no need to look below it),
               - otherwise whatever its children reported.
            2. **The split point:** if the left subtree reports something **and** the right subtree reports something, then `p` is on one side and `q` on the other, and this node is the LCA; return it.
            3. **If only one side reports,** pass that up: it's either `p`/`q` itself or an LCA already found lower down. The first split (bottom-up) is the lowest common ancestor.
            4. **Why stopping at `p` is safe:** if `q` is below `p`, then `p` is the answer anyway, so there is nothing to gain by searching under it.
            5. **Walk Example 1 with p = 6, q = 4:** 6 reports itself. Under 2, 7 reports nothing and 4 reports itself, so 2 passes 4 up. At 5, left says 6 and right says 4 → **5** is the split. The right half (1, 0, 8) reports nothing, so the root passes 5 up.

            **Pattern to remember:** post-order DFS where each call returns "what I found below me" and the parent combines the two answers.

            **Common mistakes:** comparing values as if it were a BST; returning `true`/`false` instead of nodes (then you can't tell which node is the answer); forgetting the "node is p or q" base case.
            """,
            Approaches = new List<ProblemApproachItem>
            {
                new()
                {
                    Name = "Parent pointers, then climb from p and q",
                    Kind = ApproachKind.Naive,
                    TimeComplexity = "O(n)",
                    SpaceComplexity = "O(n)",
                    Intuition = "Record every node's parent with a traversal. Climb from `p` to the root collecting its ancestors, then climb from `q` until you reach one of them.",
                    BottleneckExplanation = "It needs a parent map and an ancestor set, `O(n)` extra memory, and two passes; the recursive answer needs only the call stack.",
                    Code = """
                    TreeNode LcaWithParents(TreeNode root, TreeNode p, TreeNode q)
                    {
                        var parent = new Dictionary<TreeNode, TreeNode> { [root] = null };
                        var stack = new Stack<TreeNode>();
                        stack.Push(root);
                        while (!parent.ContainsKey(p) || !parent.ContainsKey(q))   // until both have been reached
                        {
                            var node = stack.Pop();
                            if (node.left != null) { parent[node.left] = node; stack.Push(node.left); }
                            if (node.right != null) { parent[node.right] = node; stack.Push(node.right); }
                        }

                        var ancestors = new HashSet<TreeNode>();
                        for (var node = p; node != null; node = parent[node]) ancestors.Add(node);
                        var up = q;
                        while (!ancestors.Contains(up)) up = parent[up];
                        return up;
                    }

                    var tree = BuildTree(3, 5, 1, 6, 2, 0, 8, null, null, 7, 4);
                    Show(LcaWithParents(tree, Find(tree, 5), Find(tree, 4)).val);   // 5
                    """
                },
                new()
                {
                    Name = "Post-order search: report what each subtree found",
                    Kind = ApproachKind.Optimal,
                    TimeComplexity = "O(n)",
                    SpaceComplexity = "O(h)",
                    Intuition = "Return the node if it is `p` or `q`. Otherwise search both children: if both report something, this node is the LCA; if one does, pass its answer up."
                }
            },
            SupportCode = TreeNodeSupportCode + FindNodeSupportCode,
            SolutionCode = """
            public class Solution
            {
                public TreeNode LowestCommonAncestor(TreeNode root, TreeNode p, TreeNode q)
                {
                    if (root == null || root == p || root == q) return root;   // nothing here, or found one of them
                    var left = LowestCommonAncestor(root.left, p, q);
                    var right = LowestCommonAncestor(root.right, p, q);
                    if (left != null && right != null) return root;              // p and q are on different sides
                    return left ?? right;                                         // pass up whatever was found below
                }
            }
            """,
            TestSetupCode = """
            // The answer is a node; the tests compare its value.
            int LcaValue(TreeNode root, int p, int q) => sol.LowestCommonAncestor(root, Find(root, p), Find(root, q)).val;
            """,
            Tests = new List<BlindTest>
            {
                new() { Name = "Example 1", Input = "root = [3,5,1,6,2,0,8,null,null,7,4], p = 5, q = 1", Expected = "3", Call = "LcaValue(BuildTree(3, 5, 1, 6, 2, 0, 8, null, null, 7, 4), 5, 1)" },
                new() { Name = "Example 2", Input = "root = [3,5,1,6,2,0,8,null,null,7,4], p = 5, q = 4", Expected = "5", Call = "LcaValue(BuildTree(3, 5, 1, 6, 2, 0, 8, null, null, 7, 4), 5, 4)" },
                new() { Name = "Example 3", Input = "root = [1,2], p = 1, q = 2", Expected = "1", Call = "LcaValue(BuildTree(1, 2), 1, 2)" }
            },
            ExtraTests = new List<BlindTest>
            {
                new() { Name = "A deep pair", Input = "root = [3,5,1,6,2,0,8,null,null,7,4], p = 7, q = 4", Expected = "2", Call = "LcaValue(BuildTree(3, 5, 1, 6, 2, 0, 8, null, null, 7, 4), 7, 4)" },
                new() { Name = "One far below the other", Input = "root = [3,5,1,6,2,0,8,null,null,7,4], p = 5, q = 7", Expected = "5", Call = "LcaValue(BuildTree(3, 5, 1, 6, 2, 0, 8, null, null, 7, 4), 5, 7)" },
                new() { Name = "Across the root", Input = "root = [3,5,1,6,2,0,8,null,null,7,4], p = 6, q = 8", Expected = "3", Call = "LcaValue(BuildTree(3, 5, 1, 6, 2, 0, 8, null, null, 7, 4), 6, 8)" },
                new() { Name = "Siblings", Input = "root = [3,5,1,6,2,0,8,null,null,7,4], p = 0, q = 8", Expected = "1", Call = "LcaValue(BuildTree(3, 5, 1, 6, 2, 0, 8, null, null, 7, 4), 0, 8)" }
            },
            VisualizerKind = "Tree",
            VisualizationDescription = """
            Example 1's tree with `p = 6` and `q = 4`. The search visits nodes on the way down (amber), and on the way back
            up each node's label shows what its subtree reported. 5 hears "6" from the left and "4" from the right, so it
            is the split point; the root then just passes 5 up.
            """,
            VisualizationCode = """
            var root = BuildTree(3, 5, 1, 6, 2, 0, 8, null, null, 7, 4);
            TreeNode p = Find(root, 6), q = Find(root, 4);
            var tracker = TreeTracker.Create(root, "236. LCA of a Binary Tree: report what each subtree found");
            tracker.SetPointer(p, "p");
            tracker.SetPointer(q, "q");
            tracker.Snapshot($"Find the deepest node with both p = {p.val} and q = {q.val} in its subtree. Each call reports what it found below");
            string Name(TreeNode node) => node == null ? "null" : node.val.ToString();

            TreeNode Search(TreeNode node)
            {
                if (node == null) return null;
                if (node == p || node == q)
                {
                    tracker.Visit(node, $"{node.val} is {(node == p ? "p" : "q")} itself: report it, no need to look below", subLabel: $"→ {node.val}");
                    return node;
                }

                tracker.Visit(node, $"{node.val} is neither p nor q: search both of its subtrees");
                var left = Search(node.left);
                var right = Search(node.right);
                var found = left != null && right != null ? node : left ?? right;
                tracker.Visit(node, left != null && right != null
                        ? $"Back at {node.val}: {left.val} came from the left and {right.val} from the right, so {node.val} is where they split"
                        : found == null ? $"Back at {node.val}: neither p nor q is below it, report null"
                        : $"Back at {node.val}: only one side found something, pass {found.val} up",
                    subLabel: $"→ {Name(found)}");
                return found;
            }

            var lca = Search(root);
            tracker.ClearCurrent();
            tracker.Highlight(lca, TreeNodeState.Matched, $"The root's search returns {lca.val}: that is the lowest common ancestor");
            Display.Visualizer(tracker);
            """
        }
    };
}
