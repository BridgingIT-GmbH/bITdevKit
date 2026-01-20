// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.IntegrationTests.Queries;

using Application.Queries;
using FluentValidation.Results;

#pragma warning disable CS0618 // Type or member is obsolete
public class StubPersonQuery(string firstName) : QueryRequestBase<IEnumerable<PersonStub>>
#pragma warning restore CS0618 // Type or member is obsolete
{
    public string FirstName { get; } = firstName;

    public override ValidationResult Validate()
    {
        return new StubPersonQueryValidator().Validate(this);
    }
}