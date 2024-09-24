// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.DinnerFiesta.Modules.Core.UnitTests.Application;

using Core.Application;
using FluentValidation.TestHelper;

public class MenuCreateCommandTests
{
    [Fact]
    public void Validator_ValidCommand_ReturnsNoErrors()
    {
        // Arrange
        var command = new MenuCreateCommand
        {
            Name = "TestMenuName",
            Description = "TestMenuDescription",
            HostId = "SomeHostId",
            Sections = new List<MenuCreateCommand.MenuSection>()
        };
        var sut = new MenuCreateCommand.Validator();

        // Act
        var result = sut.TestValidate(command);

        // Assert
        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Validate_InvalidCommand_ErrorsContainsProperties()
    {
        // Arrange
        var command = new MenuCreateCommand
        {
            Name = string.Empty,
            Description = "TestMenuDescription",
            Sections = new List<MenuCreateCommand.MenuSection>()
        };
        var sut = new MenuCreateCommand.Validator();

        // Act
        var result = sut.TestValidate(command);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "Name" && e.ErrorMessage == "Must not be empty.");
        result.Errors.ShouldContain(e => e.PropertyName == "HostId" && e.ErrorMessage == "Must not be empty.");
    }
}