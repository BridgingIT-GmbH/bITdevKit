// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.DinnerFiesta.Modules.Core.Application;

using BridgingIT.DevKit.Application.Commands;
using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Examples.DinnerFiesta.Modules.Core.Domain;
using FluentValidation;
using FluentValidation.Results;

public class HostCreateCommand : CommandRequestBase<Result<Host>>
{
    public string FirstName { get; set; }

    public string LastName { get; set; }

    public string UserId { get; set; }

    public string ImageUrl { get; set; }

    public override ValidationResult Validate() =>
        new Validator().Validate(this);

    public class Validator : AbstractValidator<HostCreateCommand>
    {
        public Validator()
        {
            this.RuleFor(c => c.FirstName).NotNull().NotEmpty().WithMessage("Must not be empty.");
            this.RuleFor(c => c.LastName).NotNull().NotEmpty().WithMessage("Must not be empty.");
            this.RuleFor(c => c.UserId).NotNull().NotEmpty().WithMessage("Must not be empty.");
        }
    }
}