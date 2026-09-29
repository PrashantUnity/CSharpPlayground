namespace PdfEditorApp.Plugins.CSharpEditor.Services.Problems.Languages.Rust;

/// <summary>
/// The Rust source a Blind 75 script or notebook carries besides the learner's solution: the harness that prints the ✅/❌ lines
/// (the same rules as the C# <c>Judge</c>: JSON-looking answers compared without regard to spacing, <c>2</c> against <c>2.0</c>, or
/// element order when a problem allows any), the list, tree and graph types LeetCode gives, the functions that build them from the
/// tests' data, and the few helpers a problem's tests need (replaying a Trie's operation list, checking an in-place change).
/// </summary>
internal static class RustProblemSupport
{
    /// <summary>What a script starts with: quiet about what a learner hasn't used yet, and the names a solution usually wants.</summary>
    public const string Prelude = "#![allow(unused_imports, dead_code)]\n\n" + Uses;

    /// <summary>The imports a solution usually wants; a notebook's setup cell starts with them too.</summary>
    public const string Uses = """
        use std::cell::RefCell;
        use std::collections::*;
        use std::rc::Rc;
        """;

    /// <summary>The judge: <c>check(name, || answer, expected, any_order)</c> prints one line and says whether it passed.</summary>
    public const string Harness = """"
        // ── Test harness: prints one ✅/❌ line per case; nothing here needs to be edited ──
        trait Show {
            fn show(&self) -> String;
        }

        fn quote(text: &str) -> String {
            let mut out = String::from("\"");
            for c in text.chars() {
                match c {
                    '"' => out.push_str("\\\""),
                    '\\' => out.push_str("\\\\"),
                    '\n' => out.push_str("\\n"),
                    '\r' => out.push_str("\\r"),
                    '\t' => out.push_str("\\t"),
                    c if (c as u32) < 0x20 => out.push_str(&format!("\\u{:04x}", c as u32)),
                    c => out.push(c),
                }
            }
            out.push('"');
            out
        }

        macro_rules! show_with_to_string {
            ($($t:ty),*) => {
                $(impl Show for $t {
                    fn show(&self) -> String {
                        self.to_string()
                    }
                })*
            };
        }
        show_with_to_string!(i8, i16, i32, i64, isize, u8, u16, u32, u64, usize, bool);

        impl Show for f64 {
            fn show(&self) -> String {
                format!("{}", self)
            }
        }

        impl Show for String {
            fn show(&self) -> String {
                quote(self)
            }
        }

        impl Show for &str {
            fn show(&self) -> String {
                quote(self)
            }
        }

        impl Show for char {
            fn show(&self) -> String {
                quote(&self.to_string())
            }
        }

        impl<T: Show> Show for Vec<T> {
            fn show(&self) -> String {
                format!("[{}]", self.iter().map(|item| item.show()).collect::<Vec<_>>().join(","))
            }
        }

        // An answer already written as JSON, such as the replayed answers of a design problem.
        struct Raw(String);

        impl Show for Raw {
            fn show(&self) -> String {
                self.0.clone()
            }
        }

        fn skip_spaces(chars: &[char], at: &mut usize) {
            while *at < chars.len() && chars[*at].is_whitespace() {
                *at += 1;
            }
        }

        // Reads JSON and writes it one way: no spaces, numbers as numbers (2 and 2.0 are alike), and with `any_order`
        // the items of every list in order of size or text. Anything that isn't JSON is left as it is.
        fn normalize(chars: &[char], at: &mut usize, any_order: bool) -> Option<String> {
            skip_spaces(chars, at);
            match *chars.get(*at)? {
                '[' => {
                    *at += 1;
                    let mut items: Vec<String> = Vec::new();
                    loop {
                        skip_spaces(chars, at);
                        if *chars.get(*at)? == ']' {
                            *at += 1;
                            break;
                        }
                        if !items.is_empty() {
                            if *chars.get(*at)? != ',' {
                                return None;
                            }
                            *at += 1;
                        }
                        items.push(normalize(chars, at, any_order)?);
                    }
                    if any_order {
                        if items.iter().all(|item| item.parse::<f64>().is_ok()) {
                            items.sort_by(|a, b| a.parse::<f64>().unwrap().partial_cmp(&b.parse::<f64>().unwrap()).unwrap());
                        } else {
                            items.sort();
                        }
                    }
                    Some(format!("[{}]", items.join(",")))
                }
                '{' => {
                    *at += 1;
                    let mut fields: Vec<String> = Vec::new();
                    loop {
                        skip_spaces(chars, at);
                        if *chars.get(*at)? == '}' {
                            *at += 1;
                            break;
                        }
                        if !fields.is_empty() {
                            if *chars.get(*at)? != ',' {
                                return None;
                            }
                            *at += 1;
                        }
                        let key = normalize(chars, at, any_order)?;
                        skip_spaces(chars, at);
                        if *chars.get(*at)? != ':' {
                            return None;
                        }
                        *at += 1;
                        fields.push(format!("{}:{}", key, normalize(chars, at, any_order)?));
                    }
                    fields.sort();
                    Some(format!("{{{}}}", fields.join(",")))
                }
                '"' => {
                    *at += 1;
                    let mut value = String::new();
                    loop {
                        let c = *chars.get(*at)?;
                        *at += 1;
                        match c {
                            '"' => break,
                            '\\' => {
                                let escape = *chars.get(*at)?;
                                *at += 1;
                                match escape {
                                    'n' => value.push('\n'),
                                    'r' => value.push('\r'),
                                    't' => value.push('\t'),
                                    'u' => {
                                        if *at + 4 > chars.len() {
                                            return None;
                                        }
                                        let hex: String = chars[*at..*at + 4].iter().collect();
                                        *at += 4;
                                        value.push(char::from_u32(u32::from_str_radix(&hex, 16).ok()?)?);
                                    }
                                    other => value.push(other),
                                }
                            }
                            other => value.push(other),
                        }
                    }
                    Some(quote(&value))
                }
                't' | 'f' | 'n' => {
                    let rest: String = chars[*at..].iter().take(5).collect();
                    for word in ["true", "false", "null"] {
                        if rest.starts_with(word) {
                            *at += word.len();
                            return Some(word.to_string());
                        }
                    }
                    None
                }
                _ => {
                    let start = *at;
                    while *at < chars.len() && matches!(chars[*at], '0'..='9' | '-' | '+' | '.' | 'e' | 'E') {
                        *at += 1;
                    }
                    let number: String = chars[start..*at].iter().collect();
                    number.parse::<f64>().ok().map(|n| format!("{}", n))
                }
            }
        }

        fn canonical(text: &str, any_order: bool) -> String {
            let chars: Vec<char> = text.chars().collect();
            let mut at = 0;
            if let Some(value) = normalize(&chars, &mut at, any_order) {
                skip_spaces(&chars, &mut at);
                if at == chars.len() {
                    return value;
                }
            }
            text.trim().to_string()
        }

        // Runs one case: a panic in the solution is that case's ❌, and the cases after it still run.
        fn check<T: Show, F: FnOnce() -> T>(name: &str, run: F, expected: &str, any_order: bool) -> bool {
            match std::panic::catch_unwind(std::panic::AssertUnwindSafe(run)) {
                Ok(actual) => {
                    let shown = actual.show();
                    let passed = canonical(&shown, any_order) == canonical(expected, any_order);
                    if passed {
                        println!("✅ {} → {}", name, shown);
                    } else {
                        println!("❌ {} → got {} · expected {}", name, shown, expected);
                    }
                    passed
                }
                Err(payload) => {
                    let reason = payload
                        .downcast_ref::<&str>()
                        .map(|s| s.to_string())
                        .or_else(|| payload.downcast_ref::<String>().cloned())
                        .unwrap_or_else(|| "the solution panicked".to_string());
                    println!("❌ {} → panicked: {} · expected {}", name, reason, expected);
                    false
                }
            }
        }
        """";

