using System.Collections.Generic;
using PdfEditorApp.Plugins.CSharpEditor.Models;

namespace PdfEditorApp.Plugins.CSharpEditor.Services;

public static partial class Blind75CatalogService
{
    private static IEnumerable<BlindProblemItem> GetTrieAndBacktrackingProblems() => new List<BlindProblemItem>
    {
        new()
        {
            Id = "blind75_208_implement_trie",
            Number = 208,
            Title = "Implement Trie (Prefix Tree)",
            Category = "Tries",
            Difficulty = ProblemDifficulty.Medium,
            AcceptanceRate = 66.5,
            IsPremium = false,
            Tags = new List<string> { "Hash Table", "String", "Design", "Trie" },
            TimeComplexity = "O(L) per operation",
            SpaceComplexity = "O(N * L)",
            DescriptionMarkdown = """
            A **trie** (pronounced as "try") or **prefix tree** is a tree data structure used to efficiently store and retrieve keys in a dataset of strings.
            Implement the `Trie` class with `Insert`, `Search`, and `StartsWith`.
            """,
            StarterCode = """
            public class TrieNode 
            {
                public TrieNode[] Children = new TrieNode[26];
                public bool IsEnd;
            }

            public class Trie 
            {
                private readonly TrieNode _root = new();

                public void Insert(string word) 
                {
                    var curr = _root;
                    foreach (var c in word)
                    {
                        int idx = c - 'a';
                        curr.Children[idx] ??= new TrieNode();
                        curr = curr.Children[idx];
                    }
                    curr.IsEnd = true;
                }

                public bool Search(string word) 
                {
                    var node = Find(word);
                    return node != null && node.IsEnd;
                }

                public bool StartsWith(string prefix) => Find(prefix) != null;

                private TrieNode Find(string prefix)
                {
                    var curr = _root;
                    foreach (var c in prefix)
                    {
                        curr = curr.Children[c - 'a'];
                        if (curr == null) return null;
                    }
                    return curr;
                }
            }

            var trie = new Trie();
            trie.Insert("apple");
            trie.Search("apple").Dump("Search apple (expected: True)");
            trie.Search("app").Dump("Search app (expected: False)");
            trie.StartsWith("app").Dump("StartsWith app (expected: True)");
            """,
            TestCases = new List<TestCaseItem>
            {
                new() { Name = "apple / app test", Input = "Insert('apple'), Search('apple'), Search('app'), StartsWith('app')", ExpectedOutput = "True, False, True" }
            }
        },
        new()
        {
            Id = "blind75_211_design_add_and_search_words_data_structure",
            Number = 211,
            Title = "Design Add and Search Words Data Structure",
            Category = "Tries",
            Difficulty = ProblemDifficulty.Medium,
            AcceptanceRate = 45.0,
            IsPremium = false,
            Tags = new List<string> { "String", "Depth-First Search", "Design", "Trie" },
            TimeComplexity = "O(M) add, O(26^M) worst-case search with dots",
            SpaceComplexity = "O(N * M)",
            DescriptionMarkdown = """
            Design a data structure that supports adding new words and finding if a string matches any previously added string.
            `bool Search(string word)`: `word` may contain dots `'.'` where dots can match any letter.
            """,
            StarterCode = """
            public class WordDictionary 
            {
                private class Node 
                {
                    public Node[] Children = new Node[26];
                    public bool IsEnd;
                }

                private readonly Node _root = new();

                public void AddWord(string word) 
                {
                    var curr = _root;
                    foreach (var c in word)
                    {
                        curr.Children[c - 'a'] ??= new Node();
                        curr = curr.Children[c - 'a'];
                    }
                    curr.IsEnd = true;
                }

                public bool Search(string word) => Dfs(word, 0, _root);

                private bool Dfs(string word, int idx, Node node)
                {
                    if (node == null) return false;
                    if (idx == word.Length) return node.IsEnd;
                    char c = word[idx];
                    if (c == '.')
                    {
                        for (int i = 0; i < 26; i++)
                        {
                            if (node.Children[i] != null && Dfs(word, idx + 1, node.Children[i])) return true;
                        }
                        return false;
                    }
                    return Dfs(word, idx + 1, node.Children[c - 'a']);
                }
            }

            var dict = new WordDictionary();
            dict.AddWord("bad"); dict.AddWord("dad"); dict.AddWord("mad");
            dict.Search(".ad").Dump("Search .ad (expected: True)");
            dict.Search("b..").Dump("Search b.. (expected: True)");
            """,
            TestCases = new List<TestCaseItem>
            {
                new() { Name = ".ad match", Input = "Add(bad, dad, mad), Search(.ad)", ExpectedOutput = "True" }
            }
        },
        new()
        {
            Id = "blind75_212_word_search_ii",
            Number = 212,
            Title = "Word Search II",
            Category = "Tries",
            Difficulty = ProblemDifficulty.Hard,
            AcceptanceRate = 37.0,
            IsPremium = false,
            Tags = new List<string> { "Array", "String", "Backtracking", "Trie", "Matrix" },
            TimeComplexity = "O(M * N * 4^L)",
            SpaceComplexity = "O(Total letters in words)",
            DescriptionMarkdown = """
            Given an `m x n` `board` of characters and a list of strings `words`, return *all words on the board*.
            """,
            StarterCode = """
            using System.Collections.Generic;

            public class Solution 
            {
                private class TrieNode 
                {
                    public TrieNode[] Children = new TrieNode[26];
                    public string Word;
                }

                public IList<string> FindWords(char[][] board, string[] words) 
                {
                    var root = new TrieNode();
                    foreach (var w in words)
                    {
                        var curr = root;
                        foreach (var c in w)
                        {
                            curr.Children[c - 'a'] ??= new TrieNode();
                            curr = curr.Children[c - 'a'];
                        }
                        curr.Word = w;
                    }

                    var res = new List<string>();
                    int m = board.Length, n = board[0].Length;

                    void Dfs(int r, int c, TrieNode node)
                    {
                        if (r < 0 || r >= m || c < 0 || c >= n || board[r][c] == '#' || node.Children[board[r][c] - 'a'] == null) return;
                        char ch = board[r][c];
                        node = node.Children[ch - 'a'];
                        if (node.Word != null)
                        {
                            res.Add(node.Word);
                            node.Word = null;
                        }
                        board[r][c] = '#';
                        Dfs(r + 1, c, node); Dfs(r - 1, c, node); Dfs(r, c + 1, node); Dfs(r, c - 1, node);
                        board[r][c] = ch;
                    }

                    for (int r = 0; r < m; r++)
                        for (int c = 0; c < n; c++)
                            Dfs(r, c, root);

                    return res;
                }
            }

            var sol = new Solution();
            var board = new[] {
                new[] { 'o','a','a','n' },
                new[] { 'e','t','a','e' },
                new[] { 'i','h','k','r' },
                new[] { 'i','f','l','v' }
            };
            sol.FindWords(board, new[] { "oath","pea","eat","rain" }).Dump("Found Words");
            """,
            TestCases = new List<TestCaseItem>
            {
                new() { Name = "Standard matrix", Input = "board, [oath, pea, eat, rain]", ExpectedOutput = "[oath, eat]" }
            }
        },
        new()
        {
            Id = "blind75_39_combination_sum",
            Number = 39,
            Title = "Combination Sum",
            Category = "Backtracking",
            Difficulty = ProblemDifficulty.Medium,
            AcceptanceRate = 71.2,
            IsPremium = false,
            Tags = new List<string> { "Array", "Backtracking" },
            TimeComplexity = "O(2^T)",
            SpaceComplexity = "O(T)",
            DescriptionMarkdown = """
            Given an array of **distinct** integers `candidates` and a target integer `target`, return a list of all **unique combinations** of `candidates` where the chosen numbers sum to `target`.
            """,
            StarterCode = """
            using System.Collections.Generic;

            public class Solution 
            {
                public IList<IList<int>> CombinationSum(int[] candidates, int target) 
                {
                    var res = new List<IList<int>>();
                    var curr = new List<int>();

                    void Backtrack(int idx, int remaining)
                    {
                        if (remaining == 0) { res.Add(new List<int>(curr)); return; }
                        if (remaining < 0 || idx == candidates.Length) return;

                        curr.Add(candidates[idx]);
                        Backtrack(idx, remaining - candidates[idx]);
                        curr.RemoveAt(curr.Count - 1);

                        Backtrack(idx + 1, remaining);
                    }

                    Backtrack(0, target);
                    return res;
                }
            }

            var sol = new Solution();
            sol.CombinationSum(new[] { 2, 3, 6, 7 }, 7).Dump("CombinationSum (target=7)");
            """,
            TestCases = new List<TestCaseItem>
            {
                new() { Name = "target 7", Input = "[2,3,6,7], 7", ExpectedOutput = "[[2,2,3],[7]]" }
            }
        },
        new()
        {
            Id = "blind75_79_word_search",
            Number = 79,
            Title = "Word Search",
            Category = "Backtracking",
            Difficulty = ProblemDifficulty.Medium,
            AcceptanceRate = 44.0,
            IsPremium = false,
            Tags = new List<string> { "Array", "String", "Backtracking", "Matrix" },
            TimeComplexity = "O(M * N * 3^L)",
            SpaceComplexity = "O(L)",
            DescriptionMarkdown = """
            Given an `m x n` grid of characters `board` and a string `word`, return `true` if `word` exists in the grid.
            """,
            StarterCode = """
            public class Solution 
            {
                public bool Exist(char[][] board, string word) 
                {
                    int m = board.Length, n = board[0].Length;

                    bool Dfs(int r, int c, int idx)
                    {
                        if (idx == word.Length) return true;
                        if (r < 0 || r >= m || c < 0 || c >= n || board[r][c] != word[idx]) return false;
                        char temp = board[r][c];
                        board[r][c] = '#';
                        bool found = Dfs(r + 1, c, idx + 1) || Dfs(r - 1, c, idx + 1) || Dfs(r, c + 1, idx + 1) || Dfs(r, c - 1, idx + 1);
                        board[r][c] = temp;
                        return found;
                    }

                    for (int r = 0; r < m; r++)
                        for (int c = 0; c < n; c++)
                            if (Dfs(r, c, 0)) return true;

                    return false;
                }
            }

            var sol = new Solution();
            var board = new[] {
                new[] { 'A','B','C','E' },
                new[] { 'S','F','C','S' },
                new[] { 'A','D','E','E' }
            };
            sol.Exist(board, "ABCCED").Dump("Exist ABCCED (expected: True)");
            """,
            TestCases = new List<TestCaseItem>
            {
                new() { Name = "ABCCED", Input = "board, ABCCED", ExpectedOutput = "True" }
            }
        }
    };
}
