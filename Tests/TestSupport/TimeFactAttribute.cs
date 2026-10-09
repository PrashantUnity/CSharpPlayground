using Xunit;

namespace CSharpEditorPlugin.Tests.TestSupport;

/// <summary>
/// Marks a test as time-intensive. Skipped by default unless 'time' arg or TIME=1 is provided.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class TimeFactAttribute : FactAttribute
{
    public TimeFactAttribute()
    {
        if (!TimeGate.IsEnabled)
        {
            Skip = TimeGate.SkipReason;
        }
    }
}
