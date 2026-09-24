using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PdfEditorApp.Plugins.CSharpEditor.Services;

public interface IBlindProgressService
{
    event Action<int, bool>? SolvedStatusChanged;
    event Action<int, bool>? BookmarkStatusChanged;

    Task<IReadOnlySet<int>> GetSolvedProblemNumbersAsync();
    Task<IReadOnlySet<int>> GetBookmarkedProblemNumbersAsync();
    Task SetProblemSolvedAsync(int problemNumber, bool isSolved);
    Task SetProblemBookmarkedAsync(int problemNumber, bool isBookmarked);
    bool IsProblemSolved(int problemNumber);
    bool IsProblemBookmarked(int problemNumber);
}
