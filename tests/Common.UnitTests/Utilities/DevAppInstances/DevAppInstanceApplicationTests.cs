// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.UnitTests.Utilities.DevAppInstances;

public class DevAppInstanceApplicationTests
{
    [Theory]
    [InlineData("Contoso.ForecastHub.Presentation.Web.Server", "forecasthub")]
    [InlineData("TaskHarbor.Api", "taskharbor")]
    [InlineData("Contoso.Mail_Hub.Host", "mail-hub")]
    [InlineData("Server", "server")]
    [InlineData("Contoso.2024Portal.Web", "app-2024portal")]
    public void DeriveAppId_ApplicationName_ReturnsNormalizedId(string applicationName, string expected)
    {
        // Act
        var result = DevAppInstanceApplication.DeriveAppId(applicationName);

        // Assert
        result.ShouldBe(expected);
    }

    [Fact]
    public void DeriveAppId_LongName_CutsAndAppendsStableHash()
    {
        // Arrange
        var name = "Contoso." + new string('a', 80) + ".Web";

        // Act
        var result = DevAppInstanceApplication.DeriveAppId(name);

        // Assert
        result.Length.ShouldBeLessThanOrEqualTo(DevAppInstanceApplication.MaxAppIdLength);
        result.ShouldMatch("^a+-[0-9a-f]{8}$");
        DevAppInstanceApplication.DeriveAppId(name).ShouldBe(result);
        DevAppInstanceApplication.DeriveAppId("Fabrikam." + new string('a', 80) + ".Web").ShouldNotBe(result);
    }

    [Fact]
    public void Validate_SixtyThreeCharacterId_Succeeds()
    {
        // Arrange
        var sut = new DevAppInstanceApplication("a" + new string('b', 62));

        // Act & Assert
        Should.NotThrow(() => sut.Validate(DevAppInstancePortPolicy.Default));
        Should.Throw<DevAppInstanceException>(() => (sut with { AppId = sut.AppId + "c" }).Validate(DevAppInstancePortPolicy.Default));
    }

    [Fact]
    public void EnsureValid_NoDeclaration_DerivesIdWithAutomaticOffsets()
    {
        // Arrange
        var sut = new DevAppInstanceBuilder();

        // Act
        sut.EnsureValid("Contoso.ForecastHub.Presentation.Web.Server");

        // Assert
        sut.Application.AppId.ShouldBe("forecasthub");
        sut.Application.HasAutomaticOffsets.ShouldBeTrue();
        sut.Application.Source.ShouldBe("Contoso.ForecastHub.Presentation.Web.Server");
    }

    [Fact]
    public void WithAppId_DeclaredId_KeepsIdWithAutomaticOffsets()
    {
        // Arrange
        var sut = new DevAppInstanceBuilder().WithAppId("forecasthub");

        // Act
        sut.EnsureValid("Contoso.Other.Web");

        // Assert
        sut.Application.AppId.ShouldBe("forecasthub");
        sut.Application.HasAutomaticOffsets.ShouldBeTrue();
        sut.Application.Source.ShouldBeNull();
    }

    [Theory]
    [InlineData(10, null, 10, 11)]
    [InlineData(4, 8, 4, 8)]
    public void WithHttpOffset_Offsets_SetsFixedOffsets(int http, int? https, int expectedHttp, int expectedHttps)
    {
        // Act
        var sut = new DevAppInstanceBuilder().WithAppId("forecasthub").WithHttpOffset(http, https);

        // Assert
        sut.Application.HasAutomaticOffsets.ShouldBeFalse();
        sut.Application.HttpOffset.ShouldBe(expectedHttp);
        sut.Application.HttpsOffset.ShouldBe(expectedHttps);
    }

    [Fact]
    public void WithHttpOffset_BeforeWithAppId_KeepsBothValues()
    {
        // Arrange
        var sut = new DevAppInstanceBuilder();

        // Act
        sut.WithHttpOffset(http: 10, https: 11).WithAppId("forecasthub");

        // Assert
        sut.Application.AppId.ShouldBe("forecasthub");
        sut.Application.HttpOffset.ShouldBe(10);
        sut.Application.HttpsOffset.ShouldBe(11);
        sut.Application.HasAutomaticOffsets.ShouldBeFalse();
    }

    [Fact]
    public void WithHttpOffset_NoAppId_DerivesIdAndKeepsOffsets()
    {
        // Arrange
        var sut = new DevAppInstanceBuilder().WithHttpOffset(http: 10);

        // Act
        sut.EnsureValid("Contoso.ForecastHub.Presentation.Web.Server");

        // Assert
        sut.Application.AppId.ShouldBe("forecasthub");
        sut.Application.HttpOffset.ShouldBe(10);
        sut.Application.HttpsOffset.ShouldBe(11);
        sut.Application.HasAutomaticOffsets.ShouldBeFalse();
    }

    [Fact]
    public void EnsureValid_NoIdAndNoApplicationName_Throws()
    {
        // Arrange
        var sut = new DevAppInstanceBuilder();

        // Act & Assert
        Should.Throw<InvalidOperationException>(() => sut.EnsureValid());
    }
}
