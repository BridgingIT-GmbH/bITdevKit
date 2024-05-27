// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

public class EchoStartupTask(ILoggerFactory loggerFactory) : IStartupTask, IChaosExceptionStartupTask, IRetryStartupTask, ITimeoutStartupTask
{
    private readonly ILogger<EchoStartupTask> logger = loggerFactory?.CreateLogger<EchoStartupTask>() ?? NullLoggerFactory.Instance.CreateLogger<EchoStartupTask>();

    ChaosExceptionStartupTaskOptions IChaosExceptionStartupTask.Options => new() { InjectionRate = 0.10 };

    RetryStartupTaskOptions IRetryStartupTask.Options => new() { Attempts = 3, Backoff = new TimeSpan(0, 0, 0, 1) };

    TimeoutStartupTaskOptions ITimeoutStartupTask.Options => new() { Timeout = new TimeSpan(0, 0, 30) };

    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        await Task.Delay(0, cancellationToken);

        this.logger.LogInformation("echo from startup task");
    }
}