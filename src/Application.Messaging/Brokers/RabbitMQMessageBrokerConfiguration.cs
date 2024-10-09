// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Messaging;

using FluentValidation;

public class RabbitMQMessageBrokerConfiguration
{
    public string HostName { get; set; }

    public string ConnectionString { get; set; }

    public string ExchangeName { get; set; }

    public string QueueName { get; set; }

    public int ProcessDelay { get; set; }

    public TimeSpan? MessageExpiration { get; set; }

    public string MessageScope { get; set; }

    public class Validator : AbstractValidator<RabbitMQMessageBrokerConfiguration>
    {
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