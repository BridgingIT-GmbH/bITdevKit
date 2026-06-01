// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Jobs;

using BridgingIT.DevKit.Common;

/// <summary>
/// Provides a base class for jobs that do not require input data.
/// </summary>
/// <example>
/// <code>
/// public sealed class CleanupJob : JobBase
/// {
///     public override Task&lt;Result&gt; ExecuteAsync(
///         IJobExecutionContext&lt;Unit&gt; context,
///         CancellationToken cancellationToken = default)
///     {
///         context.Messages.Add("Cleanup completed.");
///         return Task.FromResult(Result.Success());
///     }
/// }
/// </code>
/// </example>
public abstract class JobBase : JobBase<Unit>
{
}

/// <summary>
/// Provides a typed base class for jobs.
/// </summary>
/// <typeparam name="TData">The typed input data contract.</typeparam>
/// <example>
/// <code>
/// public sealed class ExportCustomersJob : JobBase&lt;ExportCustomersRequest&gt;
/// {
///     public override Task&lt;Result&gt; ExecuteAsync(
///         IJobExecutionContext&lt;ExportCustomersRequest&gt; context,
///         CancellationToken cancellationToken = default)
///     {
///         context.Messages.Add($"Exporting {context.Data.Profile}.");
///         return Task.FromResult(Result.Success());
///     }
/// }
/// </code>
/// </example>
public abstract class JobBase<TData> : IJob<TData>
{
    /// <summary>
    /// Gets the inferred data contract for the job type.
    /// </summary>
    public static Type InferredDataType => typeof(TData);

    /// <summary>
    /// Executes the job using the supplied typed execution context.
    /// </summary>
    /// <param name="context">The typed execution context.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The execution result.</returns>
    public abstract Task<Result> ExecuteAsync(
        IJobExecutionContext<TData> context,
        CancellationToken cancellationToken = default);

    async Task<IResult> IJob.ExecuteAsync(
        IJobExecutionContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context is not IJobExecutionContext<TData> typedContext)
        {
            return Result.Failure($"The job context for '{context.JobName}' does not provide the expected data contract '{typeof(TData).FullName}'.");
        }

        return await this.ExecuteAsync(typedContext, cancellationToken).ConfigureAwait(false);
    }

    async Task<IResult> IJob<TData>.ExecuteAsync(
        IJobExecutionContext<TData> context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        return await this.ExecuteAsync(context, cancellationToken).ConfigureAwait(false);
    }
}