using System.Collections.Generic;
using PdfEditorApp.Plugins.CSharpEditor.Models;
using PdfEditorApp.Plugins.CSharpEditor.Services.Problems.Core;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Problems.Catalogs.Blind75.MultiLanguage;

public static partial class Blind75MultiLanguageSolutions
{
    static partial void RegisterArraysAndHashing()
    {
        // 1. Two Sum
        Register(1, p =>
        {
            p.LanguageImplementations["python"] = new ProblemLanguageBundle
            {
                LanguageId = "python",
                StarterCode = """
                class Solution:
                    def twoSum(self, nums: list[int], target: int) -> list[int]:
                        pass
                """,
                SolutionCode = """
                class Solution:
                    def twoSum(self, nums: list[int], target: int) -> list[int]:
                        seen = {}
                        for i, num in enumerate(nums):
                            complement = target - num
                            if complement in seen:
                                return [seen[complement], i]
                            seen[num] = i
                        return []
                """
            };

            p.LanguageImplementations["javascript"] = new ProblemLanguageBundle
            {
                LanguageId = "javascript",
                StarterCode = """
                class Solution {
                    twoSum(nums, target) {
                        // TODO: Implement solution
                    }
                }
                """,
                SolutionCode = """
                class Solution {
                    twoSum(nums, target) {
                        const seen = new Map();
                        for (let i = 0; i < nums.length; i++) {
                            const complement = target - nums[i];
                            if (seen.has(complement)) {
                                return [seen.get(complement), i];
                            }
                            seen.set(nums[i], i);
                        }
                        return [];
                    }
                }
                """
            };

            p.LanguageImplementations["java"] = new ProblemLanguageBundle
            {
                LanguageId = "java",
                StarterCode = """
                public class Solution {
                    public int[] twoSum(int[] nums, int target) {
                        return new int[0];
                    }
                }
                """,
                SolutionCode = """
                public class Solution {
                    public int[] twoSum(int[] nums, int target) {
                        Map<Integer, Integer> seen = new HashMap<>();
                        for (int i = 0; i < nums.length; i++) {
                            int complement = target - nums[i];
                            if (seen.containsKey(complement)) {
                                return new int[] { seen.get(complement), i };
                            }
                            seen.put(nums[i], i);
                        }
                        return new int[0];
                    }
                }
                """
            };
        });

        // 217. Contains Duplicate
        Register(217, p =>
        {
            p.LanguageImplementations["python"] = new ProblemLanguageBundle
            {
                LanguageId = "python",
                StarterCode = """
                class Solution:
                    def containsDuplicate(self, nums: list[int]) -> bool:
                        pass
                """,
                SolutionCode = """
                class Solution:
                    def containsDuplicate(self, nums: list[int]) -> bool:
                        return len(nums) != len(set(nums))
                """
            };

            p.LanguageImplementations["javascript"] = new ProblemLanguageBundle
            {
                LanguageId = "javascript",
                StarterCode = """
                class Solution {
                    containsDuplicate(nums) {
                        return false;
                    }
                }
                """,
                SolutionCode = """
                class Solution {
                    containsDuplicate(nums) {
                        return new Set(nums).size !== nums.length;
                    }
                }
                """
            };

            p.LanguageImplementations["java"] = new ProblemLanguageBundle
            {
                LanguageId = "java",
                StarterCode = """
                public class Solution {
                    public boolean containsDuplicate(int[] nums) {
                        return false;
                    }
                }
                """,
                SolutionCode = """
                public class Solution {
                    public boolean containsDuplicate(int[] nums) {
                        Set<Integer> seen = new HashSet<>();
                        for (int num : nums) {
                            if (!seen.add(num)) return true;
                        }
                        return false;
                    }
                }
                """
            };
        });

        // 242. Valid Anagram
        Register(242, p =>
        {
            p.LanguageImplementations["python"] = new ProblemLanguageBundle
            {
                LanguageId = "python",
                StarterCode = """
                class Solution:
                    def isAnagram(self, s: str, t: str) -> bool:
                        pass
                """,
                SolutionCode = """
                from collections import Counter

                class Solution:
                    def isAnagram(self, s: str, t: str) -> bool:
                        return Counter(s) == Counter(t)
                """
            };

            p.LanguageImplementations["javascript"] = new ProblemLanguageBundle
            {
                LanguageId = "javascript",
                StarterCode = """
                class Solution {
                    isAnagram(s, t) {
                        return false;
                    }
                }
                """,
                SolutionCode = """
                class Solution {
                    isAnagram(s, t) {
                        if (s.length !== t.length) return false;
                        const count = {};
                        for (const c of s) count[c] = (count[c] || 0) + 1;
                        for (const c of t) {
                            if (!count[c]) return false;
                            count[c]--;
                        }
                        return true;
                    }
                }
                """
            };

            p.LanguageImplementations["java"] = new ProblemLanguageBundle
            {
                LanguageId = "java",
                StarterCode = """
                public class Solution {
                    public boolean isAnagram(String s, String t) {
                        return false;
                    }
                }
                """,
                SolutionCode = """
                public class Solution {
                    public boolean isAnagram(String s, String t) {
                        if (s.length() != t.length()) return false;
                        int[] counts = new int[26];
                        for (int i = 0; i < s.length(); i++) {
                            counts[s.charAt(i) - 'a']++;
                            counts[t.charAt(i) - 'a']--;
                        }
                        for (int c : counts) {
                            if (c != 0) return false;
                        }
                        return true;
                    }
                }
                """
            };
        });

        // 49. Group Anagrams
        Register(49, p =>
        {
            p.LanguageImplementations["python"] = new ProblemLanguageBundle
            {
                LanguageId = "python",
                StarterCode = """
                class Solution:
                    def groupAnagrams(self, strs: list[str]) -> list[list[str]]:
                        pass
                """,
                SolutionCode = """
                from collections import defaultdict

                class Solution:
                    def groupAnagrams(self, strs: list[str]) -> list[list[str]]:
                        groups = defaultdict(list)
                        for s in strs:
                            key = "".join(sorted(s))
                            groups[key].append(s)
                        return list(groups.values())
                """
            };

            p.LanguageImplementations["javascript"] = new ProblemLanguageBundle
            {
                LanguageId = "javascript",
                StarterCode = """
                class Solution {
                    groupAnagrams(strs) {
                        return [];
                    }
                }
                """,
                SolutionCode = """
                class Solution {
                    groupAnagrams(strs) {
                        const map = new Map();
                        for (const s of strs) {
                            const key = s.split('').sort().join('');
                            if (!map.has(key)) map.set(key, []);
                            map.get(key).push(s);
                        }
                        return Array.from(map.values());
                    }
                }
                """
            };

            p.LanguageImplementations["java"] = new ProblemLanguageBundle
            {
                LanguageId = "java",
                StarterCode = """
                public class Solution {
                    public List<List<String>> groupAnagrams(String[] strs) {
                        return new ArrayList<>();
                    }
                }
                """,
                SolutionCode = """
                public class Solution {
                    public List<List<String>> groupAnagrams(String[] strs) {
                        Map<String, List<String>> map = new HashMap<>();
                        for (String s : strs) {
                            char[] ca = s.toCharArray();
                            Arrays.sort(ca);
                            String key = String.valueOf(ca);
                            map.computeIfAbsent(key, k -> new ArrayList<>()).add(s);
                        }
                        return new ArrayList<>(map.values());
                    }
                }
                """
            };
        });

        // 238. Product of Array Except Self
        Register(238, p =>
        {
            p.LanguageImplementations["python"] = new ProblemLanguageBundle
            {
                LanguageId = "python",
                StarterCode = """
                class Solution:
                    def productExceptSelf(self, nums: list[int]) -> list[int]:
                        pass
                """,
                SolutionCode = """
                class Solution:
                    def productExceptSelf(self, nums: list[int]) -> list[int]:
                        res = [1] * len(nums)
                        prefix = 1
                        for i in range(len(nums)):
                            res[i] = prefix
                            prefix *= nums[i]
                        postfix = 1
                        for i in range(len(nums) - 1, -1, -1):
                            res[i] *= postfix
                            postfix *= nums[i]
                        return res
                """
            };

            p.LanguageImplementations["javascript"] = new ProblemLanguageBundle
            {
                LanguageId = "javascript",
                StarterCode = """
                class Solution {
                    productExceptSelf(nums) {
                        return [];
                    }
                }
                """,
                SolutionCode = """
                class Solution {
                    productExceptSelf(nums) {
                        const n = nums.length;
                        const res = new Array(n).fill(1);
                        let prefix = 1;
                        for (let i = 0; i < n; i++) {
                            res[i] = prefix;
                            prefix *= nums[i];
                        }
                        let postfix = 1;
                        for (let i = n - 1; i >= 0; i--) {
                            res[i] *= postfix;
                            postfix *= nums[i];
                        }
                        return res;
                    }
                }
                """
            };

            p.LanguageImplementations["java"] = new ProblemLanguageBundle
            {
                LanguageId = "java",
                StarterCode = """
                public class Solution {
                    public int[] productExceptSelf(int[] nums) {
                        return new int[0];
                    }
                }
                """,
                SolutionCode = """
                public class Solution {
                    public int[] productExceptSelf(int[] nums) {
                        int n = nums.length;
                        int[] res = new int[n];
                        int prefix = 1;
                        for (int i = 0; i < n; i++) {
                            res[i] = prefix;
                            prefix *= nums[i];
                        }
                        int postfix = 1;
                        for (int i = n - 1; i >= 0; i--) {
                            res[i] *= postfix;
                            postfix *= nums[i];
                        }
                        return res;
                    }
                }
                """
            };
        });
    }
}
