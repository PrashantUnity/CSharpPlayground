using Xunit;

namespace CSharpEditorPlugin.Tests;

/// <summary>
/// Single collection definition for all tests that interact with extension loading or the ambient StudioAppContext.
/// Disables parallelization so concurrent tests don't mutate shared extension state or language registries.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class ExtensionTestsCollection
{
    public const string Name = "SettingsTests";
}
