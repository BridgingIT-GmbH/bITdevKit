// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.DataPorter;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>
/// Executes typed export row interceptors for a single export operation.
/// </summary>
/// <typeparam name="TSource">The export source type.</typeparam>
public interface IExportRowInterceptionExecutor<TSource>
    where TSource : class
{
    /// <summary>
    /// Executes the pre-export interceptor pipeline for the specified item.
    /// </summary>
    /// <param name="item">The current item.</param>
    /// <param name="rowNumber">The logical row number.</param>
    /// <param name="format">The export format.</param>
    /// <param name="sheetName">The sheet or section name when available.</param>
    /// <param name="isStreaming">Indicates whether the operation is streaming.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The resulting interception state.</returns>
    Task<ExportRowInterceptionState<TSource>> BeforeAsync(
        TSource item,
        int rowNumber,
        Format format,
        string sheetName,
        bool isStreaming,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes the post-export interceptor callbacks for a successful row.
    /// </summary>
    /// <param name="state">The interception state returned by <see cref="BeforeAsync(TSource, int, Format, string, bool, CancellationToken)"/>.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that completes when post-processing has finished.</returns>
    Task AfterAsync(
        ExportRowInterceptionState<TSource> state,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Represents the state accumulated while executing typed export row interceptors.
/// </summary>
/// <typeparam name="TSource">The export source type.</typeparam>
/// <param name="Outcome">The interception outcome.</param>
/// <param name="Item">The current item after interception.</param>
/// <param name="Reason">The skip or abort reason when available.</param>
/// <param name="ExecutedInterceptors">The interceptors that completed successfully.</param>
/// <param name="Context">The shared row context.</param>
public sealed record ExportRowInterceptionState<TSource>(
    RowInterceptionOutcome Outcome,
    TSource Item,
    string Reason,
    IReadOnlyList<IExportRowInterceptor<TSource>> ExecutedInterceptors,
    ExportRowContext<TSource> Context)
    where TSource : class
{
    /// <summary>
    /// Creates a successful continuation state.
    /// </summary>
    public static ExportRowInterceptionState<TSource> Continue(
        TSource item,
        IReadOnlyList<IExportRowInterceptor<TSource>> executedInterceptors,
        ExportRowContext<TSource> context)
    {
        return new ExportRowInterceptionState<TSource>(
            RowInterceptionOutcome.Continue,
            item,
            null,
            executedInterceptors,
            context);
    }

    /// <summary>
    /// Creates a skip state.
    /// </summary>
    public static ExportRowInterceptionState<TSource> Skip(TSource item, string reason, ExportRowContext<TSource> context)
    {
        return new ExportRowInterceptionState<TSource>(
            RowInterceptionOutcome.Skip,
            item,
            reason,
            [],
            context);
    }

    /// <summary>
    /// Creates an abort state.
    /// </summary>
    public static ExportRowInterceptionState<TSource> Abort(TSource item, string reason, ExportRowContext<TSource> context)
    {
        return new ExportRowInterceptionState<TSource>(
            RowInterceptionOutcome.Abort,
            item,
            reason,
            [],
            context);
    }
}

/// <summary>
/// Default implementation of <see cref="IExportRowInterceptionExecutor{TSource}"/>.
/// </summary>
/// <typeparam name="TSource">The export source type.</typeparam>
public sealed class ExportRowInterceptionExecutor<TSource>(
    IReadOnlyList<IExportRowInterceptor<TSource>> interceptors,
    ILogger logger = null) : IExportRowInterceptionExecutor<TSource>
    where TSource : class
{
    private readonly IReadOnlyList<IExportRowInterceptor<TSource>> interceptors = interceptors ?? [];
    private readonly ILogger logger = logger ?? NullLogger.Instance;

    /// <inheritdoc/>
    public async Task<ExportRowInterceptionState<TSource>> BeforeAsync(
        TSource item,
        int rowNumber,
        Format format,
        string sheetName,
        bool isStreaming,
        CancellationToken cancellationToken = default)
    {
        var context = new ExportRowContext<TSource>
        {
            Item = item,
            RowNumber = rowNumber,
            Format = format,
            SheetName = sheetName,
            IsStreaming = isStreaming
        };

        if (this.interceptors.Count == 0)
        {
            return ExportRowInterceptionState<TSource>.Continue(context.Item, [], context);
        }

        var executed = new List<IExportRowInterceptor<TSource>>(this.interceptors.Count);

        foreach (var interceptor in this.interceptors)
        {
            var decision = await interceptor.BeforeExportAsync(context, cancellationToken) ?? RowInterceptionDecision.Continue();

            if (decision.Outcome == RowInterceptionOutcome.Skip)
            {
                this.logger.LogWarning("{LogKey} row interceptor skipped (interceptor={InterceptorType}, rowNumber={RowNumber}, format={Format}, sheetName={SheetName}, reason={Reason})", Constants.LogKeyExport, interceptor.GetType().Name, rowNumber, format, sheetName, decision.Reason);

                return ExportRowInterceptionState<TSource>.Skip(context.Item, decision.Reason, context);
            }

            if (decision.Outcome == RowInterceptionOutcome.Abort)
            {
                this.logger.LogWarning("{LogKey} row interceptor aborted (interceptor={InterceptorType}, rowNumber={RowNumber}, format={Format}, sheetName={SheetName}, reason={Reason})", Constants.LogKeyExport, interceptor.GetType().Name, rowNumber, format, sheetName, decision.Reason);

                return ExportRowInterceptionState<TSource>.Abort(context.Item, decision.Reason, context);
            }

            executed.Add(interceptor);
        }

        return ExportRowInterceptionState<TSource>.Continue(context.Item, executed, context);
    }

    /// <inheritdoc/>
    public async Task AfterAsync(
        ExportRowInterceptionState<TSource> state,
        CancellationToken cancellationToken = default)
    {
        foreach (var interceptor in state.ExecutedInterceptors)
        {
            await interceptor.AfterExportAsync(state.Context, cancellationToken);
        }
    }
}
