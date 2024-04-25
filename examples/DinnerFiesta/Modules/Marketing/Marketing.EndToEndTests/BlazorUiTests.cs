namespace BridgingIT.DevKit.Examples.DinnerFiesta.Modules.Marketing.EndToEndTests;

using Microsoft.Playwright.NUnit;
using Xunit;

public class BlazorUiTests : PageTest, IClassFixture<KestrelWebApplicationFactoryFixture<Program>> // https://xunit.net/docs/shared-context#class-fixture
{
    private readonly string serverAddress;

    public BlazorUiTests(ITestOutputHelper output, KestrelWebApplicationFactoryFixture<Program> fixture)
    {
        Microsoft.Playwright.Program.Main(new[] { "install" }); // install the playwritght browsers https://github.com/microsoft/playwright-dotnet/issues/1788#issuecomment-943908634

        fixture.WithOutput(output);
        this.serverAddress = fixture.ServerAddress;
    }

    [Fact]
    public async Task HomePageIsVisible()
    {
        //Arrange
        using var playwright = await Microsoft.Playwright.Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync();
        var page = await browser.NewPageAsync();

        //Act
        await page.GotoAsync(this.serverAddress);

        //Assert
        await this.Expect(page.GetByText("DinnerFiesta")).ToBeVisibleAsync();
    }
}