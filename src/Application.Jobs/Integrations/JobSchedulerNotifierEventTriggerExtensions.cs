// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace Microsoft.Extensions.DependencyInjection;

using BridgingIT.DevKit.Application.Jobs;
using BridgingIT.DevKit.Common;

/// <summary>
/// Provides Notifier adapter registration helpers for scheduler event triggers.
/// </summary>
public static class JobSchedulerNotifierEventTriggerExtensions
{
    /// <summary>
    /// Connects Common Notifier publishing to the scheduler event-trigger pipeline.
    /// </summary>
    /// <example>
    /// <code>
    /// services.AddNotifier()
    ///     .UseJobSchedulerEventTriggers();
    /// </code>
    /// </example>
    public static NotifierBuilder UseJobSchedulerEventTriggers(this NotifierBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.WithBehavior(typeof(JobSchedulerNotifierAcceptedEventBehavior<,>));
    }
}