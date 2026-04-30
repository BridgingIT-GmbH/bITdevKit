// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.Azure;

using FluentValidation;

/// <summary>
/// Provides configuration-bound settings for the Azure Service Bus queue broker.
/// </summary>
public class ServiceBusQueueBrokerConfiguration
{
    /// <summary>
    /// Gets or sets the Azure Service Bus connection string.
    /// </summary>
    public string ConnectionString { get; set; }

    /// <summary>
    /// Gets or sets the queue name prefix.
    /// </summary>
    public string QueueNamePrefix { get; set; }

    /// <summary>
    /// Gets or sets the queue name suffix.
    /// </summary>
    public string QueueNameSuffix { get; set; }

    /// <summary>
    /// Gets or sets the maximum number of concurrent processing calls.
    /// </summary>
    public int MaxConcurrentCalls { get; set; } = 8;

    /// <summary>
    /// Gets or sets the prefetch count for the processor.
    /// </summary>
    public int PrefetchCount { get; set; } = 20;

    /// <summary>
    /// Gets or sets a value indicating whether queues should be created automatically at runtime.
    /// </summary>
    public bool AutoCreateQueue { get; set; } = true;

    /// <summary>
    /// Gets or sets the default message expiration.
    /// </summary>
    public TimeSpan? MessageExpiration { get; set; } = TimeSpan.FromDays(7);

    /// <summary>
    /// Gets or sets the maximum number of delivery attempts before dead-lettering.
    /// </summary>
    public int MaxDeliveryAttempts { get; set; } = 5;

    /// <summary>
    /// Gets or sets the processing delay in milliseconds.
    /// </summary>
    public int ProcessDelay { get; set; } = 100;

    /// <summary>
    /// Validates the configuration.
    /// </summary>
    public class Validator : AbstractValidator<ServiceBusQueueBrokerConfiguration>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Validator"/> class.
        /// </summary>
        public Validator()
        {
            this.RuleFor(c => c.ConnectionString)
                .NotNull()
                .NotEmpty()
                .WithMessage("ConnectionString cannot be null or empty");
        }
    }
}
