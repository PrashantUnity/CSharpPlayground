using System.Collections.Generic;
using PdfEditorApp.Plugins.CSharpEditor.Models;

namespace PdfEditorApp.Plugins.CSharpEditor.Services;

public static partial class Blind75CatalogService
{
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
            HasVisualizer = true,
            VisualizerKind = "Tree",
            Tags = new List<string> { "Tree", "Depth-First Search", "Breadth-First Search", "Binary Tree" },
            TimeComplexity = "O(N)",
            SpaceComplexity = "O(H)",
            DescriptionMarkdown = """
            Given the `root` of a binary tree, invert the tree, and return *its root*.
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
                public TreeNode InvertTree(TreeNode root) 
                {
                    if (root == null) return null;
                    var temp = root.left;
                    root.left = InvertTree(root.right);
                    root.right = InvertTree(temp);
                    return root;
                }
            }

            var root = new TreeNode(4, new TreeNode(2, new TreeNode(1), new TreeNode(3)), new TreeNode(7, new TreeNode(6), new TreeNode(9)));
            var sol = new Solution();
            sol.InvertTree(root).Dump("Inverted Tree");
            """,
            TestCases = new List<TestCaseItem>
            {
                new() { Name = "[4,2,7,1,3,6,9]", Input = "[4,2,7,1,3,6,9]", ExpectedOutput = "[4,7,2,9,6,3,1]" }
            }
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
            HasVisualizer = true,
            VisualizerKind = "Tree",
            Tags = new List<string> { "Tree", "DFS", "BFS", "Binary Tree" },
            TimeComplexity = "O(N)",
            SpaceComplexity = "O(H)",
            DescriptionMarkdown = """
            Given the `root` of a binary tree, return *its maximum depth*.
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
                public int MaxDepth(TreeNode root) 
                {
                    if (root == null) return 0;
                    return 1 + Math.Max(MaxDepth(root.left), MaxDepth(root.right));
                }
            }

            var root = new TreeNode(3, new TreeNode(9), new TreeNode(20, new TreeNode(15), new TreeNode(7)));
            var sol = new Solution();
            sol.MaxDepth(root).Dump("MaxDepth (expected: 3)");
            """,
            TestCases = new List<TestCaseItem>
            {
                new() { Name = "[3,9,20,null,null,15,7]", Input = "[3,9,20,null,null,15,7]", ExpectedOutput = "3" }
            }
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
            HasVisualizer = true,
            VisualizerKind = "Tree",
            Tags = new List<string> { "Tree", "DFS", "BFS", "Binary Tree" },
            TimeComplexity = "O(N)",
            SpaceComplexity = "O(H)",
            DescriptionMarkdown = """
            Given the roots of two binary trees `p` and `q`, write a function to check if they are the same or not.
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
                public bool IsSameTree(TreeNode p, TreeNode q) 
                {
                    if (p == null && q == null) return true;
                    if (p == null || q == null || p.val != q.val) return false;
                    return IsSameTree(p.left, q.left) && IsSameTree(p.right, q.right);
                }
            }

            var sol = new Solution();
            var t1 = new TreeNode(1, new TreeNode(2), new TreeNode(3));
            var t2 = new TreeNode(1, new TreeNode(2), new TreeNode(3));
            sol.IsSameTree(t1, t2).Dump("IsSameTree (expected: True)");
            """,
            TestCases = new List<TestCaseItem>
            {
                new() { Name = "[1,2,3], [1,2,3]", Input = "[1,2,3], [1,2,3]", ExpectedOutput = "True" }
            }
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
            HasVisualizer = true,
            VisualizerKind = "Tree",
            Tags = new List<string> { "Tree", "DFS", "Binary Tree" },
            TimeComplexity = "O(N * M)",
            SpaceComplexity = "O(H)",
            DescriptionMarkdown = """
            Given the roots of two binary trees `root` and `subRoot`, return `true` if there is a subtree of `root` with the same structure and node values of `subRoot` and `false` otherwise.
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
                public bool IsSubtree(TreeNode root, TreeNode subRoot) 
                {
                    if (root == null) return false;
                    if (IsSame(root, subRoot)) return true;
                    return IsSubtree(root.left, subRoot) || IsSubtree(root.right, subRoot);
                }

                private bool IsSame(TreeNode p, TreeNode q)
                {
                    if (p == null && q == null) return true;
                    if (p == null || q == null || p.val != q.val) return false;
                    return IsSame(p.left, q.left) && IsSame(p.right, q.right);
                }
            }

            var sol = new Solution();
            var r = new TreeNode(3, new TreeNode(4, new TreeNode(1), new TreeNode(2)), new TreeNode(5));
            var s = new TreeNode(4, new TreeNode(1), new TreeNode(2));
            sol.IsSubtree(r, s).Dump("IsSubtree (expected: True)");
            """,
            TestCases = new List<TestCaseItem>
            {
                new() { Name = "Valid Subtree", Input = "root=[3,4,5,1,2], sub=[4,1,2]", ExpectedOutput = "True" }
            }
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
            HasVisualizer = true,
            VisualizerKind = "Tree",
            Tags = new List<string> { "Tree", "DFS", "BST", "Binary Tree" },
            TimeComplexity = "O(H)",
            SpaceComplexity = "O(1)",
            DescriptionMarkdown = """
            Given a binary search tree (BST), find the lowest common ancestor (LCA) node of two given nodes in the BST.
            """,
            StarterCode = """
            public class TreeNode 
            {
                public int val;
                public TreeNode left;
                public TreeNode right;
                public TreeNode(int x) { val = x; }
            }

            public class Solution 
            {
                public TreeNode LowestCommonAncestor(TreeNode root, TreeNode p, TreeNode q) 
                {
                    var curr = root;
                    while (curr != null)
                    {
                        if (p.val > curr.val && q.val > curr.val) curr = curr.right;
                        else if (p.val < curr.val && q.val < curr.val) curr = curr.left;
                        else return curr;
                    }
                    return null;
                }
            }

            var sol = new Solution();
            var root = new TreeNode(6);
            root.left = new TreeNode(2); root.right = new TreeNode(8);
            sol.LowestCommonAncestor(root, root.left, root.right).val.Dump("LCA of 2 and 8");
            """,
            TestCases = new List<TestCaseItem>
            {
                new() { Name = "LCA 2 and 8", Input = "[6,2,8], 2, 8", ExpectedOutput = "6" }
            }
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
            HasVisualizer = true,
            VisualizerKind = "Tree",
            Tags = new List<string> { "Tree", "DFS", "Binary Tree" },
            TimeComplexity = "O(N)",
            SpaceComplexity = "O(H)",
            DescriptionMarkdown = """
            Given a binary tree, find the lowest common ancestor (LCA) of two given nodes in the tree.
            """,
            StarterCode = """
            public class TreeNode 
            {
                public int val;
                public TreeNode left;
                public TreeNode right;
                public TreeNode(int x) { val = x; }
            }

            public class Solution 
            {
                public TreeNode LowestCommonAncestor(TreeNode root, TreeNode p, TreeNode q) 
                {
                    if (root == null || root == p || root == q) return root;
                    var left = LowestCommonAncestor(root.left, p, q);
                    var right = LowestCommonAncestor(root.right, p, q);
                    if (left != null && right != null) return root;
                    return left ?? right;
                }
            }

            var sol = new Solution();
            var root = new TreeNode(3);
            root.left = new TreeNode(5); root.right = new TreeNode(1);
            sol.LowestCommonAncestor(root, root.left, root.right).val.Dump("LCA of 5 and 1");
            """,
            TestCases = new List<TestCaseItem>
            {
                new() { Name = "LCA 5 and 1", Input = "[3,5,1], 5, 1", ExpectedOutput = "3" }
            }
        }
    };
}
