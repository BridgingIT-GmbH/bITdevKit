// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Application.Storage;

using BridgingIT.DevKit.Common;

/// <summary>Wraps a complete explicit scan, accepted event, or actual processor invocation.</summary>
/// <example><code>services.AddFileMonitoring(builder => builder.WithAuditing().UseInMemory("inbox", location => location.UseProcessor&lt;CustomerImportProcessor&gt;()));</code></example>
public interface IFileProcessingExecutionBehavior
{
    /// <summary>Invokes the existing source delegate once and awaits its completion and cleanup.</summary>
    /// <example><code>return behavior.ExecuteAsync(context, next);</code></example>
    Task<TResult> ExecuteAsync<TResult>(FileProcessingExecutionContext context, Func<Task<TResult>> next);
}

/// <summary>Configures local capture without copying paths, checksums, file contents, or arbitrary properties.</summary>
/// <example><code>var options = new FileMonitoringAuditOptions { Events = true, Processors = true, Scans = true };</code></example>
public sealed record FileMonitoringAuditOptions
{
    /// <summary>Selects finite accepted-event executions.</summary>
    /// <example><code>var selected = options.Events;</code></example>
    public bool Events { get; init; } = true;

    /// <summary>Selects actual processor invocations.</summary>
    /// <example><code>var selected = options.Processors;</code></example>
    public bool Processors { get; init; } = true;

    /// <summary>Allows explicitly requested scan capture. Automatic scans remain unselected.</summary>
    /// <example><code>var selected = options.Scans;</code></example>
    public bool Scans { get; init; }

    /// <summary>Selects approved logical locations during enabled capture.</summary>
    /// <example><code>var options = new FileMonitoringAuditOptions { SelectLocation = name => name == "inbox" };</code></example>
    public Func<string, bool> SelectLocation { get; init; }

    /// <summary>Selects processor types during enabled capture, without resolving processors for diagnostics.</summary>
    /// <example><code>var options = new FileMonitoringAuditOptions { SelectProcessor = type => type == typeof(CustomerImportProcessor) };</code></example>
    public Func<Type, bool> SelectProcessor { get; init; }
}

/// <summary>Contains safe source identities and observed state, independently of the file-event payload.</summary>
/// <example><code>var context = new FileProcessingExecutionContext { Operation = "Event", LocationName = "inbox" };</code></example>
public sealed record FileProcessingExecutionContext
{
    /// <summary>The fixed source boundary: Scan, Event, or Processor.</summary>
    /// <example><code>var operation = context.Operation;</code></example>
    public string Operation { get; init; }

    /// <summary>The configured logical location alias.</summary>
    /// <example><code>var location = context.LocationName;</code></example>
    public string LocationName { get; init; }

    /// <summary>The native event identity, when available.</summary>
    /// <example><code>var id = context.EventId;</code></example>
    public string EventId { get; init; }

    /// <summary>The native scan identity, when available.</summary>
    /// <example><code>var id = context.ScanId;</code></example>
    public string ScanId { get; init; }

    /// <summary>The native event kind, without its filesystem path.</summary>
    /// <example><code>var kind = context.EventKind;</code></example>
    public string EventKind { get; init; }

    /// <summary>The actual processor type before callback decorators.</summary>
    /// <example><code>var type = context.ProcessorType;</code></example>
    public Type ProcessorType { get; init; }

    /// <summary>An optional per-location capture configuration.</summary>
    /// <example><code>var selection = context.Selection;</code></example>
    public FileMonitoringAuditOptions Selection { get; init; }

    /// <summary>An explicit processor-level override.</summary>
    /// <example><code>var selected = context.SelectProcessor;</code></example>
    public bool? SelectProcessor { get; init; }

    /// <summary>Whether a host explicitly requested scan capture for this invocation.</summary>
    /// <example><code>var selected = context.ExplicitScan;</code></example>
    public bool ExplicitScan { get; init; }

    /// <summary>Whether the built-in recorder admitted this source boundary.</summary>
    /// <example><code>var admitted = context.CaptureAdmitted;</code></example>
    public bool CaptureAdmitted { get; set; }

    /// <summary>Whether actual processing failed, including failures caught by legacy callback decorators.</summary>
    /// <example><code>context.Failed = true;</code></example>
    public bool Failed { get; set; }

    /// <summary>Whether actual processing observed cancellation.</summary>
    /// <example><code>context.Canceled = true;</code></example>
    public bool Canceled { get; set; }
}
