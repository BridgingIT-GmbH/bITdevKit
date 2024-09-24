// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.UnitTests.Abstractions;

public class NullCurrentUserAccessorTests
{
    private readonly Faker faker = new();

    [Fact]
    public void UserId_WhenCalled_ReturnsNull()
    {
        // Arrange
        var sut = new NullCurrentUserAccessor();

        // Act
        var result = sut.UserId;

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public void UserName_WhenCalled_ReturnsNull()
    {
        // Arrange
        var sut = new NullCurrentUserAccessor();

        // Act
        var result = sut.UserName;

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public void Email_WhenCalled_ReturnsNull()
    {
        // Arrange
        var sut = new NullCurrentUserAccessor();

        // Act
        var result = sut.Email;

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public void Roles_WhenCalled_ReturnsNull()
    {
        // Arrange
        var sut = new NullCurrentUserAccessor();

        // Act
        var result = sut.Roles;

        // Assert
        result.ShouldBeNull();
    }
}