// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.IntegrationTests.Queries;

using BridgingIT.DevKit.Application.Queries;
using FluentValidation.Results;

public class StubPersonQuery : QueryRequestBase<IEnumerable<PersonStub>>
{
    public StubPersonQuery(string firstName)
    {
        this.FirstName = firstName;
    }

    public string FirstName { get; }

    public override ValidationResult Validate() =>
        new StubPersonQueryValidator().Validate(this);
}