    public const string ListSupport = """
        // Definition for singly-linked list.
        #[derive(PartialEq, Eq, Clone, Debug)]
        pub struct ListNode {
            pub val: i32,
            pub next: Option<Box<ListNode>>,
        }

        impl ListNode {
            #[inline]
            fn new(val: i32) -> Self {
                ListNode { next: None, val }
            }
        }

        // build_list(&[1, 2, 3]) is 1 -> 2 -> 3; build_list(&[]) is the empty list, None.
        fn build_list(values: &[i32]) -> Option<Box<ListNode>> {
            let mut head = None;
            for &value in values.iter().rev() {
                head = Some(Box::new(ListNode { val: value, next: head }));
            }
            head
        }

        impl Show for Option<Box<ListNode>> {
            fn show(&self) -> String {
                let mut values: Vec<String> = Vec::new();
                let mut node = self.as_ref();
                while let Some(current) = node {
                    values.push(current.val.to_string());
                    node = current.next.as_ref();
                    if values.len() > 1000 {
                        break;
                    }
                }
                format!("[{}]", values.join(","))
            }
        }
        """;

    /// <summary>Linked List Cycle's nodes: shared, so the tail can point back into the list.</summary>
    public const string CycleListSupport = """
        // Definition for a singly-linked list whose nodes can be shared, so a tail can point back into the list.
        #[derive(Debug)]
        pub struct ListNode {
            pub val: i32,
            pub next: Option<Rc<RefCell<ListNode>>>,
        }

        impl ListNode {
            #[inline]
            fn new(val: i32) -> Self {
                ListNode { next: None, val }
            }
        }

        // build_cycle(&[3, 2, 0, -4], 1): the tail's next points back to the node at index 1 (-1 for no cycle).
        fn build_cycle(values: &[i32], pos: i32) -> Option<Rc<RefCell<ListNode>>> {
            let nodes: Vec<Rc<RefCell<ListNode>>> = values.iter().map(|&value| Rc::new(RefCell::new(ListNode::new(value)))).collect();
            for pair in nodes.windows(2) {
                pair[0].borrow_mut().next = Some(Rc::clone(&pair[1]));
            }
            if pos >= 0 {
                if let (Some(tail), Some(target)) = (nodes.last(), nodes.get(pos as usize)) {
                    tail.borrow_mut().next = Some(Rc::clone(target));
                }
            }
            nodes.first().cloned()
        }

        // build_list(&[1, 2, 3]) is a list with no cycle.
        fn build_list(values: &[i32]) -> Option<Rc<RefCell<ListNode>>> {
            build_cycle(values, -1)
        }
        """;

