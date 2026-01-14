// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.UnitTests;

using System.Linq.Expressions;
using BridgingIT.DevKit.Domain.Model;

public class PersonStub : AggregateRoot<Guid>
{
    private List<AddressStub> addresses = [];

    public string FirstName { get; set; }

    public string LastName { get; set; }

    public int Age { get; set; }

    public DateTime BirthDate { get; set; }

    public string Email { get; set; }

    public TimeSpan WorkStartTime { get; set; }

    public EmploymentStatus EmploymentStatus { get; set; }

    public List<OrderStub> Orders { get; set; }

    public IReadOnlyCollection<AddressStub> Addresses { get => this.addresses; }

    //public List<AddressStub> Addresses { get; set; } = [];

    public AddressStub BillingAddress { get; set; }

    public Result<PersonStub> ChangeName(string first, string last, int age, string email)
    {
        return this.Change()
            .Set(p => p.ChangeName(first, last)) // Result should propagate failure
            .Set(p => p.ChangeAge(age)) // Result should propagate failure
            .Set(p => p.ChangeEmail(email)) // Result should propagate failure
            .Apply();
    }

    public Result<PersonStub> ChangeName(string first, string last)
    {
        return this.Change()
            .Set(p => p.FirstName, first)
            .Set(p => p.LastName, last)
            .Register(p => new PersonNameChangedEvent(p.Id)) // custom event, replaces default EntityUpdatedDomainEvent<PersonStub>
            .Check(p => p.FirstName != string.Empty, "First name cannot be empty") // Post-condition
            .Apply();
    }

    public Result<PersonStub> ChangeAge(int age)
    {
        return this.Change()
            .When(_ => age != 0)
            .Ensure(p => age > 0, "Age must be non-negative") // Pre-condition
            .Set(p => p.Age, age)
            .Check(p => p.Age >= 0, "Age cannot be negative") // Post-condition
            .Apply();
    }

    public Result<PersonStub> ChangeEmail(string email)
    {
        return this.Change()
            // Ensure runs BEFORE any changes
            .Ensure(p => p.EmploymentStatus != EmploymentStatus.Unemployed, "Must be employed to have email")
            .Set(p => p.Email, email)
            .Apply();
    }

    public Result<PersonStub> ChangeEmailWithValidation(string email)
    {
        // Simulating a Result-returning factory
        static Result<string> CreateEmail(string input)
        {
            if (input.Contains("invalid"))
                return Result<string>.Failure().WithError(new ValidationError("Invalid format"));
            return Result<string>.Success(input);
        }

        return this.Change()
            .Set(p => p.Email, CreateEmail(email))
            .Apply();
    }

    public Result<PersonStub> PromoteToAdult()
    {
        return this.Change()
            .When(p => p.Age >= 18)
            .Set(p => p.FirstName, "Adult")
            .Execute(r => r.Map(e => { e.LastName = "Adult"; return e; }))
            .Execute(r => r.Tap(e => Console.WriteLine($"Promoted {e.FirstName} {e.LastName} to Adult")))
            .Apply();
    }

    public Result<PersonStub> AddAddress(AddressStub address)
    {
        return this.Change()
            .Add(p => p.addresses, address)
            .Register(_ => new AddressListChangedEvent())
            .Apply();
    }

    public Result<PersonStub> RemoveAddress(AddressStub address)
    {
        return this.Change()
            .Remove(p => p.addresses, address)
            .Register(_ => new AddressListChangedEvent())
            .Apply();
    }

    public Result<PersonStub> ClearAddresses()
    {
        return this.Change()
            .Execute(p => p.addresses.Clear()) // Using Execute for void methods
            .Apply();
    }

    public Result<PersonStub> UpdateEmailWithHistory(string newEmail)
    {
        return this.Change()
            .Set(p => p.Email, newEmail)
            .Register((p, ctx) => new EmailChangedEvent(ctx.GetOldValue<string>(nameof(this.Email)), p.Email))
            .Apply();
    }

    // Domain Events for testing
    public class PersonNameChangedEvent(Guid id) : DomainEventBase
    {
        public Guid PersonId { get; } = id;
    }

    public class AddressListChangedEvent : DomainEventBase;

    public class EmailChangedEvent(string oldEmail, string newEmail) : DomainEventBase
    {
        public string OldEmail { get; } = oldEmail;

        public string NewEmail { get; } = newEmail;
    }
}

public class OrderStub
{
    public decimal TotalAmount { get; set; }

    public OrderDetails Details { get; set; }
}

public class OrderDetails
{
    public bool IsGift { get; set; }

    public string GiftMessage { get; set; }
}

public enum EmploymentStatus
{
    FullTime,
    PartTime,
    Contractor,
    Unemployed
}

public class AddressStub : ValueObject
{
    private AddressStub() { } // Private constructor required by EF Core

    private AddressStub(string name, string line1, string line2, string postalCode, string city, string country)
    {
        this.Name = name;
        this.Line1 = line1;
        this.Line2 = line2;
        this.PostalCode = postalCode;
        this.City = city;
        this.Country = country;
    }

    public string Name { get; }

    public string Line1 { get; }

    public string Line2 { get; }

    public string PostalCode { get; }

    public string City { get; }

    public string Country { get; }

    public static AddressStub Create(
        string name,
        string line1,
        string line2,
        string postalCode,
        string city,
        string country)
    {
        var address = new AddressStub(name, line1, line2, postalCode, city, country);
        if (!IsValid(address))
        {
            throw new ValidationException("Invalid address");
        }

        return address;
    }

    protected override IEnumerable<object> GetAtomicValues()
    {
        yield return this.Name;
        yield return this.Line1;
        yield return this.Line2;
        yield return this.PostalCode;
        yield return this.City;
        yield return this.Country;
    }

    private static Result IsValid(AddressStub address)
    {
        return Result.SuccessIf(() => !string.IsNullOrEmpty(address.Name) &&
            !string.IsNullOrEmpty(address.Line1) &&
            !string.IsNullOrEmpty(address.PostalCode) &&
            !string.IsNullOrEmpty(address.Country) &&
            !string.IsNullOrEmpty(address.Country));
    }
}

public class PersonDtoStub : AggregateRoot<string>
{
    public Guid Identifier { get; set; }

    public string Nationality { get; set; }

    public string FullName { get; set; }

    public int Age { get; set; }
}

public class AdultSpecification(int minAge) : Specification<PersonStub>
{
    public override Expression<Func<PersonStub, bool>> ToExpression()
    {
        return person => person.Age >= minAge;
    }
}

public class NameStartsWithSpecification(string prefix) : Specification<PersonStub>
{
    public override Expression<Func<PersonStub, bool>> ToExpression()
    {
        return person => person.FirstName.StartsWith(prefix);
    }
}