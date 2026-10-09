using Xunit;

namespace CSharpEditorPlugin.Tests.Debugging;

/// <summary>
/// Single collection definition for all tests that interact with static <see cref="PdfEditorApp.Plugins.CSharpEditor.Services.Debugging.ScriptDebugSession"/>.
/// Disables parallelization so concurrent test suites don't overwrite the ambient debug session.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class ScriptDebugSessionCollection
{
    public const string Name = "ScriptDebugSession";
}
