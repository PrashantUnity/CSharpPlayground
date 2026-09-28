using System.Collections.Generic;
using PdfEditorApp.Plugins.CSharpEditor.Models;
using PdfEditorApp.Plugins.CSharpEditor.Services.Problems.Core;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Problems.Catalogs.Blind75.MultiLanguage;

public static partial class Blind75MultiLanguageSolutions
{
    static partial void RegisterLinkedListsAndTrees()
    {
        // 206. Reverse Linked List
        Register(206, p =>
        {
            p.LanguageImplementations["python"] = new ProblemLanguageBundle
            {
                LanguageId = "python",
                StarterCode = """
                class Solution:
                    def reverseList(self, head: Optional[ListNode]) -> Optional[ListNode]:
                        pass
                """,
                SolutionCode = """
                class Solution:
                    def reverseList(self, head: Optional[ListNode]) -> Optional[ListNode]:
                        prev = None
                        curr = head
                        while curr:
                            nxt = curr.next
                            curr.next = prev
                            prev = curr
                            curr = nxt
                        return prev
                """
            };

            p.LanguageImplementations["javascript"] = new ProblemLanguageBundle
            {
                LanguageId = "javascript",
                StarterCode = """
                class Solution {
                    reverseList(head) {
                        return null;
                    }
                }
                """,
                SolutionCode = """
                class Solution {
                    reverseList(head) {
                        let prev = null;
                        let curr = head;
                        while (curr) {
                            const nxt = curr.next;
                            curr.next = prev;
                            prev = curr;
                            curr = nxt;
                        }
                        return prev;
                    }
                }
                """
            };

            p.LanguageImplementations["java"] = new ProblemLanguageBundle
            {
                LanguageId = "java",
                StarterCode = """
                public class Solution {
                    public ListNode reverseList(ListNode head) {
                        return null;
                    }
                }
                """,
                SolutionCode = """
                public class Solution {
                    public ListNode reverseList(ListNode head) {
                        ListNode prev = null;
                        ListNode curr = head;
                        while (curr != null) {
                            ListNode nxt = curr.next;
                            curr.next = prev;
                            prev = curr;
                            curr = nxt;
                        }
                        return prev;
                    }
                }
                """
            };
        });

        // 21. Merge Two Sorted Lists
        Register(21, p =>
        {
            p.LanguageImplementations["python"] = new ProblemLanguageBundle
            {
                LanguageId = "python",
                StarterCode = """
                class Solution:
                    def mergeTwoLists(self, list1: Optional[ListNode], list2: Optional[ListNode]) -> Optional[ListNode]:
                        pass
                """,
                SolutionCode = """
                class Solution:
                    def mergeTwoLists(self, list1: Optional[ListNode], list2: Optional[ListNode]) -> Optional[ListNode]:
                        dummy = ListNode(0)
                        tail = dummy
                        while list1 and list2:
                            if list1.val < list2.val:
                                tail.next = list1
                                list1 = list1.next
                            else:
                                tail.next = list2
                                list2 = list2.next
                            tail = tail.next
                        tail.next = list1 or list2
                        return dummy.next
                """
            };

            p.LanguageImplementations["javascript"] = new ProblemLanguageBundle
            {
                LanguageId = "javascript",
                StarterCode = """
                class Solution {
                    mergeTwoLists(list1, list2) {
                        return null;
                    }
                }
                """,
                SolutionCode = """
                class Solution {
                    mergeTwoLists(list1, list2) {
                        const dummy = new ListNode(0);
                        let tail = dummy;
                        while (list1 && list2) {
                            if (list1.val < list2.val) {
                                tail.next = list1;
                                list1 = list1.next;
                            } else {
                                tail.next = list2;
                                list2 = list2.next;
                            }
                            tail = tail.next;
                        }
                        tail.next = list1 || list2;
                        return dummy.next;
                    }
                }
                """
            };

            p.LanguageImplementations["java"] = new ProblemLanguageBundle
            {
                LanguageId = "java",
                StarterCode = """
                public class Solution {
                    public ListNode mergeTwoLists(ListNode list1, ListNode list2) {
                        return null;
                    }
                }
                """,
                SolutionCode = """
                public class Solution {
                    public ListNode mergeTwoLists(ListNode list1, ListNode list2) {
                        ListNode dummy = new ListNode(0);
                        ListNode tail = dummy;
                        while (list1 != null && list2 != null) {
                            if (list1.val < list2.val) {
                                tail.next = list1;
                                list1 = list1.next;
                            } else {
                                tail.next = list2;
                                list2 = list2.next;
                            }
                            tail = tail.next;
                        }
                        tail.next = (list1 != null) ? list1 : list2;
                        return dummy.next;
                    }
                }
                """
            };
        });

        // 226. Invert Binary Tree
        Register(226, p =>
        {
            p.LanguageImplementations["python"] = new ProblemLanguageBundle
            {
                LanguageId = "python",
                StarterCode = """
                class Solution:
                    def invertTree(self, root: Optional[TreeNode]) -> Optional[TreeNode]:
                        pass
                """,
                SolutionCode = """
                class Solution:
                    def invertTree(self, root: Optional[TreeNode]) -> Optional[TreeNode]:
                        if not root:
                            return None
                        root.left, root.right = self.invertTree(root.right), self.invertTree(root.left)
                        return root
                """
            };

            p.LanguageImplementations["javascript"] = new ProblemLanguageBundle
            {
                LanguageId = "javascript",
                StarterCode = """
                class Solution {
                    invertTree(root) {
                        return null;
                    }
                }
                """,
                SolutionCode = """
                class Solution {
                    invertTree(root) {
                        if (!root) return null;
                        const temp = this.invertTree(root.left);
                        root.left = this.invertTree(root.right);
                        root.right = temp;
                        return root;
                    }
                }
                """
            };

            p.LanguageImplementations["java"] = new ProblemLanguageBundle
            {
                LanguageId = "java",
                StarterCode = """
                public class Solution {
                    public TreeNode invertTree(TreeNode root) {
                        return null;
                    }
                }
                """,
                SolutionCode = """
                public class Solution {
                    public TreeNode invertTree(TreeNode root) {
                        if (root == null) return null;
                        TreeNode temp = invertTree(root.left);
                        root.left = invertTree(root.right);
                        root.right = temp;
                        return root;
                    }
                }
                """
            };
        });

        // 104. Maximum Depth of Binary Tree
        Register(104, p =>
        {
            p.LanguageImplementations["python"] = new ProblemLanguageBundle
            {
                LanguageId = "python",
                StarterCode = """
                class Solution:
                    def maxDepth(self, root: Optional[TreeNode]) -> int:
                        pass
                """,
                SolutionCode = """
                class Solution:
                    def maxDepth(self, root: Optional[TreeNode]) -> int:
                        if not root:
                            return 0
                        return 1 + max(self.maxDepth(root.left), self.maxDepth(root.right))
                """
            };

            p.LanguageImplementations["javascript"] = new ProblemLanguageBundle
            {
                LanguageId = "javascript",
                StarterCode = """
                class Solution {
                    maxDepth(root) {
                        return 0;
                    }
                }
                """,
                SolutionCode = """
                class Solution {
                    maxDepth(root) {
                        if (!root) return 0;
                        return 1 + Math.max(this.maxDepth(root.left), this.maxDepth(root.right));
                    }
                }
                """
            };

            p.LanguageImplementations["java"] = new ProblemLanguageBundle
            {
                LanguageId = "java",
                StarterCode = """
                public class Solution {
                    public int maxDepth(TreeNode root) {
                        return 0;
                    }
                }
                """,
                SolutionCode = """
                public class Solution {
                    public int maxDepth(TreeNode root) {
                        if (root == null) return 0;
                        return 1 + Math.max(maxDepth(root.left), maxDepth(root.right));
                    }
                }
                """
            };
        });
    }
}
