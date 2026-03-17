// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.DataPorter;

/// <summary>
/// Defines a typed interceptor for row-level export processing.
/// </summary>
/// <typeparam name="TSource">The exported item type.</typeparam>
public interface IExportRowInterceptor<TSource>
    where TSource : class
{
    /// <summary>
    /// Invoked before an exported row is written.
    /// </summary>
    /// <param name="context">The row context.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The interception decision.</returns>
    Task<RowInterceptionDecision> BeforeExportAsync(
        ExportRowContext<TSource> context,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Invoked after an exported row was written successfully.
    /// </summary>
    /// <param name="context">The row context.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A completed task.</returns>
    Task AfterExportAsync(
        ExportRowContext<TSource> context,
        CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}
