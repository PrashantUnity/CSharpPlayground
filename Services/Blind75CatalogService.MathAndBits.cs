using System.Collections.Generic;
using PdfEditorApp.Plugins.CSharpEditor.Models;

namespace PdfEditorApp.Plugins.CSharpEditor.Services;

public static partial class Blind75CatalogService
{
    private static IEnumerable<BlindProblemItem> GetIntervalsAndMathProblems()
    {
        var list = new List<BlindProblemItem>(GetIntervalProblems());
        list.AddRange(GetMathAndBitProblems());
        return list;
    }

    private static IEnumerable<BlindProblemItem> GetMathAndBitProblems() => new List<BlindProblemItem>
    {
        new()
        {
            Id = "blind75_48_rotate_image",
            Number = 48,
            Title = "Rotate Image",
            Category = "Math & Geometry",
            Difficulty = ProblemDifficulty.Medium,
            AcceptanceRate = 75.0,
            IsPremium = false,
            Tags = new List<string> { "Array", "Math", "Matrix" },
            TimeComplexity = "O(n²)",
            SpaceComplexity = "O(1)",
            DescriptionMarkdown = """
            You are given an `n × n` matrix (an image). Rotate it **90° clockwise, in place**: change the given matrix itself instead of building a new one.

            ### Example 1
            - **Input:** `matrix = [[1,2,3],[4,5,6],[7,8,9]]`
            - **Output:** `[[7,4,1],[8,5,2],[9,6,3]]`

            ### Example 2
            - **Input:** `matrix = [[5,1,9,11],[2,4,8,10],[13,3,6,7],[15,14,12,16]]`
            - **Output:** `[[15,13,2,5],[14,3,4,1],[12,6,8,9],[16,7,10,11]]`

            ### Constraints
            - `n == matrix.length == matrix[i].length`, `1 <= n <= 20`
            - `-1000 <= matrix[i][j] <= 1000`
            """,
            ThinkingProcessMarkdown = """
            **Restate it:** row `r` of the old image becomes column `n − 1 − r` of the new one, without a second matrix.

            1. **With extra memory it's one line:** `rotated[c][n-1-r] = matrix[r][c]`, then copy back. The follow-up is to avoid that `O(n²)` copy.
            2. **Split the rotation into two mirror images you can do in place:**
               - **transpose** (mirror across the main diagonal): swap `matrix[r][c]` with `matrix[c][r]`, which turns rows into columns;
               - then **reverse each row** (mirror left–right).
               Together they are exactly a quarter turn clockwise.
            3. **Only swap above the diagonal** (`c > r`) when transposing; swapping every pair twice would undo it.
            4. **Walk Example 1:** transpose → `[[1,4,7],[2,5,8],[3,6,9]]`; reverse rows → `[[7,4,1],[8,5,2],[9,6,3]]`.

            **Another way:** rotate the four corners of each square "ring" at once (`top → right → bottom → left → top`), layer by layer.

            **Pattern to remember:** geometric transforms decompose into mirrors: transpose + reverse rows = clockwise; transpose + reverse columns = counter-clockwise.

            **Common mistakes:** transposing the whole square (each pair swapped twice); reversing columns instead of rows (that's counter-clockwise); allocating a new matrix when in-place was asked.
            """,
            Approaches = new List<ProblemApproachItem>
            {
                new()
                {
                    Name = "Copy into a rotated matrix",
                    Kind = ApproachKind.Naive,
                    TimeComplexity = "O(n²)",
                    SpaceComplexity = "O(n²)",
                    Intuition = "Write each value to its rotated position in a new matrix (`[r][c]` → `[c][n−1−r]`), then copy it back.",
                    BottleneckExplanation = "It needs a whole second matrix; the problem asks for the rotation in place.",
                    Code = """
                    void RotateWithCopy(int[][] matrix)
                    {
                        int n = matrix.Length;
                        var rotated = new int[n][];
                        for (int r = 0; r < n; r++) rotated[r] = new int[n];
                        for (int r = 0; r < n; r++)
                            for (int c = 0; c < n; c++)
                                rotated[c][n - 1 - r] = matrix[r][c];   // row r becomes column n - 1 - r
                        for (int r = 0; r < n; r++) matrix[r] = rotated[r];
                    }

                    var image = new[] { new[] { 1, 2, 3 }, new[] { 4, 5, 6 }, new[] { 7, 8, 9 } };
                    RotateWithCopy(image);
                    Console.WriteLine(Judge.Format(image));   // [[7,4,1],[8,5,2],[9,6,3]]
                    """
                },
                new()
                {
                    Name = "Transpose, then reverse every row",
                    Kind = ApproachKind.Optimal,
                    TimeComplexity = "O(n²)",
                    SpaceComplexity = "O(1)",
                    Intuition = "Mirror across the main diagonal, then mirror left–right; the two mirrors make a quarter turn clockwise, all with swaps."
                }
            },
            SolutionCode = """
            public class Solution
            {
                public void Rotate(int[][] matrix)
                {
                    int n = matrix.Length;
                    for (int r = 0; r < n; r++)
                        for (int c = r + 1; c < n; c++)
                            (matrix[r][c], matrix[c][r]) = (matrix[c][r], matrix[r][c]);   // transpose: mirror across the diagonal
                    foreach (var row in matrix) Array.Reverse(row);                        // then mirror left-right
                }
            }
            """,
            TestSetupCode = """
            // Rotate changes the matrix in place, so the tests look at it afterwards.
            int[][] Rotated(int[][] matrix)
            {
                sol.Rotate(matrix);
                return matrix;
            }
            """,
            Tests = new List<BlindTest>
            {
                new() { Name = "Example 1", Input = "matrix = [[1,2,3],[4,5,6],[7,8,9]]", Expected = "[[7,4,1],[8,5,2],[9,6,3]]", Call = "Rotated(new[] { new[] { 1, 2, 3 }, new[] { 4, 5, 6 }, new[] { 7, 8, 9 } })" },
                new() { Name = "Example 2", Input = "matrix = [[5,1,9,11],[2,4,8,10],[13,3,6,7],[15,14,12,16]]", Expected = "[[15,13,2,5],[14,3,4,1],[12,6,8,9],[16,7,10,11]]", Call = "Rotated(new[] { new[] { 5, 1, 9, 11 }, new[] { 2, 4, 8, 10 }, new[] { 13, 3, 6, 7 }, new[] { 15, 14, 12, 16 } })" }
            },
            ExtraTests = new List<BlindTest>
            {
                new() { Name = "A single cell", Input = "matrix = [[1]]", Expected = "[[1]]", Call = "Rotated(new[] { new[] { 1 } })" },
                new() { Name = "Two by two", Input = "matrix = [[1,2],[3,4]]", Expected = "[[3,1],[4,2]]", Call = "Rotated(new[] { new[] { 1, 2 }, new[] { 3, 4 } })" },
                new() { Name = "Negative values", Input = "matrix = [[-1,-2],[-3,-4]]", Expected = "[[-3,-1],[-4,-2]]", Call = "Rotated(new[] { new[] { -1, -2 }, new[] { -3, -4 } })" }
            },
            StressTestCode = """
            judge.Agree("Random squares vs copying",
                random =>
                {
                    int n = random.Next(1, 6);
                    return Enumerable.Range(0, n).Select(_ => Enumerable.Range(0, n).Select(_ => random.Next(-9, 10)).ToArray()).ToArray();
                },
                matrix => { var copy = matrix.Select(row => row.ToArray()).ToArray(); RotateWithCopy(copy); return copy; },
                matrix => { var copy = matrix.Select(row => row.ToArray()).ToArray(); sol.Rotate(copy); return copy; });
            """,
            VisualizerKind = "Matrix",
            VisualizationDescription = """
            Example 1. First the transpose swaps each pair across the main diagonal (the two cells being swapped light
            up), turning rows into columns. Then each row is reversed. After both mirrors the image has turned 90°
            clockwise.
            """,
            VisualizationCode = """
            var matrix = new[] { new[] { 1, 2, 3 }, new[] { 4, 5, 6 }, new[] { 7, 8, 9 } };
            int n = matrix.Length;
            var grid = MatrixTracker.Create(matrix, "48. Rotate Image: transpose, then reverse each row",
                new MatrixParseOptions { StateClassifier = _ => GridCellState.Default });
            void Show(int r, int c) => grid.SetCell(r, c, val: matrix[r][c].ToString());
            void Clear() { for (int r = 0; r < n; r++) for (int c = 0; c < n; c++) grid.SetCell(r, c, state: GridCellState.Default); }

            for (int r = 0; r < n; r++)
                for (int c = r + 1; c < n; c++)
                {
                    (matrix[r][c], matrix[c][r]) = (matrix[c][r], matrix[r][c]);
                    Show(r, c);
                    Show(c, r);
                    grid.SetCell(r, c, state: GridCellState.Current);
                    grid.SetCell(c, r, state: GridCellState.Current);
                    grid.Snapshot($"Transpose: swap ({r},{c}) and ({c},{r}) across the diagonal");
                    Clear();
                }
            grid.Snapshot("The transpose is done: every row has become a column");

            for (int r = 0; r < n; r++)
            {
                Array.Reverse(matrix[r]);
                for (int c = 0; c < n; c++)
                {
                    Show(r, c);
                    grid.SetCell(r, c, state: GridCellState.Visited);
                }
                grid.Snapshot($"Reverse row {r}: [{string.Join(",", matrix[r])}]");
                Clear();
            }

            grid.Snapshot($"Rotated 90° clockwise: {Judge.Format(matrix)}");
            Display.Visualizer(grid);
            """
        },
        new()
        {
            Id = "blind75_54_spiral_matrix",
            Number = 54,
            Title = "Spiral Matrix",
            Category = "Math & Geometry",
            Difficulty = ProblemDifficulty.Medium,
            AcceptanceRate = 51.0,
            IsPremium = false,
            Tags = new List<string> { "Array", "Matrix", "Simulation" },
            TimeComplexity = "O(m · n)",
            SpaceComplexity = "O(1) besides the output",
            DescriptionMarkdown = """
            Given an `m × n` matrix, return all of its elements in **spiral order**: along the top row, down the right side, back along the bottom, up the left side, and so on inwards.

            ### Example 1
            - **Input:** `matrix = [[1,2,3],[4,5,6],[7,8,9]]`
            - **Output:** `[1,2,3,6,9,8,7,4,5]`

            ### Example 2
            - **Input:** `matrix = [[1,2,3,4],[5,6,7,8],[9,10,11,12]]`
            - **Output:** `[1,2,3,4,8,12,11,10,9,5,6,7]`

            ### Constraints
            - `1 <= m, n <= 10`
            - `-100 <= matrix[i][j] <= 100`
            """,
            ThinkingProcessMarkdown = """
            **Restate it:** peel the matrix like an onion, one ring at a time, clockwise.

            1. **Keep four walls:** `top`, `bottom`, `left`, `right` bound the part not yet read.
            2. **One lap:** read the top row left → right, then move `top` down; read the right column top → bottom, then move `right` in; read the bottom row right → left and move `bottom` up; read the left column bottom → top and move `left` in.
            3. **Stop when the walls cross.** The tricky part: after the first two sides, the remaining rectangle may be a single row or column, so check `top <= bottom` before the bottom row and `left <= right` before the left column, or you'd read cells twice.
            4. **Every cell is read once:** `O(m · n)`.
            5. **Walk Example 2:** `1 2 3 4` → `8 12` → `11 10 9` → `5` → then the inner row `6 7`.

            **Another way:** walk with a direction and turn right whenever the next cell is outside the matrix or already visited.

            **Pattern to remember:** "traverse in layers" → maintain shrinking boundaries; re-check them before each side.

            **Common mistakes:** duplicating the middle row/column of a non-square matrix (missing the boundary checks); off-by-one when shrinking a wall.
            """,
            Approaches = new List<ProblemApproachItem>
            {
                new()
                {
                    Name = "Walk and turn right at walls",
                    Kind = ApproachKind.Alternative,
                    TimeComplexity = "O(m · n)",
                    SpaceComplexity = "O(m · n) for the visited marks",
                    Intuition = "Move right, down, left, up in turn; whenever the next cell is off the grid or already taken, turn clockwise.",
                    Code = """
                    List<int> SpiralByTurning(int[][] matrix)
                    {
                        int rows = matrix.Length, cols = matrix[0].Length;
                        var seen = new bool[rows, cols];
                        var order = new List<int>();
                        int[] dr = { 0, 1, 0, -1 }, dc = { 1, 0, -1, 0 };   // right, down, left, up
                        int r = 0, c = 0, d = 0;
                        for (int k = 0; k < rows * cols; k++)
                        {
                            order.Add(matrix[r][c]);
                            seen[r, c] = true;
                            int nr = r + dr[d], nc = c + dc[d];
                            if (nr < 0 || nc < 0 || nr >= rows || nc >= cols || seen[nr, nc])
                            {
                                d = (d + 1) % 4;                                // turn clockwise
                                nr = r + dr[d];
                                nc = c + dc[d];
                            }
                            (r, c) = (nr, nc);
                        }
                        return order;
                    }

                    Console.WriteLine(Judge.Format(SpiralByTurning(new[] { new[] { 1, 2, 3 }, new[] { 4, 5, 6 }, new[] { 7, 8, 9 } })));   // [1,2,3,6,9,8,7,4,5]
                    """
                },
                new()
                {
                    Name = "Four shrinking walls",
                    Kind = ApproachKind.Optimal,
                    TimeComplexity = "O(m · n)",
                    SpaceComplexity = "O(1)",
                    Intuition = "Read the top row, right column, bottom row and left column of the unread rectangle, moving each wall inward after its side."
                }
            },
            SolutionCode = """
            public class Solution
            {
                public IList<int> SpiralOrder(int[][] matrix)
                {
                    var order = new List<int>();
                    int top = 0, bottom = matrix.Length - 1, left = 0, right = matrix[0].Length - 1;
                    while (top <= bottom && left <= right)
                    {
                        for (int c = left; c <= right; c++) order.Add(matrix[top][c]);          // → along the top row
                        top++;
                        for (int r = top; r <= bottom; r++) order.Add(matrix[r][right]);        // ↓ down the right column
                        right--;
                        if (top <= bottom)
                            for (int c = right; c >= left; c--) order.Add(matrix[bottom][c]);   // ← along the bottom row
                        bottom--;
                        if (left <= right)
                            for (int r = bottom; r >= top; r--) order.Add(matrix[r][left]);     // ↑ up the left column
                        left++;
                    }
                    return order;
                }
            }
            """,
            Tests = new List<BlindTest>
            {
                new() { Name = "Example 1", Input = "matrix = [[1,2,3],[4,5,6],[7,8,9]]", Expected = "[1,2,3,6,9,8,7,4,5]", Call = "sol.SpiralOrder(new[] { new[] { 1, 2, 3 }, new[] { 4, 5, 6 }, new[] { 7, 8, 9 } })" },
                new() { Name = "Example 2", Input = "matrix = [[1,2,3,4],[5,6,7,8],[9,10,11,12]]", Expected = "[1,2,3,4,8,12,11,10,9,5,6,7]", Call = "sol.SpiralOrder(new[] { new[] { 1, 2, 3, 4 }, new[] { 5, 6, 7, 8 }, new[] { 9, 10, 11, 12 } })" }
            },
            ExtraTests = new List<BlindTest>
            {
                new() { Name = "A single cell", Input = "matrix = [[1]]", Expected = "[1]", Call = "sol.SpiralOrder(new[] { new[] { 1 } })" },
                new() { Name = "A single row", Input = "matrix = [[1,2,3]]", Expected = "[1,2,3]", Call = "sol.SpiralOrder(new[] { new[] { 1, 2, 3 } })" },
                new() { Name = "A single column", Input = "matrix = [[1],[2],[3]]", Expected = "[1,2,3]", Call = "sol.SpiralOrder(new[] { new[] { 1 }, new[] { 2 }, new[] { 3 } })" },
                new() { Name = "Four by four", Input = "matrix = [[1,2,3,4],[5,6,7,8],[9,10,11,12],[13,14,15,16]]", Expected = "[1,2,3,4,8,12,16,15,14,13,9,5,6,7,11,10]", Call = "sol.SpiralOrder(new[] { new[] { 1, 2, 3, 4 }, new[] { 5, 6, 7, 8 }, new[] { 9, 10, 11, 12 }, new[] { 13, 14, 15, 16 } })" }
            },
            StressTestCode = """
            judge.Agree("Random shapes vs turning at walls",
                random =>
                {
                    int rows = random.Next(1, 6), cols = random.Next(1, 6);
                    return Enumerable.Range(0, rows).Select(r => Enumerable.Range(0, cols).Select(c => r * cols + c).ToArray()).ToArray();
                },
                matrix => (object)SpiralByTurning(matrix),
                matrix => sol.SpiralOrder(matrix));
            """,
            VisualizerKind = "Matrix",
            VisualizationDescription = """
            Example 2 (3 × 4). The walk reads the top row, the right column, the bottom row and the left column of the
            unread rectangle, then the walls move inward; each cell shows its position in the output, which `order`
            collects.
            """,
            VisualizationCode = """
            var matrix = new[] { new[] { 1, 2, 3, 4 }, new[] { 5, 6, 7, 8 }, new[] { 9, 10, 11, 12 } };
            var grid = MatrixTracker.Create(matrix, "54. Spiral Matrix: read the outer ring, then move the walls in",
                new MatrixParseOptions { StateClassifier = _ => GridCellState.Default });
            var order = new List<int>();
            grid.Watch(order);
            int top = 0, bottom = matrix.Length - 1, left = 0, right = matrix[0].Length - 1;

            void Read(int r, int c, string side)
            {
                order.Add(matrix[r][c]);
                grid.Visit(r, c, $"{side}: read {matrix[r][c]}", subLabel: $"#{order.Count}");
            }

            while (top <= bottom && left <= right)
            {
                for (int c = left; c <= right; c++) Read(top, c, $"Top row {top}, left to right");
                top++;
                for (int r = top; r <= bottom; r++) Read(r, right, $"Right column {right}, downwards");
                right--;
                if (top <= bottom)
                    for (int c = right; c >= left; c--) Read(bottom, c, $"Bottom row {bottom}, right to left");
                bottom--;
                if (left <= right)
                    for (int r = bottom; r >= top; r--) Read(r, left, $"Left column {left}, upwards");
                left++;
            }

            grid.Snapshot($"The walls have crossed: {Judge.Format(order)}");
            Display.Visualizer(grid);
            """
        },
        new()
        {
            Id = "blind75_73_set_matrix_zeroes",
            Number = 73,
            Title = "Set Matrix Zeroes",
            Category = "Math & Geometry",
            Difficulty = ProblemDifficulty.Medium,
            AcceptanceRate = 57.0,
            IsPremium = false,
            Tags = new List<string> { "Array", "Hash Table", "Matrix" },
            TimeComplexity = "O(m · n)",
            SpaceComplexity = "O(1)",
            DescriptionMarkdown = """
            Given an `m × n` matrix, wherever an element is `0`, set its **whole row and whole column** to `0`. Do it **in place**.

            ### Example 1
            - **Input:** `matrix = [[1,1,1],[1,0,1],[1,1,1]]`
            - **Output:** `[[1,0,1],[0,0,0],[1,0,1]]`

            ### Example 2
            - **Input:** `matrix = [[0,1,2,0],[3,4,5,2],[1,3,1,5]]`
            - **Output:** `[[0,0,0,0],[0,4,5,0],[0,3,1,0]]`

            ### Constraints
            - `1 <= m, n <= 200`
            - `-2^31 <= matrix[i][j] <= 2^31 - 1`
            - **Follow-up:** can you use only `O(1)` extra space?
            """,
            ThinkingProcessMarkdown = """
            **Restate it:** zero the rows and columns of the **original** zeros, without being fooled by zeros you write yourself.

            1. **The trap:** zeroing rows and columns while you scan creates new zeros, which then wipe out more rows and columns. First **find**, then **write**.
            2. **Remember which rows and columns to clear:** two sets (or two boolean arrays) of size `m` and `n`. That's `O(m + n)` extra space.
            3. **Store those marks inside the matrix:** use row 0 as the "clear this column" flags and column 0 as the "clear this row" flags. For every zero at `(r, c)`, set `matrix[r][0] = 0` and `matrix[0][c] = 0`.
            4. **Row 0 and column 0 are also data,** so before using them as flags, remember whether they contained a zero themselves (two booleans). Clear the inner cells from the flags, then clear row 0 and column 0 last if needed.
            5. **Walk Example 2:** row 0 already has zeros (at columns 0 and 3) → remember it. No inner zeros. Columns 0 and 3 are flagged, so every row gets zeros there; finally row 0 is cleared.

            **Pattern to remember:** in-place matrix tricks often borrow the first row/column as scratch space, with special care for their own values.

            **Common mistakes:** clearing while scanning; forgetting to remember row 0 / column 0's own zeros; clearing row 0 or column 0 before the inner cells have read their flags.
            """,
            Approaches = new List<ProblemApproachItem>
            {
                new()
                {
                    Name = "Remember zero rows and columns in sets",
                    Kind = ApproachKind.Naive,
                    TimeComplexity = "O(m · n)",
                    SpaceComplexity = "O(m + n)",
                    Intuition = "Scan once to collect the rows and columns that contain a zero, then scan again and clear every cell in one of them.",
                    BottleneckExplanation = "The two sets cost O(m + n) extra memory; the follow-up asks for O(1).",
                    Code = """
                    void SetZeroesWithSets(int[][] matrix)
                    {
                        var rows = new HashSet<int>();
                        var cols = new HashSet<int>();
                        for (int r = 0; r < matrix.Length; r++)
                            for (int c = 0; c < matrix[0].Length; c++)
                                if (matrix[r][c] == 0) { rows.Add(r); cols.Add(c); }
                        for (int r = 0; r < matrix.Length; r++)
                            for (int c = 0; c < matrix[0].Length; c++)
                                if (rows.Contains(r) || cols.Contains(c)) matrix[r][c] = 0;
                    }

                    var example = new[] { new[] { 1, 1, 1 }, new[] { 1, 0, 1 }, new[] { 1, 1, 1 } };
                    SetZeroesWithSets(example);
                    Console.WriteLine(Judge.Format(example));   // [[1,0,1],[0,0,0],[1,0,1]]
                    """
                },
                new()
                {
                    Name = "First row and column as flags",
                    Kind = ApproachKind.Optimal,
                    TimeComplexity = "O(m · n)",
                    SpaceComplexity = "O(1)",
                    Intuition = "Mark a zero's row in column 0 and its column in row 0, clear the inner cells from those marks, then clear row 0 and column 0 if they had zeros of their own."
                }
            },
            SolutionCode = """
            public class Solution
            {
                public void SetZeroes(int[][] matrix)
                {
                    int rows = matrix.Length, cols = matrix[0].Length;
                    bool firstRowZero = matrix[0].Contains(0);            // row 0 and column 0 will hold the marks,
                    bool firstColZero = matrix.Any(row => row[0] == 0);   // so remember their own zeros first

                    for (int r = 1; r < rows; r++)
                        for (int c = 1; c < cols; c++)
                            if (matrix[r][c] == 0) matrix[r][0] = matrix[0][c] = 0;   // mark this row and this column

                    for (int r = 1; r < rows; r++)
                        for (int c = 1; c < cols; c++)
                            if (matrix[r][0] == 0 || matrix[0][c] == 0) matrix[r][c] = 0;

                    if (firstRowZero) Array.Fill(matrix[0], 0);
                    if (firstColZero) foreach (var row in matrix) row[0] = 0;
                }
            }
            """,
            TestSetupCode = """
            // SetZeroes changes the matrix in place, so the tests look at it afterwards.
            int[][] Zeroed(int[][] matrix)
            {
                sol.SetZeroes(matrix);
                return matrix;
            }
            """,
            Tests = new List<BlindTest>
            {
                new() { Name = "Example 1", Input = "matrix = [[1,1,1],[1,0,1],[1,1,1]]", Expected = "[[1,0,1],[0,0,0],[1,0,1]]", Call = "Zeroed(new[] { new[] { 1, 1, 1 }, new[] { 1, 0, 1 }, new[] { 1, 1, 1 } })" },
                new() { Name = "Example 2", Input = "matrix = [[0,1,2,0],[3,4,5,2],[1,3,1,5]]", Expected = "[[0,0,0,0],[0,4,5,0],[0,3,1,0]]", Call = "Zeroed(new[] { new[] { 0, 1, 2, 0 }, new[] { 3, 4, 5, 2 }, new[] { 1, 3, 1, 5 } })" }
            },
            ExtraTests = new List<BlindTest>
            {
                new() { Name = "No zeros", Input = "matrix = [[1]]", Expected = "[[1]]", Call = "Zeroed(new[] { new[] { 1 } })" },
                new() { Name = "Just a zero", Input = "matrix = [[0]]", Expected = "[[0]]", Call = "Zeroed(new[] { new[] { 0 } })" },
                new() { Name = "A zero in a single row", Input = "matrix = [[1,0]]", Expected = "[[0,0]]", Call = "Zeroed(new[] { new[] { 1, 0 } })" },
                new() { Name = "A zero in a single column", Input = "matrix = [[1],[0]]", Expected = "[[0],[0]]", Call = "Zeroed(new[] { new[] { 1 }, new[] { 0 } })" },
                new() { Name = "Only the first row has a zero", Input = "matrix = [[1,0,3],[4,5,6]]", Expected = "[[0,0,0],[4,0,6]]", Call = "Zeroed(new[] { new[] { 1, 0, 3 }, new[] { 4, 5, 6 } })" }
            },
            StressTestCode = """
            judge.Agree("Random matrices vs remembering sets",
                random =>
                {
                    int rows = random.Next(1, 5), cols = random.Next(1, 5);
                    return Enumerable.Range(0, rows).Select(_ => Enumerable.Range(0, cols).Select(_ => random.Next(4) == 0 ? 0 : random.Next(1, 9)).ToArray()).ToArray();
                },
                matrix => { var copy = matrix.Select(row => row.ToArray()).ToArray(); SetZeroesWithSets(copy); return copy; },
                matrix => { var copy = matrix.Select(row => row.ToArray()).ToArray(); sol.SetZeroes(copy); return copy; });
            """,
            VisualizerKind = "Matrix",
            VisualizationDescription = """
            `[[1,1,1,1],[1,0,1,1],[1,1,1,0]]`. The inner zeros are found and their row and column are marked in column 0
            and row 0 (teal flags). Then every inner cell in a flagged row or column is cleared, and finally row 0 and
            column 0 themselves, which here had no zeros of their own, keep their flag values.
            """,
            VisualizationCode = """
            var matrix = new[] { new[] { 1, 1, 1, 1 }, new[] { 1, 0, 1, 1 }, new[] { 1, 1, 1, 0 } };
            int rows = matrix.Length, cols = matrix[0].Length;
            var grid = MatrixTracker.Create(matrix, "73. Set Matrix Zeroes: flags in the first row and column",
                new MatrixParseOptions { StateClassifier = _ => GridCellState.Default });
            void Show(int r, int c, GridCellState state) => grid.SetCell(r, c, val: matrix[r][c].ToString(), state: state);

            bool firstRowZero = matrix[0].Contains(0);
            bool firstColZero = matrix.Any(row => row[0] == 0);
            grid.Snapshot($"Row 0 and column 0 will hold the flags. Do they contain zeros themselves? Row 0: {(firstRowZero ? "yes" : "no")}, column 0: {(firstColZero ? "yes" : "no")}");

            for (int r = 1; r < rows; r++)
                for (int c = 1; c < cols; c++)
                    if (matrix[r][c] == 0)
                    {
                        matrix[r][0] = matrix[0][c] = 0;
                        Show(r, c, GridCellState.Target);
                        Show(r, 0, GridCellState.Candidate);
                        Show(0, c, GridCellState.Candidate);
                        grid.Snapshot($"A zero at ({r},{c}): flag row {r} in column 0 and column {c} in row 0");
                        grid.SetCell(r, c, state: GridCellState.Default);
                    }

            for (int r = 1; r < rows; r++)
                for (int c = 1; c < cols; c++)
                    if ((matrix[r][0] == 0 || matrix[0][c] == 0) && matrix[r][c] != 0)
                    {
                        matrix[r][c] = 0;
                        Show(r, c, GridCellState.Visited);
                        grid.Snapshot($"({r},{c}) sits in a flagged {(matrix[r][0] == 0 ? $"row {r}" : $"column {c}")}: clear it");
                    }

            if (firstRowZero) { Array.Fill(matrix[0], 0); for (int c = 0; c < cols; c++) Show(0, c, GridCellState.Visited); }
            if (firstColZero) { foreach (var row in matrix) row[0] = 0; for (int r = 0; r < rows; r++) Show(r, 0, GridCellState.Visited); }
            grid.Snapshot(firstRowZero || firstColZero
                ? $"Finally clear row 0 / column 0 because they had zeros of their own: {Judge.Format(matrix)}"
                : $"Row 0 and column 0 had no zeros of their own, so they keep only their flags: {Judge.Format(matrix)}");
            Display.Visualizer(grid);
            """
        },
        new()
        {
            Id = "blind75_371_sum_of_two_integers",
            Number = 371,
            Title = "Sum of Two Integers",
            Category = "Bit Manipulation",
            Difficulty = ProblemDifficulty.Medium,
            AcceptanceRate = 53.5,
            IsPremium = false,
            Tags = new List<string> { "Math", "Bit Manipulation" },
            TimeComplexity = "O(1) (at most 32 rounds)",
            SpaceComplexity = "O(1)",
            DescriptionMarkdown = """
            Given two integers `a` and `b`, return their sum **without using the `+` or `-` operators**.

            ### Example 1
            - **Input:** `a = 1, b = 2`
            - **Output:** `3`

            ### Example 2
            - **Input:** `a = 2, b = 3`
            - **Output:** `5`

            ### Constraints
            - `-1000 <= a, b <= 1000`
            """,
            ThinkingProcessMarkdown = """
            **Restate it:** build addition out of bit operations.

            1. **Add one bit position the way you add digits on paper:** `0+0 = 0`, `0+1 = 1`, `1+1 = 0` carry 1. The sum bit, ignoring the carry, is **XOR**; the carry happens exactly where **both** bits are 1, i.e. **AND**, and it moves one position left.
            2. **So** `a + b = (a ^ b) + ((a & b) << 1)`: a carry-free sum plus the carries. That second `+` is again an addition, so repeat the trick with the new pair until there are no carries left.
            3. **It always stops:** each round pushes the carries at least one position left, so after at most 32 rounds they fall off the end.
            4. **Negative numbers just work** in two's complement: C# `int` arithmetic wraps, and the same bit rules apply.
            5. **Walk 5 + 3** (`101 + 011`): XOR `110`, carry `010`; XOR `100`, carry `100`; XOR `000`, carry `1000`; XOR `1000`, carry 0 → **8**.

            **Pattern to remember:** XOR is addition without carry, AND finds the carries, `<< 1` moves them. The same pieces build subtraction and multiplication in hardware.

            **Common mistakes:** stopping after one round; in languages with unbounded integers (Python) needing a 32-bit mask for negatives (not in C#).
            """,
            Approaches = new List<ProblemApproachItem>
            {
                new()
                {
                    Name = "A full adder, one bit at a time",
                    Kind = ApproachKind.Alternative,
                    TimeComplexity = "O(32)",
                    SpaceComplexity = "O(1)",
                    Intuition = "Walk the 32 bit positions like pencil-and-paper addition: sum bit = x ⊕ y ⊕ carry, and the carry out is set when at least two of them are 1.",
                    Code = """
                    int AddBitByBit(int a, int b)
                    {
                        int result = 0, carry = 0;
                        for (int bit = 0; bit < 32; bit++)
                        {
                            int x = (a >> bit) & 1, y = (b >> bit) & 1;
                            result |= (x ^ y ^ carry) << bit;              // this position's sum bit
                            carry = (x & y) | (x & carry) | (y & carry);   // carry into the next position
                        }
                        return result;
                    }

                    Console.WriteLine(Judge.Format(AddBitByBit(-12, 5)));   // -7
                    """
                },
                new()
                {
                    Name = "XOR for the sum, AND << 1 for the carries, repeat",
                    Kind = ApproachKind.Optimal,
                    TimeComplexity = "O(1)",
                    SpaceComplexity = "O(1)",
                    Intuition = "a ^ b adds without carrying and (a & b) << 1 is exactly the carries; keep adding the carries the same way until there are none."
                }
            },
            SolutionCode = """
            public class Solution
            {
                public int GetSum(int a, int b)
                {
                    while (b != 0)
                    {
                        int carry = (a & b) << 1;   // where both have a 1, a carry moves one position left
                        a ^= b;                     // add every position, ignoring carries
                        b = carry;                  // now add the carries the same way
                    }
                    return a;
                }
            }
            """,
            Tests = new List<BlindTest>
            {
                new() { Name = "Example 1", Input = "a = 1, b = 2", Expected = "3", Call = "sol.GetSum(1, 2)" },
                new() { Name = "Example 2", Input = "a = 2, b = 3", Expected = "5", Call = "sol.GetSum(2, 3)" }
            },
            ExtraTests = new List<BlindTest>
            {
                new() { Name = "Opposites cancel", Input = "a = -1, b = 1", Expected = "0", Call = "sol.GetSum(-1, 1)" },
                new() { Name = "Two negatives", Input = "a = -2, b = -3", Expected = "-5", Call = "sol.GetSum(-2, -3)" },
                new() { Name = "Adding zero", Input = "a = 0, b = 7", Expected = "7", Call = "sol.GetSum(0, 7)" },
                new() { Name = "The extremes", Input = "a = 1000, b = -1000", Expected = "0", Call = "sol.GetSum(1000, -1000)" },
                new() { Name = "Negative plus positive", Input = "a = -12, b = 5", Expected = "-7", Call = "sol.GetSum(-12, 5)" }
            },
            StressTestCode = """
            judge.Agree("Random pairs vs the + operator",
                random => (a: random.Next(-1000, 1001), b: random.Next(-1000, 1001)),
                input => input.a + input.b,
                input => sol.GetSum(input.a, input.b));
            judge.Agree("Random pairs vs the full adder",
                random => (a: random.Next(-1000, 1001), b: random.Next(-1000, 1001)),
                input => AddBitByBit(input.a, input.b),
                input => sol.GetSum(input.a, input.b));
            """,
            VisualizerKind = "Matrix",
            VisualizationDescription = """
            `5 + 3` in 8-bit binary. Each round writes `a`, the carry-free sum `a ^ b`, and the carries `(a & b) << 1`;
            the carries become the next `b`. The carry moves left one place per round until none is left and `a` holds 8.
            """,
            VisualizationCode = """
            int a = 5, b = 3;
            const int Width = 8;
            string Bits(int value) => Convert.ToString(value, 2).PadLeft(Width, '0')[^Width..];
            string[] Row(int value) => Bits(value).Select(c => c.ToString()).ToArray();
            var grid = MatrixTracker.Create(new[] { Row(a), Row(b), Row(0), Row(0) }, "371. Sum of Two Integers: XOR adds, AND finds the carries",
                new MatrixParseOptions { StateClassifier = _ => GridCellState.Default },
                rowHeaders: new[] { "a", "b", "a ^ b", "carry" },
                columnHeaders: Enumerable.Range(0, Width).Select(i => $"2^{Width - 1 - i}"));

            void Write(int row, int value)
            {
                var bits = Bits(value);
                for (int c = 0; c < Width; c++)
                    grid.SetCell(row, c, val: bits[c].ToString(), state: bits[c] == '1' && row == 3 ? GridCellState.Candidate : GridCellState.Default);
            }

            grid.Snapshot($"Add a = {a} and b = {b} without +");
            int round = 0;
            while (b != 0)
            {
                round++;
                int carry = (a & b) << 1;
                int sum = a ^ b;
                Write(0, a);
                Write(1, b);
                Write(2, sum);
                Write(3, carry);
                grid.Snapshot($"Round {round}: a ^ b = {sum} adds without carrying; (a & b) << 1 = {carry} are the carries");
                a = sum;
                b = carry;
                Write(0, a);
                Write(1, b);
                grid.Snapshot(b == 0 ? $"No carries left: the sum is {a}" : $"Now add the carries: a = {a}, b = {b}");
            }
            Display.Visualizer(grid);
            """
        },
        new()
        {
            Id = "blind75_191_number_of_1_bits",
            Number = 191,
            Title = "Number of 1 Bits",
            Category = "Bit Manipulation",
            Difficulty = ProblemDifficulty.Easy,
            AcceptanceRate = 72.5,
            IsPremium = false,
            Tags = new List<string> { "Divide and Conquer", "Bit Manipulation" },
            TimeComplexity = "O(number of 1 bits)",
            SpaceComplexity = "O(1)",
            DescriptionMarkdown = """
            Given a positive integer `n`, return how many `1`s its binary form has (its **Hamming weight**).

            ### Example 1
            - **Input:** `n = 11`
            - **Output:** `3`
            - **Why:** 11 is `1011` in binary.

            ### Example 2
            - **Input:** `n = 128`
            - **Output:** `1`
            - **Why:** `10000000`.

            ### Example 3
            - **Input:** `n = 2147483645`
            - **Output:** `30`
            - **Why:** `1111111111111111111111111111101`.

            ### Constraints
            - `1 <= n <= 2^31 - 1`
            - **Follow-up:** if it's called many times, how would you optimise it?
            """,
            ThinkingProcessMarkdown = """
            **Restate it:** count the set bits.

            1. **Check every bit:** look at `n & 1`, shift right, repeat 32 times. Always 32 rounds.
            2. **Jump straight from one 1 to the next:** `n - 1` flips the lowest 1 to 0 and every 0 below it to 1. So `n & (n - 1)` is `n` with its **lowest 1 removed**, and nothing else changed.
            3. **Repeat until `n` is 0,** counting the rounds: one round per 1 bit, so 11 (`1011`) takes 3 rounds, 128 takes 1.
            4. **Walk 11:** `1011 & 1010 = 1010`, `1010 & 1001 = 1000`, `1000 & 0111 = 0000` → **3**.
            5. **Follow-up:** precompute the counts for all 256 bytes and add up the four bytes of `n`; or use the processor's popcount (`BitOperations.PopCount`).

            **Pattern to remember:** `n & (n - 1)` clears the lowest set bit; `n & -n` isolates it. Checking `n & (n - 1) == 0` tells whether `n` is a power of two.

            **Common mistakes:** looping forever with a signed right shift on negative numbers (not an issue for 1 ≤ n here); counting 32 rounds when the bits are sparse.
            """,
            Approaches = new List<ProblemApproachItem>
            {
                new()
                {
                    Name = "Test all 32 bits",
                    Kind = ApproachKind.Naive,
                    TimeComplexity = "O(32)",
                    SpaceComplexity = "O(1)",
                    Intuition = "Add the lowest bit, shift right, and repeat for every position.",
                    BottleneckExplanation = "It always looks at all 32 positions, even for a number with a single 1.",
                    Code = """
                    int CountBitsOneByOne(int n)
                    {
                        int count = 0;
                        for (int bit = 0; bit < 32; bit++) count += (n >> bit) & 1;
                        return count;
                    }

                    Console.WriteLine(Judge.Format(CountBitsOneByOne(11)));   // 3
                    """
                },
                new()
                {
                    Name = "Clear the lowest 1 until nothing is left",
                    Kind = ApproachKind.Optimal,
                    TimeComplexity = "O(number of 1 bits)",
                    SpaceComplexity = "O(1)",
                    Intuition = "n & (n − 1) removes exactly the lowest 1 bit, so the number of rounds until n is 0 is the answer."
                }
            },
            SolutionCode = """
            public class Solution
            {
                public int HammingWeight(int n)
                {
                    int count = 0;
                    while (n != 0)
                    {
                        n &= n - 1;   // n - 1 flips the lowest 1 (and the 0s below it), so this clears that 1
                        count++;
                    }
                    return count;
                }
            }
            """,
            Tests = new List<BlindTest>
            {
                new() { Name = "Example 1", Input = "n = 11", Expected = "3", Call = "sol.HammingWeight(11)" },
                new() { Name = "Example 2", Input = "n = 128", Expected = "1", Call = "sol.HammingWeight(128)" },
                new() { Name = "Example 3", Input = "n = 2147483645", Expected = "30", Call = "sol.HammingWeight(2147483645)" }
            },
            ExtraTests = new List<BlindTest>
            {
                new() { Name = "One", Input = "n = 1", Expected = "1", Call = "sol.HammingWeight(1)" },
                new() { Name = "A full byte", Input = "n = 255", Expected = "8", Call = "sol.HammingWeight(255)" },
                new() { Name = "The largest n", Input = "n = 2147483647", Expected = "31", Call = "sol.HammingWeight(int.MaxValue)" },
                new() { Name = "A power of two", Input = "n = 1024", Expected = "1", Call = "sol.HammingWeight(1024)" }
            },
            StressTestCode = """
            judge.Agree("Random numbers vs testing every bit",
                random => random.Next(1, int.MaxValue),
                n => CountBitsOneByOne(n),
                n => sol.HammingWeight(n));
            """,
            VisualizerKind = "Matrix",
            VisualizationDescription = """
            `n = 11` in 8-bit binary. Each round writes `n` and `n − 1`: subtracting 1 flips the lowest 1 and the 0s below
            it, so `n & (n − 1)` loses exactly that 1 (red). Three rounds empty `n`, so there are three 1 bits.
            """,
            VisualizationCode = """
            int n = 11;
            const int Width = 8;
            string Bits(int value) => Convert.ToString(value, 2).PadLeft(Width, '0')[^Width..];
            string[] Row(int value) => Bits(value).Select(c => c.ToString()).ToArray();
            var grid = MatrixTracker.Create(new[] { Row(n), Row(n - 1) }, "191. Number of 1 Bits: n & (n − 1) drops the lowest 1",
                new MatrixParseOptions { StateClassifier = _ => GridCellState.Default },
                rowHeaders: new[] { "n", "n − 1" },
                columnHeaders: Enumerable.Range(0, Width).Select(i => $"2^{Width - 1 - i}"));
            int count = 0;
            grid.Watch(() => count);

            void Write(int row, int value, int lowest)
            {
                var bits = Bits(value);
                for (int c = 0; c < Width; c++)
                    grid.SetCell(row, c, val: bits[c].ToString(), state: c == lowest ? GridCellState.Target : GridCellState.Default);
            }

            while (n != 0)
            {
                int lowest = Width - 1 - System.Numerics.BitOperations.TrailingZeroCount(n);   // column of the lowest 1
                Write(0, n, lowest);
                Write(1, n - 1, lowest);
                grid.Snapshot($"n = {n}: n − 1 = {n - 1} flips the lowest 1 (red) and the 0s below it");
                n &= n - 1;
                count++;
                Write(0, n, -1);
                grid.Snapshot($"n & (n − 1) = {n}: that 1 is gone. Count = {count}");
            }
            grid.Snapshot($"n is 0: it had {count} one bits");
            Display.Visualizer(grid);
            """
        },
        new()
        {
            Id = "blind75_338_counting_bits",
            Number = 338,
            Title = "Counting Bits",
            Category = "Bit Manipulation",
            Difficulty = ProblemDifficulty.Easy,
            AcceptanceRate = 80.5,
            IsPremium = false,
            Tags = new List<string> { "Dynamic Programming", "Bit Manipulation" },
            TimeComplexity = "O(n)",
            SpaceComplexity = "O(1) besides the output",
            DescriptionMarkdown = """
            Given an integer `n`, return an array `ans` of length `n + 1` where `ans[i]` is the number of `1`s in the binary form of `i`, for every `i` from 0 to `n`.

            ### Example 1
            - **Input:** `n = 2`
            - **Output:** `[0,1,1]`
            - **Why:** 0 = `0`, 1 = `1`, 2 = `10`.

            ### Example 2
            - **Input:** `n = 5`
            - **Output:** `[0,1,1,2,1,2]`

            ### Constraints
            - `0 <= n <= 10^5`
            - **Follow-up:** can you do it in `O(n)`, without a built-in popcount?
            """,
            ThinkingProcessMarkdown = """
            **Restate it:** popcount for every number up to `n`, reusing earlier answers.

            1. **Counting each number separately** (problem 191) costs up to 32 steps per number: `O(n log n)`.
            2. **Every number is a smaller number with one bit appended.** `i >> 1` is `i` without its last bit, and `i & 1` is that last bit. So `bits[i] = bits[i >> 1] + (i & 1)`, and `i >> 1` is smaller than `i`, so its answer is already known.
            3. **Fill the array from 0 upwards:** one addition per number, `O(n)`.
            4. **Walk 6 = `110`:** `6 >> 1 = 3` (`11`, 2 ones) and 6 ends in 0 → `bits[6] = 2`.
            5. **Another recurrence:** `bits[i] = bits[i & (i − 1)] + 1`, since `i & (i − 1)` is `i` with its lowest 1 removed.

            **Pattern to remember:** DP over numbers via their binary structure: halving (`>> 1`) or dropping the lowest bit (`& (i − 1)`) gives an already-solved smaller number.

            **Common mistakes:** returning `n` values instead of `n + 1`; recomputing popcounts from scratch.
            """,
            Approaches = new List<ProblemApproachItem>
            {
                new()
                {
                    Name = "Count each number's bits separately",
                    Kind = ApproachKind.Naive,
                    TimeComplexity = "O(n log n)",
                    SpaceComplexity = "O(1) besides the output",
                    Intuition = "For every i, clear its lowest 1 bit repeatedly (problem 191) and count the rounds.",
                    BottleneckExplanation = "Each number is counted from scratch, although its answer is one of a smaller number's plus 0 or 1.",
                    Code = """
                    int[] CountBitsSeparately(int n)
                    {
                        var bits = new int[n + 1];
                        for (int i = 0; i <= n; i++)
                            for (int x = i; x != 0; x &= x - 1) bits[i]++;
                        return bits;
                    }

                    Console.WriteLine(Judge.Format(CountBitsSeparately(5)));   // [0,1,1,2,1,2]
                    """
                },
                new()
                {
                    Name = "Reuse the answer for i >> 1",
                    Kind = ApproachKind.Optimal,
                    TimeComplexity = "O(n)",
                    SpaceComplexity = "O(1) besides the output",
                    RecurrenceRelation = "bits[0] = 0\nbits[i] = bits[i >> 1] + (i & 1)",
                    Intuition = "i is i >> 1 with its last bit appended, so its count is that smaller number's count plus the last bit."
                }
            },
            SolutionCode = """
            public class Solution
            {
                public int[] CountBits(int n)
                {
                    var bits = new int[n + 1];
                    for (int i = 1; i <= n; i++)
                        bits[i] = bits[i >> 1] + (i & 1);   // i is (i >> 1) with one more bit on the end
                    return bits;
                }
            }
            """,
            Tests = new List<BlindTest>
            {
                new() { Name = "Example 1", Input = "n = 2", Expected = "[0,1,1]", Call = "sol.CountBits(2)" },
                new() { Name = "Example 2", Input = "n = 5", Expected = "[0,1,1,2,1,2]", Call = "sol.CountBits(5)" }
            },
            ExtraTests = new List<BlindTest>
            {
                new() { Name = "Zero only", Input = "n = 0", Expected = "[0]", Call = "sol.CountBits(0)" },
                new() { Name = "One", Input = "n = 1", Expected = "[0,1]", Call = "sol.CountBits(1)" },
                new() { Name = "Up to a power of two", Input = "n = 8", Expected = "[0,1,1,2,1,2,2,3,1]", Call = "sol.CountBits(8)" }
            },
            StressTestCode = """
            judge.Agree("Random n vs counting separately",
                random => random.Next(0, 300),
                n => CountBitsSeparately(n),
                n => sol.CountBits(n));
            """,
            VisualizerKind = "ArrayPointers",
            VisualizationDescription = """
            `n = 8`. Each cell `i` is filled from the cell `i >> 1` (i without its last bit, highlighted with it) plus
            the last bit `i & 1`, so every count is one lookup and one addition.
            """,
            VisualizationCode = """
            int n = 8;
            var bits = new int[n + 1];
            var tracker = VisualizerRecorder.CreateArray(bits, title: "338. Counting Bits: reuse the count of i >> 1");
            string Binary(int value) => Convert.ToString(value, 2);
            tracker.Step("bits[0] = 0: zero has no 1 bits", highlight: new[] { 0 });

            for (int i = 1; i <= n; i++)
            {
                bits[i] = bits[i >> 1] + (i & 1);
                tracker.Step($"{i} = {Binary(i)}: without its last bit it is {i >> 1} = {Binary(i >> 1)} ({bits[i >> 1]} ones), and it ends in {i & 1}, so bits[{i}] = {bits[i]}",
                    pointers: new { i, half = i >> 1 }, highlight: new[] { i >> 1, i });
            }

            tracker.Step($"Done: {Judge.Format(bits)}");
            Display.Visualizer(tracker);
            """
        },
        new()
        {
            Id = "blind75_268_missing_number",
            Number = 268,
            Title = "Missing Number",
            Category = "Bit Manipulation",
            Difficulty = ProblemDifficulty.Easy,
            AcceptanceRate = 72.2,
            IsPremium = false,
            Tags = new List<string> { "Array", "Hash Table", "Math", "Binary Search", "Bit Manipulation" },
            TimeComplexity = "O(n)",
            SpaceComplexity = "O(1)",
            DescriptionMarkdown = """
            An array `nums` holds `n` **distinct** numbers taken from the range `0 … n` (that's `n + 1` possible numbers), so exactly one is missing. Return it.

            ### Example 1
            - **Input:** `nums = [3,0,1]`
            - **Output:** `2`

            ### Example 2
            - **Input:** `nums = [0,1]`
            - **Output:** `2`
            - **Why:** `n = 2`, so the range is 0, 1, 2.

            ### Example 3
            - **Input:** `nums = [9,6,4,2,3,5,7,0,1]`
            - **Output:** `8`

            ### Constraints
            - `n == nums.length`, `1 <= n <= 10^4`
            - `0 <= nums[i] <= n`, all distinct.
            - **Follow-up:** `O(1)` extra space and `O(n)` time?
            """,
            ThinkingProcessMarkdown = """
            **Restate it:** which of `0 … n` is absent?

            1. **Sorting** puts each number at its own index until the gap: `O(n log n)`. **A hash set** of the numbers finds it in `O(n)` but needs `O(n)` memory.
            2. **Arithmetic:** `0 + 1 + … + n = n(n + 1)/2`; subtract the array's sum and what's left is the missing number. `O(1)` memory.
            3. **XOR, without any overflow worries:** `x ^ x = 0` and `x ^ 0 = x`, and order doesn't matter. XOR together every index `0 … n` and every value in the array: each present number meets its twin and cancels, leaving only the missing one.
            4. **Walk `[3,0,1]`:** start with `n = 3`, then `^ (0 ^ 3) ^ (1 ^ 0) ^ (2 ^ 1)` → the 3s, 0s and 1s cancel → **2**.

            **Pattern to remember:** "every element appears twice except one" → XOR everything. Pairing indices with values turns "one missing" into that form.

            **Common mistakes:** forgetting `n` itself (the index loop only covers 0 … n − 1); overflow with the sum formula on larger ranges (use `long`, or XOR).
            """,
            Approaches = new List<ProblemApproachItem>
            {
                new()
                {
                    Name = "Sort, then find the first gap",
                    Kind = ApproachKind.Naive,
                    TimeComplexity = "O(n log n)",
                    SpaceComplexity = "O(1) (sorting in place)",
                    Intuition = "After sorting, nums[i] == i until the missing number; if nothing breaks, the missing one is n.",
                    BottleneckExplanation = "Sorting costs O(n log n) just to find one number; XOR or a sum finds it in one pass.",
                    Code = """
                    int MissingBySorting(int[] nums)
                    {
                        var sorted = nums.OrderBy(x => x).ToArray();
                        for (int i = 0; i < sorted.Length; i++)
                            if (sorted[i] != i) return i;
                        return sorted.Length;
                    }

                    Console.WriteLine(Judge.Format(MissingBySorting(new[] { 3, 0, 1 })));   // 2
                    """
                },
                new()
                {
                    Name = "Expected sum minus actual sum",
                    Kind = ApproachKind.Alternative,
                    TimeComplexity = "O(n)",
                    SpaceComplexity = "O(1)",
                    Intuition = "0 + 1 + … + n = n(n + 1)/2; whatever the array's sum falls short by is the missing number.",
                    Code = """
                    int MissingBySum(int[] nums) => nums.Length * (nums.Length + 1) / 2 - nums.Sum();

                    Console.WriteLine(Judge.Format(MissingBySum(new[] { 9, 6, 4, 2, 3, 5, 7, 0, 1 })));   // 8
                    """
                },
                new()
                {
                    Name = "XOR the indices and the values",
                    Kind = ApproachKind.Optimal,
                    TimeComplexity = "O(n)",
                    SpaceComplexity = "O(1)",
                    Intuition = "Each present number cancels its matching index under XOR; only the missing number survives."
                }
            },
            SolutionCode = """
            public class Solution
            {
                public int MissingNumber(int[] nums)
                {
                    int missing = nums.Length;             // n itself: the one index the loop never reaches
                    for (int i = 0; i < nums.Length; i++)
                        missing ^= i ^ nums[i];            // a number that is present cancels with its own index
                    return missing;
                }
            }
            """,
            Tests = new List<BlindTest>
            {
                new() { Name = "Example 1", Input = "nums = [3,0,1]", Expected = "2", Call = "sol.MissingNumber(new[] { 3, 0, 1 })" },
                new() { Name = "Example 2", Input = "nums = [0,1]", Expected = "2", Call = "sol.MissingNumber(new[] { 0, 1 })" },
                new() { Name = "Example 3", Input = "nums = [9,6,4,2,3,5,7,0,1]", Expected = "8", Call = "sol.MissingNumber(new[] { 9, 6, 4, 2, 3, 5, 7, 0, 1 })" }
            },
            ExtraTests = new List<BlindTest>
            {
                new() { Name = "Only zero", Input = "nums = [0]", Expected = "1", Call = "sol.MissingNumber(new[] { 0 })" },
                new() { Name = "Zero is missing", Input = "nums = [1]", Expected = "0", Call = "sol.MissingNumber(new[] { 1 })" },
                new() { Name = "Zero missing from two", Input = "nums = [1,2]", Expected = "0", Call = "sol.MissingNumber(new[] { 1, 2 })" }
            },
            StressTestCode = """
            judge.Agree("Random ranges vs sorting",
                random =>
                {
                    int n = random.Next(1, 20), gap = random.Next(0, n + 1);
                    return Enumerable.Range(0, n + 1).Where(x => x != gap).OrderBy(_ => random.Next()).ToArray();
                },
                nums => MissingBySorting(nums),
                nums => sol.MissingNumber(nums));
            """,
            VisualizerKind = "ArrayPointers",
            VisualizationDescription = """
            `[3,0,1]`. `missing` starts at n = 3 and XORs in each index with the value stored there; the binary in each
            step shows present numbers cancelling in pairs. What remains at the end, 2, is the number with no partner.
            """,
            VisualizationCode = """
            var nums = new[] { 3, 0, 1 };
            var tracker = VisualizerRecorder.CreateArray(nums, title: "268. Missing Number: everything present cancels under XOR");
            string Binary(int value) => Convert.ToString(value, 2).PadLeft(2, '0');
            int missing = nums.Length;
            tracker.Watch(() => missing);
            tracker.Step($"Start with n = {nums.Length} ({Binary(nums.Length)}), the one index the loop never visits");

            for (int i = 0; i < nums.Length; i++)
            {
                int before = missing;
                missing ^= i ^ nums[i];
                tracker.Step($"XOR in index {i} and value {nums[i]}: {Binary(before)} ^ {Binary(i)} ^ {Binary(nums[i])} = {Binary(missing)} ({missing})",
                    pointers: new { i }, highlight: new[] { i });
            }

            tracker.Step($"0, 1 and 3 each appeared twice (as an index or n, and as a value) and cancelled; {missing} appeared only once: it is missing");
            Display.Visualizer(tracker);
            """
        },
        new()
        {
            Id = "blind75_190_reverse_bits",
            Number = 190,
            Title = "Reverse Bits",
            Category = "Bit Manipulation",
            Difficulty = ProblemDifficulty.Easy,
            AcceptanceRate = 60.5,
            IsPremium = false,
            Tags = new List<string> { "Divide and Conquer", "Bit Manipulation" },
            TimeComplexity = "O(32)",
            SpaceComplexity = "O(1)",
            DescriptionMarkdown = """
            Reverse the bits of a 32-bit unsigned integer: bit 0 swaps with bit 31, bit 1 with bit 30, and so on.

            ### Example 1
            - **Input:** `n = 00000010100101000001111010011100` (43261596)
            - **Output:** `00111001011110000010100101000000` (964176192)

            ### Example 2
            - **Input:** `n = 11111111111111111111111111111101` (4294967293)
            - **Output:** `10111111111111111111111111111111` (3221225471)

            ### Constraints
            - The input is a 32-bit unsigned integer.
            - **Follow-up:** if it's called many times, how would you optimise it?
            """,
            ThinkingProcessMarkdown = """
            **Restate it:** read `n`'s bits from the right and write them from the left.

            1. **Peel bits off the right end of `n`:** `n & 1` is the lowest bit, and `n >>= 1` drops it.
            2. **Push them onto the right end of the result:** `result = (result << 1) | bit` shifts what you have left and appends the new bit. The first bit you peel (n's lowest) is pushed first, so after 32 rounds it has travelled to the top: the order is reversed.
            3. **Exactly 32 rounds,** even when `n`'s high bits are 0: those zeros must land in the result's low positions.
            4. **Why `uint`:** with a signed `int`, `>>` copies the sign bit and the top bit means "negative"; unsigned shifts keep it simple.
            5. **Follow-up:** reverse whole bytes with a 256-entry lookup table, or swap halves, then quarters, … with masks (`0xFFFF0000`, `0xFF00FF00`, …) in five steps.

            **Pattern to remember:** moving bits between numbers: `(n >> i) & 1` reads bit i, `result << 1 | bit` appends a bit.

            **Common mistakes:** stopping when `n` becomes 0 (the leading zeros are lost); using a signed type and arithmetic shifts.
            """,
            Approaches = new List<ProblemApproachItem>
            {
                new()
                {
                    Name = "Through a binary string",
                    Kind = ApproachKind.Naive,
                    TimeComplexity = "O(32)",
                    SpaceComplexity = "O(32) for the strings",
                    Intuition = "Write n as 32 characters of 0s and 1s, reverse the string, and parse it back.",
                    BottleneckExplanation = "It builds and parses strings for what a few shifts do directly, which matters when it's called millions of times.",
                    Code = """
                    uint ReverseByString(uint n) =>
                        Convert.ToUInt32(new string(Convert.ToString(n, 2).PadLeft(32, '0').Reverse().ToArray()), 2);

                    Console.WriteLine(Judge.Format(ReverseByString(43261596)));   // 964176192
                    """
                },
                new()
                {
                    Name = "Peel from the right, push onto the result",
                    Kind = ApproachKind.Optimal,
                    TimeComplexity = "O(32)",
                    SpaceComplexity = "O(1)",
                    Intuition = "32 times: take n's lowest bit, append it to result (result << 1 | bit), and shift n right."
                }
            },
            SolutionCode = """
            public class Solution
            {
                public uint reverseBits(uint n)
                {
                    uint result = 0;
                    for (int i = 0; i < 32; i++)
                    {
                        result = (result << 1) | (n & 1);   // append n's lowest bit to the end of result
                        n >>= 1;                            // and drop it from n
                    }
                    return result;
                }
            }
            """,
            Tests = new List<BlindTest>
            {
                new() { Name = "Example 1", Input = "n = 43261596", Expected = "964176192", Call = "sol.reverseBits(43261596u)" },
                new() { Name = "Example 2", Input = "n = 4294967293", Expected = "3221225471", Call = "sol.reverseBits(4294967293u)" }
            },
            ExtraTests = new List<BlindTest>
            {
                new() { Name = "Zero", Input = "n = 0", Expected = "0", Call = "sol.reverseBits(0u)" },
                new() { Name = "The lowest bit goes to the top", Input = "n = 1", Expected = "2147483648", Call = "sol.reverseBits(1u)" },
                new() { Name = "The top bit comes to the bottom", Input = "n = 2147483648", Expected = "1", Call = "sol.reverseBits(2147483648u)" },
                new() { Name = "All ones", Input = "n = 4294967295", Expected = "4294967295", Call = "sol.reverseBits(4294967295u)" }
            },
            StressTestCode = """
            judge.Agree("Random numbers vs the string version",
                random => (uint)random.NextInt64(0, 1L << 32),
                n => ReverseByString(n),
                n => sol.reverseBits(n));
            """,
            VisualizerKind = "Matrix",
            VisualizationDescription = """
            Example 1 in 32 bits. Each step takes the next bit from the right end of `n` (highlighted) and pushes it onto
            the right end of `result`; everything already in `result` moves one place left, so after 32 steps the bits
            are in reverse order.
            """,
            VisualizationCode = """
            uint n = 43261596;
            const int Width = 32;
            string Bits(uint value) => Convert.ToString(value, 2).PadLeft(Width, '0');
            string[] Row(uint value) => Bits(value).Select(c => c.ToString()).ToArray();
            var original = Bits(n);
            var grid = MatrixTracker.Create(new[] { Row(n), Enumerable.Repeat("", Width).ToArray() }, "190. Reverse Bits: peel from the right, push onto the result",
                new MatrixParseOptions { StateClassifier = _ => GridCellState.Default },
                rowHeaders: new[] { "n", "result" });

            uint result = 0;
            for (int i = 0; i < 32; i++)
            {
                uint bit = n & 1;
                result = (result << 1) | bit;
                n >>= 1;
                for (int c = 0; c < Width; c++) grid.SetCell(0, c, state: c == Width - 1 - i ? GridCellState.Current : c > Width - 1 - i ? GridCellState.Visited : GridCellState.Default);
                string pushed = Bits(result)[^(i + 1)..];
                for (int c = 0; c <= i; c++) grid.SetCell(1, Width - 1 - i + c, val: pushed[c].ToString(), state: c == i ? GridCellState.Current : GridCellState.Default);
                grid.Snapshot($"Bit {i} of n is {bit}: push it onto the right end of result, which shifts the {i} bits already there one place left");
            }

            grid.Snapshot($"All 32 bits moved: {Bits(result)} = {result}");
            Display.Visualizer(grid);
            """
        },
        new()
        {
            Id = "blind75_295_find_median_from_data_stream",
            Number = 295,
            Title = "Find Median from Data Stream",
            Category = "Heap / Priority Queue",
            Difficulty = ProblemDifficulty.Hard,
            AcceptanceRate = 52.0,
            IsPremium = false,
            Tags = new List<string> { "Two Pointers", "Design", "Sorting", "Heap (Priority Queue)", "Data Stream" },
            TimeComplexity = "O(log n) per AddNum, O(1) per FindMedian",
            SpaceComplexity = "O(n)",
            DescriptionMarkdown = """
            Design a `MedianFinder` that receives numbers one at a time and can report the **median** of everything received so far:

            - `AddNum(num)` adds a number.
            - `FindMedian()` returns the median: the middle value of the sorted numbers, or the average of the two middle values when the count is even.

            ### Example 1
            - **Input:** `["MedianFinder","addNum","addNum","findMedian","addNum","findMedian"]`, `[[],[1],[2],[],[3],[]]`
            - **Output:** `[null,null,null,1.5,null,2.0]`
            - **Why:** `[1,2]` → (1 + 2) / 2 = 1.5; `[1,2,3]` → 2.

            ### Example 2
            - **Input:** `["MedianFinder","addNum","findMedian","addNum","findMedian","addNum","findMedian","addNum","findMedian"]`, `[[],[5],[],[15],[],[1],[],[3],[]]`
            - **Output:** `[null,null,5.0,null,10.0,null,5.0,null,4.0]`

            ### Constraints
            - `-10^5 <= num <= 10^5`
            - `FindMedian` is only called after at least one number was added; at most `5 · 10^4` calls in total.
            """,
            ThinkingProcessMarkdown = """
            **Restate it:** keep the numbers so that the middle one (or two) can be read instantly while numbers keep arriving.

            1. **Sorting on every query** is `O(n log n)` each time; keeping a sorted list makes queries `O(1)` but each insert `O(n)` (shifting elements).
            2. **You only ever need the middle.** Split the numbers into a **lower half** and an **upper half**, where everything in the lower half ≤ everything in the upper half. The median is the top of the lower half, or the average of the two tops.
            3. **Heaps give the tops fast:** a **max-heap** for the lower half (its biggest on top) and a **min-heap** for the upper half (its smallest on top). Both push and pop in `O(log n)`.
            4. **Adding a number:** push it into the lower half, then move the lower half's biggest to the upper half (so the order between halves stays right); if the upper half became bigger, move its smallest back. The lower half then has the same size or one more.
            5. **Walk Example 1:** add 1 → lower {1}. Add 2 → lower {1}, upper {2} → median 1.5. Add 3 → lower {2, 1}, upper {3} → median 2.

            **Pattern to remember:** "running median" / "k-th element of a stream" → two heaps balanced around the middle.

            **Common mistakes:** unbalanced halves; forgetting that `PriorityQueue` is a min-heap (the lower half needs a reversed comparer); integer division when averaging.
            """,
            Approaches = new List<ProblemApproachItem>
            {
                new()
                {
                    Name = "Keep a sorted list",
                    Kind = ApproachKind.Naive,
                    TimeComplexity = "O(n) per AddNum, O(1) per FindMedian",
                    SpaceComplexity = "O(n)",
                    Intuition = "Insert each number at its sorted position (binary search for the spot); the median is the middle element or two.",
                    BottleneckExplanation = "Inserting into the middle of a list shifts everything after it: O(n) per number, which adds up to O(n²).",
                    Code = """
                    class SortedListMedianFinder
                    {
                        private readonly List<int> _sorted = new();
                        public void AddNum(int num)
                        {
                            int index = _sorted.BinarySearch(num);
                            _sorted.Insert(index < 0 ? ~index : index, num);   // shifts the rest of the list
                        }
                        public double FindMedian()
                        {
                            int n = _sorted.Count;
                            return n % 2 == 1 ? _sorted[n / 2] : (_sorted[n / 2 - 1] + (double)_sorted[n / 2]) / 2;
                        }
                    }

                    var finder = new SortedListMedianFinder();
                    foreach (int num in new[] { 1, 2, 3 }) finder.AddNum(num);
                    Console.WriteLine(Judge.Format(finder.FindMedian()));   // 2
                    """
                },
                new()
                {
                    Name = "Two heaps: the lower and the upper half",
                    Kind = ApproachKind.Optimal,
                    TimeComplexity = "O(log n) add, O(1) median",
                    SpaceComplexity = "O(n)",
                    Intuition = "A max-heap holds the smaller half and a min-heap the larger half, balanced so their tops are the middle values."
                }
            },
            SolutionCode = """
            public class MedianFinder
            {
                private readonly PriorityQueue<int, int> _lower = new(Comparer<int>.Create((x, y) => y.CompareTo(x)));   // max-heap: the smaller half
                private readonly PriorityQueue<int, int> _upper = new();                                                // min-heap: the larger half

                public void AddNum(int num)
                {
                    _lower.Enqueue(num, num);
                    int biggestSmall = _lower.Dequeue();         // the lower half's biggest moves up, so every upper value >= every lower value
                    _upper.Enqueue(biggestSmall, biggestSmall);
                    if (_upper.Count > _lower.Count)             // keep the lower half the same size or one bigger
                    {
                        int smallestLarge = _upper.Dequeue();
                        _lower.Enqueue(smallestLarge, smallestLarge);
                    }
                }

                public double FindMedian() =>
                    _lower.Count > _upper.Count ? _lower.Peek() : (_lower.Peek() + (double)_upper.Peek()) / 2.0;
            }
            """,
            TestSetupCode = """
            // Replays LeetCode's operation list; void calls answer null, as in LeetCode's output.
            List<object> Replay(string[] operations, int[] arguments)
            {
                MedianFinder finder = null;
                var answers = new List<object>();
                for (int i = 0; i < operations.Length; i++)
                {
                    switch (operations[i])
                    {
                        case "MedianFinder": finder = new MedianFinder(); answers.Add(null); break;
                        case "addNum": finder.AddNum(arguments[i]); answers.Add(null); break;
                        case "findMedian": answers.Add(finder.FindMedian()); break;
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
                    Input = """["MedianFinder","addNum","addNum","findMedian","addNum","findMedian"], [[],[1],[2],[],[3],[]]""",
                    Expected = "[null,null,null,1.5,null,2.0]",
                    Call = """Replay(new[] { "MedianFinder", "addNum", "addNum", "findMedian", "addNum", "findMedian" }, new[] { 0, 1, 2, 0, 3, 0 })"""
                },
                new()
                {
                    Name = "Example 2",
                    Input = """["MedianFinder","addNum","findMedian","addNum","findMedian","addNum","findMedian","addNum","findMedian"], [[],[5],[],[15],[],[1],[],[3],[]]""",
                    Expected = "[null,null,5.0,null,10.0,null,5.0,null,4.0]",
                    Call = """Replay(new[] { "MedianFinder", "addNum", "findMedian", "addNum", "findMedian", "addNum", "findMedian", "addNum", "findMedian" }, new[] { 0, 5, 0, 15, 0, 1, 0, 3, 0 })"""
                }
            },
            ExtraTests = new List<BlindTest>
            {
                new()
                {
                    Name = "Negative numbers",
                    Input = """["MedianFinder","addNum","addNum","findMedian"], [[],[-1],[-2],[]]""",
                    Expected = "[null,null,null,-1.5]",
                    Call = """Replay(new[] { "MedianFinder", "addNum", "addNum", "findMedian" }, new[] { 0, -1, -2, 0 })"""
                },
                new()
                {
                    Name = "Repeated numbers",
                    Input = """["MedianFinder","addNum","addNum","addNum","findMedian"], [[],[2],[2],[2],[]]""",
                    Expected = "[null,null,null,null,2.0]",
                    Call = """Replay(new[] { "MedianFinder", "addNum", "addNum", "addNum", "findMedian" }, new[] { 0, 2, 2, 2, 0 })"""
                },
                new()
                {
                    Name = "Arriving in descending order",
                    Input = """["MedianFinder","addNum","addNum","addNum","addNum","findMedian"], [[],[4],[3],[2],[1],[]]""",
                    Expected = "[null,null,null,null,null,2.5]",
                    Call = """Replay(new[] { "MedianFinder", "addNum", "addNum", "addNum", "addNum", "findMedian" }, new[] { 0, 4, 3, 2, 1, 0 })"""
                }
            },
            StressTestCode = """
            judge.Agree("Random streams vs a sorted list",
                random => Enumerable.Range(0, random.Next(1, 20)).Select(_ => random.Next(-20, 21)).ToArray(),
                stream => { var f = new SortedListMedianFinder(); return stream.Select(x => { f.AddNum(x); return f.FindMedian(); }).ToList(); },
                stream => { var f = new MedianFinder(); return stream.Select(x => { f.AddNum(x); return f.FindMedian(); }).ToList(); });
            """,
            VisualizerKind = "Bars",
            VisualizationDescription = """
            Example 2's stream 5, 15, 1, 3 plus 8 and 2. The bars show every number received so far in sorted order (only
            to see the middle), with the median highlighted. Underneath, `lower` (a max-heap) and `upper` (a min-heap)
            hold the two halves; their tops are always the middle values.
            """,
            VisualizationCode = """
            var lower = new PriorityQueue<int, int>(Comparer<int>.Create((x, y) => y.CompareTo(x)));
            var upper = new PriorityQueue<int, int>();
            var sortedView = new List<int>();   // only for the drawing
            var tracker = VisualizerRecorder.CreateBars(sortedView, title: "295. Find Median: two heaps around the middle");
            tracker.Watch(() => lower.UnorderedItems.Select(e => e.Element).OrderByDescending(x => x).ToList(), "lower half (max-heap)");
            tracker.Watch(() => upper.UnorderedItems.Select(e => e.Element).OrderBy(x => x).ToList(), "upper half (min-heap)");

            double Median() => lower.Count > upper.Count ? lower.Peek() : (lower.Peek() + (double)upper.Peek()) / 2.0;
            IEnumerable<int> Middle() => sortedView.Count % 2 == 1 ? new[] { sortedView.Count / 2 } : new[] { sortedView.Count / 2 - 1, sortedView.Count / 2 };

            foreach (int num in new[] { 5, 15, 1, 3, 8, 2 })
            {
                lower.Enqueue(num, num);
                int biggestSmall = lower.Dequeue();
                upper.Enqueue(biggestSmall, biggestSmall);
                bool rebalanced = upper.Count > lower.Count;
                if (rebalanced)
                {
                    int smallestLarge = upper.Dequeue();
                    lower.Enqueue(smallestLarge, smallestLarge);
                }

                sortedView.Add(num);
                sortedView.Sort();
                tracker.Step($"Add {num}: it goes into lower, lower's biggest ({biggestSmall}) moves to upper{(rebalanced ? ", and upper's smallest comes back to keep lower the bigger half" : "")}. Median = {Median():0.#}",
                    highlight: Middle());
            }

            tracker.Step($"The tops of the two heaps are the middle of the stream: median {Median():0.#}", highlight: Middle());
            Display.Visualizer(tracker);
            """
        }
    };
}
