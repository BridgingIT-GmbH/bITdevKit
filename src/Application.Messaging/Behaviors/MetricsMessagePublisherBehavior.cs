// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Messaging;

using System.Diagnostics.Metrics;
using Common;
using Microsoft.Extensions.Logging;

public class MetricsMessagePublisherBehavior(ILoggerFactory loggerFactory, IMeterFactory meterFactory = null)
    : MessagePublisherBehaviorBase(loggerFactory)
{
    public override async Task Publish<TMessage>(
        TMessage message,
        CancellationToken cancellationToken,
        MessagePublisherDelegate next)
    {
        if (meterFactory is null || message is null)
        {
            return;
        }

        if (cancellationToken.IsCancellationRequested)
        {
            return;
        }

        var meter = meterFactory.Create("bridgingit_devkit");
        meter.CreateCounter<int>("messages_publish").Add(1);
        meter.CreateCounter<int>($"messages_publish_{message.GetType().Name.ToLower()}").Add(1);

        try
        {
            await next().AnyContext(); // continue pipeline
        }
        catch
        {
            meter.CreateCounter<int>("messages_publish_failure").Add(1);
            meter.CreateCounter<int>($"messages_publish_{message.GetType().Name.ToLower()}_failure").Add(1);

            throw;
        }
    }
}