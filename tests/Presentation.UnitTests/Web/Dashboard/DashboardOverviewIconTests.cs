// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.UnitTests.Web.Dashboard;

using BridgingIT.DevKit.Presentation.Web.Dashboard;
using BridgingIT.DevKit.Presentation.Web.Dashboard.Pages;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

/// <summary>Verifies that overview cards use the sidebar feature icons.</summary>
/// <example><code>dotnet test --filter FullyQualifiedName~DashboardOverviewIconTests</code></example>
[UnitTest("Presentation")]
public sealed class DashboardOverviewIconTests
{
    /// <summary>Checks the rendered icon for default, custom, and unavailable cards.</summary>
    /// <example><code>await RenderAsync_BuiltInPage_UsesSidebarIcon("custom");</code></example>
    [Theory]
    [InlineData("default")]
    [InlineData("custom")]
    [InlineData("error")]
    public async Task RenderAsync_BuiltInPage_UsesSidebarIcon(string cardMode)
    {
        var page = new DashboardPage("jobs", "Jobs", "calendar2-check", "/_bdk/dashboard/jobs")
        {
            Card = cardMode switch
            {
                "custom" => _ => ValueTask.FromResult(new DashboardPageCard("Jobs") { Icon = "calendar2-check" }),
                "error" => _ => throw new InvalidOperationException("Card data unavailable"),
                _ => null
            }
        };

        var html = await RenderAsync(page);

        html.ShouldContain(DashboardFeatureIcons.GetSvg(page.Key).Value);
        html.ShouldContain("data-dashboard-feature=\"calendar2-check\"");
        html.ShouldNotContain("bi bi-calendar2-check");
        if (cardMode == "error")
        {
            html.ShouldContain("Card unavailable");
        }
    }

    /// <summary>Preserves configured Bootstrap icons for project-specific pages.</summary>
    /// <example><code>await RenderAsync_CustomPage_UsesConfiguredBootstrapIcon();</code></example>
    [Fact]
    public async Task RenderAsync_CustomPage_UsesConfiguredBootstrapIcon()
    {
        var page = new DashboardPage("weatherfiesta.cities", "Cities", "cloud-sun", "/cities");

        var html = await RenderAsync(page);

        html.ShouldContain("bi bi-cloud-sun");
        html.ShouldNotContain("data-dashboard-icon");
    }

    /// <summary>Prefers the card's explicit feature icon over its owning page.</summary>
    /// <example><code>await RenderAsync_ExplicitFeature_OverridesPageIcon();</code></example>
    [Fact]
    public async Task RenderAsync_ExplicitFeature_OverridesPageIcon()
    {
        var page = new DashboardPage("jobs", "Activity", "calendar2-check", "/activity")
        {
            Card = _ => ValueTask.FromResult(new DashboardPageCard("Auditing") { FeatureKey = "auditing", Icon = "journal-text" })
        };

        var html = await RenderAsync(page);

        html.ShouldContain(DashboardFeatureIcons.GetSvg("auditing").Value);
        html.ShouldNotContain(DashboardFeatureIcons.GetSvg("jobs").Value);
        html.ShouldContain("data-dashboard-feature=\"auditing\"");
        html.ShouldContain("dashboard-card-icon flex-shrink-0");
    }

    private static async Task<string> RenderAsync(DashboardPage page)
    {
        var provider = Substitute.For<IDashboardPageProvider>();
        provider.GetPages(Arg.Any<HttpContext>()).Returns([page]);
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(provider);
        using var serviceProvider = services.BuildServiceProvider();
        await using var body = new MemoryStream();
        var context = new DefaultHttpContext { RequestServices = serviceProvider };
        context.Response.Body = body;

        await Results.RazorSlice<DashboardIndexContent>().ExecuteAsync(context);

        body.Position = 0;
        using var reader = new StreamReader(body);
        return await reader.ReadToEndAsync();
    }
}
