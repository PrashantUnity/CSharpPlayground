using System;
using System.IO;
using System.Threading.Tasks;
using PdfEditorApp.Plugins.CSharpEditor.Models.Git;
using PdfEditorApp.Plugins.CSharpEditor.Services.Toolchains;
using PdfEditorApp.Plugins.CSharpEditor.Services.Workspace.Git;
using Xunit;

namespace CSharpEditorPlugin.Tests;

public class GitServiceAndStatusParserTests
{
    [Fact]
    public void GitStatusParser_ParsesPorcelainV2_Accurately()
    {
        string v2Output = """
            # branch.oid 4a25c276326e0b749d68d1844b207ecda11a43a0
            # branch.head feature/git-management
            # branch.upstream origin/feature/git-management
            # branch.ab +2 -1
            1 M. N... 100644 100644 100644 e69de29bb2d1d6434b8b29ae775ad8c2e48c5391 e69de29bb2d1d6434b8b29ae775ad8c2e48c5391 src/StagedModified.cs
            1 .M N... 100644 100644 100644 e69de29bb2d1d6434b8b29ae775ad8c2e48c5391 e69de29bb2d1d6434b8b29ae775ad8c2e48c5391 src/WorkingModified.cs
            1 MM N... 100644 100644 100644 e69de29bb2d1d6434b8b29ae775ad8c2e48c5391 e69de29bb2d1d6434b8b29ae775ad8c2e48c5391 src/BothModified.cs
            1 A. N... 000000 100644 100644 0000000000000000000000000000000000000000 e69de29bb2d1d6434b8b29ae775ad8c2e48c5391 src/Added.cs
            1 D. N... 100644 000000 000000 e69de29bb2d1d6434b8b29ae775ad8c2e48c5391 0000000000000000000000000000000000000000 src/Deleted.cs
            2 R. N... 100644 100644 100644 e69de29bb2d1d6434b8b29ae775ad8c2e48c5391 e69de29bb2d1d6434b8b29ae775ad8c2e48c5391 R100 src/NewName.cs	src/OldName.cs
            ? src/Untracked.cs
            u MM N... 100644 100644 100644 100644 e69de29bb2d1d6434b8b29ae775ad8c2e48c5391 e69de29bb2d1d6434b8b29ae775ad8c2e48c5391 e69de29bb2d1d6434b8b29ae775ad8c2e48c5391 src/Conflict.cs
            """;

        var status = GitStatusParser.Parse(v2Output);

        Assert.True(status.IsRepository);
        Assert.Equal("feature/git-management", status.CurrentBranch);
        Assert.Equal("origin/feature/git-management", status.UpstreamBranch);
        Assert.Equal(2, status.AheadCount);
        Assert.Equal(1, status.BehindCount);

        // Staged changes: StagedModified, BothModified, Added, Deleted, Renamed
        Assert.Equal(5, status.StagedChanges.Count);
        Assert.Contains(status.StagedChanges, c => c.RelativePath == "src/StagedModified.cs" && c.StatusKind == GitFileStatusKind.Modified && c.IsStaged);
        Assert.Contains(status.StagedChanges, c => c.RelativePath == "src/BothModified.cs" && c.StatusKind == GitFileStatusKind.Modified && c.IsStaged);
        Assert.Contains(status.StagedChanges, c => c.RelativePath == "src/Added.cs" && c.StatusKind == GitFileStatusKind.Added && c.IsStaged);
        Assert.Contains(status.StagedChanges, c => c.RelativePath == "src/Deleted.cs" && c.StatusKind == GitFileStatusKind.Deleted && c.IsStaged);
        Assert.Contains(status.StagedChanges, c => c.RelativePath == "src/NewName.cs" && c.StatusKind == GitFileStatusKind.Renamed && c.OldRelativePath == "src/OldName.cs");

        // Working tree changes: WorkingModified, BothModified, Untracked, Conflict
        Assert.Equal(4, status.WorkingChanges.Count);
        Assert.Contains(status.WorkingChanges, c => c.RelativePath == "src/WorkingModified.cs" && c.StatusKind == GitFileStatusKind.Modified && !c.IsStaged);
        Assert.Contains(status.WorkingChanges, c => c.RelativePath == "src/BothModified.cs" && c.StatusKind == GitFileStatusKind.Modified && !c.IsStaged);
        Assert.Contains(status.WorkingChanges, c => c.RelativePath == "src/Untracked.cs" && c.StatusKind == GitFileStatusKind.Untracked && !c.IsStaged);
        Assert.Contains(status.WorkingChanges, c => c.RelativePath == "src/Conflict.cs" && c.StatusKind == GitFileStatusKind.Conflicted && !c.IsStaged);

        Assert.Equal(9, status.TotalUncommittedCount);
        Assert.False(status.IsClean);
    }

    [Fact]
    public void GitStatusParser_ParsesPorcelainV1_Fallback()
    {
        string v1Output = """
            ## main...origin/main [ahead 1, behind 2]
             M modified.cs
            M  staged.cs
            MM both.cs
            A  added.cs
            D  deleted.cs
            ?? untracked.cs
            """;

        var status = GitStatusParser.Parse(v1Output);

        Assert.True(status.IsRepository);
        Assert.Equal("main", status.CurrentBranch);
        Assert.Equal("origin/main", status.UpstreamBranch);
        Assert.Equal(1, status.AheadCount);
        Assert.Equal(2, status.BehindCount);

        Assert.Contains(status.StagedChanges, c => c.RelativePath == "staged.cs" && c.StatusKind == GitFileStatusKind.Modified);
        Assert.Contains(status.StagedChanges, c => c.RelativePath == "both.cs" && c.StatusKind == GitFileStatusKind.Modified);
        Assert.Contains(status.StagedChanges, c => c.RelativePath == "added.cs" && c.StatusKind == GitFileStatusKind.Added);
        Assert.Contains(status.StagedChanges, c => c.RelativePath == "deleted.cs" && c.StatusKind == GitFileStatusKind.Deleted);

        Assert.Contains(status.WorkingChanges, c => c.RelativePath == "modified.cs" && c.StatusKind == GitFileStatusKind.Modified);
        Assert.Contains(status.WorkingChanges, c => c.RelativePath == "both.cs" && c.StatusKind == GitFileStatusKind.Modified);
        Assert.Contains(status.WorkingChanges, c => c.RelativePath == "untracked.cs" && c.StatusKind == GitFileStatusKind.Untracked);
    }

    [Fact]
    public void GitStatusParser_HandlesCleanRepository()
    {
        string cleanOutput = """
            # branch.oid 4a25c276326e0b749d68d1844b207ecda11a43a0
            # branch.head main
            # branch.upstream origin/main
            # branch.ab +0 -0
            """;

        var status = GitStatusParser.Parse(cleanOutput);

        Assert.True(status.IsRepository);
        Assert.Equal("main", status.CurrentBranch);
        Assert.Equal("origin/main", status.UpstreamBranch);
        Assert.Equal(0, status.AheadCount);
        Assert.Equal(0, status.BehindCount);
        Assert.Empty(status.StagedChanges);
        Assert.Empty(status.WorkingChanges);
        Assert.True(status.IsClean);
    }

    [Fact]
    public async Task GitService_DetectsInstalledGit_ViaHost()
    {
        var service = new GitService();
        bool installed = await service.IsGitInstalledAsync();
        Assert.True(installed);
    }
}
