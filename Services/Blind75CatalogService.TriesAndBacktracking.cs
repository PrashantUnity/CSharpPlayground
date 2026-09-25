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
            TimeComplexity = "O(L) per call",
            SpaceComplexity = "O(total letters inserted)",
            DescriptionMarkdown = """
            A **trie** (said "try"), or **prefix tree**, stores strings so that anything sharing a prefix shares the path for that prefix. Autocomplete and spell checkers are built on it.

            Implement the `Trie` class:
            - `Trie()` creates an empty trie.
            - `void Insert(string word)` adds `word`.
            - `bool Search(string word)` returns `true` if `word` was inserted before (as a whole word).
            - `bool StartsWith(string prefix)` returns `true` if some inserted word starts with `prefix`.

            ### Example 1
            - **Input:** `["Trie","insert","search","search","startsWith","insert","search"]` with arguments `[[],["apple"],["apple"],["app"],["app"],["app"],["app"]]`
            - **Output:** `[null,null,true,false,true,null,true]`
            - **Why:** after inserting `"apple"`, `search("app")` is **false** (`"app"` is only a prefix, not a word yet) while `startsWith("app")` is **true**. Once `"app"` itself is inserted, `search("app")` becomes true. `null` is what the constructor and `insert` return.

            ### Example 2
            - **Input:** `["Trie","insert","insert","search","startsWith","startsWith"]` with arguments `[[],["car"],["cat"],["ca"],["ca"],["cb"]]`
            - **Output:** `[null,null,null,false,true,false]`
            - **Why:** `"car"` and `"cat"` share the path `c → a`; nothing continues with `b` after `c`.

            ### Constraints
            - `1 <= word.length, prefix.length <= 2000`
            - `word` and `prefix` contain only lowercase English letters.
            - At most `3 * 10^4` calls in total.
            """,
            ThinkingProcessMarkdown = """
            **What has to be fast:** "was this exact word inserted?" and "does any word start with this prefix?". A hash set answers the first instantly but the second only by scanning every word.

            1. **Share prefixes.** Put the letters on the edges of a tree: the root is the empty string, and following `a → p → p` spells `"app"`. `"apple"` and `"app"` then share their first three nodes, and a prefix question becomes "can I walk these letters from the root?"
            2. **A node needs two things:** its children, keyed by the next letter (`Dictionary<char, TrieNode>`, or an array of 26), and a flag saying **a word ends here**. The flag is what separates `search("app")` from `startsWith("app")`: after inserting only `"apple"`, the path `a → p → p` exists but its last node is not a word end.
            3. **Every operation is one walk from the root.**
               - `Insert`: walk the letters, creating any missing child, then set the end flag on the last node.
               - `Search`: walk; if a letter is missing, the answer is false; otherwise return the last node's end flag.
               - `StartsWith`: the same walk, but reaching the end is enough.
            4. **Cost:** each call touches one node per letter, so `O(L)` for a word of length `L`, however many words are stored.

            **Pattern to remember:** when many strings share prefixes, or you need prefix queries, reach for a trie. It also powers Design Add and Search Words (wildcards) and Word Search II (searching many words at once).

            **Common mistakes:** answering `search` with "the path exists" (that's `startsWith`); forgetting to set the end flag on insert; storing whole words at the root instead of one letter per level.
            """,
            Approaches = new List<ProblemApproachItem>
            {
                new()
                {
                    Name = "A hash set of whole words",
                    Kind = ApproachKind.Naive,
                    TimeComplexity = "Insert/Search O(L) · StartsWith O(N·L)",
                    SpaceComplexity = "O(total letters)",
                    Intuition = "Keep every inserted word in a `HashSet<string>`. `Search` is a set lookup; `StartsWith` checks every stored word.",
                    BottleneckExplanation = "Prefix questions scan all N words, because the set knows nothing about how words relate to each other.",
                    Code = """
                    class WordSetTrie
                    {
                        private readonly HashSet<string> _words = new();
                        public void Insert(string word) => _words.Add(word);
                        public bool Search(string word) => _words.Contains(word);
                        public bool StartsWith(string prefix) => _words.Any(w => w.StartsWith(prefix, StringComparison.Ordinal));
                    }

                    var words = new WordSetTrie();
                    words.Insert("apple");
                    Console.WriteLine(Judge.Format(new[] { words.Search("apple"), words.Search("app"), words.StartsWith("app") }));   // [true,false,true]
                    """
                },
                new()
                {
                    Name = "Trie nodes: children by letter plus an end-of-word flag",
                    Kind = ApproachKind.Optimal,
                    TimeComplexity = "O(L) per call",
                    SpaceComplexity = "O(total letters inserted)",
                    Intuition = "Each node maps the next letter to a child and knows whether a word ends there. Insert creates missing children along the word; Search and StartsWith walk the same path and differ only in whether the last node must end a word."
                }
            },
            SolutionCode = """
            public class TrieNode
            {
                public Dictionary<char, TrieNode> Children { get; } = new();
                public bool IsEndOfWord { get; set; }
            }

            public class Trie
            {
                private readonly TrieNode _root = new();

                public void Insert(string word)
                {
                    var node = _root;
                    foreach (char c in word)
                    {
                        if (!node.Children.TryGetValue(c, out var next))
                        {
                            next = new TrieNode();
                            node.Children[c] = next;
                        }
                        node = next;
                    }
                    node.IsEndOfWord = true;
                }

                public bool Search(string word) => Walk(word) is { IsEndOfWord: true };

                public bool StartsWith(string prefix) => Walk(prefix) != null;

                // Follows the letters from the root; null as soon as one is missing.
                private TrieNode Walk(string letters)
                {
                    var node = _root;
                    foreach (char c in letters)
                    {
                        if (!node.Children.TryGetValue(c, out var next)) return null;
                        node = next;
                    }
                    return node;
                }
            }
            """,
            TestSetupCode = """
            // Replays LeetCode's operation list on a fresh Trie; void calls answer null, as in LeetCode's output.
            List<object> Replay(string[] operations, string[] arguments)
            {
                Trie trie = null;
                var answers = new List<object>();
                for (int i = 0; i < operations.Length; i++)
                {
                    switch (operations[i])
                    {
                        case "Trie": trie = new Trie(); answers.Add(null); break;
                        case "insert": trie.Insert(arguments[i]); answers.Add(null); break;
                        case "search": answers.Add(trie.Search(arguments[i])); break;
                        case "startsWith": answers.Add(trie.StartsWith(arguments[i])); break;
                    }
                }
                return answers;
            }
            """,
            Tests = new List<BlindTest>
            {
                new()
                {
                    Name = "Example 1",
                    Input = """["Trie","insert","search","search","startsWith","insert","search"], [[],["apple"],["apple"],["app"],["app"],["app"],["app"]]""",
                    Expected = "[null,null,true,false,true,null,true]",
                    Call = """Replay(new[] { "Trie", "insert", "search", "search", "startsWith", "insert", "search" }, new[] { "", "apple", "apple", "app", "app", "app", "app" })"""
                },
                new()
                {
                    Name = "Example 2",
                    Input = """["Trie","insert","insert","search","startsWith","startsWith"], [[],["car"],["cat"],["ca"],["ca"],["cb"]]""",
                    Expected = "[null,null,null,false,true,false]",
                    Call = """Replay(new[] { "Trie", "insert", "insert", "search", "startsWith", "startsWith" }, new[] { "", "car", "cat", "ca", "ca", "cb" })"""
                }
            },
            ExtraTests = new List<BlindTest>
            {
                new()
                {
                    Name = "Empty trie",
                    Input = """["Trie","search","startsWith"], [[],["a"],["a"]]""",
                    Expected = "[null,false,false]",
                    Call = """Replay(new[] { "Trie", "search", "startsWith" }, new[] { "", "a", "a" })"""
                },
                new()
                {
                    Name = "A word is its own prefix",
                    Input = """["Trie","insert","search","startsWith","search"], [[],["ab"],["ab"],["ab"],["abc"]]""",
                    Expected = "[null,null,true,true,false]",
                    Call = """Replay(new[] { "Trie", "insert", "search", "startsWith", "search" }, new[] { "", "ab", "ab", "ab", "abc" })"""
                },
                new()
                {
                    Name = "Inserting twice changes nothing",
                    Input = """["Trie","insert","insert","search","search"], [[],["go"],["go"],["go"],["g"]]""",
                    Expected = "[null,null,null,true,false]",
                    Call = """Replay(new[] { "Trie", "insert", "insert", "search", "search" }, new[] { "", "go", "go", "go", "g" })"""
                }
            },
            VisualizerKind = "Tree",
            VisualizationDescription = """
            Every word is a path of letters from the root (•). Watch `insert("apple")` create five nodes, then see
            `search("app")` fail although the path exists (its last `p` does not end a word) while `startsWith("app")`
            succeeds. After `insert("app")` the same `p` turns green. Newly created letters are outlined in lime, the
            letters walked by the current call are highlighted, and each word end shows the word it completes.
            """,
            VisualizationCode = """
            var root = new TrieNode();
            var tracker = TrieTracker.Create(root, title: "208. Implement Trie: every word is a path of letters");

            void Insert(string word)
            {
                var node = root;
                for (int i = 0; i < word.Length; i++)
                {
                    char c = word[i];
                    bool created = !node.Children.TryGetValue(c, out var next);
                    if (created)
                    {
                        next = new TrieNode();
                        node.Children[c] = next;
                    }
                    node = next;
                    tracker.Step(created
                        ? $"insert(\"{word}\"): no '{c}' here yet, so create it"
                        : $"insert(\"{word}\"): '{c}' is already there (a shared prefix), reuse it", path: word[..(i + 1)]);
                }
                node.IsEndOfWord = true;
                tracker.Step($"insert(\"{word}\"): mark this '{word[^1]}' as the end of a word", path: word);
            }

            bool Find(string letters, bool wholeWord)
            {
                string call = wholeWord ? $"search(\"{letters}\")" : $"startsWith(\"{letters}\")";
                var node = root;
                for (int i = 0; i < letters.Length; i++)
                {
                    if (!node.Children.TryGetValue(letters[i], out var next))
                    {
                        tracker.Step($"{call}: no '{letters[i]}' after \"{letters[..i]}\", so the answer is false", path: letters[..i]);
                        return false;
                    }
                    node = next;
                }
                bool answer = !wholeWord || node.IsEndOfWord;
                tracker.Step(wholeWord
                    ? $"{call}: the path exists and its last letter {(node.IsEndOfWord ? "ends a word" : "does NOT end a word")}, so {(answer ? "true" : "false")}"
                    : $"{call}: the path exists, so some word starts with it: true", path: letters);
                return answer;
            }

            Insert("apple");
            Find("apple", wholeWord: true);
            Find("app", wholeWord: true);
            Find("app", wholeWord: false);
            Insert("app");
            Find("app", wholeWord: true);
            Insert("ant");
            Find("bat", wholeWord: false);

            Display.Visualizer(tracker);
            """
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
            TimeComplexity = "O(L) to add; O(L) to search without dots, up to O(26^d · L) with d dots",
            SpaceComplexity = "O(total letters added)",
            DescriptionMarkdown = """
            Design a `WordDictionary` that supports:

            - `AddWord(word)`: store `word`.
            - `Search(word)`: return `true` if some stored word matches `word`, where a dot `.` matches **any one letter**.

            ### Example 1
            - **Input:** `["WordDictionary","addWord","addWord","addWord","search","search","search","search"]`, `[[],["bad"],["dad"],["mad"],["pad"],["bad"],[".ad"],["b.."]]`
            - **Output:** `[null,null,null,null,false,true,true,true]`
            - **Why:** `"pad"` was never added; `".ad"` matches `bad`, `dad` or `mad`; `"b.."` matches `bad`.

            ### Example 2
            - **Input:** `["WordDictionary","addWord","addWord","search","search","search","search"]`, `[[],["at"],["and"],["a."],["."],["a.d"],["an."]]`
            - **Output:** `[null,null,null,true,false,true,true]`
            - **Why:** a dot stands for exactly one letter, so `"."` needs a one-letter word and there is none.

            ### Constraints
            - `1 <= word.length <= 25`
            - Added words are lowercase letters; searched words are lowercase letters or `.`.
            - At most 2 dots per search, and at most `10^4` calls in total.
            """,
            ThinkingProcessMarkdown = """
            **Restate it:** problem 208's trie, except `search` may contain wildcards.

            1. **Store the words in a trie** (problem 208): each word is a path of letters from the root, and its last node is marked as a word end. Adding costs one step per letter.
            2. **A normal letter** in the search follows exactly one child, like a trie lookup.
            3. **A dot** could be any letter, so try **every child** of the current node and continue the search from each. That is depth-first search with backtracking: as soon as one branch succeeds, stop.
            4. **At the end of the pattern,** the node must be a word end: `"ba"` is a path inside `"bad"`, but it isn't a word.
            5. **Walk `".ad"`** with `bad, dad, mad`: the dot tries `b`, then `a` and `d` follow, and `bad` ends a word → `true` without trying `d` or `m`.

            **Pattern to remember:** a trie turns "does any stored word match?" into a walk; wildcards turn the walk into DFS over the children.

            **Common mistakes:** returning `true` when the pattern runs out on a node that isn't a word end; letting a dot match zero letters; scanning every stored word for every search.
            """,
            Approaches = new List<ProblemApproachItem>
            {
                new()
                {
                    Name = "Keep a list of words and compare letter by letter",
                    Kind = ApproachKind.Naive,
                    TimeComplexity = "O(1) to add, O(n · L) to search",
                    SpaceComplexity = "O(total letters)",
                    Intuition = "Store the words in a list. A search checks every word of the right length, letter by letter, where a dot matches anything.",
                    BottleneckExplanation = "Every search compares against every stored word, even ones that differ in the first letter; a trie discards them all in one step.",
                    Code = """
                    class WordListDictionary
                    {
                        private readonly List<string> _words = new();
                        public void AddWord(string word) => _words.Add(word);
                        public bool Search(string pattern) =>
                            _words.Any(w => w.Length == pattern.Length && w.Zip(pattern).All(pair => pair.Second == '.' || pair.First == pair.Second));
                    }

                    var list = new WordListDictionary();
                    foreach (var word in new[] { "bad", "dad", "mad" }) list.AddWord(word);
                    Console.WriteLine(Judge.Format(new[] { "pad", "bad", ".ad", "b.." }.Select(list.Search)));   // [false,true,true,true]
                    """
                },
                new()
                {
                    Name = "A trie, with depth-first search for the dots",
                    Kind = ApproachKind.Optimal,
                    TimeComplexity = "O(L) add; O(L) search without dots",
                    SpaceComplexity = "O(total letters)",
                    Intuition = "Add words to a trie. Search follows one child per letter, and for a dot tries each child in turn, stopping at the first branch that ends on a word."
                }
            },
            SolutionCode = """
            public class WordNode
            {
                public Dictionary<char, WordNode> Children { get; } = new();
                public bool IsWord { get; set; }
            }

            public class WordDictionary
            {
                private readonly WordNode _root = new();

                public void AddWord(string word)
                {
                    var node = _root;
                    foreach (char c in word)
                    {
                        if (!node.Children.TryGetValue(c, out var next)) node.Children[c] = next = new WordNode();
                        node = next;
                    }
                    node.IsWord = true;
                }

                public bool Search(string word) => Search(word, 0, _root);

                // Can word[i..] be spelled from this node? A dot may take any child.
                private bool Search(string word, int i, WordNode node)
                {
                    if (i == word.Length) return node.IsWord;
                    if (word[i] != '.')
                        return node.Children.TryGetValue(word[i], out var next) && Search(word, i + 1, next);

                    foreach (var child in node.Children.Values)
                        if (Search(word, i + 1, child)) return true;   // try every letter for the dot
                    return false;
                }
            }
            """,
            TestSetupCode = """
            // Replays LeetCode's operation list; void calls answer null, as in LeetCode's output.
            List<object> Replay(string[] operations, string[] arguments)
            {
                WordDictionary dictionary = null;
                var answers = new List<object>();
                for (int i = 0; i < operations.Length; i++)
                {
                    switch (operations[i])
                    {
                        case "WordDictionary": dictionary = new WordDictionary(); answers.Add(null); break;
                        case "addWord": dictionary.AddWord(arguments[i]); answers.Add(null); break;
                        case "search": answers.Add(dictionary.Search(arguments[i])); break;
                    }
                }
                return answers;
            }
            """,
            Tests = new List<BlindTest>
            {
                new()
                {
                    Name = "Example 1",
                    Input = """["WordDictionary","addWord","addWord","addWord","search","search","search","search"], [[],["bad"],["dad"],["mad"],["pad"],["bad"],[".ad"],["b.."]]""",
                    Expected = "[null,null,null,null,false,true,true,true]",
                    Call = """Replay(new[] { "WordDictionary", "addWord", "addWord", "addWord", "search", "search", "search", "search" }, new[] { "", "bad", "dad", "mad", "pad", "bad", ".ad", "b.." })"""
                },
                new()
                {
                    Name = "Example 2",
                    Input = """["WordDictionary","addWord","addWord","search","search","search","search"], [[],["at"],["and"],["a."],["."],["a.d"],["an."]]""",
                    Expected = "[null,null,null,true,false,true,true]",
                    Call = """Replay(new[] { "WordDictionary", "addWord", "addWord", "search", "search", "search", "search" }, new[] { "", "at", "and", "a.", ".", "a.d", "an." })"""
                }
            },
            ExtraTests = new List<BlindTest>
            {
                new()
                {
                    Name = "Nothing added yet",
                    Input = """["WordDictionary","search"], [[],["."]]""",
                    Expected = "[null,false]",
                    Call = """Replay(new[] { "WordDictionary", "search" }, new[] { "", "." })"""
                },
                new()
                {
                    Name = "Dots only",
                    Input = """["WordDictionary","addWord","search","search"], [[],["xyz"],["..."],["...."]]""",
                    Expected = "[null,null,true,false]",
                    Call = """Replay(new[] { "WordDictionary", "addWord", "search", "search" }, new[] { "", "xyz", "...", "...." })"""
                },
                new()
                {
                    Name = "A prefix is not a word",
                    Input = """["WordDictionary","addWord","search","search"], [[],["apple"],["app"],["app.."]]""",
                    Expected = "[null,null,false,true]",
                    Call = """Replay(new[] { "WordDictionary", "addWord", "search", "search" }, new[] { "", "apple", "app", "app.." })"""
                }
            },
            StressTestCode = """
            judge.Agree("Random words and patterns vs a plain word list",
                random =>
                {
                    string Word(int length, string letters) => new string(Enumerable.Range(0, length).Select(_ => letters[random.Next(letters.Length)]).ToArray());
                    var added = Enumerable.Range(0, random.Next(0, 6)).Select(_ => Word(random.Next(1, 4), "ab")).ToArray();
                    var patterns = Enumerable.Range(0, 5).Select(_ => Word(random.Next(1, 4), "ab.")).ToArray();
                    return (added, patterns);
                },
                input => { var list = new WordListDictionary(); foreach (var w in input.added) list.AddWord(w); return input.patterns.Select(list.Search).ToList(); },
                input => { var trie = new WordDictionary(); foreach (var w in input.added) trie.AddWord(w); return input.patterns.Select(trie.Search).ToList(); });
            """,
            VisualizerKind = "Tree",
            VisualizationDescription = """
            Example 1's words in a trie (word ends are green), then the searches. A plain letter follows one branch; a
            dot tries the children one by one, and the highlighted letters are the path currently being tried. The
            extra search `".ab"` shows a dot backtracking through every branch before answering false.
            """,
            VisualizationCode = """
            var root = new WordNode();
            var tracker = TrieTracker.Create(root, title: "211. Add and Search Words: a dot tries every branch");
            string current = "";
            tracker.Watch(() => current);

            void AddWord(string word)
            {
                var node = root;
                foreach (char c in word)
                {
                    if (!node.Children.TryGetValue(c, out var next)) node.Children[c] = next = new WordNode();
                    node = next;
                }
                node.IsWord = true;
                current = $"addWord(\"{word}\")";
                tracker.Step($"addWord(\"{word}\"): its letters become a path from the root; the last one marks a word end", path: word);
            }

            bool Search(string pattern, int i, WordNode node, string walked)
            {
                if (i == pattern.Length)
                {
                    tracker.Step(node.IsWord ? $"The pattern is used up on \"{walked}\", which is a word: true" : $"\"{walked}\" fits the pattern but is not a whole word", path: walked);
                    return node.IsWord;
                }
                if (pattern[i] != '.')
                {
                    if (!node.Children.TryGetValue(pattern[i], out var next))
                    {
                        tracker.Step(walked.Length == 0 ? $"No word starts with '{pattern[i]}'" : $"No '{pattern[i]}' after \"{walked}\": this branch fails", path: walked);
                        return false;
                    }
                    tracker.Step(walked.Length == 0 ? $"'{pattern[i]}' is a first letter in the trie" : $"'{pattern[i]}' follows \"{walked}\"", path: walked + pattern[i]);
                    return Search(pattern, i + 1, next, walked + pattern[i]);
                }

                foreach (var (letter, child) in node.Children)
                {
                    tracker.Step($"The dot at position {i} tries '{letter}'", path: walked + letter);
                    if (Search(pattern, i + 1, child, walked + letter)) return true;
                }
                tracker.Step(walked.Length == 0 ? "No letter works for the first dot" : $"No letter works for the dot after \"{walked}\": back up", path: walked);
                return false;
            }

            foreach (var word in new[] { "bad", "dad", "mad" }) AddWord(word);
            foreach (var pattern in new[] { "pad", "bad", ".ad", "b..", ".ab" })
            {
                current = $"search(\"{pattern}\")";
                bool answer = Search(pattern, 0, root, "");
                tracker.Step($"search(\"{pattern}\") returns {(answer ? "true" : "false")}");
            }
            Display.Visualizer(tracker);
            """
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
            TimeComplexity = "O(m · n · 4 · 3^(L−1)) worst case, usually far less",
            SpaceComplexity = "O(total letters in words)",
            DescriptionMarkdown = """
            Given an `m × n` `board` of letters and a list of `words`, return **every word that can be found on the board**.

            A word is spelled by moving between **horizontally or vertically neighbouring** cells, and a cell can be used **at most once** within the same word. The answer may be in any order.

            ### Example 1
            - **Input:** `board = [["o","a","a","n"],["e","t","a","e"],["i","h","k","r"],["i","f","l","v"]], words = ["oath","pea","eat","rain"]`
            - **Output:** `["eat","oath"]`
            - **Why:** `oath` runs o → a → t → h down from the top-left corner and `eat` starts at the `e` on the right; there is no `p` for `pea`, and `rain` can't be connected.

            ### Example 2
            - **Input:** `board = [["a","b"],["c","d"]], words = ["abcb"]`
            - **Output:** `[]`

            ### Constraints
            - `1 <= m, n <= 12`
            - `1 <= words.length <= 3 * 10^4`, `1 <= words[i].length <= 10`
            - Letters are lowercase English letters, and all words are distinct.
            """,
            ThinkingProcessMarkdown = """
            **Restate it:** Word Search (problem 79), but for many words at once.

            1. **One word at a time** means running a full board search per word: with `3 · 10^4` words that repeats the same work again and again, especially for words sharing a prefix (`oath`, `oat`, `oats`).
            2. **Put all the words in a trie** (problem 208). Now one board search can follow the trie **alongside**: from a cell, you may only step to a neighbour whose letter is a child of the current trie node.
            3. **The trie prunes the search:** the moment the letters so far aren't the start of any word, that path stops. And when a trie node marks the end of a word, record it.
            4. **Details:** mark a cell as in use while it's on the current path (`#`) and restore it when backtracking; clear a word from the trie once found so it is reported only once.
            5. **Walk Example 1:** from `o`, the trie allows `a`, then `t`, then `h` → `oath`. The `e` on the left can't continue (`eat` needs an `a` next to it), but the `e` on the right reaches `a`, then `t` → `eat`.

            **Pattern to remember:** many searches over the same space → share their common prefixes in a trie and search once.

            **Common mistakes:** reusing a cell within one word; reporting the same word twice; forgetting to restore the cell after backtracking.
            """,
            Approaches = new List<ProblemApproachItem>
            {
                new()
                {
                    Name = "Run Word Search once per word",
                    Kind = ApproachKind.Naive,
                    TimeComplexity = "O(w · m · n · 4^L)",
                    SpaceComplexity = "O(L)",
                    Intuition = "For each word, try every starting cell with the backtracking search from problem 79.",
                    BottleneckExplanation = "Words that share a prefix are searched separately, so the board is re-explored once per word.",
                    Code = """
                    List<string> FindWordsOneByOne(char[][] board, string[] words)
                    {
                        bool Exists(string word, int r, int c, int i)
                        {
                            if (i == word.Length) return true;
                            if (r < 0 || c < 0 || r >= board.Length || c >= board[0].Length || board[r][c] != word[i]) return false;
                            char saved = board[r][c];
                            board[r][c] = '#';
                            bool found = Exists(word, r + 1, c, i + 1) || Exists(word, r - 1, c, i + 1) || Exists(word, r, c + 1, i + 1) || Exists(word, r, c - 1, i + 1);
                            board[r][c] = saved;
                            return found;
                        }
                        return words.Where(word => Enumerable.Range(0, board.Length).Any(r => Enumerable.Range(0, board[0].Length).Any(c => Exists(word, r, c, 0)))).ToList();
                    }

                    Console.WriteLine(Judge.Format(FindWordsOneByOne(Board("oaan", "etae", "ihkr", "iflv"), new[] { "oath", "pea", "eat", "rain" })));   // ["oath","eat"]
                    """
                },
                new()
                {
                    Name = "One board search guided by a trie of all the words",
                    Kind = ApproachKind.Optimal,
                    TimeComplexity = "O(m · n · 4 · 3^(L−1)) worst case",
                    SpaceComplexity = "O(total letters)",
                    Intuition = "Build a trie of the words, then DFS from every cell while walking the trie: stop as soon as the letters aren't a prefix of any word, and record a word when its end is reached."
                }
            },
            SupportCode = """
            // Board("oaan", "etae") is the grid [["o","a","a","n"],["e","t","a","e"]].
            char[][] Board(params string[] rows) => rows.Select(row => row.ToCharArray()).ToArray();
            """,
            SolutionCode = """
            public class TrieNode
            {
                public Dictionary<char, TrieNode> Children { get; } = new();
                public string Word { get; set; }   // set on the node where a word ends
            }

            public class Solution
            {
                public IList<string> FindWords(char[][] board, string[] words)
                {
                    var root = new TrieNode();
                    foreach (var word in words)
                    {
                        var node = root;
                        foreach (char c in word)
                        {
                            if (!node.Children.TryGetValue(c, out var next)) node.Children[c] = next = new TrieNode();
                            node = next;
                        }
                        node.Word = word;
                    }

                    var found = new List<string>();
                    for (int r = 0; r < board.Length; r++)
                        for (int c = 0; c < board[0].Length; c++)
                            Search(board, r, c, root, found);
                    return found;
                }

                private void Search(char[][] board, int r, int c, TrieNode parent, List<string> found)
                {
                    if (r < 0 || c < 0 || r >= board.Length || c >= board[0].Length) return;
                    char letter = board[r][c];
                    if (!parent.Children.TryGetValue(letter, out var node)) return;   // no word goes this way (or the cell is in use)

                    if (node.Word != null)
                    {
                        found.Add(node.Word);
                        node.Word = null;                                              // report each word once
                    }

                    board[r][c] = '#';                                                 // in use on the current path
                    Search(board, r + 1, c, node, found);
                    Search(board, r - 1, c, node, found);
                    Search(board, r, c + 1, node, found);
                    Search(board, r, c - 1, node, found);
                    board[r][c] = letter;                                              // backtrack
                }
            }
            """,
            Tests = new List<BlindTest>
            {
                new() { Name = "Example 1", Input = "board = [[\"o\",\"a\",\"a\",\"n\"],[\"e\",\"t\",\"a\",\"e\"],[\"i\",\"h\",\"k\",\"r\"],[\"i\",\"f\",\"l\",\"v\"]], words = [\"oath\",\"pea\",\"eat\",\"rain\"]", Expected = "[\"eat\",\"oath\"]", Call = "sol.FindWords(Board(\"oaan\", \"etae\", \"ihkr\", \"iflv\"), new[] { \"oath\", \"pea\", \"eat\", \"rain\" })", AnyOrder = true },
                new() { Name = "Example 2", Input = "board = [[\"a\",\"b\"],[\"c\",\"d\"]], words = [\"abcb\"]", Expected = "[]", Call = "sol.FindWords(Board(\"ab\", \"cd\"), new[] { \"abcb\" })", AnyOrder = true }
            },
            ExtraTests = new List<BlindTest>
            {
                new() { Name = "A single cell", Input = "board = [[\"a\"]], words = [\"a\"]", Expected = "[\"a\"]", Call = "sol.FindWords(Board(\"a\"), new[] { \"a\" })", AnyOrder = true },
                new() { Name = "A cell can't be used twice", Input = "board = [[\"a\",\"a\"]], words = [\"aaa\"]", Expected = "[]", Call = "sol.FindWords(Board(\"aa\"), new[] { \"aaa\" })", AnyOrder = true },
                new() { Name = "Words sharing a prefix", Input = "board = [[\"o\",\"a\",\"b\",\"n\"],[\"o\",\"t\",\"a\",\"e\"],[\"a\",\"h\",\"k\",\"r\"],[\"a\",\"f\",\"l\",\"v\"]], words = [\"oa\",\"oaa\"]", Expected = "[\"oa\",\"oaa\"]", Call = "sol.FindWords(Board(\"oabn\", \"otae\", \"ahkr\", \"aflv\"), new[] { \"oa\", \"oaa\" })", AnyOrder = true },
                new() { Name = "Found in two places, reported once", Input = "board = [[\"a\",\"b\"],[\"b\",\"a\"]], words = [\"ab\"]", Expected = "[\"ab\"]", Call = "sol.FindWords(Board(\"ab\", \"ba\"), new[] { \"ab\" })", AnyOrder = true }
            },
            StressTestCode = """
            judge.Agree("Random boards vs one search per word",
                random =>
                {
                    string Letters(int length) => new string(Enumerable.Range(0, length).Select(_ => "ab"[random.Next(2)]).ToArray());
                    var rows = Enumerable.Range(0, random.Next(1, 4)).Select(_ => Letters(3)).ToArray();
                    var words = Enumerable.Range(0, random.Next(1, 6)).Select(_ => Letters(random.Next(1, 5))).Distinct().ToArray();
                    return (rows, words);
                },
                input => (object)FindWordsOneByOne(Board(input.rows), input.words),
                input => sol.FindWords(Board(input.rows), input.words),
                anyOrder: true);
            """,
            VisualizerKind = "Matrix",
            VisualizationDescription = """
            Example 1's board. Every cell is a possible start, but the search only continues while the letters so far
            begin some word in the trie (the number on a cell is its position in the word). `oath` and `eat` are found
            and marked, `found` collects them, and each cell is freed again when the search backs out of it.
            """,
            VisualizationCode = """
            var board = Board("oaan", "etae", "ihkr", "iflv");
            var words = new[] { "oath", "pea", "eat", "rain" };
            var grid = MatrixTracker.Create(new[] { "oaan", "etae", "ihkr", "iflv" }, "212. Word Search II: walk the board and the trie together",
                new MatrixParseOptions { StateClassifier = _ => GridCellState.Default });

            var root = new TrieNode();
            foreach (var word in words)
            {
                var node = root;
                foreach (char c in word)
                {
                    if (!node.Children.TryGetValue(c, out var next)) node.Children[c] = next = new TrieNode();
                    node = next;
                }
                node.Word = word;
            }
            var found = new List<string>();
            grid.Watch(found);
            grid.Snapshot($"The trie holds {string.Join(", ", words)}. Try every cell as a start; a path continues only while its letters begin a word");

            var path = new List<(int Row, int Col)>();
            var foundCells = new List<(int Row, int Col)>();
            void Search(int r, int c, TrieNode parent, string prefix)
            {
                if (r < 0 || c < 0 || r >= board.Length || c >= board[0].Length) return;
                char letter = board[r][c];
                if (!parent.Children.TryGetValue(letter, out var node)) return;

                string letters = prefix + letter;
                path.Add((r, c));
                grid.Visit(r, c, $"\"{letters}\" is a path in the trie, so the search goes on from ({r},{c})", subLabel: letters.Length.ToString());
                if (node.Word != null)
                {
                    found.Add(node.Word);
                    foundCells.AddRange(path);
                    grid.MarkPath(path, $"\"{node.Word}\" ends here: found it!");
                    node.Word = null;
                }

                board[r][c] = '#';
                Search(r + 1, c, node, letters);
                Search(r - 1, c, node, letters);
                Search(r, c + 1, node, letters);
                Search(r, c - 1, node, letters);
                board[r][c] = letter;
                path.RemoveAt(path.Count - 1);
                grid.SetCell(r, c, subLabel: "");
                grid.Backtrack(r, c, $"No neighbour continues \"{letters}\" any further: free ({r},{c}) and back up", restoreState: GridCellState.Default);
            }

            for (int r = 0; r < board.Length; r++)
                for (int c = 0; c < board[0].Length; c++)
                    Search(r, c, root, "");

            grid.MarkPath(foundCells, $"Every cell has been a starting point: found {Judge.Format(found)}", color: "#15803d");
            Display.Visualizer(grid);
            """
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
            TimeComplexity = "Exponential in target / min(candidates)",
            SpaceComplexity = "O(target / min(candidates)) for the current combination",
            DescriptionMarkdown = """
            Given an array of **distinct** positive integers `candidates` and a `target`, return all **unique combinations** of candidates that add up to `target`. You may use the same number **as many times as you like**.

            Two combinations are the same if they use every number the same number of times (so `[2,2,3]` and `[3,2,2]` count once). Return them in any order.

            ### Example 1
            - **Input:** `candidates = [2,3,6,7], target = 7`
            - **Output:** `[[2,2,3],[7]]`
            - **Why:** `2 + 2 + 3 = 7`, and 7 on its own; nothing else works.

            ### Example 2
            - **Input:** `candidates = [2,3,5], target = 8`
            - **Output:** `[[2,2,2,2],[2,3,3],[3,5]]`

            ### Example 3
            - **Input:** `candidates = [2], target = 1`
            - **Output:** `[]`

            ### Constraints
            - `1 <= candidates.length <= 30`
            - `2 <= candidates[i] <= 40`, all distinct.
            - `1 <= target <= 40`
            """,
            ThinkingProcessMarkdown = """
            **Restate it:** list every multiset of candidates that adds up to `target`.

            1. **Build combinations one choice at a time.** Keep the numbers chosen so far and how much is still missing. Pick a candidate, subtract it, and recurse; when nothing is missing, record the combination.
            2. **Avoid duplicates like `[2,3,2]`:** only allow choices from the current candidate **onwards**. Each combination is then built in one order only (non-decreasing), so it's found once.
            3. **Allow reuse:** after choosing `candidates[i]`, recurse starting at `i` again, not `i + 1`.
            4. **Prune:** sort first; as soon as a candidate is bigger than what's missing, every later one is too, so stop the loop.
            5. **Undo after trying:** remove the last number before trying the next candidate. Choose → recurse → undo is the heart of backtracking.
            6. **Walk Example 1:** 2 → 2 → (2 is too much for the 1 left) → 3 gives `[2,2,3]`. Back up: 2 → 3 leaves 2, too little for 3. … Finally 7 alone gives `[7]`.

            **Pattern to remember:** "all combinations/subsets/permutations" → backtracking with a start index (to avoid duplicates) and pruning.

            **Common mistakes:** restarting the loop at 0 (produces permutations like `[2,3,2]`); recursing with `i + 1` (forbids reuse); forgetting to copy the combination when recording it.
            """,
            Approaches = new List<ProblemApproachItem>
            {
                new()
                {
                    Name = "Decide how many times to use each number",
                    Kind = ApproachKind.Alternative,
                    TimeComplexity = "Exponential",
                    SpaceComplexity = "O(target)",
                    Intuition = "Go through the candidates in order and, for each, choose a count 0, 1, 2… while it still fits. A combination is complete when every candidate has a count and nothing is missing.",
                    Code = """
                    List<List<int>> CombinationSumByCounts(int[] candidates, int target)
                    {
                        var result = new List<List<int>>();
                        void Pick(int index, int remaining, List<int> chosen)
                        {
                            if (index == candidates.Length)
                            {
                                if (remaining == 0) result.Add(chosen);
                                return;
                            }
                            for (int count = 0; count * candidates[index] <= remaining; count++)   // use candidates[index] count times
                            {
                                Pick(index + 1, remaining - count * candidates[index], chosen.Concat(Enumerable.Repeat(candidates[index], count)).ToList());
                            }
                        }
                        Pick(0, target, new List<int>());
                        return result;
                    }

                    Console.WriteLine(Judge.Format(CombinationSumByCounts(new[] { 2, 3, 6, 7 }, 7)));   // [[7],[2,2,3]]
                    """
                },
                new()
                {
                    Name = "Backtracking: choose, recurse, undo",
                    Kind = ApproachKind.Optimal,
                    TimeComplexity = "Exponential in target / min(candidates)",
                    SpaceComplexity = "O(target / min(candidates))",
                    Intuition = "Sort the candidates. From a start index, add each candidate that still fits, recurse with the same index (reuse allowed), then remove it again. Record the combination when nothing is missing."
                }
            },
            SolutionCode = """
            public class Solution
            {
                public IList<IList<int>> CombinationSum(int[] candidates, int target)
                {
                    Array.Sort(candidates);
                    var result = new List<IList<int>>();
                    var combination = new List<int>();

                    void Choose(int start, int remaining)
                    {
                        if (remaining == 0)
                        {
                            result.Add(new List<int>(combination));   // a copy: the list keeps changing
                            return;
                        }
                        for (int i = start; i < candidates.Length && candidates[i] <= remaining; i++)   // sorted: bigger ones won't fit either
                        {
                            combination.Add(candidates[i]);
                            Choose(i, remaining - candidates[i]);     // i again: the same number may be reused
                            combination.RemoveAt(combination.Count - 1);   // undo the choice
                        }
                    }

                    Choose(0, target);
                    return result;
                }
            }
            """,
            Tests = new List<BlindTest>
            {
                new() { Name = "Example 1", Input = "candidates = [2,3,6,7], target = 7", Expected = "[[2,2,3],[7]]", Call = "sol.CombinationSum(new[] { 2, 3, 6, 7 }, 7)", AnyOrder = true },
                new() { Name = "Example 2", Input = "candidates = [2,3,5], target = 8", Expected = "[[2,2,2,2],[2,3,3],[3,5]]", Call = "sol.CombinationSum(new[] { 2, 3, 5 }, 8)", AnyOrder = true },
                new() { Name = "Example 3", Input = "candidates = [2], target = 1", Expected = "[]", Call = "sol.CombinationSum(new[] { 2 }, 1)", AnyOrder = true }
            },
            ExtraTests = new List<BlindTest>
            {
                new() { Name = "The target is a candidate", Input = "candidates = [5], target = 5", Expected = "[[5]]", Call = "sol.CombinationSum(new[] { 5 }, 5)", AnyOrder = true },
                new() { Name = "Two ways", Input = "candidates = [2,3], target = 6", Expected = "[[2,2,2],[3,3]]", Call = "sol.CombinationSum(new[] { 2, 3 }, 6)", AnyOrder = true },
                new() { Name = "Unsorted input", Input = "candidates = [7,3,2,6], target = 7", Expected = "[[2,2,3],[7]]", Call = "sol.CombinationSum(new[] { 7, 3, 2, 6 }, 7)", AnyOrder = true },
                new() { Name = "One number many times", Input = "candidates = [3], target = 9", Expected = "[[3,3,3]]", Call = "sol.CombinationSum(new[] { 3 }, 9)", AnyOrder = true }
            },
            StressTestCode = """
            judge.Agree("Random inputs vs choosing counts",
                random => (candidates: Enumerable.Range(2, 8).OrderBy(_ => random.Next()).Take(random.Next(1, 5)).ToArray(), target: random.Next(1, 16)),
                input => (object)CombinationSumByCounts(input.candidates, input.target),
                input => sol.CombinationSum((int[])input.candidates.Clone(), input.target),
                anyOrder: true);
            """,
            VisualizerKind = "Tree",
            VisualizationDescription = """
            Example 1 as a tree of choices. Each call is labelled with the number added and what is still missing
            (`+2 → 5`). Calls that reach 0 are solutions (green ✓); a candidate bigger than what's missing is pruned (grey),
            and so is everything after it. `combination` shows the current choices and `result` the combinations found.
            """,
            VisualizationCode = """
            int[] candidates = { 2, 3, 6, 7 };
            int target = 7;
            Array.Sort(candidates);
            var calls = RecursionTracker.Create("39. Combination Sum: choose, recurse, undo");
            var combination = new List<int>();
            var result = new List<List<int>>();
            calls.Watch(combination);
            calls.Watch(result);

            void Choose(int start, int remaining, RecursionCall call)
            {
                if (remaining == 0)
                {
                    result.Add(new List<int>(combination));
                    call.Found($"[{string.Join(",", combination)}] adds up to {target}: record it");
                    return;
                }
                for (int i = start; i < candidates.Length; i++)
                {
                    if (candidates[i] > remaining)
                    {
                        calls.Enter($"+{candidates[i]}").Prune($"{candidates[i]} is more than the {remaining} still missing, and the candidates after it are bigger");
                        break;
                    }
                    combination.Add(candidates[i]);
                    Choose(i, remaining - candidates[i], calls.Enter($"+{candidates[i]} → {remaining - candidates[i]}"));
                    combination.RemoveAt(combination.Count - 1);
                }
                call.Done(combination.Count == 0
                    ? $"Every choice has been tried: {result.Count} combinations"
                    : $"Everything after [{string.Join(",", combination)}] has been tried: undo the last choice and go back");
            }

            Choose(0, target, calls.Enter($"{target} missing"));
            Display.Visualizer(calls);
            """
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
            TimeComplexity = "O(m · n · 3^L)",
            SpaceComplexity = "O(L) for the recursion",
            DescriptionMarkdown = """
            Given an `m × n` grid of letters `board` and a string `word`, return `true` if `word` can be spelled on the board.

            The word is built from letters of **horizontally or vertically neighbouring** cells, and the same cell may **not** be used more than once.

            ### Example 1
            - **Input:** `board = [["A","B","C","E"],["S","F","C","S"],["A","D","E","E"]], word = "ABCCED"`
            - **Output:** `true`
            - **Why:** A → B → C along the top row, down to the second C, down to E, then left to D.

            ### Example 2
            - **Input:** same board, `word = "SEE"`
            - **Output:** `true`

            ### Example 3
            - **Input:** same board, `word = "ABCB"`
            - **Output:** `false`
            - **Why:** after A → B → C, the only B next to that C is the one already used.

            ### Constraints
            - `1 <= m, n <= 6`
            - `1 <= word.length <= 15`
            - `board` and `word` consist of English letters.
            """,
            ThinkingProcessMarkdown = """
            **Restate it:** find a path of neighbouring cells, never reusing one, whose letters spell `word`.

            1. **Try every cell as the start.** From a cell holding `word[0]`, the rest of the word must continue into one of its four neighbours, and so on.
            2. **Depth-first search with backtracking:** `Search(r, c, i)` asks "can `word[i..]` start at `(r, c)`?". It fails at the edge or on the wrong letter, succeeds when `i` reaches the end, and otherwise asks the four neighbours about `i + 1`.
            3. **No reuse:** while a cell is on the current path, overwrite it with `#` so it can't match; restore the letter when the search backs out (that's the backtracking).
            4. **Stop early:** `||` stops at the first neighbour that works.
            5. **Walk Example 3 (`ABCB`):** A → B → C, then C's neighbours are C, E and the B that is already `#`, so that path fails; no other start works either → `false`.

            **Pattern to remember:** grid path search = DFS over the 4 neighbours, marking the path and unmarking on the way back. Word Search II (212) adds a trie to search many words at once.

            **Common mistakes:** forgetting to restore the cell (later paths see a `#`); using a global visited set that is never cleared; checking bounds after reading the cell.
            """,
            Approaches = new List<ProblemApproachItem>
            {
                new()
                {
                    Name = "Backtracking with a set of used cells",
                    Kind = ApproachKind.Alternative,
                    TimeComplexity = "O(m · n · 3^L)",
                    SpaceComplexity = "O(L)",
                    Intuition = "The same search, but cells on the current path are kept in a set instead of being overwritten on the board. It leaves the input untouched, at the cost of hashing.",
                    Code = """
                    bool ExistWithUsedSet(char[][] board, string word)
                    {
                        var used = new HashSet<(int, int)>();
                        bool Search(int r, int c, int i)
                        {
                            if (i == word.Length) return true;
                            if (r < 0 || c < 0 || r >= board.Length || c >= board[0].Length || board[r][c] != word[i] || !used.Add((r, c))) return false;
                            bool found = Search(r + 1, c, i + 1) || Search(r - 1, c, i + 1) || Search(r, c + 1, i + 1) || Search(r, c - 1, i + 1);
                            used.Remove((r, c));
                            return found;
                        }
                        for (int r = 0; r < board.Length; r++)
                            for (int c = 0; c < board[0].Length; c++)
                                if (Search(r, c, 0)) return true;
                        return false;
                    }

                    Console.WriteLine(Judge.Format(ExistWithUsedSet(Board("ABCE", "SFCS", "ADEE"), "ABCB")));   // false
                    """
                },
                new()
                {
                    Name = "Backtracking that marks the board",
                    Kind = ApproachKind.Optimal,
                    TimeComplexity = "O(m · n · 3^L)",
                    SpaceComplexity = "O(L)",
                    Intuition = "From every start, extend the word into neighbouring cells; mark a cell with `#` while it's on the path and restore it when backing out."
                }
            },
            SupportCode = """
            // Board("ABCE", "SFCS") is the grid [["A","B","C","E"],["S","F","C","S"]].
            char[][] Board(params string[] rows) => rows.Select(row => row.ToCharArray()).ToArray();
            """,
            SolutionCode = """
            public class Solution
            {
                public bool Exist(char[][] board, string word)
                {
                    for (int r = 0; r < board.Length; r++)
                        for (int c = 0; c < board[0].Length; c++)
                            if (Search(board, word, r, c, 0)) return true;
                    return false;
                }

                // Can word[i..] be spelled starting at (r, c) without reusing a cell?
                private bool Search(char[][] board, string word, int r, int c, int i)
                {
                    if (i == word.Length) return true;
                    if (r < 0 || c < 0 || r >= board.Length || c >= board[0].Length || board[r][c] != word[i]) return false;

                    char letter = board[r][c];
                    board[r][c] = '#';                                   // on the current path: can't be reused
                    bool found = Search(board, word, r + 1, c, i + 1) || Search(board, word, r - 1, c, i + 1)
                              || Search(board, word, r, c + 1, i + 1) || Search(board, word, r, c - 1, i + 1);
                    board[r][c] = letter;                                // free it again (backtrack)
                    return found;
                }
            }
            """,
            Tests = new List<BlindTest>
            {
                new() { Name = "Example 1", Input = "board = [[\"A\",\"B\",\"C\",\"E\"],[\"S\",\"F\",\"C\",\"S\"],[\"A\",\"D\",\"E\",\"E\"]], word = \"ABCCED\"", Expected = "true", Call = "sol.Exist(Board(\"ABCE\", \"SFCS\", \"ADEE\"), \"ABCCED\")" },
                new() { Name = "Example 2", Input = "same board, word = \"SEE\"", Expected = "true", Call = "sol.Exist(Board(\"ABCE\", \"SFCS\", \"ADEE\"), \"SEE\")" },
                new() { Name = "Example 3", Input = "same board, word = \"ABCB\"", Expected = "false", Call = "sol.Exist(Board(\"ABCE\", \"SFCS\", \"ADEE\"), \"ABCB\")" }
            },
            ExtraTests = new List<BlindTest>
            {
                new() { Name = "A single cell", Input = "board = [[\"a\"]], word = \"a\"", Expected = "true", Call = "sol.Exist(Board(\"a\"), \"a\")" },
                new() { Name = "Longer than the board", Input = "board = [[\"a\",\"b\"]], word = \"abc\"", Expected = "false", Call = "sol.Exist(Board(\"ab\"), \"abc\")" },
                new() { Name = "Snaking through the board", Input = "board = [[\"A\",\"B\",\"C\",\"E\"],[\"S\",\"F\",\"E\",\"S\"],[\"A\",\"D\",\"E\",\"E\"]], word = \"ABCESEEEFS\"", Expected = "true", Call = "sol.Exist(Board(\"ABCE\", \"SFES\", \"ADEE\"), \"ABCESEEEFS\")" },
                new() { Name = "Needs to turn back", Input = "board = [[\"C\",\"A\",\"A\"],[\"A\",\"A\",\"A\"],[\"B\",\"C\",\"D\"]], word = \"AAB\"", Expected = "true", Call = "sol.Exist(Board(\"CAA\", \"AAA\", \"BCD\"), \"AAB\")" }
            },
            StressTestCode = """
            judge.Agree("Random boards vs a set of used cells",
                random =>
                {
                    string Letters(int length) => new string(Enumerable.Range(0, length).Select(_ => "ab"[random.Next(2)]).ToArray());
                    return (rows: Enumerable.Range(0, random.Next(1, 4)).Select(_ => Letters(3)).ToArray(), word: Letters(random.Next(1, 6)));
                },
                input => ExistWithUsedSet(Board(input.rows), input.word),
                input => sol.Exist(Board(input.rows), input.word));
            """,
            VisualizerKind = "Matrix",
            VisualizationDescription = """
            The board from the examples, searched first for `ABCCED` and then for `ABCB`. The path being built is
            highlighted and each cell shows its position in the word; a dead end frees its cell and backs up. `ABCCED` is
            found and marked; `ABCB` fails because the only B next to the C is already on the path.
            """,
            VisualizationCode = """
            var board = Board("ABCE", "SFCS", "ADEE");
            var original = Board("ABCE", "SFCS", "ADEE");   // only to explain dead ends
            var grid = MatrixTracker.Create(new[] { "ABCE", "SFCS", "ADEE" }, "79. Word Search: extend the word one neighbour at a time",
                new MatrixParseOptions { StateClassifier = _ => GridCellState.Default });
            var path = new List<(int Row, int Col)>();

            string WhyStuck(int r, int c, char next)
            {
                var around = new[] { (r + 1, c), (r - 1, c), (r, c + 1), (r, c - 1) }
                    .Where(p => p.Item1 >= 0 && p.Item2 >= 0 && p.Item1 < board.Length && p.Item2 < board[0].Length && original[p.Item1][p.Item2] == next)
                    .ToList();
                if (around.Count == 0) return $"No neighbour of ({r},{c}) holds '{next}'";
                if (around.All(p => board[p.Item1][p.Item2] == '#')) return $"The only '{next}' next to ({r},{c}) is already on the path, and a cell can't be used twice";
                return $"Every path through ({r},{c}) dies out";
            }

            bool Search(string word, int r, int c, int i)
            {
                if (i == word.Length) return true;
                if (r < 0 || c < 0 || r >= board.Length || c >= board[0].Length || board[r][c] != word[i]) return false;

                char letter = board[r][c];
                path.Add((r, c));
                grid.Visit(r, c, $"({r},{c}) holds '{letter}', letter {i + 1} of \"{word}\": \"{word[..(i + 1)]}\" so far", subLabel: (i + 1).ToString());
                board[r][c] = '#';
                bool found = Search(word, r + 1, c, i + 1) || Search(word, r - 1, c, i + 1)
                          || Search(word, r, c + 1, i + 1) || Search(word, r, c - 1, i + 1);
                if (!found)
                {
                    string why = WhyStuck(r, c, word[i + 1]);
                    board[r][c] = letter;
                    path.RemoveAt(path.Count - 1);
                    grid.SetCell(r, c, subLabel: "");
                    grid.Backtrack(r, c, $"{why}: free ({r},{c}) and back up", restoreState: GridCellState.Default);
                }
                board[r][c] = letter;
                return found;
            }

            bool Exist(string word)
            {
                for (int r = 0; r < board.Length; r++)
                    for (int c = 0; c < board[0].Length; c++)
                        if (Search(word, r, c, 0)) return true;
                return false;
            }

            if (Exist("ABCCED")) grid.MarkPath(path, "All six letters of \"ABCCED\" are spelled by neighbouring cells: true");

            // Clear the board's colours and labels before the second word.
            for (int r = 0; r < board.Length; r++)
                for (int c = 0; c < board[0].Length; c++)
                    grid.SetCell(r, c, state: GridCellState.Default, subLabel: "");
            path.Clear();
            grid.Snapshot("Now search for \"ABCB\"");
            bool second = Exist("ABCB");
            grid.Snapshot(second ? "\"ABCB\" found" : "No start works for \"ABCB\": false");
            Display.Visualizer(grid);
            """
        }
    };
}