    public const string TreeSupport = """
        // Definition for a binary tree node.
        #[derive(Debug, PartialEq, Eq)]
        pub struct TreeNode {
            pub val: i32,
            pub left: Option<Rc<RefCell<TreeNode>>>,
            pub right: Option<Rc<RefCell<TreeNode>>>,
        }

        impl TreeNode {
            #[inline]
            pub fn new(val: i32) -> Self {
                TreeNode { val, left: None, right: None }
            }
        }

        // build_tree(&[Some(3), Some(9), Some(20), None, None, Some(15), Some(7)]) reads LeetCode's level order: None marks a missing child.
        fn build_tree(values: &[Option<i32>]) -> Option<Rc<RefCell<TreeNode>>> {
            let root = Rc::new(RefCell::new(TreeNode::new((*values.first()?)?)));
            let mut parents = VecDeque::new();
            parents.push_back(Rc::clone(&root));
            let mut i = 1;
            while i < values.len() {
                let parent = parents.pop_front()?;
                if let Some(value) = values[i] {
                    let child = Rc::new(RefCell::new(TreeNode::new(value)));
                    parent.borrow_mut().left = Some(Rc::clone(&child));
                    parents.push_back(child);
                }
                if i + 1 < values.len() {
                    if let Some(value) = values[i + 1] {
                        let child = Rc::new(RefCell::new(TreeNode::new(value)));
                        parent.borrow_mut().right = Some(Rc::clone(&child));
                        parents.push_back(child);
                    }
                }
                i += 2;
            }
            Some(root)
        }

        impl Show for Option<Rc<RefCell<TreeNode>>> {
            fn show(&self) -> String {
                let mut order: Vec<Option<i32>> = Vec::new();
                let mut queue: VecDeque<Option<Rc<RefCell<TreeNode>>>> = VecDeque::new();
                queue.push_back(self.clone());
                while let Some(node) = queue.pop_front() {
                    match node {
                        Some(n) => {
                            order.push(Some(n.borrow().val));
                            queue.push_back(n.borrow().left.clone());
                            queue.push_back(n.borrow().right.clone());
                        }
                        None => order.push(None),
                    }
                    if order.len() > 2000 {
                        break;
                    }
                }
                while order.last() == Some(&None) {
                    order.pop();
                }
                let items: Vec<String> = order
                    .iter()
                    .map(|v| match v {
                        Some(x) => x.to_string(),
                        None => "null".to_string(),
                    })
                    .collect();
                format!("[{}]", items.join(","))
            }
        }

        // Find the node holding a value (values are unique in these problems).
        fn find(node: &Option<Rc<RefCell<TreeNode>>>, value: i32) -> Option<Rc<RefCell<TreeNode>>> {
            let n = node.as_ref()?;
            if n.borrow().val == value {
                return Some(Rc::clone(n));
            }
            find(&n.borrow().left, value).or_else(|| find(&n.borrow().right, value))
        }
        """;

