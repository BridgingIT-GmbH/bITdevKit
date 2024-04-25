// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.DinnerFiesta.Application.Modules.Core;

using FluentValidation;

public class CoreModuleConfiguration
{
    public IReadOnlyDictionary<string, string> ConnectionStrings { get; set; }

    public string SeederTaskStartupDelay { get; set; } = "00:00:05";

    public class Validator : AbstractValidator<CoreModuleConfiguration>
    {
        public Validator()
        {
            this.RuleFor(c => c.ConnectionStrings)
                .NotNull().NotEmpty()
                .Must(c => c.ContainsKey("Default"))
                .WithMessage("Connection string with name 'Default' is required");

            this.RuleFor(c => c.SeederTaskStartupDelay)
                .NotNull().NotEmpty()
                .WithMessage("SeederTaskStartupDelay cannot be null or empty");
        }
    }
}