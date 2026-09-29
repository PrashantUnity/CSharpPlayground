using System.IO.Pipelines;
using System.Text;
using System.Text.Json;
using PdfEditorApp.Plugins.CSharpEditor.Tests.Debugging;

namespace PdfEditorApp.Plugins.CSharpEditor.Tests.TestSupport;

/// <summary>Makes a pretend program behave as a debug adapter that talks over its standard streams (netcoredbg, lldb-dap).</summary>
internal static class FakeDapAdapter
{
    /// <summary>
    /// The program reads Debug Adapter Protocol requests from its input and answers them as <paramref name="onRequest"/> says, until it is
    /// killed or <paramref name="ends"/> completes (the program's own decision to exit).
    /// </summary>
    public static async Task RunOnStandardStreamsAsync(
        FakeProcess process, Func<MockDapServer, string, int, JsonElement, Task> onRequest, Action<MockDapServer>? started = null, Task? ends = null)
    {
        var input = new Pipe();
        _ = Task.Run(async () =>
        {
            string? chunk;
            while ((chunk = await process.ReadChunkAsync()) != null)
            {
                await input.Writer.WriteAsync(Encoding.UTF8.GetBytes(chunk));
            }

            await input.Writer.CompleteAsync();
        });

        await using var server = new MockDapServer(input.Reader.AsStream(), new TextToProcess(process), onRequest);
        started?.Invoke(server);
        try
        {
            var killed = Task.Delay(Timeout.Infinite, process.KilledToken);
            await (ends == null ? killed : Task.WhenAny(killed, ends));
        }
        catch (OperationCanceledException)
        {
        }
    }

    /// <summary>What the program writes to its standard output, as the bytes of a stream.</summary>
    private sealed class TextToProcess(FakeProcess process) : Stream
    {
        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => process.Write(Encoding.UTF8.GetString(buffer, offset, count));
        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            process.Write(Encoding.UTF8.GetString(buffer.Span));
            return ValueTask.CompletedTask;
        }
    }
}
