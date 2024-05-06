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

public class HostFindAllQuery : QueryRequestBase<Result<IEnumerable<Host>>>
{
    public override ValidationResult Validate() =>
        new Validator().Validate(this);

    public class Validator : AbstractValidator<HostFindAllQuery>
    {
    }
}