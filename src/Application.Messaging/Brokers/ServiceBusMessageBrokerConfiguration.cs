// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Messaging;

using FluentValidation;

public class ServiceBusMessageBrokerConfiguration
{
    public string ConnectionString { get; set; }

    public int ProcessDelay { get; set; }

    public TimeSpan? MessageExpiration { get; set; }

    public string MessageScope { get; set; }

    public class Validator : AbstractValidator<ServiceBusMessageBrokerConfiguration>
    {
        public Validator()
        {
            this.RuleFor(c => c.ConnectionString)
                .NotNull()
                .NotEmpty()
                .WithMessage("ConnectionString cannot be null or empty");
        }
    }
}