    public const string GraphSupport = """
        // Definition for a Node.
        #[derive(Debug)]
        pub struct Node {
            pub val: i32,
            pub neighbors: Vec<Option<Rc<RefCell<Node>>>>,
        }

        impl Node {
            pub fn new(val: i32) -> Self {
                Node { val, neighbors: Vec::new() }
            }
        }

        // build_graph(vec![vec![2, 4], vec![1, 3], vec![2, 4], vec![1, 3]]): entry i lists the neighbours of node i + 1; returns node 1 (None when empty).
        fn build_graph(adjacency: Vec<Vec<i32>>) -> Option<Rc<RefCell<Node>>> {
            if adjacency.is_empty() {
                return None;
            }
            let nodes: Vec<Rc<RefCell<Node>>> = (1..=adjacency.len() as i32).map(|value| Rc::new(RefCell::new(Node::new(value)))).collect();
            for (i, neighbours) in adjacency.iter().enumerate() {
                for &neighbour in neighbours {
                    nodes[i].borrow_mut().neighbors.push(Some(Rc::clone(&nodes[neighbour as usize - 1])));
                }
            }
            Some(Rc::clone(&nodes[0]))
        }

        // The adjacency list LeetCode shows for Clone Graph: the neighbours of node 1, node 2, … by value.
        impl Show for Option<Rc<RefCell<Node>>> {
            fn show(&self) -> String {
                let Some(start) = self else {
                    return "[]".to_string();
                };
                let mut seen = HashSet::new();
                let mut queue = VecDeque::new();
                let mut nodes: Vec<Rc<RefCell<Node>>> = Vec::new();
                seen.insert(Rc::as_ptr(start));
                queue.push_back(Rc::clone(start));
                while let Some(node) = queue.pop_front() {
                    nodes.push(Rc::clone(&node));
                    for next in node.borrow().neighbors.iter().flatten() {
                        if seen.insert(Rc::as_ptr(next)) {
                            queue.push_back(Rc::clone(next));
                        }
                    }
                }
                nodes.sort_by_key(|n| n.borrow().val);
                let rows: Vec<String> = nodes
                    .iter()
                    .map(|n| {
                        let values: Vec<String> = n.borrow().neighbors.iter().flatten().map(|m| m.borrow().val.to_string()).collect();
                        format!("[{}]", values.join(","))
                    })
                    .collect();
                format!("[{}]", rows.join(","))
            }
        }

        fn reachable(start: &Option<Rc<RefCell<Node>>>) -> HashSet<*const RefCell<Node>> {
            let mut seen = HashSet::new();
            let mut stack: Vec<Rc<RefCell<Node>>> = Vec::new();
            if let Some(s) = start {
                stack.push(Rc::clone(s));
            }
            while let Some(node) = stack.pop() {
                if seen.insert(Rc::as_ptr(&node)) {
                    for next in node.borrow().neighbors.iter().flatten() {
                        stack.push(Rc::clone(next));
                    }
                }
            }
            seen
        }
        """;

    /// <summary>A rectangular grid of letters for Word Search and Number of Islands.</summary>
    public const string BoardSupport = """
        // board(&["ABCE", "SFCS"]) is the grid [['A','B','C','E'], ['S','F','C','S']].
        fn board(rows: &[&str]) -> Vec<Vec<char>> {
            rows.iter().map(|row| row.chars().collect()).collect()
        }
        """;

