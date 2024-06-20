// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.IntegrationTests.Queries;

using BridgingIT.DevKit.Application.Queries;
using FluentValidation.Results;

public class StubPersonQuery(string firstName) : QueryRequestBase<IEnumerable<PersonStub>>
{
    public string FirstName { get; } = firstName;

    public override ValidationResult Validate() =>
        new StubPersonQueryValidator().Validate(this);
}