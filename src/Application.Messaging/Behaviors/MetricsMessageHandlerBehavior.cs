// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Messaging;

using System.Diagnostics.Metrics;
using System.Threading;
using BridgingIT.DevKit.Common;
using Microsoft.Extensions.Logging;

public class MetricsMessageHandlerBehavior : MessageHandlerBehaviorBase
{
    private readonly IMeterFactory meterFactory;

    public MetricsMessageHandlerBehavior(ILoggerFactory loggerFactory, IMeterFactory meterFactory = null)
        : base(loggerFactory)
    {
        this.meterFactory = meterFactory;
    }

    public override async Task Handle<TMessage>(TMessage message, CancellationToken cancellationToken, object handler, MessageHandlerDelegate next)
    {
        if (this.meterFactory is null || message is null)
        {
            return;
        }

        if (cancellationToken.IsCancellationRequested)
        {
            return;
        }

        var meter = this.meterFactory.Create("bridgingit_devkit");
        meter.CreateCounter<int>("messaging_handle").Add(1);
        meter.CreateCounter<int>($"messaging_handle_{message.GetType().Name.ToLower()}").Add(1);

        try
        {
            await next().AnyContext(); // continue pipeline
        }
        catch
        {
            meter.CreateCounter<int>("messaging_handle_failure").Add(1);
            meter.CreateCounter<int>($"messaging_handle_{message.GetType().Name.ToLower()}_failure").Add(1);
            throw;
        }
    }
}