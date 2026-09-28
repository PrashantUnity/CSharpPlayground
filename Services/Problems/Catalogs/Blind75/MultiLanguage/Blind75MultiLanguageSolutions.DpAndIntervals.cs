using System.Collections.Generic;
using PdfEditorApp.Plugins.CSharpEditor.Models;
using PdfEditorApp.Plugins.CSharpEditor.Services.Problems.Core;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Problems.Catalogs.Blind75.MultiLanguage;

public static partial class Blind75MultiLanguageSolutions
{
    static partial void RegisterDynamicProgrammingAndIntervals()
    {
        // 70. Climbing Stairs
        Register(70, p =>
        {
            p.LanguageImplementations["python"] = new ProblemLanguageBundle
            {
                LanguageId = "python",
                StarterCode = """
                class Solution:
                    def climbStairs(self, n: int) -> int:
                        pass
                """,
                SolutionCode = """
                class Solution:
                    def climbStairs(self, n: int) -> int:
                        if n <= 2:
                            return n
                        a, b = 1, 2
                        for _ in range(3, n + 1):
                            a, b = b, a + b
                        return b
                """
            };

            p.LanguageImplementations["javascript"] = new ProblemLanguageBundle
            {
                LanguageId = "javascript",
                StarterCode = """
                class Solution {
                    climbStairs(n) {
                        return 0;
                    }
                }
                """,
                SolutionCode = """
                class Solution {
                    climbStairs(n) {
                        if (n <= 2) return n;
                        let a = 1, b = 2;
                        for (let i = 3; i <= n; i++) {
                            const next = a + b;
                            a = b;
                            b = next;
                        }
                        return b;
                    }
                }
                """
            };

            p.LanguageImplementations["java"] = new ProblemLanguageBundle
            {
                LanguageId = "java",
                StarterCode = """
                public class Solution {
                    public int climbStairs(int n) {
                        return 0;
                    }
                }
                """,
                SolutionCode = """
                public class Solution {
                    public int climbStairs(int n) {
                        if (n <= 2) return n;
                        int a = 1, b = 2;
                        for (int i = 3; i <= n; i++) {
                            int next = a + b;
                            a = b;
                            b = next;
                        }
                        return b;
                    }
                }
                """
            };
        });

        // 322. Coin Change
        Register(322, p =>
        {
            p.LanguageImplementations["python"] = new ProblemLanguageBundle
            {
                LanguageId = "python",
                StarterCode = """
                class Solution:
                    def coinChange(self, coins: list[int], amount: int) -> int:
                        pass
                """,
                SolutionCode = """
                class Solution:
                    def coinChange(self, coins: list[int], amount: int) -> int:
                        dp = [float('inf')] * (amount + 1)
                        dp[0] = 0
                        for coin in coins:
                            for x in range(coin, amount + 1):
                                dp[x] = min(dp[x], dp[x - coin] + 1)
                        return dp[amount] if dp[amount] != float('inf') else -1
                """
            };

            p.LanguageImplementations["javascript"] = new ProblemLanguageBundle
            {
                LanguageId = "javascript",
                StarterCode = """
                class Solution {
                    coinChange(coins, amount) {
                        return 0;
                    }
                }
                """,
                SolutionCode = """
                class Solution {
                    coinChange(coins, amount) {
                        const dp = new Array(amount + 1).fill(Infinity);
                        dp[0] = 0;
                        for (const coin of coins) {
                            for (let x = coin; x <= amount; x++) {
                                dp[x] = Math.min(dp[x], dp[x - coin] + 1);
                            }
                        }
                        return dp[amount] === Infinity ? -1 : dp[amount];
                    }
                }
                """
            };

            p.LanguageImplementations["java"] = new ProblemLanguageBundle
            {
                LanguageId = "java",
                StarterCode = """
                public class Solution {
                    public int coinChange(int[] coins, int amount) {
                        return 0;
                    }
                }
                """,
                SolutionCode = """
                public class Solution {
                    public int coinChange(int[] coins, int amount) {
                        int[] dp = new int[amount + 1];
                        Arrays.fill(dp, amount + 1);
                        dp[0] = 0;
                        for (int coin : coins) {
                            for (int x = coin; x <= amount; x++) {
                                dp[x] = Math.min(dp[x], dp[x - coin] + 1);
                            }
                        }
                        return dp[amount] > amount ? -1 : dp[amount];
                    }
                }
                """
            };
        });

        // 53. Maximum Subarray
        Register(53, p =>
        {
            p.LanguageImplementations["python"] = new ProblemLanguageBundle
            {
                LanguageId = "python",
                StarterCode = """
                class Solution:
                    def maxSubArray(self, nums: list[int]) -> int:
                        pass
                """,
                SolutionCode = """
                class Solution:
                    def maxSubArray(self, nums: list[int]) -> int:
                        max_sum = curr_sum = nums[0]
                        for x in nums[1:]:
                            curr_sum = max(x, curr_sum + x)
                            max_sum = max(max_sum, curr_sum)
                        return max_sum
                """
            };

            p.LanguageImplementations["javascript"] = new ProblemLanguageBundle
            {
                LanguageId = "javascript",
                StarterCode = """
                class Solution {
                    maxSubArray(nums) {
                        return 0;
                    }
                }
                """,
                SolutionCode = """
                class Solution {
                    maxSubArray(nums) {
                        let maxSum = nums[0], currSum = nums[0];
                        for (let i = 1; i < nums.length; i++) {
                            currSum = Math.max(nums[i], currSum + nums[i]);
                            maxSum = Math.max(maxSum, currSum);
                        }
                        return maxSum;
                    }
                }
                """
            };

            p.LanguageImplementations["java"] = new ProblemLanguageBundle
            {
                LanguageId = "java",
                StarterCode = """
                public class Solution {
                    public int maxSubArray(int[] nums) {
                        return 0;
                    }
                }
                """,
                SolutionCode = """
                public class Solution {
                    public int maxSubArray(int[] nums) {
                        int maxSum = nums[0], currSum = nums[0];
                        for (int i = 1; i < nums.length; i++) {
                            currSum = Math.max(nums[i], currSum + nums[i]);
                            maxSum = Math.max(maxSum, currSum);
                        }
                        return maxSum;
                    }
                }
                """
            };
        });

        // 55. Jump Game
        Register(55, p =>
        {
            p.LanguageImplementations["python"] = new ProblemLanguageBundle
            {
                LanguageId = "python",
                StarterCode = """
                class Solution:
                    def canJump(self, nums: list[int]) -> bool:
                        pass
                """,
                SolutionCode = """
                class Solution:
                    def canJump(self, nums: list[int]) -> bool:
                        reachable = 0
                        for i, jump in enumerate(nums):
                            if i > reachable:
                                return False
                            reachable = max(reachable, i + jump)
                        return True
                """
            };

            p.LanguageImplementations["javascript"] = new ProblemLanguageBundle
            {
                LanguageId = "javascript",
                StarterCode = """
                class Solution {
                    canJump(nums) {
                        return false;
                    }
                }
                """,
                SolutionCode = """
                class Solution {
                    canJump(nums) {
                        let reachable = 0;
                        for (let i = 0; i < nums.length; i++) {
                            if (i > reachable) return false;
                            reachable = Math.max(reachable, i + nums[i]);
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
                    public boolean canJump(int[] nums) {
                        return false;
                    }
                }
                """,
                SolutionCode = """
                public class Solution {
                    public boolean canJump(int[] nums) {
                        int reachable = 0;
                        for (int i = 0; i < nums.length; i++) {
                            if (i > reachable) return false;
                            reachable = Math.max(reachable, i + nums[i]);
                        }
                        return true;
                    }
                }
                """
            };
        });

        // 62. Unique Paths
        Register(62, p =>
        {
            p.LanguageImplementations["python"] = new ProblemLanguageBundle
            {
                LanguageId = "python",
                StarterCode = """
                class Solution:
                    def uniquePaths(self, m: int, n: int) -> int:
                        pass
                """,
                SolutionCode = """
                import math

                class Solution:
                    def uniquePaths(self, m: int, n: int) -> int:
                        return math.comb(m + n - 2, m - 1)
                """
            };

            p.LanguageImplementations["javascript"] = new ProblemLanguageBundle
            {
                LanguageId = "javascript",
                StarterCode = """
                class Solution {
                    uniquePaths(m, n) {
                        return 0;
                    }
                }
                """,
                SolutionCode = """
                class Solution {
                    uniquePaths(m, n) {
                        const row = new Array(n).fill(1);
                        for (let i = 1; i < m; i++) {
                            for (let j = 1; j < n; j++) {
                                row[j] += row[j - 1];
                            }
                        }
                        return row[n - 1];
                    }
                }
                """
            };

            p.LanguageImplementations["java"] = new ProblemLanguageBundle
            {
                LanguageId = "java",
                StarterCode = """
                public class Solution {
                    public int uniquePaths(int m, int n) {
                        return 0;
                    }
                }
                """,
                SolutionCode = """
                public class Solution {
                    public int uniquePaths(int m, int n) {
                        int[] row = new int[n];
                        Arrays.fill(row, 1);
                        for (int i = 1; i < m; i++) {
                            for (int j = 1; j < n; j++) {
                                row[j] += row[j - 1];
                            }
                        }
                        return row[n - 1];
                    }
                }
                """
            };
        });
    }
}
