namespace PdfEditorApp.Plugins.CSharpEditor.Tests.RealRust;

/// <summary>
/// Correct Rust solutions for problems the catalog has no Rust solution for, used only to prove the harness and the translation on
/// every shape of problem: a solution that is right must read ✅ on every case, whatever it takes and returns (shared-node lists,
/// in-place matrices, boards, graphs, design classes, floats, unsigned integers, answers that may come in any order).
/// </summary>
internal static class RustBlind75Oracles
{
    public static readonly IReadOnlyDictionary<int, string> Solutions = new Dictionary<int, string>
    {
        [15] = """
            struct Solution;

            impl Solution {
                pub fn three_sum(mut nums: Vec<i32>) -> Vec<Vec<i32>> {
                    nums.sort();
                    let n = nums.len();
                    let mut result = Vec::new();
                    for i in 0..n {
                        if i > 0 && nums[i] == nums[i - 1] {
                            continue;
                        }
                        let (mut lo, mut hi) = (i + 1, n.wrapping_sub(1));
                        while lo < hi {
                            let sum = nums[i] + nums[lo] + nums[hi];
                            if sum < 0 {
                                lo += 1;
                            } else if sum > 0 {
                                hi -= 1;
                            } else {
                                result.push(vec![nums[i], nums[lo], nums[hi]]);
                                lo += 1;
                                hi -= 1;
                                while lo < hi && nums[lo] == nums[lo - 1] {
                                    lo += 1;
                                }
                                while lo < hi && nums[hi] == nums[hi + 1] {
                                    hi -= 1;
                                }
                            }
                        }
                    }
                    result
                }
            }
            """,
        [19] = """
            struct Solution;

            impl Solution {
                pub fn remove_nth_from_end(head: Option<Box<ListNode>>, n: i32) -> Option<Box<ListNode>> {
                    let mut dummy = Box::new(ListNode { val: 0, next: head });
                    let mut length = 0;
                    let mut cursor = dummy.next.as_ref();
                    while let Some(node) = cursor {
                        length += 1;
                        cursor = node.next.as_ref();
                    }
                    let mut cursor = &mut dummy;
                    for _ in 0..(length - n) {
                        cursor = cursor.next.as_mut().unwrap();
                    }
                    let removed = cursor.next.take();
                    cursor.next = removed.and_then(|node| node.next);
                    dummy.next
                }
            }
            """,
        [20] = """
            struct Solution;

            impl Solution {
                pub fn is_valid(s: String) -> bool {
                    let mut stack = Vec::new();
                    for c in s.chars() {
                        match c {
                            '(' => stack.push(')'),
                            '[' => stack.push(']'),
                            '{' => stack.push('}'),
                            _ => {
                                if stack.pop() != Some(c) {
                                    return false;
                                }
                            }
                        }
                    }
                    stack.is_empty()
                }
            }
            """,
        [23] = """
            struct Solution;

            impl Solution {
                pub fn merge_k_lists(lists: Vec<Option<Box<ListNode>>>) -> Option<Box<ListNode>> {
                    let mut values = Vec::new();
                    for list in lists {
                        let mut node = list;
                        while let Some(n) = node {
                            values.push(n.val);
                            node = n.next;
                        }
                    }
                    values.sort();
                    let mut head = None;
                    for &v in values.iter().rev() {
                        head = Some(Box::new(ListNode { val: v, next: head }));
                    }
                    head
                }
            }
            """,
        [48] = """
            struct Solution;

            impl Solution {
                pub fn rotate(matrix: &mut Vec<Vec<i32>>) {
                    let n = matrix.len();
                    for i in 0..n {
                        for j in i + 1..n {
                            let t = matrix[i][j];
                            matrix[i][j] = matrix[j][i];
                            matrix[j][i] = t;
                        }
                    }
                    for row in matrix.iter_mut() {
                        row.reverse();
                    }
                }
            }
            """,
        [54] = """
            struct Solution;

            impl Solution {
                pub fn spiral_order(matrix: Vec<Vec<i32>>) -> Vec<i32> {
                    let mut result = Vec::new();
                    if matrix.is_empty() {
                        return result;
                    }
                    let (mut top, mut bottom) = (0i32, matrix.len() as i32 - 1);
                    let (mut left, mut right) = (0i32, matrix[0].len() as i32 - 1);
                    while top <= bottom && left <= right {
                        for j in left..=right {
                            result.push(matrix[top as usize][j as usize]);
                        }
                        top += 1;
                        for i in top..=bottom {
                            result.push(matrix[i as usize][right as usize]);
                        }
                        right -= 1;
                        if top <= bottom {
                            for j in (left..=right).rev() {
                                result.push(matrix[bottom as usize][j as usize]);
                            }
                            bottom -= 1;
                        }
                        if left <= right {
                            for i in (top..=bottom).rev() {
                                result.push(matrix[i as usize][left as usize]);
                            }
                            left += 1;
                        }
                    }
                    result
                }
            }
            """,
        [56] = """
            struct Solution;

            impl Solution {
                pub fn merge(mut intervals: Vec<Vec<i32>>) -> Vec<Vec<i32>> {
                    intervals.sort();
                    let mut merged: Vec<Vec<i32>> = Vec::new();
                    for interval in intervals {
                        match merged.last_mut() {
                            Some(last) if interval[0] <= last[1] => last[1] = last[1].max(interval[1]),
                            _ => merged.push(interval),
                        }
                    }
                    merged
                }
            }
            """,
        [57] = """
            struct Solution;

            impl Solution {
                pub fn insert(intervals: Vec<Vec<i32>>, new_interval: Vec<i32>) -> Vec<Vec<i32>> {
                    let mut result = Vec::new();
                    let mut new_interval = new_interval;
                    let mut placed = false;
                    for interval in intervals {
                        if interval[1] < new_interval[0] {
                            result.push(interval);
                        } else if interval[0] > new_interval[1] {
                            if !placed {
                                result.push(new_interval.clone());
                                placed = true;
                            }
                            result.push(interval);
                        } else {
                            new_interval = vec![new_interval[0].min(interval[0]), new_interval[1].max(interval[1])];
                        }
                    }
                    if !placed {
                        result.push(new_interval);
                    }
                    result
                }
            }
            """,
        [73] = """
            struct Solution;

            impl Solution {
                pub fn set_zeroes(matrix: &mut Vec<Vec<i32>>) {
                    let rows = matrix.len();
                    let cols = matrix[0].len();
                    let zero_rows: Vec<usize> = (0..rows).filter(|&i| matrix[i].contains(&0)).collect();
                    let zero_cols: Vec<usize> = (0..cols).filter(|&j| (0..rows).any(|i| matrix[i][j] == 0)).collect();
                    for i in zero_rows {
                        for j in 0..cols {
                            matrix[i][j] = 0;
                        }
                    }
                    for j in zero_cols {
                        for i in 0..rows {
                            matrix[i][j] = 0;
                        }
                    }
                }
            }
            """,
        [79] = """
            struct Solution;

            impl Solution {
                pub fn exist(board: Vec<Vec<char>>, word: String) -> bool {
                    let word: Vec<char> = word.chars().collect();
                    let mut board = board;
                    for r in 0..board.len() {
                        for c in 0..board[0].len() {
                            if Self::walk(&mut board, &word, r as i32, c as i32, 0) {
                                return true;
                            }
                        }
                    }
                    false
                }

                fn walk(board: &mut Vec<Vec<char>>, word: &[char], r: i32, c: i32, k: usize) -> bool {
                    if k == word.len() {
                        return true;
                    }
                    if r < 0 || c < 0 || r as usize >= board.len() || c as usize >= board[0].len() {
                        return false;
                    }
                    let (ru, cu) = (r as usize, c as usize);
                    if board[ru][cu] != word[k] {
                        return false;
                    }
                    let saved = board[ru][cu];
                    board[ru][cu] = '#';
                    let found = Self::walk(board, word, r + 1, c, k + 1)
                        || Self::walk(board, word, r - 1, c, k + 1)
                        || Self::walk(board, word, r, c + 1, k + 1)
                        || Self::walk(board, word, r, c - 1, k + 1);
                    board[ru][cu] = saved;
                    found
                }
            }
            """,
        [98] = """
            struct Solution;

            impl Solution {
                pub fn is_valid_bst(root: Option<Rc<RefCell<TreeNode>>>) -> bool {
                    fn check(node: &Option<Rc<RefCell<TreeNode>>>, low: i64, high: i64) -> bool {
                        match node {
                            None => true,
                            Some(n) => {
                                let n = n.borrow();
                                let v = n.val as i64;
                                v > low && v < high && check(&n.left, low, v) && check(&n.right, v, high)
                            }
                        }
                    }
                    check(&root, i64::MIN, i64::MAX)
                }
            }
            """,
        [100] = """
            struct Solution;

            impl Solution {
                pub fn is_same_tree(p: Option<Rc<RefCell<TreeNode>>>, q: Option<Rc<RefCell<TreeNode>>>) -> bool {
                    match (p, q) {
                        (None, None) => true,
                        (Some(a), Some(b)) => {
                            let (a, b) = (a.borrow(), b.borrow());
                            a.val == b.val && Self::is_same_tree(a.left.clone(), b.left.clone()) && Self::is_same_tree(a.right.clone(), b.right.clone())
                        }
                        _ => false,
                    }
                }
            }
            """,
        [102] = """
            struct Solution;

            impl Solution {
                pub fn level_order(root: Option<Rc<RefCell<TreeNode>>>) -> Vec<Vec<i32>> {
                    let mut result = Vec::new();
                    let mut queue = VecDeque::new();
                    if let Some(r) = root {
                        queue.push_back(r);
                    }
                    while !queue.is_empty() {
                        let mut level = Vec::new();
                        for _ in 0..queue.len() {
                            let node = queue.pop_front().unwrap();
                            let node = node.borrow();
                            level.push(node.val);
                            if let Some(l) = &node.left {
                                queue.push_back(Rc::clone(l));
                            }
                            if let Some(r) = &node.right {
                                queue.push_back(Rc::clone(r));
                            }
                        }
                        result.push(level);
                    }
                    result
                }
            }
            """,
        [105] = """
            struct Solution;

            impl Solution {
                pub fn build_tree(preorder: Vec<i32>, inorder: Vec<i32>) -> Option<Rc<RefCell<TreeNode>>> {
                    fn build(pre: &[i32], ino: &[i32]) -> Option<Rc<RefCell<TreeNode>>> {
                        if pre.is_empty() {
                            return None;
                        }
                        let root_val = pre[0];
                        let mid = ino.iter().position(|&v| v == root_val)?;
                        let node = Rc::new(RefCell::new(TreeNode::new(root_val)));
                        node.borrow_mut().left = build(&pre[1..=mid], &ino[..mid]);
                        node.borrow_mut().right = build(&pre[mid + 1..], &ino[mid + 1..]);
                        Some(node)
                    }
                    build(&preorder, &inorder)
                }
            }
            """,
        [133] = """
            struct Solution;

            impl Solution {
                pub fn clone_graph(node: Option<Rc<RefCell<Node>>>) -> Option<Rc<RefCell<Node>>> {
                    fn copy(node: &Rc<RefCell<Node>>, copies: &mut HashMap<i32, Rc<RefCell<Node>>>) -> Rc<RefCell<Node>> {
                        let val = node.borrow().val;
                        if let Some(existing) = copies.get(&val) {
                            return Rc::clone(existing);
                        }
                        let clone = Rc::new(RefCell::new(Node::new(val)));
                        copies.insert(val, Rc::clone(&clone));
                        for neighbour in node.borrow().neighbors.iter().flatten() {
                            let copied = copy(neighbour, copies);
                            clone.borrow_mut().neighbors.push(Some(copied));
                        }
                        clone
                    }
                    let start = node?;
                    Some(copy(&start, &mut HashMap::new()))
                }
            }
            """,
        [141] = """
            struct Solution;

            impl Solution {
                pub fn has_cycle(head: Option<Rc<RefCell<ListNode>>>) -> bool {
                    let mut slow = head.clone();
                    let mut fast = head;
                    while let Some(f) = fast.clone() {
                        let next = f.borrow().next.clone();
                        let Some(step) = next else {
                            return false;
                        };
                        fast = step.borrow().next.clone();
                        slow = slow.and_then(|s| s.borrow().next.clone());
                        if let (Some(a), Some(b)) = (&slow, &fast) {
                            if Rc::ptr_eq(a, b) {
                                return true;
                            }
                        }
                    }
                    false
                }
            }
            """,
        [143] = """
            struct Solution;

            impl Solution {
                pub fn reorder_list(head: &mut Option<Box<ListNode>>) {
                    let mut values = Vec::new();
                    let mut cursor = head.as_ref();
                    while let Some(node) = cursor {
                        values.push(node.val);
                        cursor = node.next.as_ref();
                    }
                    let (mut i, mut j) = (0usize, values.len());
                    let mut order = Vec::new();
                    while i < j {
                        order.push(values[i]);
                        i += 1;
                        if i < j {
                            j -= 1;
                            order.push(values[j]);
                        }
                    }
                    let mut rebuilt = None;
                    for &v in order.iter().rev() {
                        rebuilt = Some(Box::new(ListNode { val: v, next: rebuilt }));
                    }
                    *head = rebuilt;
                }
            }
            """,
        [190] = """
            struct Solution;

            impl Solution {
                pub fn reverse_bits(n: u32) -> u32 {
                    n.reverse_bits()
                }
            }
            """,
        [191] = """
            struct Solution;

            impl Solution {
                pub fn hamming_weight(n: i32) -> i32 {
                    n.count_ones() as i32
                }
            }
            """,
        [200] = """
            struct Solution;

            impl Solution {
                pub fn num_islands(grid: Vec<Vec<char>>) -> i32 {
                    let mut grid = grid;
                    let mut count = 0;
                    for r in 0..grid.len() {
                        for c in 0..grid[0].len() {
                            if grid[r][c] == '1' {
                                count += 1;
                                Self::sink(&mut grid, r, c);
                            }
                        }
                    }
                    count
                }

                fn sink(grid: &mut Vec<Vec<char>>, r: usize, c: usize) {
                    grid[r][c] = '0';
                    if r > 0 && grid[r - 1][c] == '1' {
                        Self::sink(grid, r - 1, c);
                    }
                    if r + 1 < grid.len() && grid[r + 1][c] == '1' {
                        Self::sink(grid, r + 1, c);
                    }
                    if c > 0 && grid[r][c - 1] == '1' {
                        Self::sink(grid, r, c - 1);
                    }
                    if c + 1 < grid[0].len() && grid[r][c + 1] == '1' {
                        Self::sink(grid, r, c + 1);
                    }
                }
            }
            """,
        [208] = """
            #[derive(Default)]
            struct Trie {
                children: HashMap<char, Trie>,
                end: bool,
            }

            impl Trie {
                fn new() -> Self {
                    Trie::default()
                }

                fn insert(&mut self, word: String) {
                    let mut node = self;
                    for c in word.chars() {
                        node = node.children.entry(c).or_default();
                    }
                    node.end = true;
                }

                fn find(&self, prefix: &str) -> Option<&Trie> {
                    let mut node = self;
                    for c in prefix.chars() {
                        node = node.children.get(&c)?;
                    }
                    Some(node)
                }

                fn search(&self, word: String) -> bool {
                    self.find(&word).map_or(false, |n| n.end)
                }

                fn starts_with(&self, prefix: String) -> bool {
                    self.find(&prefix).is_some()
                }
            }
            """,
        [211] = """
            #[derive(Default)]
            struct WordDictionary {
                children: HashMap<char, WordDictionary>,
                end: bool,
            }

            impl WordDictionary {
                fn new() -> Self {
                    WordDictionary::default()
                }

                fn add_word(&mut self, word: String) {
                    let mut node = self;
                    for c in word.chars() {
                        node = node.children.entry(c).or_default();
                    }
                    node.end = true;
                }

                fn search(&self, word: String) -> bool {
                    let chars: Vec<char> = word.chars().collect();
                    self.matches(&chars)
                }

                fn matches(&self, word: &[char]) -> bool {
                    match word.split_first() {
                        None => self.end,
                        Some(('.', rest)) => self.children.values().any(|child| child.matches(rest)),
                        Some((c, rest)) => self.children.get(c).map_or(false, |child| child.matches(rest)),
                    }
                }
            }
            """,
        [212] = """
            struct Solution;

            impl Solution {
                pub fn find_words(board: Vec<Vec<char>>, words: Vec<String>) -> Vec<String> {
                    words.into_iter().filter(|word| Self::found(&board, word)).collect()
                }

                fn found(board: &Vec<Vec<char>>, word: &str) -> bool {
                    let word: Vec<char> = word.chars().collect();
                    let mut board = board.clone();
                    for r in 0..board.len() {
                        for c in 0..board[0].len() {
                            if Self::walk(&mut board, &word, r as i32, c as i32, 0) {
                                return true;
                            }
                        }
                    }
                    false
                }

                fn walk(board: &mut Vec<Vec<char>>, word: &[char], r: i32, c: i32, k: usize) -> bool {
                    if k == word.len() {
                        return true;
                    }
                    if r < 0 || c < 0 || r as usize >= board.len() || c as usize >= board[0].len() {
                        return false;
                    }
                    let (ru, cu) = (r as usize, c as usize);
                    if board[ru][cu] != word[k] {
                        return false;
                    }
                    let saved = board[ru][cu];
                    board[ru][cu] = '#';
                    let found = Self::walk(board, word, r + 1, c, k + 1)
                        || Self::walk(board, word, r - 1, c, k + 1)
                        || Self::walk(board, word, r, c + 1, k + 1)
                        || Self::walk(board, word, r, c - 1, k + 1);
                    board[ru][cu] = saved;
                    found
                }
            }
            """,
        [235] = LowestCommonAncestor,
        [236] = LowestCommonAncestor,
        [269] = """
            struct Solution;

            impl Solution {
                pub fn alien_order(words: Vec<String>) -> String {
                    let mut edges: HashMap<char, BTreeSet<char>> = HashMap::new();
                    let mut indegree: HashMap<char, usize> = HashMap::new();
                    for word in &words {
                        for c in word.chars() {
                            indegree.entry(c).or_insert(0);
                            edges.entry(c).or_default();
                        }
                    }
                    for pair in words.windows(2) {
                        let a: Vec<char> = pair[0].chars().collect();
                        let b: Vec<char> = pair[1].chars().collect();
                        if a.len() > b.len() && a.starts_with(&b) {
                            return String::new();
                        }
                        if let Some((x, y)) = a.iter().zip(b.iter()).find(|(x, y)| x != y) {
                            if edges.get_mut(x).unwrap().insert(*y) {
                                *indegree.get_mut(y).unwrap() += 1;
                            }
                        }
                    }
                    let mut ready: BTreeSet<char> = indegree.iter().filter(|(_, &d)| d == 0).map(|(&c, _)| c).collect();
                    let mut order = String::new();
                    while let Some(c) = ready.pop_first() {
                        order.push(c);
                        for &next in &edges[&c] {
                            let d = indegree.get_mut(&next).unwrap();
                            *d -= 1;
                            if *d == 0 {
                                ready.insert(next);
                            }
                        }
                    }
                    if order.chars().count() == indegree.len() { order } else { String::new() }
                }
            }
            """,
        [271] = """
            struct Codec {}

            impl Codec {
                fn new() -> Self {
                    Codec {}
                }

                fn encode(&self, strs: Vec<String>) -> String {
                    strs.iter().map(|s| format!("{}#{}", s.chars().count(), s)).collect()
                }

                fn decode(&self, s: String) -> Vec<String> {
                    let chars: Vec<char> = s.chars().collect();
                    let mut result = Vec::new();
                    let mut i = 0;
                    while i < chars.len() {
                        let mut j = i;
                        while chars[j] != '#' {
                            j += 1;
                        }
                        let length: usize = chars[i..j].iter().collect::<String>().parse().unwrap();
                        result.push(chars[j + 1..j + 1 + length].iter().collect());
                        i = j + 1 + length;
                    }
                    result
                }
            }
            """,
        [295] = """
            struct MedianFinder {
                values: Vec<i32>,
            }

            impl MedianFinder {
                fn new() -> Self {
                    MedianFinder { values: Vec::new() }
                }

                fn add_num(&mut self, num: i32) {
                    let at = self.values.partition_point(|&v| v < num);
                    self.values.insert(at, num);
                }

                fn find_median(&self) -> f64 {
                    let n = self.values.len();
                    if n % 2 == 1 {
                        self.values[n / 2] as f64
                    } else {
                        (self.values[n / 2 - 1] + self.values[n / 2]) as f64 / 2.0
                    }
                }
            }
            """,
        [297] = """
            struct Codec {}

            impl Codec {
                fn new() -> Self {
                    Codec {}
                }

                fn serialize(&self, root: Option<Rc<RefCell<TreeNode>>>) -> String {
                    fn write(node: &Option<Rc<RefCell<TreeNode>>>, out: &mut Vec<String>) {
                        match node {
                            None => out.push("#".to_string()),
                            Some(n) => {
                                let n = n.borrow();
                                out.push(n.val.to_string());
                                write(&n.left, out);
                                write(&n.right, out);
                            }
                        }
                    }
                    let mut out = Vec::new();
                    write(&root, &mut out);
                    out.join(",")
                }

                fn deserialize(&self, data: String) -> Option<Rc<RefCell<TreeNode>>> {
                    fn read(tokens: &mut std::slice::Iter<&str>) -> Option<Rc<RefCell<TreeNode>>> {
                        let token = tokens.next()?;
                        if *token == "#" {
                            return None;
                        }
                        let node = Rc::new(RefCell::new(TreeNode::new(token.parse().unwrap())));
                        node.borrow_mut().left = read(tokens);
                        node.borrow_mut().right = read(tokens);
                        Some(node)
                    }
                    let tokens: Vec<&str> = data.split(',').collect();
                    read(&mut tokens.iter())
                }
            }
            """
    };

    private const string LowestCommonAncestor = """
        struct Solution;

        impl Solution {
            pub fn lowest_common_ancestor(
                root: Option<Rc<RefCell<TreeNode>>>,
                p: Option<Rc<RefCell<TreeNode>>>,
                q: Option<Rc<RefCell<TreeNode>>>,
            ) -> Option<Rc<RefCell<TreeNode>>> {
                let (p, q) = (p.as_ref().unwrap().borrow().val, q.as_ref().unwrap().borrow().val);
                fn go(node: &Option<Rc<RefCell<TreeNode>>>, p: i32, q: i32) -> Option<Rc<RefCell<TreeNode>>> {
                    let n = node.as_ref()?;
                    let val = n.borrow().val;
                    if val == p || val == q {
                        return Some(Rc::clone(n));
                    }
                    let left = go(&n.borrow().left, p, q);
                    let right = go(&n.borrow().right, p, q);
                    match (left, right) {
                        (Some(_), Some(_)) => Some(Rc::clone(n)),
                        (l, None) => l,
                        (None, r) => r,
                    }
                }
                go(&root, p, q)
            }
        }
        """;
}
