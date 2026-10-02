using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Threading;
using FrySharp.Sdk;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Extensibility.Commands;

/// <summary>
/// Pipeline for command registration, execution, and middleware interception.
/// </summary>
public class ExtensibilityCommandPipeline : ICommandApi
{
    private readonly ConcurrentDictionary<string, CustomCommandDescriptor> _commands = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<Func<CommandInvocationContext, Func<Task>, Task>> _middlewares = new();
    private readonly object _middlewareLock = new();

    // Fallback invoker for built-in host commands (e.g. "csharp.editor.run", "csharp.editor.save")
    public Func<string, object?, Task<bool>>? HostCommandFallback { get; set; }

    public event Action? CommandsChanged;

    public IReadOnlyList<string> RegisteredCommands => _commands.Keys.OrderBy(k => k).ToList();
    public IReadOnlyList<CustomCommandDescriptor> Descriptors => _commands.Values.ToList();

    public IDisposable Register(string id, string title, Action action, string? gesture = null, string? category = null)
    {
        return Register(new CustomCommandDescriptor
        {
            Id = id,
            Title = title,
            Action = action,
            Shortcut = gesture,
            Category = category
        });
    }

    public IDisposable Register(CustomCommandDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        ArgumentException.ThrowIfNullOrWhiteSpace(descriptor.Id);
        ArgumentNullException.ThrowIfNull(descriptor.Action);

        _commands[descriptor.Id] = descriptor;
        CommandsChanged?.Invoke();

        return new ActionDisposable(() =>
        {
            if (_commands.TryRemove(descriptor.Id, out _))
            {
                CommandsChanged?.Invoke();
            }
        });
    }

    public IDisposable Use(Func<CommandInvocationContext, Func<Task>, Task> middleware)
    {
        ArgumentNullException.ThrowIfNull(middleware);

        lock (_middlewareLock)
        {
            _middlewares.Add(middleware);
        }

        return new ActionDisposable(() =>
        {
            lock (_middlewareLock)
            {
                _middlewares.Remove(middleware);
            }
        });
    }

    public bool HasCommand(string commandId)
    {
        return !string.IsNullOrWhiteSpace(commandId) && _commands.ContainsKey(commandId);
    }

    public async Task<bool> ExecuteAsync(string commandId, object? parameter = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(commandId);

        var context = new CommandInvocationContext
        {
            CommandId = commandId,
            Parameter = parameter
        };

        List<Func<CommandInvocationContext, Func<Task>, Task>> activeMiddlewares;
        lock (_middlewareLock)
        {
            activeMiddlewares = _middlewares.ToList();
        }

        // Build middleware chain
        Func<Task> pipeline = async () =>
        {
            if (context.IsCancelled) return;

            if (_commands.TryGetValue(commandId, out var customCmd))
            {
                if (Avalonia.Application.Current != null && !Dispatcher.UIThread.CheckAccess())
                {
                    Dispatcher.UIThread.Post(customCmd.Action);
                }
                else
                {
                    customCmd.Action();
                }
            }
            else if (HostCommandFallback != null)
            {
                await HostCommandFallback(commandId, parameter);
            }
        };

        // Compose middleware in reverse
        for (int i = activeMiddlewares.Count - 1; i >= 0; i--)
        {
            var mw = activeMiddlewares[i];
            var next = pipeline;
            pipeline = () => mw(context, next);
        }

        try
        {
            await pipeline();
            return !context.IsCancelled;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ExtensibilityCommandPipeline] Error executing command '{commandId}': {ex.Message}");
            throw;
        }
    }

    private sealed class ActionDisposable : IDisposable
    {
        private Action? _action;
        public ActionDisposable(Action action) => _action = action;
        public void Dispose()
        {
            var act = System.Threading.Interlocked.Exchange(ref _action, null);
            act?.Invoke();
        }
    }
}
