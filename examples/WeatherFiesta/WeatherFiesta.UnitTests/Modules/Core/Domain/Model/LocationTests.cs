// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.WeatherFiesta.UnitTests.Modules.Core.Domain.Model;

using BridgingIT.DevKit.Examples.WeatherFiesta.Domain.Modules.Core.Model;

/// <summary>
/// Unit tests for the <see cref="Location"/> value object.
/// </summary>
public class LocationTests
{
    [Fact]
    public void Create_ValidCoordinates_ReturnsSuccess()
    {
        // Arrange & Act
        var result = Location.Create(51.5074m, -0.1278m);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Latitude.ShouldBe(51.5074m);
        result.Value.Longitude.ShouldBe(-0.1278m);
    }

    [Theory]
    [InlineData(-90)]
    [InlineData(0)]
    [InlineData(90)]
    public void Create_LatitudeBoundary_ReturnsSuccess(int latitude)
    {
        // Arrange & Act
        var result = Location.Create(latitude, 0);

        // Assert
        result.IsSuccess.ShouldBeTrue();
    }

    [Theory]
    [InlineData(-180)]
    [InlineData(0)]
    [InlineData(180)]
    public void Create_LongitudeBoundary_ReturnsSuccess(int longitude)
    {
        // Arrange & Act
        var result = Location.Create(0, longitude);

        // Assert
        result.IsSuccess.ShouldBeTrue();
    }

    [Theory]
    [InlineData(-90.1)]
    [InlineData(90.1)]
    public void Create_InvalidLatitude_ReturnsFailure(decimal latitude)
    {
        // Arrange & Act
        var result = Location.Create(latitude, 0);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Errors.ShouldContain(e => e.Message == "Latitude must be between -90 and 90.");
    }

    [Theory]
    [InlineData(-180.1)]
    [InlineData(180.1)]
    public void Create_InvalidLongitude_ReturnsFailure(decimal longitude)
    {
        // Arrange & Act
        var result = Location.Create(0, longitude);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Errors.ShouldContain(e => e.Message == "Longitude must be between -180 and 180.");
    }

    [Fact]
    public void Create_InvalidCoordinates_ReturnsBothFailures()
    {
        // Arrange & Act
        var result = Location.Create(91, 181);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Errors.ShouldContain(e => e.Message == "Latitude must be between -90 and 90.");
        result.Errors.ShouldContain(e => e.Message == "Longitude must be between -180 and 180.");
    }

    [Fact]
    public void Equals_SameCoordinates_ReturnsTrue()
    {
        // Arrange
        var first = Location.Create(51.5074m, -0.1278m).Value;
        var second = Location.Create(51.5074m, -0.1278m).Value;

        // Act & Assert
        first.Equals(second).ShouldBeTrue();
    }
}
