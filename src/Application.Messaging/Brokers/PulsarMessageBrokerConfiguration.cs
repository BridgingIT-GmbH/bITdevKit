// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Messaging;

using FluentValidation;

public class PulsarMessageBrokerConfiguration
{
    public string ServiceUrl { get; set; }

    public string Subscription { get; set; }

    public int ProcessDelay { get; set; }

    public string MessageScope { get; set; }

    public class Validator : AbstractValidator<PulsarMessageBrokerConfiguration>
    {
        public Validator()
        {
            this.RuleFor(c => c.ServiceUrl).NotNull().NotEmpty().WithMessage("ServiceUrl cannot be null or empty");

            this.RuleFor(c => c.Subscription).NotNull().NotEmpty().WithMessage("Subscription cannot be null or empty");
        }
    }
}