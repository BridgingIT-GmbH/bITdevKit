// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.IntegrationTests.Queries;

using FluentValidation;

public class StubPersonQueryValidator : AbstractValidator<StubPersonQuery>
{
    public StubPersonQueryValidator()
    {
        this.RuleFor(c => c.FirstName)
            .NotNull()
            .NotEmpty();
    }
}