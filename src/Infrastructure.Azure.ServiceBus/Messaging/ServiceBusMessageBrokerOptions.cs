// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.Azure;

using Application.Messaging;
using Common;

/// <summary>
/// Configures service bus message broker.
/// </summary>
public class ServiceBusMessageBrokerOptions : OptionsBase
{
    /// <summary>
    /// Gets or sets the publisher behaviors.
    /// </summary>
    public IEnumerable<IMessagePublisherBehavior> PublisherBehaviors { get; set; }

    /// <summary>
    /// Gets or sets the handler behaviors.
    /// </summary>
    public IEnumerable<IMessageHandlerBehavior> HandlerBehaviors { get; set; }

    /// <summary>
    /// Gets or sets the handler factory.
    /// </summary>
    public IMessageHandlerFactory HandlerFactory { get; set; }

    /// <summary>
    /// Gets or sets the serializer.
    /// </summary>
    public ISerializer Serializer { get; set; }

    /// <summary>
    /// Gets or sets the connection string.
    /// </summary>
    public string ConnectionString { get; set; }

    /// <summary>
    /// Gets or sets the topic scope.
    /// </summary>
    public string TopicScope { get; set; }

    /// <summary>
    /// Gets or sets the retries.
    /// </summary>
    public int Retries { get; set; } = 3;

    /// <summary>
    /// Gets or sets the process delay.
    /// </summary>
    public int ProcessDelay { get; set; } = 100;

    /// <summary>
    ///     The default message time to live.
    /// </summary>
    public TimeSpan? MessageExpiration { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether topics and subscriptions should be created at runtime.
    /// </summary>
    public bool AutoCreateTopic { get; set; } = true;
}
