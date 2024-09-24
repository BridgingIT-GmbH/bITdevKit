// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.DinnerFiesta.Modules.Core.UnitTests.Domain;

using Core.Domain;

public class UserTests
{
    [Fact]
    public void Create_ShouldCreateAggregate_WhenValidArguments()
    {
        // Arrange
        const string firstName = "John";
        const string lastName = "Doe";
        const string email = "john.doe@example.com";
        const string password = "password123";

        // Act
        var user = User.Create(firstName, lastName, email, password);

        // Assert
        user.ShouldNotBeNull();
        user.FirstName.ShouldBe(firstName);
        user.LastName.ShouldBe(lastName);
        ((string)user.Email).ShouldBe(email);
        user.Password.ShouldBe(password);
    }

    [Fact]
    public void Create_ShouldThrowArgumentNullException_WhenFirstNameIsNull()
    {
        // Arrange
        const string firstName = null;
        const string lastName = "Doe";
        const string email = "john.doe@example.com";
        const string password = "password123";

        // Act and Assert
        Should.Throw<ArgumentNullException>(() => User.Create(firstName, lastName, email, password));
    }

    [Fact]
    public void Create_ShouldThrowArgumentNullException_WhenLastNameIsNull()
    {
        // Arrange
        const string firstName = "John";
        const string lastName = null;
        const string email = "john.doe@example.com";
        const string password = "password123";

        // Act and Assert
        Should.Throw<ArgumentNullException>(() => User.Create(firstName, lastName, email, password));
    }

    [Fact]
    public void Create_ShouldThrowArgumentNullException_WhenEmailIsNull()
    {
        // Arrange
        const string firstName = "John";
        const string lastName = "Doe";
        const string email = null;
        const string password = "password123";

        // Act and Assert
        Should.Throw<ArgumentNullException>(() => User.Create(firstName, lastName, email, password));
    }

    [Fact]
    public void Create_ShouldThrowArgumentNullException_WhenPasswordIsNull()
    {
        // Arrange
        const string firstName = "John";
        const string lastName = "Doe";
        const string email = "john.doe@example.com";
        const string password = null;

        // Act and Assert
        Should.Throw<ArgumentNullException>(() => User.Create(firstName, lastName, email, password));
    }
}