    /// <summary>
    /// The helpers a problem's tests call that depend on its solution, in Rust: what the C# problem keeps in its
    /// <c>TestSetupCode</c> or <c>SupportCode</c>. Empty for a problem whose tests only call the solution.
    /// </summary>
    public static string TestHelpers(int problemNumber) => problemNumber switch
    {
        48 => """
            // rotate changes the matrix in place, so the tests look at it afterwards.
            fn rotated(mut matrix: Vec<Vec<i32>>) -> Vec<Vec<i32>> {
                Solution::rotate(&mut matrix);
                matrix
            }
            """,
        73 => """
            // set_zeroes changes the matrix in place, so the tests look at it afterwards.
            fn zeroed(mut matrix: Vec<Vec<i32>>) -> Vec<Vec<i32>> {
                Solution::set_zeroes(&mut matrix);
                matrix
            }
            """,
        143 => """
            // reorder_list returns nothing, so the tests look at the list it rearranged.
            fn reordered(values: &[i32]) -> Option<Box<ListNode>> {
                let mut head = build_list(values);
                Solution::reorder_list(&mut head);
                head
            }
            """,
        133 => """
            // A deep copy shares no node with the original and has the same shape.
            fn is_deep_copy(original: Option<Rc<RefCell<Node>>>) -> bool {
                let copy = Solution::clone_graph(original.clone());
                reachable(&copy).is_disjoint(&reachable(&original)) && copy.show() == original.show()
            }
            """,
        235 or 236 => """
            // The answer is a node; the tests compare its value.
            fn lca_value(root: Option<Rc<RefCell<TreeNode>>>, p: i32, q: i32) -> i32 {
                let (a, b) = (find(&root, p), find(&root, q));
                Solution::lowest_common_ancestor(root, a, b).map(|node| node.borrow().val).unwrap_or(-1)
            }
            """,
        269 => """
            // An order is valid when it lists every letter once and sorts the words exactly as given.
            fn is_consistent(words: Vec<String>, order: String) -> bool {
                let mut letters: Vec<char> = Vec::new();
                for word in &words {
                    for c in word.chars() {
                        if !letters.contains(&c) {
                            letters.push(c);
                        }
                    }
                }
                if order.chars().count() != letters.len() || !letters.iter().all(|c| order.contains(*c)) {
                    return false;
                }
                let rank = |c: char| order.chars().position(|o| o == c).unwrap();
                let compare = |x: &str, y: &str| -> std::cmp::Ordering {
                    for (a, b) in x.chars().zip(y.chars()) {
                        if a != b {
                            return rank(a).cmp(&rank(b));
                        }
                    }
                    x.chars().count().cmp(&y.chars().count())
                };
                words.windows(2).all(|pair| compare(&pair[0], &pair[1]) != std::cmp::Ordering::Greater)
            }
            """,
        271 => """
            // Decoding the encoding must give back exactly the same list.
            fn round_trip(strs: Vec<String>) -> Vec<String> {
                let codec = Codec::new();
                codec.decode(codec.encode(strs))
            }
            """,
        297 => """
            fn round_trip(root: Option<Rc<RefCell<TreeNode>>>) -> Option<Rc<RefCell<TreeNode>>> {
                let codec = Codec::new();
                codec.deserialize(codec.serialize(root))
            }
            """,
        208 => Replay("Trie", "Trie", [("insert", "insert", "void"), ("search", "search", "bool"), ("startsWith", "starts_with", "bool")], "String"),
        211 => Replay("WordDictionary", "WordDictionary", [("addWord", "add_word", "void"), ("search", "search", "bool")], "String"),
        295 => Replay("MedianFinder", "MedianFinder", [("addNum", "add_num", "void"), ("findMedian", "find_median", "value")], "i32"),
        _ => string.Empty
    };

    /// <summary>Statements a problem's tests need before the first case (Serialize and Deserialize keeps one Codec for all of them).</summary>
    public static string TestSetupStatements(int problemNumber) => problemNumber == 297 ? "let codec = Codec::new();" : string.Empty;

    // Replays LeetCode's operation list on a fresh object; void calls answer null, as in LeetCode's output. The argument list
    // holds strings, or numbers (as i32) for Find Median from Data Stream.
    private static string Replay(string className, string type, (string Operation, string Method, string Kind)[] operations, string argumentType)
    {
        var arms = new System.Text.StringBuilder();
        arms.AppendLine($"        \"{className}\" => {{");
        arms.AppendLine($"            object = Some({type}::new());");
        arms.AppendLine("            answers.push(\"null\".to_string());");
        arms.AppendLine("        }");
        foreach (var (operation, method, kind) in operations)
        {
            var argument = argumentType == "String" ? "arguments[i].clone()" : "arguments[i]";
            var receiver = kind == "void" ? "object.as_mut().unwrap()" : "object.as_ref().unwrap()";
            if (operation is "findMedian")
            {
                arms.AppendLine($"        \"{operation}\" => answers.push(format!(\"{{}}\", {receiver}.{method}())),");
            }
            else if (kind == "void")
            {
                arms.AppendLine($"        \"{operation}\" => {{");
                arms.AppendLine($"            {receiver}.{method}({argument});");
                arms.AppendLine("            answers.push(\"null\".to_string());");
                arms.AppendLine("        }");
            }
            else
            {
                arms.AppendLine($"        \"{operation}\" => answers.push({receiver}.{method}({argument}).to_string()),");
            }
        }

        return $$"""
            // Replays LeetCode's operation list on a fresh {{className}}; void calls answer null, as in LeetCode's output.
            fn replay(operations: Vec<String>, arguments: Vec<{{argumentType}}>) -> Raw {
                let mut object: Option<{{type}}> = None;
                let mut answers: Vec<String> = Vec::new();
                for (i, operation) in operations.iter().enumerate() {
                    match operation.as_str() {
            {{arms.ToString().TrimEnd()}}
                        _ => {}
                    }
                }
                Raw(format!("[{}]", answers.join(",")))
            }
            """;
    }
}
