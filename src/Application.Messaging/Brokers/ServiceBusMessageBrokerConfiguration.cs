// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Messaging;

using FluentValidation;

/// <summary>
/// Represents service bus message broker configuration.
/// </summary>
public class ServiceBusMessageBrokerConfiguration
{
    /// <summary>
    /// Gets or sets the connection string.
    /// </summary>
    public string ConnectionString { get; set; }

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
    public class Validator : AbstractValidator<ServiceBusMessageBrokerConfiguration>
    {
        /// <summary>
        /// Initializes a new instance of the <c>Validator</c> class.
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
