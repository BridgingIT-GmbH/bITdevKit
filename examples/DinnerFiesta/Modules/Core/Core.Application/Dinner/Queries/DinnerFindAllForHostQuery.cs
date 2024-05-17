// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.DinnerFiesta.Modules.Core.Application;

using System.Collections.Generic;
using BridgingIT.DevKit.Application.Queries;
using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Examples.DinnerFiesta.Modules.Core.Domain;
using FluentValidation;
using FluentValidation.Results;

public class DinnerFindAllForHostQuery(string hostId) : QueryRequestBase<Result<IEnumerable<Dinner>>>
{
    public string HostId { get; set; } = hostId;

    public override ValidationResult Validate() =>
        new Validator().Validate(this);

    public class Validator : AbstractValidator<DinnerFindAllForHostQuery>
    {
        public Validator()
        {
            this.RuleFor(c => c.HostId).NotNull().NotEmpty().WithMessage("Must not be empty.");
        }
    }
}