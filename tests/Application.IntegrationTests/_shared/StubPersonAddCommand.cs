// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.IntegrationTests.Commands;

using Application.Commands;
using FluentValidation;
using FluentValidation.Results;

public class StubPersonAddCommand(PersonStub person) : CommandRequestBase<bool>
{
    public PersonStub Person { get; } = person;

    public override ValidationResult Validate()
    {
        return new Validator().Validate(this);
    }

    public class Validator : AbstractValidator<StubPersonAddCommand>
    {
        public Validator()
        {
            this.RuleFor(c => c.Person)
                .NotNull();
            this.RuleFor(c => c.Person.Id)
                .Empty();
            this.RuleFor(c => c.Person.FirstName)
                .NotNull()
                .NotEmpty();
            this.RuleFor(c => c.Person.LastName)
                .NotNull()
                .NotEmpty();
        }
    }
}