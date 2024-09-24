// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.DinnerFiesta.Application.Modules.Marketing;

using FluentValidation;

public class MarketingModuleConfiguration
{
    public IReadOnlyDictionary<string, string> ConnectionStrings { get; set; }

    public string SeederTaskStartupDelay { get; set; } = "00:00:05";

    public class Validator : AbstractValidator<MarketingModuleConfiguration>
    {
        public Validator()
        {
            this.RuleFor(c => c.ConnectionStrings)
                .NotNull()
                .NotEmpty()
                .Must(c => c.ContainsKey("Default"))
                .WithMessage("Connection string with name 'Default' is required");

            this.RuleFor(c => c.SeederTaskStartupDelay)
                .NotNull()
                .NotEmpty()
                .WithMessage("SeederTaskStartupDelay cannot be null or empty");
        }
    }
}