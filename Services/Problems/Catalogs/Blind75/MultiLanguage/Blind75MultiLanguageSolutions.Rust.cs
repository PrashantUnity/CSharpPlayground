using PdfEditorApp.Plugins.CSharpEditor.Models;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages;
using PdfEditorApp.Plugins.CSharpEditor.Services.Problems.Core;
using PdfEditorApp.Plugins.CSharpEditor.Services.Problems.Languages.Rust;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Problems.Catalogs.Blind75.MultiLanguage;

/// <summary>
/// Rust reference solutions for the problems that have Python, JavaScript and Java ones: the same algorithms, written the way
/// LeetCode's Rust solutions are (<c>impl Solution</c> with snake_case methods). A problem without one starts from LeetCode's
/// empty method instead.
/// </summary>
public static partial class Blind75MultiLanguageSolutions
{
    static partial void RegisterRust()
    {
        RustSolution(1, """
            struct Solution;

            impl Solution {
                pub fn two_sum(nums: Vec<i32>, target: i32) -> Vec<i32> {
                    let mut seen: HashMap<i32, i32> = HashMap::new();
                    for (i, &num) in nums.iter().enumerate() {
                        if let Some(&j) = seen.get(&(target - num)) {
                            return vec![j, i as i32];
                        }
                        seen.insert(num, i as i32);
                    }
                    vec![]
                }
            }
            """);

        RustSolution(3, """
            struct Solution;

            impl Solution {
                pub fn length_of_longest_substring(s: String) -> i32 {
                    let mut last: HashMap<char, usize> = HashMap::new();
                    let (mut best, mut start) = (0, 0);
                    for (i, c) in s.chars().enumerate() {
                        if let Some(&j) = last.get(&c) {
                            if j >= start {
                                start = j + 1;
                            }
                        }
                        last.insert(c, i);
                        best = best.max(i + 1 - start);
                    }
                    best as i32
                }
            }
            """);

        RustSolution(11, """
            struct Solution;

            impl Solution {
                pub fn max_area(height: Vec<i32>) -> i32 {
                    let (mut left, mut right) = (0usize, height.len().saturating_sub(1));
                    let mut best = 0;
                    while left < right {
                        best = best.max((right - left) as i32 * height[left].min(height[right]));
                        if height[left] < height[right] {
                            left += 1;
                        } else {
                            right -= 1;
                        }
                    }
                    best
                }
            }
            """);

        RustSolution(21, """
            struct Solution;

            impl Solution {
                pub fn merge_two_lists(list1: Option<Box<ListNode>>, list2: Option<Box<ListNode>>) -> Option<Box<ListNode>> {
                    let mut dummy = Box::new(ListNode::new(0));
                    let mut tail = &mut dummy;
                    let (mut a, mut b) = (list1, list2);
                    while a.is_some() && b.is_some() {
                        let take_a = a.as_ref().unwrap().val <= b.as_ref().unwrap().val;
                        let source = if take_a { &mut a } else { &mut b };
                        let mut node = source.take().unwrap();
                        *source = node.next.take();
                        tail.next = Some(node);
                        tail = tail.next.as_mut().unwrap();
                    }
                    tail.next = if a.is_some() { a } else { b };
                    dummy.next
                }
            }
            """);

        RustSolution(49, """
            struct Solution;

            impl Solution {
                pub fn group_anagrams(strs: Vec<String>) -> Vec<Vec<String>> {
                    let mut groups: HashMap<Vec<char>, Vec<String>> = HashMap::new();
                    for s in strs {
                        let mut key: Vec<char> = s.chars().collect();
                        key.sort_unstable();
                        groups.entry(key).or_default().push(s);
                    }
                    groups.into_values().collect()
                }
            }
            """);

        RustSolution(53, """
            struct Solution;

            impl Solution {
                pub fn max_sub_array(nums: Vec<i32>) -> i32 {
                    let mut best = nums[0];
                    let mut current = nums[0];
                    for &n in &nums[1..] {
                        current = n.max(current + n);
                        best = best.max(current);
                    }
                    best
                }
            }
            """);

        RustSolution(55, """
            struct Solution;

            impl Solution {
                pub fn can_jump(nums: Vec<i32>) -> bool {
                    let mut reach = 0usize;
                    for (i, &n) in nums.iter().enumerate() {
                        if i > reach {
                            return false;
                        }
                        reach = reach.max(i + n as usize);
                    }
                    true
                }
            }
            """);

        RustSolution(62, """
            struct Solution;

            impl Solution {
                pub fn unique_paths(m: i32, n: i32) -> i32 {
                    let (m, n) = (m as usize, n as usize);
                    let mut row = vec![1i64; n];
                    for _ in 1..m {
                        for j in 1..n {
                            row[j] += row[j - 1];
                        }
                    }
                    row[n - 1] as i32
                }
            }
            """);

        RustSolution(70, """
            struct Solution;

            impl Solution {
                pub fn climb_stairs(n: i32) -> i32 {
                    // ways(0) and ways(1); each step adds the two before it, and stops at ways(n) so nothing past it can overflow
                    let (mut previous, mut current) = (1, 1);
                    for _ in 1..n {
                        let next = previous + current;
                        previous = current;
                        current = next;
                    }
                    current
                }
            }
            """);

        RustSolution(104, """
            struct Solution;

            impl Solution {
                pub fn max_depth(root: Option<Rc<RefCell<TreeNode>>>) -> i32 {
                    match root {
                        None => 0,
                        Some(node) => {
                            let node = node.borrow();
                            1 + Self::max_depth(node.left.clone()).max(Self::max_depth(node.right.clone()))
                        }
                    }
                }
            }
            """);

        RustSolution(121, """
            struct Solution;

            impl Solution {
                pub fn max_profit(prices: Vec<i32>) -> i32 {
                    let mut lowest = i32::MAX;
                    let mut best = 0;
                    for &price in &prices {
                        lowest = lowest.min(price);
                        best = best.max(price - lowest);
                    }
                    best
                }
            }
            """);

        RustSolution(125, """
            struct Solution;

            impl Solution {
                pub fn is_palindrome(s: String) -> bool {
                    let letters: Vec<char> = s.chars().filter(|c| c.is_alphanumeric()).map(|c| c.to_ascii_lowercase()).collect();
                    letters.iter().eq(letters.iter().rev())
                }
            }
            """);

        RustSolution(206, """
            struct Solution;

            impl Solution {
                pub fn reverse_list(head: Option<Box<ListNode>>) -> Option<Box<ListNode>> {
                    let mut previous = None;
                    let mut current = head;
                    while let Some(mut node) = current {
                        current = node.next.take();
                        node.next = previous;
                        previous = Some(node);
                    }
                    previous
                }
            }
            """);

        RustSolution(217, """
            struct Solution;

            impl Solution {
                pub fn contains_duplicate(nums: Vec<i32>) -> bool {
                    let mut seen = HashSet::new();
                    nums.iter().any(|n| !seen.insert(*n))
                }
            }
            """);

        RustSolution(226, """
            struct Solution;

            impl Solution {
                pub fn invert_tree(root: Option<Rc<RefCell<TreeNode>>>) -> Option<Rc<RefCell<TreeNode>>> {
                    if let Some(node) = &root {
                        let mut n = node.borrow_mut();
                        let left = n.left.take();
                        let right = n.right.take();
                        n.left = Self::invert_tree(right);
                        n.right = Self::invert_tree(left);
                    }
                    root
                }
            }
            """);

        RustSolution(238, """
            struct Solution;

            impl Solution {
                pub fn product_except_self(nums: Vec<i32>) -> Vec<i32> {
                    let n = nums.len();
                    let mut answer = vec![1; n];
                    let mut prefix = 1;
                    for i in 0..n {
                        answer[i] = prefix;
                        prefix *= nums[i];
                    }
                    let mut suffix = 1;
                    for i in (0..n).rev() {
                        answer[i] *= suffix;
                        suffix *= nums[i];
                    }
                    answer
                }
            }
            """);

        RustSolution(242, """
            struct Solution;

            impl Solution {
                pub fn is_anagram(s: String, t: String) -> bool {
                    let mut counts: HashMap<char, i32> = HashMap::new();
                    for c in s.chars() {
                        *counts.entry(c).or_insert(0) += 1;
                    }
                    for c in t.chars() {
                        *counts.entry(c).or_insert(0) -= 1;
                    }
                    counts.values().all(|&n| n == 0)
                }
            }
            """);

        RustSolution(322, """
            struct Solution;

            impl Solution {
                pub fn coin_change(coins: Vec<i32>, amount: i32) -> i32 {
                    let amount = amount as usize;
                    let mut best = vec![usize::MAX; amount + 1];
                    best[0] = 0;
                    for total in 1..=amount {
                        for &coin in &coins {
                            let coin = coin as usize;
                            if coin <= total && best[total - coin] != usize::MAX {
                                best[total] = best[total].min(best[total - coin] + 1);
                            }
                        }
                    }
                    if best[amount] == usize::MAX { -1 } else { best[amount] as i32 }
                }
            }
            """);
    }

    // The starter is LeetCode's empty method for the problem, built from its signature.
    private static void RustSolution(int problemNumber, string solution) => Register(problemNumber, problem =>
    {
        problem.LanguageImplementations[LanguageIds.Rust] = new ProblemLanguageBundle
        {
            LanguageId = LanguageIds.Rust,
            StarterCode = RustProblemLanguageAdapter.Starter(problem),
            SolutionCode = solution
        };
    });
}
