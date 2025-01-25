// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.DinnerFiesta.Modules.Core.UnitTests.Application;

using Core.Application;

public class DinnerCreateCommandTests
{
    [Fact]
    public void Validate_ValidCommand_ReturnsNoErrors()
    {
        // Arrange
        var command = new DinnerCreateCommand
        {
            Name = "TestDinner",
            Description = "TestDescription",
            Schedule =
                new DinnerCreateCommand.DinnerSchedule
                {
                    StartDateTime = DateTimeOffset.Now.AddDays(1),
                    EndDateTime = DateTimeOffset.Now.AddDays(2)
                },
            IsPublic = true,
            MaxGuests = 10,
            Price = new DinnerCreateCommand.DinnerPrice { Amount = 50, Currency = "USD" },
            HostId = "testhost01",
            MenuId = "testmenu01",
            ImageUrl = "https://www.example.com",
            Location = new DinnerCreateCommand.DinnerLocation
            {
                Name = "TestLocation",
                AddressLine1 = "#01 Test Street",
                City = "Test City",
                PostalCode = "123456",
                Country = "Test Country",
                Latitude = 1.23,
                Longitude = 4.56,
                WebsiteUrl = "https://www.example.com"
            }
        };
        var sut = new DinnerCreateCommand.Validator();

        // Act
        var result = sut.Validate(command);

        // Assert
        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Validate_InvalidCommand_ErrorsContainsProperties()
    {
        // Arrange
        var command = new DinnerCreateCommand
        {
            //Name = "TestName",
            Description = "TestDescription",
            Schedule =
                new DinnerCreateCommand.DinnerSchedule
                {
                    StartDateTime = DateTimeOffset.Now.AddDays(1),
                    EndDateTime = DateTimeOffset.Now.AddDays(2)
                },
            IsPublic = true,
            MaxGuests = 10,
            Price = new DinnerCreateCommand.DinnerPrice { Amount = 50, Currency = "USD" },
            HostId = "testhost01",
            MenuId = "testmenu01",
            ImageUrl = "https://www.example.com",
            Location = new DinnerCreateCommand.DinnerLocation
            {
                Name = "TestLocation",
                AddressLine1 = "#01 Test Street",
                City = "Test City",
                PostalCode = "123456",
                Country = "Test Country",
                Latitude = 1.23,
                Longitude = 4.56,
                WebsiteUrl = "https://www.example.com"
            }
        };
        var validator = new DinnerCreateCommand.Validator();

        // Act
        var result = validator.Validate(command);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "Name" && e.ErrorMessage == "Must not be empty.");
    }
}