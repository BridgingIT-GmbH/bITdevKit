// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Jobs;

using System.Collections.Concurrent;
using BridgingIT.DevKit.Common;

internal sealed class InlineJobHandlerRegistry
{
    private readonly ConcurrentDictionary<string, Func<IJobExecutionContext, IServiceProvider, CancellationToken, Task<Result>>> handlers =
        new(StringComparer.OrdinalIgnoreCase);

    public void Register(string jobName, Func<IJobExecutionContext, IServiceProvider, CancellationToken, Task<Result>> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);

        if (string.IsNullOrWhiteSpace(jobName))
        {
            throw new InvalidOperationException("An inline job requires a non-empty job name.");
        }

        if (!this.handlers.TryAdd(jobName.Trim(), handler))
        {
            throw new InvalidOperationException($"The inline job '{jobName}' is already registered.");
        }
    }

    public Task<Result> ExecuteAsync(IJobExecutionContext context, IServiceProvider serviceProvider, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(serviceProvider);

        if (!this.handlers.TryGetValue(context.JobName, out var handler))
        {
            return Task.FromResult(Result.Failure($"No inline job handler is registered for '{context.JobName}'."));
        }

        return handler(context, serviceProvider, cancellationToken);
    }
}