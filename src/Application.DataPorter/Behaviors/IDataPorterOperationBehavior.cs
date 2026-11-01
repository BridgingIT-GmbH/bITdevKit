// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Application.DataPorter;

using BridgingIT.DevKit.Common;

/// <summary>Wraps complete aggregate and deferred streaming operations without inspecting input or output payloads.</summary>
/// <example><code>services.AddDataPorter().WithAuditing();</code></example>
public interface IDataPorterOperationBehavior
{
    /// <summary>Awaits one aggregate operation through its provider and compression cleanup.</summary>
    /// <example><code>return behavior.ExecuteAsync(context, next);</code></example>
    Task<TResult> ExecuteAsync<TResult>(DataPorterOperationContext context, Func<Task<TResult>> next) where TResult : IResult;

    /// <summary>Owns the actual enumerator lifetime through completion, exception, or early disposal.</summary>
    /// <example><code>await foreach (var row in behavior.ExecuteStreamAsync(context, next, cancellationToken)) { Process(row); }</code></example>
    IAsyncEnumerable<Result<T>> ExecuteStreamAsync<T>(DataPorterOperationContext context, Func<IAsyncEnumerable<Result<T>>> next, CancellationToken cancellationToken = default) where T : class, new();
}

/// <summary>Contains source-owned format, type, and observed processing counts. Row acceptance does not assert committed business data.</summary>
/// <example><code>var context = new DataPorterOperationContext { Operation = "Import", Format = Format.Csv };</code></example>
public sealed record DataPorterOperationContext
{
    /// <summary>The fixed source operation label.</summary>
    /// <example><code>var operation = context.Operation;</code></example>
    public string Operation { get; init; }

    /// <summary>The source data type, without an instance or reflection-based value capture.</summary>
    /// <example><code>var type = context.DataType;</code></example>
    public Type DataType { get; init; }

    /// <summary>The actual selected format.</summary>
    /// <example><code>var format = context.Format;</code></example>
    public Format Format { get; init; }

    /// <summary>Whether validation admitted actual provider processing.</summary>
    /// <example><code>context.ProcessingStarted = true;</code></example>
    public bool ProcessingStarted { get; set; }

    /// <summary>Whether the source observed cancellation, including APIs that return a failure Result.</summary>
    /// <example><code>context.Canceled = true;</code></example>
    public bool Canceled { get; set; }

    /// <summary>The observed source row count, excluding setup failure items.</summary>
    /// <example><code>var rows = context.TotalRows;</code></example>
    public long TotalRows { get; set; }

    /// <summary>The observed accepted row count, without a business commit claim.</summary>
    /// <example><code>var rows = context.SuccessfulRows;</code></example>
    public long SuccessfulRows { get; set; }

    /// <summary>The observed failed row count.</summary>
    /// <example><code>var rows = context.FailedRows;</code></example>
    public long FailedRows { get; set; }

    /// <summary>Whether the native aggregate result reports processing errors, without copying their messages.</summary>
    /// <example><code>var partial = context.HasErrors;</code></example>
    public bool HasErrors { get; set; }

    /// <summary>The observed skipped row count.</summary>
    /// <example><code>var rows = context.SkippedRows;</code></example>
    public long SkippedRows { get; set; }
}
