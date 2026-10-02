namespace CSharpEditorPlugin.Tests.TestSupport;

/// <summary>Files checked into the repository, found from wherever the tests run.</summary>
internal static class RepositoryPaths
{
    public static string Root { get; } = FindRoot();

    private static string FindRoot()
    {
        for (var folder = new DirectoryInfo(AppContext.BaseDirectory); folder != null; folder = folder.Parent)
        {
            if (File.Exists(Path.Combine(folder.FullName, "CSharpEditorPlugin.slnx"))) return folder.FullName;
        }

        throw new DirectoryNotFoundException($"No CSharpEditorPlugin.slnx above {AppContext.BaseDirectory}.");
    }
}
