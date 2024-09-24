// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.DinnerFiesta.Modules.Core.Application;

using Common;
using DevKit.Application.Commands;
using Domain;
using FluentValidation;
using FluentValidation.Results;

public class MenuCreateCommand : CommandRequestBase<Result<Menu>>
{
    public string Name { get; set; }

    public string Description { get; set; }

    public string HostId { get; set; }

    public IReadOnlyList<MenuSection> Sections { get; set; }

    public override ValidationResult Validate()
    {
        return new Validator().Validate(this);
    }

    public class Validator : AbstractValidator<MenuCreateCommand>
    {
        public Validator()
        {
            this.RuleFor(c => c.Name).NotNull().NotEmpty().WithMessage("Must not be empty.");
            this.RuleFor(c => c.HostId).NotNull().NotEmpty().WithMessage("Must not be empty.");
            this.RuleForEach(c => c.Sections)
                .SetValidator(new MenuSection.Validator())
                .When(c => c.Sections is not null);
        }
    }

    public class MenuSection
    {
        public string Name { get; set; }

        public string Description { get; set; }

        public IReadOnlyList<MenuSectionItem> Items { get; set; }

        public class Validator : AbstractValidator<MenuSection>
        {
            public Validator()
            {
                this.RuleFor(c => c.Name).NotNull().NotEmpty().WithMessage("Must not be empty.");
                this.RuleForEach(c => c.Items)
                    .SetValidator(new MenuSectionItem.Validator())
                    .When(c => c.Items is not null);
            }
        }
    }

    public class MenuSectionItem
    {
        public string Name { get; set; }

        public string Description { get; set; }

        public class Validator : AbstractValidator<MenuSectionItem>
        {
            public Validator()
            {
                this.RuleFor(c => c.Name).NotNull().NotEmpty().WithMessage("Must not be empty.");
            }
        }
    }
}