using System.Collections.Generic;
using PdfEditorApp.Plugins.CSharpEditor.Models;
using PdfEditorApp.Plugins.CSharpEditor.Services.Problems.Core;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Problems.Catalogs.Blind75.MultiLanguage;

public static partial class Blind75MultiLanguageSolutions
{
    static partial void RegisterTwoPointersAndSlidingWindow()
    {
        // 121. Best Time to Buy and Sell Stock
        Register(121, p =>
        {
            p.LanguageImplementations["python"] = new ProblemLanguageBundle
            {
                LanguageId = "python",
                StarterCode = """
                class Solution:
                    def maxProfit(self, prices: list[int]) -> int:
                        pass
                """,
                SolutionCode = """
                class Solution:
                    def maxProfit(self, prices: list[int]) -> int:
                        min_price = float('inf')
                        max_p = 0
                        for p in prices:
                            if p < min_price:
                                min_price = p
                            elif p - min_price > max_p:
                                max_p = p - min_price
                        return max_p
                """
            };

            p.LanguageImplementations["javascript"] = new ProblemLanguageBundle
            {
                LanguageId = "javascript",
                StarterCode = """
                class Solution {
                    maxProfit(prices) {
                        return 0;
                    }
                }
                """,
                SolutionCode = """
                class Solution {
                    maxProfit(prices) {
                        let minPrice = Infinity;
                        let maxProfit = 0;
                        for (const p of prices) {
                            if (p < minPrice) minPrice = p;
                            else if (p - minPrice > maxProfit) maxProfit = p - minPrice;
                        }
                        return maxProfit;
                    }
                }
                """
            };

            p.LanguageImplementations["java"] = new ProblemLanguageBundle
            {
                LanguageId = "java",
                StarterCode = """
                public class Solution {
                    public int maxProfit(int[] prices) {
                        return 0;
                    }
                }
                """,
                SolutionCode = """
                public class Solution {
                    public int maxProfit(int[] prices) {
                        int minPrice = Integer.MAX_VALUE;
                        int maxProfit = 0;
                        for (int p : prices) {
                            if (p < minPrice) minPrice = p;
                            else if (p - minPrice > maxProfit) maxProfit = p - minPrice;
                        }
                        return maxProfit;
                    }
                }
                """
            };
        });

        // 125. Valid Palindrome
        Register(125, p =>
        {
            p.LanguageImplementations["python"] = new ProblemLanguageBundle
            {
                LanguageId = "python",
                StarterCode = """
                class Solution:
                    def isPalindrome(self, s: str) -> bool:
                        pass
                """,
                SolutionCode = """
                class Solution:
                    def isPalindrome(self, s: str) -> bool:
                        cleaned = [c.lower() for c in s if c.isalnum()]
                        return cleaned == cleaned[::-1]
                """
            };

            p.LanguageImplementations["javascript"] = new ProblemLanguageBundle
            {
                LanguageId = "javascript",
                StarterCode = """
                class Solution {
                    isPalindrome(s) {
                        return false;
                    }
                }
                """,
                SolutionCode = """
                class Solution {
                    isPalindrome(s) {
                        const cleaned = s.replace(/[^a-zA-Z0-9]/g, '').toLowerCase();
                        let l = 0, r = cleaned.length - 1;
                        while (l < r) {
                            if (cleaned[l] !== cleaned[r]) return false;
                            l++;
                            r--;
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
                    public boolean isPalindrome(String s) {
                        return false;
                    }
                }
                """,
                SolutionCode = """
                public class Solution {
                    public boolean isPalindrome(String s) {
                        int l = 0, r = s.length() - 1;
                        while (l < r) {
                            while (l < r && !Character.isLetterOrDigit(s.charAt(l))) l++;
                            while (l < r && !Character.isLetterOrDigit(s.charAt(r))) r--;
                            if (Character.toLowerCase(s.charAt(l)) != Character.toLowerCase(s.charAt(r))) {
                                return false;
                            }
                            l++;
                            r--;
                        }
                        return true;
                    }
                }
                """
            };
        });

        // 11. Container With Most Water
        Register(11, p =>
        {
            p.LanguageImplementations["python"] = new ProblemLanguageBundle
            {
                LanguageId = "python",
                StarterCode = """
                class Solution:
                    def maxArea(self, height: list[int]) -> int:
                        pass
                """,
                SolutionCode = """
                class Solution:
                    def maxArea(self, height: list[int]) -> int:
                        l, r = 0, len(height) - 1
                        max_w = 0
                        while l < r:
                            area = (r - l) * min(height[l], height[r])
                            max_w = max(max_w, area)
                            if height[l] < height[r]:
                                l += 1
                            else:
                                r -= 1
                        return max_w
                """
            };

            p.LanguageImplementations["javascript"] = new ProblemLanguageBundle
            {
                LanguageId = "javascript",
                StarterCode = """
                class Solution {
                    maxArea(height) {
                        return 0;
                    }
                }
                """,
                SolutionCode = """
                class Solution {
                    maxArea(height) {
                        let l = 0, r = height.length - 1;
                        let maxArea = 0;
                        while (l < r) {
                            const area = (r - l) * Math.min(height[l], height[r]);
                            if (area > maxArea) maxArea = area;
                            if (height[l] < height[r]) l++;
                            else r--;
                        }
                        return maxArea;
                    }
                }
                """
            };

            p.LanguageImplementations["java"] = new ProblemLanguageBundle
            {
                LanguageId = "java",
                StarterCode = """
                public class Solution {
                    public int maxArea(int[] height) {
                        return 0;
                    }
                }
                """,
                SolutionCode = """
                public class Solution {
                    public int maxArea(int[] height) {
                        int l = 0, r = height.length - 1;
                        int maxArea = 0;
                        while (l < r) {
                            int area = (r - l) * Math.min(height[l], height[r]);
                            maxArea = Math.max(maxArea, area);
                            if (height[l] < height[r]) l++;
                            else r--;
                        }
                        return maxArea;
                    }
                }
                """
            };
        });

        // 3. Longest Substring Without Repeating Characters
        Register(3, p =>
        {
            p.LanguageImplementations["python"] = new ProblemLanguageBundle
            {
                LanguageId = "python",
                StarterCode = """
                class Solution:
                    def lengthOfLongestSubstring(self, s: str) -> int:
                        pass
                """,
                SolutionCode = """
                class Solution:
                    def lengthOfLongestSubstring(self, s: str) -> int:
                        seen = {}
                        l = 0
                        max_len = 0
                        for r, c in enumerate(s):
                            if c in seen and seen[c] >= l:
                                l = seen[c] + 1
                            seen[c] = r
                            max_len = max(max_len, r - l + 1)
                        return max_len
                """
            };

            p.LanguageImplementations["javascript"] = new ProblemLanguageBundle
            {
                LanguageId = "javascript",
                StarterCode = """
                class Solution {
                    lengthOfLongestSubstring(s) {
                        return 0;
                    }
                }
                """,
                SolutionCode = """
                class Solution {
                    lengthOfLongestSubstring(s) {
                        const seen = new Map();
                        let l = 0;
                        let maxLen = 0;
                        for (let r = 0; r < s.length; r++) {
                            const c = s[r];
                            if (seen.has(c) && seen.get(c) >= l) {
                                l = seen.get(c) + 1;
                            }
                            seen.set(c, r);
                            maxLen = Math.max(maxLen, r - l + 1);
                        }
                        return maxLen;
                    }
                }
                """
            };

            p.LanguageImplementations["java"] = new ProblemLanguageBundle
            {
                LanguageId = "java",
                StarterCode = """
                public class Solution {
                    public int lengthOfLongestSubstring(String s) {
                        return 0;
                    }
                }
                """,
                SolutionCode = """
                public class Solution {
                    public int lengthOfLongestSubstring(String s) {
                        Map<Character, Integer> seen = new HashMap<>();
                        int l = 0, maxLen = 0;
                        for (int r = 0; r < s.length(); r++) {
                            char c = s.charAt(r);
                            if (seen.containsKey(c) && seen.get(c) >= l) {
                                l = seen.get(c) + 1;
                            }
                            seen.put(c, r);
                            maxLen = Math.max(maxLen, r - l + 1);
                        }
                        return maxLen;
                    }
                }
                """
            };
        });
    }
}
