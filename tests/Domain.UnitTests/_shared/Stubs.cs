// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.UnitTests;

using System.Linq.Expressions;
using Model;

public class PersonStub : Entity<Guid>
{
    public string FirstName { get; set; }

    public string LastName { get; set; }

    public int Age { get; set; }

    public DateTime BirthDate { get; set; }

    public string Email { get; set; }

    public TimeSpan WorkStartTime { get; set; }

    public EmploymentStatus EmploymentStatus { get; set; }

    public List<OrderStub> Orders { get; set; }

    public List<AddressStub> Addresses { get; set; } = [];

    public AddressStub BillingAddress { get; set; }
}

public class OrderStub
{
    public decimal TotalAmount { get; set; }
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