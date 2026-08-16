// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Messaging;

using FluentValidation;

/// <summary>
/// Represents rabbit mq message broker configuration.
/// </summary>
public class RabbitMQMessageBrokerConfiguration
{
    /// <summary>
    /// Gets or sets the host name.
    /// </summary>
    public string HostName { get; set; }

    /// <summary>
    /// Gets or sets the connection string.
    /// </summary>
    public string ConnectionString { get; set; }

    /// <summary>
    /// Gets or sets the exchange name.
    /// </summary>
    public string ExchangeName { get; set; }

    /// <summary>
    /// Gets or sets the queue name.
    /// </summary>
    public string QueueName { get; set; }

    /// <summary>
    /// Gets or sets the process delay.
    /// </summary>
    public int ProcessDelay { get; set; }

    /// <summary>
    /// Gets or sets the message expiration.
    /// </summary>
    public TimeSpan? MessageExpiration { get; set; }

    /// <summary>
    /// Gets or sets the message scope.
    /// </summary>
    public string MessageScope { get; set; }

    /// <summary>
    /// Represents validator.
    /// </summary>
    public class Validator : AbstractValidator<RabbitMQMessageBrokerConfiguration>
    {
        /// <summary>
        /// Initializes a new instance of the <c>Validator</c> class.
        /// </summary>
        public Validator()
        {
            this.RuleFor(c => c.HostName)
                .NotNull()
                .NotEmpty()
                .When(c => c.ConnectionString.IsNullOrEmpty())
                .WithMessage("HostName cannot be null or empty");

            this.RuleFor(c => c.ConnectionString)
                .NotNull()
                .NotEmpty()
                .When(c => c.HostName.IsNullOrEmpty())
                .WithMessage("ConnectionString cannot be null or empty");
        }
    }
}
