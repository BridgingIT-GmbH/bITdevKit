// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.DinnerFiesta.Modules.Core.Application;

using Common;
using DevKit.Application.Queries;
using Domain;
using FluentValidation;
using FluentValidation.Results;

public class MenuFindAllForHostQuery(string hostId) : QueryRequestBase<Result<IEnumerable<Menu>>>
{
    public string HostId { get; set; } = hostId;

    public override ValidationResult Validate()
    {
        return new Validator().Validate(this);
    }

    public class Validator : AbstractValidator<MenuFindAllForHostQuery>
    {
        public Validator()
        {
            this.RuleFor(c => c.HostId).NotNull().NotEmpty().WithMessage("Must not be empty.");
        }
    }
}