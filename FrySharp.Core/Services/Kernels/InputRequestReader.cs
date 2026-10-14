namespace PdfEditorApp.Plugins.CSharpEditor.Services.Kernels;

/// <summary>
/// <c>Console.In</c> for a C# cell: each line the code reads is asked of the person running the notebook (an input box in
/// the cell, on the desktop or in the browser) through <see cref="KernelExecutionRequest.OnInputRequest"/>, as Python's
/// <c>input()</c> is. The cell's code reads synchronously on its own thread-pool thread, so waiting here holds only the
/// cell. No answer (the box was dismissed, or nobody is connected) is the end of input: <c>ReadLine</c> returns null.
/// </summary>
public sealed class InputRequestReader(Func<string, bool, CancellationToken, Task<string?>> ask, CancellationToken ct) : TextReader
{
    // What Read and Peek have taken of the last answer, with its line break: Console.Read reads one character at a time.
    private string _buffer = string.Empty;
    private int _position;
    private bool _ended;

    public override string? ReadLine()
    {
        if (_position < _buffer.Length)
        {
            var end = _buffer.IndexOf('\n', _position);
            var rest = _buffer[_position..end];
            _position = end + 1;
            return rest;
        }
        return Ask();
    }

    public override int Peek() => Fill() ? _buffer[_position] : -1;

    public override int Read() => Fill() ? _buffer[_position++] : -1;

    private bool Fill()
    {
        if (_position < _buffer.Length) return true;
        if (Ask() is not { } line) return false;
        _buffer = line + "\n";
        _position = 0;
        return true;
    }

    private string? Ask()
    {
        if (_ended) return null;
        // The prompt is what the code wrote before reading (Console.Write("Name? ")), already in the cell's output.
        var line = ask(string.Empty, false, ct).GetAwaiter().GetResult();
        if (line == null) _ended = true;
        return line;
    }
}
