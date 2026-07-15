// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.DataPorter;

using BridgingIT.DevKit.Common;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>
/// Executes typed import row interceptors for a single import operation.
/// </summary>
/// <typeparam name="TTarget">The import target type.</typeparam>
public interface IImportRowInterceptionExecutor<TTarget>
    where TTarget : class
{
    /// <summary>
    /// Executes the pre-import interceptor pipeline for the specified item.
    /// </summary>
    /// <param name="item">The current item.</param>
    /// <param name="rowNumber">The logical row number.</param>
    /// <param name="format">The import format.</param>
    /// <param name="sheetName">The sheet or section name when available.</param>
    /// <param name="isStreaming">Indicates whether the operation is streaming.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The resulting interception state.</returns>
    Task<ImportRowInterceptionState<TTarget>> BeforeAsync(
        TTarget item,
        int rowNumber,
        Format format,
        string sheetName,
        bool isStreaming,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes the post-import interceptor callbacks for a successful row.
    /// </summary>
    /// <param name="state">The interception state returned by <see cref="BeforeAsync(TTarget, int, Format, string, bool, CancellationToken)"/>.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that completes when post-processing has finished.</returns>
    Task AfterAsync(
        ImportRowInterceptionState<TTarget> state,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes the import completion callbacks after all rows have been processed.
    /// </summary>
    /// <param name="result">The completed import result.</param>
    /// <param name="format">The import format.</param>
    /// <param name="sheetName">The sheet or section name when available.</param>
    /// <param name="isStreaming">Indicates whether the operation is streaming.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The aggregated completion result.</returns>
    Task<Result> CompleteAsync(
        ImportResult<TTarget> result,
        Format format,
        string sheetName,
        bool isStreaming,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Represents the state accumulated while executing typed import row interceptors.
/// </summary>
/// <typeparam name="TTarget">The import target type.</typeparam>
/// <param name="Outcome">The interception outcome.</param>
/// <param name="Item">The current item after interception.</param>
/// <param name="Reason">The skip or abort reason when available.</param>
/// <param name="ExecutedInterceptors">The interceptors that completed successfully.</param>
/// <param name="Context">The shared row context.</param>
public sealed record ImportRowInterceptionState<TTarget>(
    RowInterceptionOutcome Outcome,
    TTarget Item,
    string Reason,
    IReadOnlyList<IImportRowInterceptor<TTarget>> ExecutedInterceptors,
    ImportRowContext<TTarget> Context)
    where TTarget : class
{
    /// <summary>
    /// Creates a successful continuation state.
    /// </summary>
    public static ImportRowInterceptionState<TTarget> Continue(
        TTarget item,
        IReadOnlyList<IImportRowInterceptor<TTarget>> executedInterceptors,
        ImportRowContext<TTarget> context)
    {
        return new ImportRowInterceptionState<TTarget>(
            RowInterceptionOutcome.Continue,
            item,
            null,
            executedInterceptors,
            context);
    }

    /// <summary>
    /// Creates a skip state.
    /// </summary>
    public static ImportRowInterceptionState<TTarget> Skip(TTarget item, string reason, ImportRowContext<TTarget> context)
    {
        return new ImportRowInterceptionState<TTarget>(
            RowInterceptionOutcome.Skip,
            item,
            reason,
            [],
            context);
    }

    /// <summary>
    /// Creates an abort state.
    /// </summary>
    public static ImportRowInterceptionState<TTarget> Abort(TTarget item, string reason, ImportRowContext<TTarget> context)
    {
        return new ImportRowInterceptionState<TTarget>(
            RowInterceptionOutcome.Abort,
            item,
            reason,
            [],
            context);
    }
}

/// <summary>
/// Default implementation of <see cref="IImportRowInterceptionExecutor{TTarget}"/>.
/// </summary>
/// <typeparam name="TTarget">The import target type.</typeparam>
public sealed class ImportRowInterceptionExecutor<TTarget>(
    IReadOnlyList<IImportRowInterceptor<TTarget>> interceptors,
    ILogger logger = null) : IImportRowInterceptionExecutor<TTarget>
    where TTarget : class
{
    private readonly IReadOnlyList<IImportRowInterceptor<TTarget>> interceptors = interceptors ?? [];
    private readonly ILogger logger = logger ?? NullLogger.Instance;

    /// <inheritdoc/>
    public async Task<ImportRowInterceptionState<TTarget>> BeforeAsync(
        TTarget item,
        int rowNumber,
        Format format,
        string sheetName,
        bool isStreaming,
        CancellationToken cancellationToken = default)
    {
        var context = new ImportRowContext<TTarget>
        {
            Item = item,
            RowNumber = rowNumber,
            Format = format,
            SheetName = sheetName,
            IsStreaming = isStreaming
        };

        if (this.interceptors.Count == 0)
        {
            return ImportRowInterceptionState<TTarget>.Continue(context.Item, [], context);
        }

        var executed = new List<IImportRowInterceptor<TTarget>>(this.interceptors.Count);

        foreach (var interceptor in this.interceptors)
        {
            var decision = await interceptor.BeforeImportAsync(context, cancellationToken) ?? RowInterceptionDecision.Continue();

            if (decision.Outcome == RowInterceptionOutcome.Skip)
            {
                this.logger.LogWarning("[{LogKey}] row interceptor skipped (interceptor={InterceptorType}, rowNumber={RowNumber}, format={Format}, sheetName={SheetName}, reason={Reason})", Constants.LogKeyImport, interceptor.GetType().Name, rowNumber, format, sheetName, decision.Reason);

                return ImportRowInterceptionState<TTarget>.Skip(context.Item, decision.Reason, context);
            }

            if (decision.Outcome == RowInterceptionOutcome.Abort)
            {
                this.logger.LogWarning("[{LogKey}] row interceptor aborted (interceptor={InterceptorType}, rowNumber={RowNumber}, format={Format}, sheetName={SheetName}, reason={Reason})", Constants.LogKeyImport, interceptor.GetType().Name, rowNumber, format, sheetName, decision.Reason);

                return ImportRowInterceptionState<TTarget>.Abort(context.Item, decision.Reason, context);
            }

            executed.Add(interceptor);
        }

        return ImportRowInterceptionState<TTarget>.Continue(context.Item, executed, context);
    }

    /// <inheritdoc/>
    public async Task AfterAsync(
        ImportRowInterceptionState<TTarget> state,
        CancellationToken cancellationToken = default)
    {
        foreach (var interceptor in state.ExecutedInterceptors)
        {
            await interceptor.AfterImportAsync(state.Context, cancellationToken);
        }
    }

    /// <inheritdoc/>
    public async Task<Result> CompleteAsync(
        ImportResult<TTarget> result,
        Format format,
        string sheetName,
        bool isStreaming,
        CancellationToken cancellationToken = default)
    {
        if (this.interceptors.Count == 0)
        {
            return Result.Success();
        }

        var context = new ImportCompletionContext<TTarget>
        {
            Result = result,
            Format = format,
            SheetName = sheetName,
            IsStreaming = isStreaming
        };
        var completionResults = new List<Result>(this.interceptors.Count);

        foreach (var interceptor in this.interceptors)
        {
            var completionResult = await interceptor.AfterImportCompletedAsync(context, cancellationToken);
            completionResults.Add(completionResult);

            if (completionResult.IsFailure)
            {
                this.logger.LogWarning("[{LogKey}] import completion interceptor failed (interceptor={InterceptorType}, format={Format}, sheetName={SheetName}, errors={Errors})",
                    Constants.LogKeyImport,
                    interceptor.GetType().Name,
                    format,
                    sheetName,
                    string.Join(", ", completionResult.Errors.Select(e => e.Message)));
            }
        }

        return Result.Merge(completionResults.ToArray());
    }
}
