// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.UnitTests.Web.DevAppInstances;

using BridgingIT.DevKit.Presentation.Web;
using Microsoft.Extensions.Configuration;

public class DevAppInstanceConfigurationSourceTests
{
    [Fact]
    public void Set_AfterConfigurationBuilt_IsVisibleToReads()
    {
        // Arrange
        var sut = new DevAppInstanceConfigurationSource(new Dictionary<string, string> { ["Kestrel:Url"] = "https://localhost:5111" });
        var configuration = new ConfigurationManager();
        configuration.AddInMemoryCollection(new Dictionary<string, string> { ["ConnectionStrings:Default"] = "Database=forecasthub" });
        ((IConfigurationBuilder)configuration).Add(sut);

        // Act
        sut.Set(new Dictionary<string, string> { ["ConnectionStrings:Default"] = "Database=forecasthub_w01" });

        // Assert
        configuration["ConnectionStrings:Default"].ShouldBe("Database=forecasthub_w01");
        configuration["Kestrel:Url"].ShouldBe("https://localhost:5111");
        sut.Values["ConnectionStrings:Default"].ShouldBe("Database=forecasthub_w01");
    }
}
