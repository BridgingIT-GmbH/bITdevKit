// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.DinnerFiesta.Modules.Marketing.IntegrationTests.Presentation.Web;

using FluentAssertions;

//[Collection(nameof(PresentationCollection))] // https://xunit.net/docs/shared-context#collection-fixture
[IntegrationTest("DinnerFiesta.Presentation")]
[Module("Marketing")]
public class MarketingEndpointTests(ITestOutputHelper output, CustomWebApplicationFactoryFixture<Program> fixture)
    : IClassFixture<CustomWebApplicationFactoryFixture<Program>> // https://xunit.net/docs/shared-context#class-fixture
{
    private readonly CustomWebApplicationFactoryFixture<Program> fixture = fixture.WithOutput(output);

    [Theory]
    [InlineData("api/marketing/echo")]
    public async Task SystemEchoGetTest(string route)
    {
        // Arrang/Act
        this.fixture.Output.WriteLine($"Start Endpoint test for route: {route}");
        var response = await this.fixture.CreateClient()
            .GetAsync(route)
            .AnyContext();
        this.fixture.Output.WriteLine($"Finish Endpoint test for route: {route} (status={(int)response.StatusCode})");

        // Assert
        response.Should().Be200Ok(); // https://github.com/adrianiftode/FluentAssertions.Web
        response.Should().MatchInContent("*echo*");
    }

    //[Theory]
    //[InlineData("api/_system/info")]
    //public async Task SystemInfoGetTest(string route)
    //{
    //    // Arrange & Act
    //    this.fixture.Output.WriteLine($"Start Endpoint test for route: {route}");
    //    var response = await this.fixture.CreateClient()
    //        .GetAsync(route).AnyContext();
    //    this.fixture.Output.WriteLine($"Finish Endpoint test for route: {route} (status={(int)response.StatusCode})");

    //    // Assert
    //    response.Should().Be200Ok(); // https://github.com/adrianiftode/FluentAssertions.Web
    //    response.Should().Satisfy<SystemInfo>(
    //        model =>
    //        {
    //            model.ShouldNotBeNull();
    //            model.Runtime.ShouldNotBeNull();
    //            model.Runtime.Count.ShouldBeGreaterThan(0);
    //            model.Request.ShouldNotBeNull();
    //            model.Request.Count.ShouldBeGreaterThan(0);
    //        });
    //}
}