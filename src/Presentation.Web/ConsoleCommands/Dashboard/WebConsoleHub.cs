// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.ConsoleCommands.Dashboard;

using System.Reflection;
using BridgingIT.DevKit.Presentation;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

/// <summary>
/// SignalR hub for the dashboard web console.
/// </summary>
/// <example>
/// <code>
/// endpoints.MapHub&lt;WebConsoleHub&gt;("/_bdk/dashboard/console/hub");
/// </code>
/// </example>
public sealed class WebConsoleHub(
    WebConsoleSessionManager sessions,
    WebAnsiConsoleFactory consoleFactory,
    ConsoleCommandExecutor executor,
    IServiceProvider services,
    ILogger<WebConsoleHub> logger) : Hub
{
    private const int HistoryLimit = 100;

    /// <inheritdoc />
    public override async Task OnConnectedAsync()
    {
        ConsoleCommandHistory.Initialize(Assembly.GetEntryAssembly()?.GetName().Name);

        var session = this.GetSession();
        session.ConnectionId = this.Context.ConnectionId;
        var bufferedOutput = session.GetBufferedOutput();

        await this.Clients.Caller.SendAsync(
            "console.history",
            ConsoleCommandHistory.GetAll().TakeLast(HistoryLimit).ToArray(),
            this.Context.ConnectionAborted);

        foreach (var text in bufferedOutput)
        {
            await this.Clients.Caller.SendAsync("console.output", text, this.Context.ConnectionAborted);
        }

        await this.Clients.Caller.SendAsync("console.ready", new
        {
            sessionId = session.SessionId,
            columns = session.Columns,
            rows = session.Rows,
            replayedOutput = bufferedOutput.Length
        }, this.Context.ConnectionAborted);

        await base.OnConnectedAsync();
    }

    /// <inheritdoc />
    public override Task OnDisconnectedAsync(Exception exception)
    {
        this.GetSession().CancelCurrentCommand();
        return base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// Executes one command line in this web console session.
    /// </summary>
    public async Task SendCommand(string line)
    {
        var session = this.GetSession();
        if (string.IsNullOrWhiteSpace(line))
        {
            return;
        }

        if (!session.TryStartCommand(out var commandCancellation))
        {
            await this.SendOutputAsync(session, "A command is already running. Wait until it completes or cancel it.\r\n", this.Context.ConnectionAborted);
            return;
        }

        using var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(
            commandCancellation.Token,
            this.Context.ConnectionAborted);

        try
        {
            await this.Clients.Caller.SendAsync("console.started", new { line }, this.Context.ConnectionAborted);
            var console = consoleFactory.Create(
                (text, cancellationToken) => this.SendOutputAsync(session, text, cancellationToken),
                session.Columns,
                session.Rows,
                linkedCancellation.Token);

            await executor.ExecuteAsync(
                line,
                console,
                services,
                ConsoleCommandExecutionSource.Web,
                linkedCancellation.Token);

            if (commandCancellation.IsCancellationRequested)
            {
                await this.Clients.Caller.SendAsync("console.cancelled", new { line }, this.Context.ConnectionAborted);
            }
            else
            {
                await this.Clients.Caller.SendAsync("console.completed", new { line }, this.Context.ConnectionAborted);
            }

            await this.Clients.Caller.SendAsync(
                "console.history",
                ConsoleCommandHistory.GetAll().TakeLast(HistoryLimit).ToArray(),
                this.Context.ConnectionAborted);
        }
        catch (OperationCanceledException)
        {
            await this.Clients.Caller.SendAsync("console.cancelled", new { line }, this.Context.ConnectionAborted);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Web console command failed (connectionId={ConnectionId})", this.Context.ConnectionId);
            await this.SendOutputAsync(session, $"Command failed: {ex.Message}\r\n", this.Context.ConnectionAborted);
            await this.Clients.Caller.SendAsync("console.completed", new { line, failed = true }, this.Context.ConnectionAborted);
        }
        finally
        {
            session.CompleteCommand();
        }
    }

    /// <summary>
    /// Cancels the currently running command for this session.
    /// </summary>
    public Task CancelCommand()
    {
        this.GetSession().CancelCurrentCommand();
        return Task.CompletedTask;
    }

    /// <summary>
    /// Updates terminal dimensions for future command rendering.
    /// </summary>
    public Task Resize(int columns, int rows)
    {
        this.GetSession().Resize(columns, rows);
        return Task.CompletedTask;
    }

    private WebConsoleSession GetSession()
    {
        var sessionId = this.Context.GetHttpContext()?.Request.Query["sessionId"].ToString();
        return sessions.GetOrCreate(string.IsNullOrWhiteSpace(sessionId) ? this.Context.ConnectionId : sessionId);
    }

    private async Task SendOutputAsync(WebConsoleSession session, string text, CancellationToken cancellationToken)
    {
        session.AppendOutput(text);
        await this.Clients.Caller.SendAsync("console.output", text, cancellationToken);
    }
}
