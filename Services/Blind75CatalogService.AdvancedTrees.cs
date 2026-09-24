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
            HasVisualizer = true,
            VisualizerKind = "Tree",
            Tags = new List<string> { "Tree", "BFS", "Binary Tree" },
            TimeComplexity = "O(N)",
            SpaceComplexity = "O(N)",
            DescriptionMarkdown = """
            Given the `root` of a binary tree, return the level order traversal of its nodes' values. (i.e., from left to right, level by level).
            """,
            StarterCode = """
            using System.Collections.Generic;

            public class TreeNode 
            {
                public int val;
                public TreeNode left;
                public TreeNode right;
                public TreeNode(int val = 0, TreeNode left = null, TreeNode right = null) { this.val = val; this.left = left; this.right = right; }
            }

            public class Solution 
            {
                public IList<IList<int>> LevelOrder(TreeNode root) 
                {
                    var res = new List<IList<int>>();
                    if (root == null) return res;
                    var q = new Queue<TreeNode>();
                    q.Enqueue(root);
                    while (q.Count > 0)
                    {
                        int size = q.Count;
                        var level = new List<int>();
                        for (int i = 0; i < size; i++)
                        {
                            var node = q.Dequeue();
                            level.Add(node.val);
                            if (node.left != null) q.Enqueue(node.left);
                            if (node.right != null) q.Enqueue(node.right);
                        }
                        res.Add(level);
                    }
                    return res;
                }
            }

            var root = new TreeNode(3, new TreeNode(9), new TreeNode(20, new TreeNode(15), new TreeNode(7)));
            var sol = new Solution();
            sol.LevelOrder(root).Dump("Level Order Traversal");
            """,
            TestCases = new List<TestCaseItem>
            {
                new() { Name = "[3,9,20,null,null,15,7]", Input = "[3,9,20,null,null,15,7]", ExpectedOutput = "[[3],[9,20],[15,7]]" }
            }
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
            HasVisualizer = true,
            VisualizerKind = "Tree",
            Tags = new List<string> { "Tree", "DFS", "BST", "Binary Tree" },
            TimeComplexity = "O(N)",
            SpaceComplexity = "O(H)",
            DescriptionMarkdown = """
            Given the `root` of a binary tree, determine if it is a valid binary search tree (BST).
            """,
            StarterCode = """
            public class TreeNode 
            {
                public int val;
                public TreeNode left;
                public TreeNode right;
                public TreeNode(int val = 0, TreeNode left = null, TreeNode right = null) { this.val = val; this.left = left; this.right = right; }
            }

            public class Solution 
            {
                public bool IsValidBST(TreeNode root) => Validate(root, null, null);

                private bool Validate(TreeNode node, long? min, long? max)
                {
                    if (node == null) return true;
                    if ((min.HasValue && node.val <= min.Value) || (max.HasValue && node.val >= max.Value)) return false;
                    return Validate(node.left, min, node.val) && Validate(node.right, node.val, max);
                }
            }

            var sol = new Solution();
            var root = new TreeNode(2, new TreeNode(1), new TreeNode(3));
            sol.IsValidBST(root).Dump("IsValidBST (expected: True)");
            """,
            TestCases = new List<TestCaseItem>
            {
                new() { Name = "[2,1,3]", Input = "[2,1,3]", ExpectedOutput = "True" }
            }
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
            HasVisualizer = true,
            VisualizerKind = "Tree",
            Tags = new List<string> { "Tree", "DFS", "BST", "Binary Tree" },
            TimeComplexity = "O(H + k)",
            SpaceComplexity = "O(H)",
            DescriptionMarkdown = """
            Given the `root` of a binary search tree, and an integer `k`, return the `kth` smallest value (1-indexed) of all the values of the nodes in the tree.
            """,
            StarterCode = """
            using System.Collections.Generic;

            public class TreeNode 
            {
                public int val;
                public TreeNode left;
                public TreeNode right;
                public TreeNode(int val = 0, TreeNode left = null, TreeNode right = null) { this.val = val; this.left = left; this.right = right; }
            }

            public class Solution 
            {
                public int KthSmallest(TreeNode root, int k) 
                {
                    var stack = new Stack<TreeNode>();
                    var curr = root;
                    while (curr != null || stack.Count > 0)
                    {
                        while (curr != null) { stack.Push(curr); curr = curr.left; }
                        curr = stack.Pop();
                        if (--k == 0) return curr.val;
                        curr = curr.right;
                    }
                    return -1;
                }
            }

            var sol = new Solution();
            var root = new TreeNode(3, new TreeNode(1, null, new TreeNode(2)), new TreeNode(4));
            sol.KthSmallest(root, 1).Dump("1st Smallest (expected: 1)");
            """,
            TestCases = new List<TestCaseItem>
            {
                new() { Name = "k=1", Input = "[3,1,4,null,2], k=1", ExpectedOutput = "1" }
            }
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
            HasVisualizer = true,
            VisualizerKind = "Tree",
            Tags = new List<string> { "Array", "Hash Table", "Divide and Conquer", "Tree", "Binary Tree" },
            TimeComplexity = "O(N)",
            SpaceComplexity = "O(N)",
            DescriptionMarkdown = """
            Given two integer arrays `preorder` and `inorder` where `preorder` is the preorder traversal of a binary tree and `inorder` is the inorder traversal of the same tree, construct and return *the binary tree*.
            """,
            StarterCode = """
            using System;
            using System.Collections.Generic;

            public class TreeNode 
            {
                public int val;
                public TreeNode left;
                public TreeNode right;
                public TreeNode(int val = 0, TreeNode left = null, TreeNode right = null) { this.val = val; this.left = left; this.right = right; }
            }

            public class Solution 
            {
                public TreeNode BuildTree(int[] preorder, int[] inorder) 
                {
                    var map = new Dictionary<int, int>();
                    for (int i = 0; i < inorder.Length; i++) map[inorder[i]] = i;
                    int preIdx = 0;

                    TreeNode Build(int inStart, int inEnd)
                    {
                        if (inStart > inEnd) return null;
                        int val = preorder[preIdx++];
                        var node = new TreeNode(val);
                        int mid = map[val];
                        node.left = Build(inStart, mid - 1);
                        node.right = Build(mid + 1, inEnd);
                        return node;
                    }

                    return Build(0, inorder.Length - 1);
                }
            }

            var sol = new Solution();
            sol.BuildTree(new[] { 3, 9, 20, 15, 7 }, new[] { 9, 3, 15, 20, 7 }).Dump("Constructed Tree");
            """,
            TestCases = new List<TestCaseItem>
            {
                new() { Name = "Standard", Input = "pre=[3,9,20,15,7], in=[9,3,15,20,7]", ExpectedOutput = "[3,9,20,null,null,15,7]" }
            }
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
            HasVisualizer = true,
            VisualizerKind = "Tree",
            Tags = new List<string> { "Dynamic Programming", "Tree", "DFS", "Binary Tree" },
            TimeComplexity = "O(N)",
            SpaceComplexity = "O(H)",
            DescriptionMarkdown = """
            A **path** in a binary tree is a sequence of nodes where each pair of adjacent nodes in the sequence has an edge connecting them. Return *the maximum path sum of any non-empty path*.
            """,
            StarterCode = """
            using System;

            public class TreeNode 
            {
                public int val;
                public TreeNode left;
                public TreeNode right;
                public TreeNode(int val = 0, TreeNode left = null, TreeNode right = null) { this.val = val; this.left = left; this.right = right; }
            }

            public class Solution 
            {
                private int _maxSum = int.MinValue;

                public int MaxPathSum(TreeNode root) 
                {
                    _maxSum = int.MinValue;
                    GetMaxGain(root);
                    return _maxSum;
                }

                private int GetMaxGain(TreeNode node)
                {
                    if (node == null) return 0;
                    int leftGain = Math.Max(GetMaxGain(node.left), 0);
                    int rightGain = Math.Max(GetMaxGain(node.right), 0);
                    _maxSum = Math.Max(_maxSum, node.val + leftGain + rightGain);
                    return node.val + Math.Max(leftGain, rightGain);
                }
            }

            var sol = new Solution();
            var root = new TreeNode(-10, new TreeNode(9), new TreeNode(20, new TreeNode(15), new TreeNode(7)));
            sol.MaxPathSum(root).Dump("Max Path Sum (expected: 42)");
            """,
            TestCases = new List<TestCaseItem>
            {
                new() { Name = "[-10,9,20,null,null,15,7]", Input = "[-10,9,20,null,null,15,7]", ExpectedOutput = "42" }
            }
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
            HasVisualizer = true,
            VisualizerKind = "Tree",
            Tags = new List<string> { "String", "Tree", "DFS", "BFS", "Design", "Binary Tree" },
            TimeComplexity = "O(N)",
            SpaceComplexity = "O(N)",
            DescriptionMarkdown = """
            Serialization is the process of converting a data structure or object into a sequence of bits so that it can be stored in a file or memory buffer. Design an algorithm to serialize and deserialize a binary tree.
            """,
            StarterCode = """
            using System.Collections.Generic;
            using System.Text;

            public class TreeNode 
            {
                public int val;
                public TreeNode left;
                public TreeNode right;
                public TreeNode(int x) { val = x; }
            }

            public class Codec 
            {
                public string serialize(TreeNode root) 
                {
                    var sb = new StringBuilder();
                    void Dfs(TreeNode node)
                    {
                        if (node == null) { sb.Append("#,"); return; }
                        sb.Append(node.val).Append(',');
                        Dfs(node.left);
                        Dfs(node.right);
                    }
                    Dfs(root);
                    return sb.ToString();
                }

                public TreeNode deserialize(string data) 
                {
                    var tokens = new Queue<string>(data.Split(',', System.StringSplitOptions.RemoveEmptyEntries));
                    TreeNode Dfs()
                    {
                        if (tokens.Count == 0) return null;
                        var val = tokens.Dequeue();
                        if (val == "#") return null;
                        var node = new TreeNode(int.Parse(val));
                        node.left = Dfs();
                        node.right = Dfs();
                        return node;
                    }
                    return Dfs();
                }
            }

            var codec = new Codec();
            var root = new TreeNode(1); root.left = new TreeNode(2); root.right = new TreeNode(3);
            var s = codec.serialize(root);
            codec.deserialize(s).Dump("Deserialized Tree");
            """,
            TestCases = new List<TestCaseItem>
            {
                new() { Name = "[1,2,3]", Input = "[1,2,3]", ExpectedOutput = "[1,2,3]" }
            }
        }
    };
}
