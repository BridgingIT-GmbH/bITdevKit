// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
/// Represents an executable background job.
/// </summary>
/// <example>
/// <code>
/// public sealed class CleanupJob : IJob
/// {
///     public Task&lt;IResult&gt; ExecuteAsync(
///         IJobExecutionContext context,
///         CancellationToken cancellationToken = default)
///     {
///         return Task.FromResult&lt;IResult&gt;(Result.Success());
///     }
/// }
/// </code>
/// </example>
public interface IJob
{
    /// <summary>
    /// Executes the job using the supplied execution context.
    /// </summary>
    /// <param name="context">The execution context.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The execution result.</returns>
    Task<IResult> ExecuteAsync(
        IJobExecutionContext context,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Represents a typed background job that expects input data of type <typeparamref name="TData"/>.
/// </summary>
/// <typeparam name="TData">The input data type.</typeparam>
/// <example>
/// <code>
/// public sealed class ExportCustomersJob : IJob&lt;ExportCustomersRequest&gt;
/// {
///     public Task&lt;IResult&gt; ExecuteAsync(
///         IJobExecutionContext&lt;ExportCustomersRequest&gt; context,
///         CancellationToken cancellationToken = default)
///     {
///         return Task.FromResult&lt;IResult&gt;(Result.Success());
///     }
/// }
/// </code>
/// </example>
public interface IJob<TData> : IJob
{
    /// <summary>
    /// Executes the job using the supplied typed execution context.
    /// </summary>
    /// <param name="context">The typed execution context.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The execution result.</returns>
    Task<IResult> ExecuteAsync(
        IJobExecutionContext<TData> context,
        CancellationToken cancellationToken = default);
}