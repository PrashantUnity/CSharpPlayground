using System.Net.Sockets;
using System.Threading.Channels;
using PdfEditorApp.Plugins.CSharpEditor.Services.Processes;

namespace PdfEditorApp.Plugins.CSharpEditor.Visuals.Interaction;

/// <summary>
/// One run of a program that shows visuals on <c>__FRY_DISPLAY__</c> lines: the visuals it showed (so its updates redraw
/// them), and the events on them, which reach it over <see cref="VisualEventHub"/> once it connects. Events that come
/// before it connects wait for it (the newest <see cref="MaxWaitingEvents"/>). Disposing it is the program's end: its
/// visuals stay, and say they are disconnected.
/// </summary>
public sealed class ExternalVisualSession : IVisualEventSink, IDisposable
{
    /// <summary>How many events wait for a program that hasn't read them; older ones are dropped.</summary>
    public const int MaxWaitingEvents = 256;

    private readonly VisualEventHub _hub;
    private readonly string _token;
    private readonly Channel<byte[]> _events = Channel.CreateBounded<byte[]>(new BoundedChannelOptions(MaxWaitingEvents)
    {
        FullMode = BoundedChannelFullMode.DropOldest,
        SingleReader = true
    });
    private readonly Lock _gate = new();
    private TcpClient? _connection;
    private bool _ended;

    public ExternalVisualSession(VisualEventHub? hub = null)
    {
        _hub = hub ?? VisualEventHub.Shared;
        _token = _hub.Register(this);
        Visuals = new ProgramVisuals(this);
    }

    /// <summary>The program's visuals, read from its display, update and subscribe lines.</summary>
    public ProgramVisuals Visuals { get; }

    /// <summary>Whether the program has connected to hear its events.</summary>
    public bool IsConnected
    {
        get
        {
            lock (_gate) return _connection != null;
        }
    }

    /// <summary>The variables that tell the program where to connect, and with which token.</summary>
    public IReadOnlyDictionary<string, string?> Environment => new Dictionary<string, string?>
    {
        [VisualEventHub.AddressVariable] = _hub.Address,
        [VisualEventHub.TokenVariable] = _token
    };

    /// <summary><paramref name="spec"/> with <see cref="Environment"/> added to its own.</summary>
    public ProcessStartSpec Apply(ProcessStartSpec spec)
    {
        var environment = new Dictionary<string, string?>(spec.Environment);
        foreach (var (name, value) in Environment) environment[name] = value;
        return spec with { Environment = environment };
    }

    public void Deliver(string displayId, VisualEvent visualEvent) =>
        _events.Writer.TryWrite(visualEvent.ToMessageLine(displayId, Guid.NewGuid().ToString("N")[..12]));

    /// <summary>The program connected (the hub checked its token): what waits, and what comes, is written to it.</summary>
    internal bool Attach(TcpClient connection)
    {
        lock (_gate)
        {
            if (_ended || _connection != null) return false;
            _connection = connection;
        }

        _ = SendAsync(connection);
        return true;
    }

    public void Dispose()
    {
        TcpClient? connection;
        lock (_gate)
        {
            if (_ended) return;
            _ended = true;
            connection = _connection;
        }

        _hub.Unregister(_token);
        _events.Writer.TryComplete();
        Visuals.Disconnect();
        connection?.Dispose();
    }

    // One writer, so events arrive in the order they came.
    private async Task SendAsync(TcpClient connection)
    {
        try
        {
            var stream = connection.GetStream();
            await foreach (var line in _events.Reader.ReadAllAsync().ConfigureAwait(false))
            {
                await stream.WriteAsync(line).ConfigureAwait(false);
                await stream.FlushAsync().ConfigureAwait(false);
            }
        }
        catch (Exception ex) when (ex is IOException or SocketException or ObjectDisposedException or InvalidOperationException)
        {
            // The program closed its end; it hears nothing more.
        }
        finally
        {
            connection.Dispose();
        }
    }
}
