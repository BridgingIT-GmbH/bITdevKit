// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Storage;

using System;
using System.Threading;
using System.Threading.Tasks;
using BridgingIT.DevKit.Common;
using Microsoft.Extensions.Logging;

/// <summary>
/// Retries failed processor operations with exponential backoff up to a specified number of attempts.
/// </summary>
public class RetryProcessorBehavior(
    ILogger<RetryProcessorBehavior> logger,
    int maxAttempts = 3,
    TimeSpan? initialDelay = null) : IProcessorBehavior
{
    private readonly ILogger<RetryProcessorBehavior> logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private readonly int maxAttempts = maxAttempts > 0 ? maxAttempts : 3;
    private readonly TimeSpan initialDelay = initialDelay ?? TimeSpan.FromSeconds(1);

    public async Task BeforeProcessAsync(FileProcessingContext context, CancellationToken token)
    {
        // No-op before processing; retry logic is in AfterProcessAsync
        await Task.CompletedTask;
    }

    public async Task AfterProcessAsync(FileProcessingContext context, Result<bool> result, CancellationToken token)
    {
        EnsureArg.IsNotNull(context, nameof(context));

        token.ThrowIfCancellationRequested();

        if (result.IsSuccess)
        {
            return; // No retry needed
        }

        var attempt = context.GetItem<int>("RetryAttempt") + 1;
        if (attempt >= this.maxAttempts)
        {
            this.logger.LogError(
                "Max retry attempts ({MaxAttempts}) reached for file event: Location={LocationName}, Path={FilePath}",
                this.maxAttempts,
                context.FileEvent.LocationName,
                context.FileEvent.FilePath);
            return;
        }

        var delay = TimeSpan.FromTicks(this.initialDelay.Ticks * (long)Math.Pow(2, attempt - 1));
        this.logger.LogWarning(
            "Retry attempt {Attempt}/{MaxAttempts} after delay {Delay} for file event: Location={LocationName}, Path={FilePath}, Error={ErrorMessage}",
            attempt,
            this.maxAttempts,
            delay,
            context.FileEvent.LocationName,
            context.FileEvent.FilePath,
            result.Errors.FirstOrDefault()?.Message);

        await Task.Delay(delay, token);
        context.SetItem("RetryAttempt", attempt);

        // Re-throw to trigger re-processing by the BehaviorDecorator
        throw new RetryException($"Retry attempt {attempt} for file event: {context.FileEvent.FilePath}");
    }
}
