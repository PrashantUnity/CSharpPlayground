using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using PdfEditorApp.Plugins.CSharpEditor.Models;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Server;

public class FryHttpListenerServerEngine : IFryHttpServerEngine
{
    private readonly IPortAvailabilityService _portService;
    private readonly RoslynServerCompilationService _compiler;
    private readonly RouteTemplateMatcher _routeMatcher = new();

    private HttpListener? _listener;
    private CancellationTokenSource? _serverCts;
    private Task? _listenerTask;

    private readonly ConcurrentDictionary<string, object> _sharedState = new();
    private readonly List<FryServerTrafficLogItem> _trafficLog = new();
    private readonly object _trafficLock = new();

    private FryServerDocumentItem? _activeDocument;
    private int _boundPort;
    private int _totalRequestsServed;
    private ServerLifecycleState _state = ServerLifecycleState.Stopped;

    public ServerLifecycleState State
    {
        get => _state;
        private set
        {
            if (_state != value)
            {
                _state = value;
                StateChanged?.Invoke(_state);
            }
        }
    }

    public int BoundPort => _boundPort;
    public string BaseUrl => _activeDocument != null
        ? $"{_activeDocument.ServerConfig.Scheme}://{_activeDocument.ServerConfig.Host}:{_boundPort}{NormalizePrefix(_activeDocument.ServerConfig.ApiPrefix)}"
        : string.Empty;

    public int TotalRequestsServed => _totalRequestsServed;
    public IDictionary<string, object> SharedState => _sharedState;

    public IReadOnlyList<FryServerTrafficLogItem> TrafficLog
    {
        get
        {
            lock (_trafficLock)
            {
                return _trafficLog.ToList();
            }
        }
    }

    public event Action<ServerLifecycleState>? StateChanged;
    public event Action<FryServerTrafficLogItem>? RequestProcessed;

    public FryHttpListenerServerEngine(IPortAvailabilityService? portService = null, RoslynServerCompilationService? compiler = null)
    {
        _portService = portService ?? new PortAvailabilityService();
        _compiler = compiler ?? new RoslynServerCompilationService();
    }

