// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.DinnerFiesta.Modules.Marketing.Application;

using Common;
using DevKit.Application.Queries;
using Domain;
using FluentValidation;
using FluentValidation.Results;

public class CustomerFindAllQuery : QueryRequestBase<Result<IEnumerable<Customer>>>
{
    public override ValidationResult Validate()
    {
        return new Validator().Validate(this);
    }

    public class Validator : AbstractValidator<CustomerFindAllQuery>;
}