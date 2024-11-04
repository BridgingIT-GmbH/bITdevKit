// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.DinnerFiesta.Modules.Core.Application;

using FluentValidation.Results;

public class HostUpdateCommand : CommandRequestBase<Result<Host>>
{
    public string Id { get; set; }

    public string FirstName { get; set; }

    public string LastName { get; set; }

    public string ImageUrl { get; set; }

    public override ValidationResult Validate()
    {
        return new Validator().Validate(this);
    }

    public class Validator : AbstractValidator<HostUpdateCommand>
    {
        public Validator()
        {
            this.RuleFor(c => c.Id).NotNull().NotEmpty().WithMessage("Must not be empty.");
            this.RuleFor(c => c.FirstName).NotNull().NotEmpty().WithMessage("Must not be empty.");
            this.RuleFor(c => c.LastName).NotNull().NotEmpty().WithMessage("Must not be empty.");
        }
    }
}