    public async Task StartAsync(FryServerDocumentItem document, CancellationToken cancellationToken = default)
    {
        if (State is ServerLifecycleState.Running or ServerLifecycleState.Starting)
        {
            return;
        }

        State = ServerLifecycleState.Starting;
        _activeDocument = document;
        _routeMatcher.RegisterCells(document.Cells);

        var config = document.ServerConfig;
        var desiredPort = config.Port;

        var portStatus = await _portService.CheckPortStatusAsync(desiredPort, cancellationToken).ConfigureAwait(false);
        if (portStatus.State != PortState.Available)
        {
            if (config.AutoPortFallback && portStatus.SuggestedPort.HasValue)
            {
                desiredPort = portStatus.SuggestedPort.Value;
            }
            else
            {
                State = ServerLifecycleState.Faulted;
                throw new InvalidOperationException($"Port {config.Port} is not available. {portStatus.Message}");
            }
        }

        _boundPort = desiredPort;
        _serverCts = new CancellationTokenSource();

        // Run Startup cells to initialize shared state
        foreach (var startupCell in document.Cells.Where(c => c.Enabled && c.Type == FryServerCellType.Startup))
        {
            await _compiler.ExecuteStartupCellAsync(startupCell, _sharedState, cancellationToken).ConfigureAwait(false);
        }

        try
        {
            _listener = new HttpListener();
            var prefix = $"{config.Scheme}://+:{_boundPort}/";
            
            // On non-Windows or without admin URL ACLs, localhost prefix is universally permitted:
            try
            {
                _listener.Prefixes.Add(prefix);
                _listener.Start();
            }
            catch
            {
                _listener.Close();
                _listener = new HttpListener();
                prefix = $"{config.Scheme}://localhost:{_boundPort}/";
                _listener.Prefixes.Add(prefix);
                _listener.Start();
            }

            State = ServerLifecycleState.Running;
            _listenerTask = Task.Run(() => ListenLoopAsync(_serverCts.Token));
        }
        catch (Exception)
        {
            State = ServerLifecycleState.Faulted;
            await StopAsync(CancellationToken.None).ConfigureAwait(false);
            throw;
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (State is ServerLifecycleState.Stopped or ServerLifecycleState.Stopping)
        {
            return;
        }

        State = ServerLifecycleState.Stopping;

        try
        {
            _serverCts?.Cancel();
            _listener?.Stop();
            _listener?.Close();

            if (_listenerTask != null)
            {
                await Task.WhenAny(_listenerTask, Task.Delay(1000, cancellationToken)).ConfigureAwait(false);
            }
        }
        catch
        {
            // Ignore shutdown errors
        }
        finally
        {
            _listener = null;
            _serverCts?.Dispose();
            _serverCts = null;
            _listenerTask = null;
            State = ServerLifecycleState.Stopped;
        }
    }

    public async Task RestartAsync(FryServerDocumentItem document, CancellationToken cancellationToken = default)
    {
        await StopAsync(cancellationToken).ConfigureAwait(false);
        await StartAsync(document, cancellationToken).ConfigureAwait(false);
    }

    public Task InvalidateCellCompilationAsync(FryServerCellItem cell, CancellationToken cancellationToken = default)
    {
        _routeMatcher.RegisterCells(_activeDocument?.Cells ?? Enumerable.Empty<FryServerCellItem>());
        return _compiler.CompileCellAsync(cell, cancellationToken);
    }

    private async Task ListenLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested && _listener != null && _listener.IsListening)
        {
            try
            {
                var context = await _listener.GetContextAsync().ConfigureAwait(false);
                _ = Task.Run(() => HandleRequestAsync(context, ct), ct);
            }
            catch (HttpListenerException) when (ct.IsCancellationRequested)
            {
                break;
            }
            catch (ObjectDisposedException)
            {
                break;
            }
            catch
            {
                if (ct.IsCancellationRequested) break;
            }
        }
    }

    private async Task HandleRequestAsync(HttpListenerContext httpContext, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        var request = httpContext.Request;
        var response = httpContext.Response;
        var config = _activeDocument?.ServerConfig;

        // Apply CORS headers
        if (config?.EnableCors == true)
        {
            ApplyCorsHeaders(request, response, config);

            // Handle preflight OPTIONS
            if (string.Equals(request.HttpMethod, "OPTIONS", StringComparison.OrdinalIgnoreCase))
            {
                response.StatusCode = 204;
                response.Close();
                return;
            }
        }

        // Read request payload
        var bodyText = string.Empty;
        byte[] requestBytes = Array.Empty<byte>();
        if (request.HasEntityBody)
        {
            using var ms = new MemoryStream();
            await request.InputStream.CopyToAsync(ms, ct).ConfigureAwait(false);
            requestBytes = ms.ToArray();
            bodyText = Encoding.UTF8.GetString(requestBytes);
        }

        // Extract headers
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var key in request.Headers.AllKeys)
        {
            if (key != null) headers[key] = request.Headers[key] ?? string.Empty;
        }

        var rawPath = request.Url?.AbsolutePath ?? "/";
        var queryString = request.Url?.Query;
        var apiPrefix = config?.ApiPrefix;

        // Match route
        var match = _routeMatcher.Match(request.HttpMethod, rawPath, queryString, apiPrefix);

        IServerResult result;
        FryServerCellItem? matchedCell = match.MatchedCell;

        if (match.IsMatched && matchedCell != null)
        {
            var serverContext = new FryServerContext(
                request.HttpMethod,
                rawPath,
                match.PathParameters,
                match.QueryParameters,
                headers,
                bodyText,
                requestBytes,
                _sharedState);

            // Simulated latency if specified
            var delayMs = Math.Max(config?.SimulatedLatencyMs ?? 0, matchedCell.SimulatedLatencyMs);
            if (delayMs > 0)
            {
                await Task.Delay(delayMs, ct).ConfigureAwait(false);
            }

            result = await _compiler.ExecuteCellAsync(matchedCell, serverContext, ct).ConfigureAwait(false);
        }
        else if (match.MethodNotAllowed)
        {
            response.Headers["Allow"] = string.Join(", ", match.AllowedMethods);
            result = new StatusCodeResult(405, new
            {
                error = "Method Not Allowed",
                allowed = match.AllowedMethods
            });
        }
        else
        {
            result = new StatusCodeResult(404, new
            {
                error = "Not Found",
                path = rawPath
            });
        }

        try
        {
            await result.ExecuteAsync(response).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            try
            {
                response.StatusCode = 500;
                var errBytes = Encoding.UTF8.GetBytes($"{{\"error\":\"{ex.Message}\"}}");
                response.ContentType = "application/json";
                await response.OutputStream.WriteAsync(errBytes, ct).ConfigureAwait(false);
            }
            catch { }
        }
        finally
        {
            try { response.Close(); } catch { }
        }

        sw.Stop();
        Interlocked.Increment(ref _totalRequestsServed);

        var logItem = new FryServerTrafficLogItem
        {
            Method = request.HttpMethod,
            Path = rawPath,
            QueryString = (queryString ?? string.Empty).TrimStart('?'),
            StatusCode = response.StatusCode,
            ElapsedMilliseconds = sw.Elapsed.TotalMilliseconds,
            ClientIp = request.RemoteEndPoint?.Address.ToString() ?? "127.0.0.1",
            MatchedCellId = matchedCell?.Id,
            ResponseBytesLength = response.ContentLength64
        };

        if (matchedCell != null)
        {
            matchedCell.RequestCount++;
            matchedCell.LastExecutionTimeMs = sw.Elapsed.TotalMilliseconds;
            matchedCell.LastStatusCode = response.StatusCode;
        }

        RecordTraffic(logItem);
    }

    public async Task<IServerResult> ExecuteLoopbackTestAsync(FryServerCellItem cell, FryServerTestHarnessItem testHarness, CancellationToken cancellationToken = default)
    {
        var headers = new Dictionary<string, string>(testHarness.Headers, StringComparer.OrdinalIgnoreCase);
        if (!string.IsNullOrEmpty(testHarness.BodyContentType) && !headers.ContainsKey("Content-Type"))
        {
            headers["Content-Type"] = testHarness.BodyContentType;
        }

        var bodyBytes = Encoding.UTF8.GetBytes(testHarness.Body ?? string.Empty);
        var path = cell.Route ?? "/";

        var context = new FryServerContext(
            cell.Method,
            path,
            testHarness.PathParams,
            testHarness.QueryParams,
            headers,
            testHarness.Body ?? string.Empty,
            bodyBytes,
            _sharedState);

        var sw = Stopwatch.StartNew();
        var result = await _compiler.ExecuteCellAsync(cell, context, cancellationToken).ConfigureAwait(false);
        sw.Stop();

        cell.LastExecutionTimeMs = sw.Elapsed.TotalMilliseconds;
        cell.LastStatusCode = result.StatusCode;

        Interlocked.Increment(ref _totalRequestsServed);
        var logItem = new FryServerTrafficLogItem
        {
            Method = cell.Method,
            Path = path,
            StatusCode = result.StatusCode,
            ElapsedMilliseconds = sw.Elapsed.TotalMilliseconds,
            ClientIp = "127.0.0.1 (Loopback)",
            Timestamp = DateTime.UtcNow
        };
        RecordTraffic(logItem);

        return result;
    }

    private void ApplyCorsHeaders(HttpListenerRequest request, HttpListenerResponse response, FryServerConfiguration config)
    {
        var origin = request.Headers["Origin"] ?? "*";
        response.Headers["Access-Control-Allow-Origin"] = config.CorsAllowedOrigins.Contains("*")
            ? "*"
            : origin;

        response.Headers["Access-Control-Allow-Methods"] = "GET, POST, PUT, DELETE, PATCH, HEAD, OPTIONS";
        response.Headers["Access-Control-Allow-Headers"] = "*";
        response.Headers["Access-Control-Max-Age"] = "86400";

        // Chrome Private Network Access (PNA) preflight support
        if (config.AllowPrivateNetwork && request.Headers["Access-Control-Request-Private-Network"] != null)
        {
            response.Headers["Access-Control-Allow-Private-Network"] = "true";
        }
    }

    private void RecordTraffic(FryServerTrafficLogItem item)
    {
        lock (_trafficLock)
        {
            _trafficLog.Add(item);
            if (_trafficLog.Count > 500)
            {
                _trafficLog.RemoveAt(0);
            }
        }

        RequestProcessed?.Invoke(item);
    }

    private static string NormalizePrefix(string? prefix)
    {
        if (string.IsNullOrWhiteSpace(prefix)) return string.Empty;
        var p = prefix.Trim();
        if (!p.StartsWith('/')) p = "/" + p;
        return p.TrimEnd('/');
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync(CancellationToken.None).ConfigureAwait(false);
    }
}
