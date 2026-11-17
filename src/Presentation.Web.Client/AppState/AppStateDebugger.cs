// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.Client;

using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

/// <summary>Retains bounded owner-local diagnostic metadata with explicit value projection.</summary>
/// <param name="logger">The safe structured logger.</param>
/// <param name="jsRuntime">The optional originating browser runtime.</param>
/// <param name="options">The diagnostic settings.</param>
/// <example>
/// <code><![CDATA[
/// var debugger = new AppStateDebugger(NullLogger<AppStateDebugger>.Instance, jsRuntime,
///     new AppStateDebugOptions { StateChangesTracked = true, MaxTrackedChanges = 100 });
/// debugger.TrackStateChange(metadata);
/// Debug.Assert(debugger.GetStateChanges().Count == 1);
/// ]]></code>
/// </example>
public class AppStateDebugger(ILogger<AppStateDebugger> logger, IJSRuntime jsRuntime, AppStateDebugOptions options)
{
    private readonly object sync = new();
    private readonly Queue<IStateChangeMetadata> changes = [];
    private readonly AppStateDebugOptions options = options ?? throw new ArgumentNullException(nameof(options));

    /// <summary>Gets whether metadata logging or bounded tracking is enabled.</summary>
    public bool IsEnabled => this.options.LoggingEnabled || this.options.StateChangesTracked;

    /// <summary>Gets whether callers must supply independent values for the configured projection.</summary>
    public bool CapturesValues => this.options.ValueProjection != null;

    /// <summary>Tracks sanitized metadata synchronously without detached asynchronous work.</summary>
    /// <param name="change">The transition to sanitize before retaining it.</param>
    /// <example>
    /// <code><![CDATA[
    /// debugger.TrackStateChange(metadata);
    /// ]]></code>
    /// </example>
    public void TrackStateChange(IStateChangeMetadata change)
    {
        if (!this.options.StateChangesTracked) { return; }

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(this.options.MaxTrackedChanges);
        var projection = this.options.ValueProjection;
        var safe = new StateChangeMetadata<object>
        {
            StateId = change.StateId, Version = change.Version, Timestamp = change.Timestamp, Operation = change.Operation,
            OldValue = projection?.Invoke(change.OldValue), NewValue = projection?.Invoke(change.NewValue)
        };
        lock (this.sync)
        {
            this.changes.Enqueue(safe);
            while (this.changes.Count > this.options.MaxTrackedChanges) { this.changes.Dequeue(); }
        }
    }

    /// <summary>Tracks metadata and awaits optional safe browser output.</summary>
    /// <param name="change">The accepted transition.</param>
    /// <param name="cancellationToken">Cancels optional console output.</param>
    /// <returns>Observed diagnostic work, independent of mutation success.</returns>
    /// <example>
    /// <code><![CDATA[
    /// await debugger.TrackStateChangeAsync(metadata, CancellationToken.None);
    /// ]]></code>
    /// </example>
    public async Task TrackStateChangeAsync(IStateChangeMetadata change, CancellationToken cancellationToken = default)
    {
        this.TrackStateChange(change);
        if (this.options.LoggingEnabled)
        {
            logger?.LogDebug("AppState transition ({Operation})", change.Operation);
            await this.WriteConsoleAsync("console.log", [change.Operation], cancellationToken);
        }
    }

    /// <summary>Returns a copied bounded metadata inventory.</summary>
    /// <returns>The sanitized tracked changes.</returns>
    /// <example>
    /// <code><![CDATA[
    /// debugger.TrackStateChange(metadata);
    /// var changes = debugger.GetStateChanges();
    /// Debug.Assert(changes.Count > 0);
    /// ]]></code>
    /// </example>
    public IReadOnlyList<IStateChangeMetadata> GetStateChanges()
    {
        lock (this.sync) { return this.changes.ToArray(); }
    }

    /// <summary>Clears owner-local retained observations on context invalidation.</summary>
    /// <example>
    /// <code><![CDATA[
    /// debugger.TrackStateChange(metadata);
    /// debugger.Clear();
    /// Debug.Assert(debugger.GetStateChanges().Count == 0);
    /// ]]></code>
    /// </example>
    public void Clear() { lock (this.sync) { this.changes.Clear(); } }

    /// <summary>Writes explicitly supplied safe values when console logging is enabled.</summary>
    /// <param name="args">Caller-selected safe console arguments.</param>
    /// <returns>Observed browser output.</returns>
    /// <example>
    /// <code><![CDATA[
    /// await debugger.ConsoleLogAsync("Catalog filters refreshed.");
    /// ]]></code>
    /// </example>
    public Task ConsoleLogAsync(params object[] args) => this.options.LoggingEnabled ? this.WriteConsoleAsync("console.log", args) : Task.CompletedTask;

    /// <summary>Writes explicit safe information when enabled.</summary>
    /// <param name="args">Caller-selected safe console arguments.</param>
    /// <returns>Observed browser output.</returns>
    /// <example>
    /// <code><![CDATA[
    /// await debugger.ConsoleInfoAsync("Catalog filters refreshed.");
    /// ]]></code>
    /// </example>
    public Task ConsoleInfoAsync(params object[] args) => this.options.LoggingEnabled ? this.WriteConsoleAsync("console.info", args) : Task.CompletedTask;

    /// <summary>Writes an explicit safe warning when enabled.</summary>
    /// <param name="args">Caller-selected safe console arguments.</param>
    /// <returns>Observed browser output.</returns>
    /// <example>
    /// <code><![CDATA[
    /// await debugger.ConsoleWarnAsync("Catalog filters refreshed.");
    /// ]]></code>
    /// </example>
    public Task ConsoleWarnAsync(params object[] args) => this.options.LoggingEnabled ? this.WriteConsoleAsync("console.warn", args) : Task.CompletedTask;

    /// <summary>Writes explicit safe error information when enabled.</summary>
    /// <param name="args">Caller-selected safe console arguments.</param>
    /// <returns>Observed browser output.</returns>
    /// <example>
    /// <code><![CDATA[
    /// await debugger.ConsoleErrorAsync("Catalog filters refreshed.");
    /// ]]></code>
    /// </example>
    public Task ConsoleErrorAsync(params object[] args) => this.options.LoggingEnabled ? this.WriteConsoleAsync("console.error", args) : Task.CompletedTask;

    private async Task WriteConsoleAsync(string method, object[] args, CancellationToken token = default)
    {
        if (jsRuntime == null) { return; }

        try { await jsRuntime.InvokeVoidAsync(method, token, args); }
        catch (Exception ex) when (ex is InvalidOperationException or JSException)
        {
            logger?.LogDebug("AppState diagnostic console unavailable ({Outcome})", ex.GetType().Name);
        }
    }
}
