// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.DinnerFiesta.Modules.Core.IntegrationTests.Presentation.Web.Client;

using Microsoft.Playwright.NUnit;

public class EndToEndTests : PageTest, IClassFixture<KestrelWebApplicationFactoryFixture<Program>>
{
    private readonly string serverAddress;

    public EndToEndTests(KestrelWebApplicationFactoryFixture<Program> fixture)
    {
        Microsoft.Playwright.Program.Main([
            "install"
        ]); // install the playwritght browsers https://github.com/microsoft/playwright-dotnet/issues/1788#issuecomment-943908634

        this.serverAddress = fixture.ServerAddress;
    }

    // [Fact]
    // public async Task HomePageIsVisible()
    // {
    //     //Arrange
    //     using var playwright = await Microsoft.Playwright.Playwright.CreateAsync();
    //     await using var browser = await playwright.Chromium.LaunchAsync();
    //     var page = await browser.NewPageAsync();
    //
    //     //Act
    //     await page.GotoAsync(this.serverAddress);
    //
    //     //Assert
    //     await this.Expect(page.GetByText("DinnerFiesta")).ToBeVisibleAsync();
    // }
}