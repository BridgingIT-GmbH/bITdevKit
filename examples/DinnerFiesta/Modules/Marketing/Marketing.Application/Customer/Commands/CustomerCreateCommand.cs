// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.DinnerFiesta.Modules.Marketing.Application;

using Common;
using DevKit.Application.Commands;
using Domain;
using FluentValidation;
using FluentValidation.Results;

public class CustomerCreateCommand : CommandRequestBase<Result<Customer>>
{
    public string FirstName { get; set; }

    public string LastName { get; set; }

    public string Email { get; set; }

    public override ValidationResult Validate()
    {
        return new Validator().Validate(this);
    }

    public class Validator : AbstractValidator<CustomerCreateCommand>
    {
        public Validator()
        {
            this.RuleFor(c => c.FirstName).NotNull().NotEmpty().WithMessage("Must not be empty.");
            this.RuleFor(c => c.LastName).NotNull().NotEmpty().WithMessage("Must not be empty.");
            this.RuleFor(c => c.Email).NotNull().NotEmpty().WithMessage("Must not be empty.");
        }
    }
}