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

public class MenuFindOneForHostQuery(string hostId, string dinnerId) : QueryRequestBase<Result<Menu>>
{
    public string HostId { get; } = hostId;

    public string MenuId { get; } = dinnerId;

    public override ValidationResult Validate()
    {
        return new Validator().Validate(this);
    }

    public class Validator : AbstractValidator<MenuFindOneForHostQuery>
    {
        public Validator()
        {
            this.RuleFor(c => c.HostId).NotNull().NotEmpty().WithMessage("Must not be empty.");
            this.RuleFor(c => c.MenuId).NotNull().NotEmpty().WithMessage("Must not be empty.");
        }
    }
}