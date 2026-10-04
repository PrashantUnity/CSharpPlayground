using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace FrySharp.Sdk;

/// <summary>
/// Public API for registering commands, executing actions, and intercepting command pipelines.
/// </summary>
public interface ICommandApi
{
    /// <summary>Registers a custom command callable by shortcut, palette, or script.</summary>
    IDisposable Register(string id, string title, Action action, string? gesture = null, string? category = null);

    /// <summary>Registers a command using a full descriptor.</summary>
    IDisposable Register(CustomCommandDescriptor descriptor);

    /// <summary>Executes a registered command by id.</summary>
    Task<bool> ExecuteAsync(string commandId, object? parameter = null);

    /// <summary>Registers middleware into the command execution pipeline.</summary>
    IDisposable Use(Func<CommandInvocationContext, Func<Task>, Task> middleware);

    /// <summary>Checks whether a command is registered.</summary>
    bool HasCommand(string commandId);

    /// <summary>Gets all registered command IDs.</summary>
    IReadOnlyList<string> RegisteredCommands { get; }
}
