// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.DataPorter;

/// <summary>
/// Represents the boxed state returned from a typed export row interception executor.
/// </summary>
/// <param name="Outcome">The interception outcome.</param>
/// <param name="Item">The boxed item after interception.</param>
/// <param name="Reason">The skip or abort reason when available.</param>
/// <param name="State">The original typed executor state.</param>
public sealed record ObjectExportRowInterceptionState(
    RowInterceptionOutcome Outcome,
    object Item,
    string Reason,
    object State);

/// <summary>
/// Invokes typed export row interception executors through reflection for object-based multi-dataset exports.
/// </summary>
public static class ObjectExportRowInterceptionInvoker
{
    /// <summary>
    /// Executes the boxed <c>BeforeAsync</c> export interception callback.
    /// </summary>
    /// <param name="executor">The typed executor instance.</param>
    /// <param name="item">The current item.</param>
    /// <param name="rowNumber">The logical row number.</param>
    /// <param name="format">The export format.</param>
    /// <param name="sheetName">The dataset or sheet name.</param>
    /// <param name="isStreaming">Indicates whether the operation is streaming.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The boxed interception state.</returns>
    public static async Task<ObjectExportRowInterceptionState> BeforeAsync(
        object executor,
        object item,
        int rowNumber,
        Format format,
        string sheetName,
        bool isStreaming,
        CancellationToken cancellationToken = default)
    {
        if (executor is null)
        {
            return new ObjectExportRowInterceptionState(RowInterceptionOutcome.Continue, item, null, null);
        }

        var method = executor.GetType().GetMethod(nameof(ExportRowInterceptionExecutor<object>.BeforeAsync));
        var task = method?.Invoke(executor, [item, rowNumber, format, sheetName, isStreaming, cancellationToken]) as Task;
        if (task is null)
        {
            return new ObjectExportRowInterceptionState(RowInterceptionOutcome.Continue, item, null, null);
        }

        await task;
        var result = task.GetType().GetProperty("Result")?.GetValue(task);
        if (result is null)
        {
            return new ObjectExportRowInterceptionState(RowInterceptionOutcome.Continue, item, null, null);
        }

        return new ObjectExportRowInterceptionState(
            (RowInterceptionOutcome)result.GetType().GetProperty(nameof(ExportRowInterceptionState<object>.Outcome))?.GetValue(result),
            result.GetType().GetProperty(nameof(ExportRowInterceptionState<object>.Item))?.GetValue(result) ?? item,
            result.GetType().GetProperty(nameof(ExportRowInterceptionState<object>.Reason))?.GetValue(result) as string,
            result);
    }

    /// <summary>
    /// Executes the boxed <c>AfterAsync</c> export interception callback.
    /// </summary>
    /// <param name="executor">The typed executor instance.</param>
    /// <param name="state">The boxed typed state returned by <see cref="BeforeAsync(object, object, int, Format, string, bool, CancellationToken)"/>.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that completes when post-processing has finished.</returns>
    public static async Task AfterAsync(
        object executor,
        object state,
        CancellationToken cancellationToken = default)
    {
        if (executor is null || state is null)
        {
            return;
        }

        var method = executor.GetType().GetMethod(nameof(ExportRowInterceptionExecutor<object>.AfterAsync));
        if (method?.Invoke(executor, [state, cancellationToken]) is Task task)
        {
            await task;
        }
    }
}
