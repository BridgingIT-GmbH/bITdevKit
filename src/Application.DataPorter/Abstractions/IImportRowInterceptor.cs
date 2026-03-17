// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.DataPorter;

/// <summary>
/// Defines a typed interceptor for row-level import processing.
/// </summary>
/// <typeparam name="TTarget">The imported item type.</typeparam>
public interface IImportRowInterceptor<TTarget>
    where TTarget : class
{
    /// <summary>
    /// Invoked before an imported row is accepted into the result.
    /// </summary>
    /// <param name="context">The row context.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The interception decision.</returns>
    Task<RowInterceptionDecision> BeforeImportAsync(
        ImportRowContext<TTarget> context,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Invoked after an imported row was accepted successfully.
    /// </summary>
    /// <param name="context">The row context.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A completed task.</returns>
    Task AfterImportAsync(
        ImportRowContext<TTarget> context,
        CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}
