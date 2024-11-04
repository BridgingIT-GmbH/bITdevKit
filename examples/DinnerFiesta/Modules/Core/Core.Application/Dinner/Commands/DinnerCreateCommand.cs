// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.DinnerFiesta.Modules.Core.Application;

using FluentValidation.Results;

public class DinnerCreateCommand : CommandRequestBase<Result<Dinner>>
{
    public string Name { get; set; }
    public string Description { get; set; }
    public DinnerSchedule Schedule { get; set; }
    public bool IsPublic { get; set; }
    public int MaxGuests { get; set; }
    public DinnerPrice Price { get; set; }
    public string HostId { get; set; }
    public string MenuId { get; set; }
    public string ImageUrl { get; set; }
    public DinnerLocation Location { get; set; }

    public override ValidationResult Validate()
    {
        return new Validator().Validate(this);
    }

    public class Validator : AbstractValidator<DinnerCreateCommand>
    {
        public Validator()
        {
            this.RuleFor(c => c.Name).NotNull().NotEmpty().WithMessage("Must not be empty.");
            this.RuleFor(c => c.MenuId).NotNull().NotEmpty().WithMessage("Must not be empty.");
            this.RuleFor(c => c.HostId).NotNull().NotEmpty().WithMessage("Must not be empty.");
            this.RuleFor(c => c.Schedule).NotNull().WithMessage("Must not be empty.");
            this.RuleFor(c => c.Price).NotNull().WithMessage("Must not be empty.");
            this.RuleFor(c => c.Location).NotNull().WithMessage("Must not be empty.");
            this.RuleFor(c => c.MaxGuests).InclusiveBetween(1, 99).WithMessage("Must be between 1 and 99.");
        }
    }

    public class DinnerSchedule
    {
        public DateTimeOffset StartDateTime { get; set; }
        public DateTimeOffset EndDateTime { get; set; }
    }

    public class DinnerPrice
    {
        public decimal Amount { get; set; }
        public string Currency { get; set; }
    }

    public class DinnerLocation
    {
        public string Name { get; set; }
        public string AddressLine1 { get; set; }
        public string AddressLine2 { get; set; }
        public string PostalCode { get; set; }
        public string City { get; set; }
        public string Country { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public string WebsiteUrl { get; set; }
    }
}