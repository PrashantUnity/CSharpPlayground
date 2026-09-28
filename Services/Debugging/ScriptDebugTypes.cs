using System;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Debugging;

public enum DebugStepMode
{
    None,
    StepOver,
    StepInto,
    Continue
}

public readonly struct DebugFrameScope : IDisposable
{
    private readonly ScriptDebugSession.DebugFrame? _parent;

    internal DebugFrameScope(ScriptDebugSession.DebugFrame? parent)
    {
        _parent = parent;
    }

    public void Dispose() => ScriptDebugSession.PopFrame(_parent);
}
