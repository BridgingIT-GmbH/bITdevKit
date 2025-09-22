// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.UnitTests;

using FluentValidation;

public class StubMapper : IMapper<PersonStub, PersonDtoStub>
{
    public void Map(PersonStub source, PersonDtoStub target)
    {
        target.Age = source.Age;
        target.FullName = $"{source.FirstName} {source.LastName}";
    }
}

public class PersonStub
{
    private readonly List<LocationStub> locations = [];

    public PersonStub() { }

    public PersonStub(string firstName, string lastName, string email, int age, IEnumerable<LocationStub> locations = null)
    {
        this.FirstName = firstName;
        this.LastName = lastName;
        this.Email = EmailAddressStub.Create(email);
        this.Age = age;
        this.locations = (locations ?? []).ToList();
    }

    public string FirstName { get; set; }

    public string LastName { get; set; }

    public string Nationality { get; set; } = "USA";

    public EmailAddressStub Email { get; set; }

    public IReadOnlyList<LocationStub> Locations => this.locations.AsReadOnly();

    public int Age { get; set; }

    public void AddLocation(LocationStub location)
    {
        this.locations.Add(location);
    }

    public void RemoveLocation(LocationStub location)
    {
        if (this.locations.Contains(location))
        {
            this.locations.Remove(location);
        }
    }

#pragma warning disable SA1204 // Static elements should appear before instance elements
    public static PersonStub Create(long ticks)
#pragma warning restore SA1204 // Static elements should appear before instance elements
    {
        return new PersonStub { FirstName = $"John{ticks}", LastName = $"Doe{ticks}", Age = 42 };
    }
}

public class EmailAddressStub
{
    private EmailAddressStub() { }

    private EmailAddressStub(string value) => this.Value = value;

    public string Value { get; }

    public static implicit operator string(EmailAddressStub email)
    {
        return email.Value;
    }

    public static EmailAddressStub Create(string value)
    {
        value = value?.Trim()
            ?.ToLowerInvariant();

        return new EmailAddressStub(value);
    }

    protected IEnumerable<object> GetAtomicValues()
    {
        yield return this.Value;
    }
}

public class LocationStub
{
    private LocationStub() { }

    private LocationStub(
        string name,
        string addressLine1,
        string addressLine2,
        string postalCode,
        string city,
        string country)
    {
        this.Name = name;
        this.AddressLine1 = addressLine1;
        this.AddressLine2 = addressLine2;
        this.PostalCode = postalCode;
        this.City = city;
        this.Country = country;
    }

    public string Name { get; private set; }

    public string AddressLine1 { get; private set; }

    public string AddressLine2 { get; private set; }

    public string PostalCode { get; private set; }

    public string City { get; private set; }

    public string Country { get; private set; }

    public Dummy Dummy { get; set; } = new Dummy();

    public static LocationStub Create(
        string name,
        string addressLine1,
        string addressLine2,
        string postalCode,
        string city,
        string country)
    {
        return new LocationStub(name,
            addressLine1,
            addressLine2,
            postalCode,
            city,
            country);
    }
}

public class Dummy
{
    public string Text { get; set; } = "ABC";
}

public class PersonDtoStub
{
    public int Age { get; set; }

    public string FullName { get; set; }
}

public class OptionsStub : OptionsBase
{
    public string Parameter1 { get; set; }

    public int Parameter2 { get; set; }

    public bool Parameter3 { get; set; }
}

public class OptionsStubBuilder : OptionsBuilderBase<OptionsStub, OptionsStubBuilder>
{
    public OptionsStubBuilder Parameter1(string value)
    {
        this.Target.Parameter1 = value;

        return this;
    }

    public OptionsStubBuilder Parameter2(int value)
    {
        this.Target.Parameter2 = value;

        return this;
    }

    public OptionsStubBuilder SetParameter3()
    {
        this.Target.Parameter3 = true;

        return this;
    }
}

public class TestValidator : AbstractValidator<PersonStub>
{
    public TestValidator()
    {
        this.RuleFor(x => x.Age).GreaterThanOrEqualTo(18).WithMessage("Must be 18 or older");
        this.RuleFor(x => x.Email.Value).NotEmpty().EmailAddress().WithMessage("Invalid email");
    }
}

public class IsAdultRule(PersonStub person) : RuleBase
{
    private const int AdultAge = 18;

    public override string Message => $"Person must be at least {AdultAge} years old";

    public override Result Execute()
    {
        return Result.SuccessIf(person?.Age >= AdultAge);
    }
}

public class ActiveStatus(int id, string value, string code, string description) : Enumeration(id, value)
{
    public static readonly ActiveStatus Active = new(0, "Active", "AKT", "Lorem Ipsum");
    public static readonly ActiveStatus Inactive = new(1, "Inactive", "INA", "Lorem Ipsum");
    public static readonly ActiveStatus Unknown = new(99, "Unknown", "UNK", "Lorem Ipsum");

    public string Code { get; } = code;

    public string Description { get; } = description;

    public static ActiveStatus FromId(int id) => FromId<ActiveStatus>(id);

    public static IEnumerable<ActiveStatus> GetAll() => GetAll<ActiveStatus>();

    public static ActiveStatus GetByCode(string code) => GetAll<ActiveStatus>().FirstOrDefault(e => e.Code == code);
}