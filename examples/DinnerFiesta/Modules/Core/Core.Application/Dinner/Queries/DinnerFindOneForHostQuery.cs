// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.DinnerFiesta.Modules.Core.Application;

using BridgingIT.DevKit.Application.Queries;
using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Examples.DinnerFiesta.Modules.Core.Domain;
using FluentValidation;
using FluentValidation.Results;

public class DinnerFindOneForHostQuery(string hostId, string dinnerId) : QueryRequestBase<Result<Dinner>>
{
    public string HostId { get; } = hostId;

    public string DinnerId { get; } = dinnerId;

    public override ValidationResult Validate() =>
        new Validator().Validate(this);

    public class Validator : AbstractValidator<DinnerFindOneForHostQuery>
    {
        public Validator()
        {
            this.RuleFor(c => c.HostId).NotNull().NotEmpty().WithMessage("Must not be empty.");
            this.RuleFor(c => c.DinnerId).NotNull().NotEmpty().WithMessage("Must not be empty.");
        }
    }
}