// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.DinnerFiesta.Modules.Core.Application;

using System.Threading;
using System.Threading.Tasks;
using BridgingIT.DevKit.Application.JobScheduling;
using BridgingIT.DevKit.Application.Messaging;
using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Examples.DinnerFiesta.Application.Modules.Core;
using EnsureThat;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Quartz;

public class EchoMessageJob : JobBase,
    IRetryJobScheduling,
    IChaosExceptionJobScheduling
{
    private readonly IMessageBroker messageBroker;
    private readonly CoreModuleConfiguration moduleConfiguration;

    public EchoMessageJob(
        ILoggerFactory loggerFactory,
        IMessageBroker messageBroker,
        IOptions<CoreModuleConfiguration> moduleConfiguration)
        : base(loggerFactory)
    {
        EnsureArg.IsNotNull(messageBroker, nameof(messageBroker));
        this.messageBroker = messageBroker;
        this.moduleConfiguration = moduleConfiguration.Value;
    }

    RetryJobSchedulingOptions IRetryJobScheduling.Options => new() { Attempts = 3, Backoff = new TimeSpan(0, 0, 0, 1) };

    ChaosExceptionJobSchedulingOptions IChaosExceptionJobScheduling.Options => new() { InjectionRate = 0.10 };

    public override async Task Process(IJobExecutionContext context, CancellationToken cancellationToken = default)
    {
        await Task.Delay(2000, cancellationToken);

        await this.messageBroker.Publish(
            new EchoMessage("from job"), cancellationToken).AnyContext();
    }
}
