// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.UnitTests.Web.Entities;

using BridgingIT.DevKit.Presentation.Web.Entities.Dashboard;

public class EntitiesDashboardJsonTests
{
    [Fact]
    public void Deserialize_ValueObjectWithPrivateConstructorAndSetters_ShouldPopulateProperties()
    {
        // Arrange
        var json = "{ \"latitude\": 51.5, \"longitude\": -0.12 }";

        // Act
        var result = EntitiesDashboardJson.Deserialize(typeof(PrivateGeoPoint), json, "Location");

        // Assert
        result.IsSuccess.ShouldBeTrue();
        var point = result.Value.ShouldBeOfType<PrivateGeoPoint>();
        point.Latitude.ShouldBe(51.5m);
        point.Longitude.ShouldBe(-0.12m);
    }

    [Fact]
    public void Deserialize_InvalidJson_ShouldFail()
    {
        // Arrange & Act
        var result = EntitiesDashboardJson.Deserialize(typeof(PrivateGeoPoint), "{ nope", "Location");

        // Assert
        result.IsFailure.ShouldBeTrue();
    }

    [Fact]
    public void Serialize_ThrowingComputedGetter_ShouldNotFail()
    {
        // Arrange
        var value = new ThrowingGetter { Name = "a" };

        // Act
        var json = EntitiesDashboardJson.Serialize(value);

        // Assert
        json.ShouldContain("\"name\": \"a\"");
    }

    private class ThrowingGetter
    {
        public string Name { get; set; }

        public int Duration => throw new InvalidOperationException("open-ended");
    }

    private class PrivateGeoPoint
    {
        private PrivateGeoPoint() { }

        public decimal Latitude { get; private set; }

        public decimal Longitude { get; private set; }
    }
}
