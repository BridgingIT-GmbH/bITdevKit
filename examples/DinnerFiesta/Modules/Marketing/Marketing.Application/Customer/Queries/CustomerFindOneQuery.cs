// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.DinnerFiesta.Modules.Marketing.Application;

using BridgingIT.DevKit.Application.Queries;
using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Examples.DinnerFiesta.Modules.Marketing.Domain;
using FluentValidation;
using FluentValidation.Results;

public class CustomerFindOneQuery(string customerId) : QueryRequestBase<Result<Customer>>
{
    public string CustomerId { get; } = customerId;

    public override ValidationResult Validate() =>
        new Validator().Validate(this);

    public class Validator : AbstractValidator<CustomerFindOneQuery>
    {
        public Validator()
        {
            this.RuleFor(c => c.CustomerId).NotNull().NotEmpty().WithMessage("Must not be empty.");
        }
    }
}